using SharpDX;

/// <summary>
/// 摄像机类，封装摄像机部分的相关代码
/// </summary>
namespace WhiteRabbit.Framework
{
    class Camera
    {
        private bool viewDirty = true;

        public Camera()
        {
            //设置视锥体
            SetLens(MathUtil.PiOverFour, 1.0f, 1.0f, 1000.0f);
        }

        //定义摄像机坐标系相对于世界空间的坐标，分别是原点、x轴、y轴、z轴
        public Vector3 Position { get; set; }
        public Vector3 Right { get; private set; } = Vector3.UnitX;
        public Vector3 Up { get; private set; } = Vector3.UnitY;
        public Vector3 Look { get; private set; } = Vector3.UnitZ;

        //定义视锥体的属性
        public float NearZ { get; private set; }
        public float FarZ { get; private set; }
        public float Aspect { get; private set; }
        public float FovY { get; private set; }
        public float FovX
        {
            get
            {
                float halfWidth = 0.5f * NearWindowWidth;
                return 2.0f * MathHelper.Atanf(halfWidth / NearZ);
            }
        }
        //定义用观察空间坐标表示的近远平面的大小
        public float NearWindowHeight { get; private set; }
        public float NearWindowWidth => Aspect * NearWindowHeight;
        public float FarWindowHeight { get; private set; }
        public float FarWindowWidth => Aspect * FarWindowHeight;

        //定义观察矩阵和投影矩阵
        public Matrix View { get; private set; } = Matrix.Identity;
        public Matrix Proj { get; private set; } = Matrix.Identity;
        
        public Matrix ViewProj => View * Proj;
        public BoundingFrustum Frustum => new BoundingFrustum(ViewProj);

        //设置视锥体，即摄像机镜头，在缓存视锥体属性以及构建投影矩阵时就要用到SetLens方法
        public void SetLens(float fovY, float aspect, float zn, float zf)
        {
            FovY = fovY;
            Aspect = aspect;
            NearZ = zn;
            FarZ = zf;

            NearWindowHeight = 2.0f * zn * MathHelper.Tanf(0.5f * fovY);
            FarWindowHeight = 2.0f * zf * MathHelper.Tanf(0.5f * fovY);

            Proj = Matrix.PerspectiveFovLH(fovY, aspect, zn, zf);
        }

        public void LookAt(Vector3 pos, Vector3 target, Vector3 up)
        {
            Position = pos;
            Look = Vector3.Normalize(target - pos);
            Right = Vector3.Normalize(Vector3.Cross(up, Look));
            Up = Vector3.Cross(Look, Right);
            viewDirty = true;
        }

        //将摄像机按距离d进行左右平移
        public void Strafe(float d)
        {
            Position += Right * d;
            viewDirty = true;
        }

        //将摄像机按距离d进行前后移动
        public void Walk(float d)
        {
            Position += Look * d;
            viewDirty = true;
        }

        //将摄像机绕观察空间的y轴旋转（进行俯视观察）
        public void Pitch(float angle)
        {
            Matrix r = Matrix.RotationAxis(Right, angle);

            Up = Vector3.TransformNormal(Up, r);
            Look = Vector3.TransformNormal(Look, r);

            viewDirty = true;
        }

        //将摄像机绕世界空间的y轴旋转（观察左右两侧）
        public void RotateY(float angle)
        {
            Matrix r = Matrix.RotationY(angle);

            Right = Vector3.TransformNormal(Right, r);
            Up = Vector3.TransformNormal(Up, r);
            Look = Vector3.TransformNormal(Look, r);

            viewDirty = true;
        }

        //将矩阵正交规范化，修改摄像机位置和朝向后，调用此函数重新构建观察矩阵
        public void UpdateViewMatrix()
        {
            if (!viewDirty) return;

            Look = Vector3.Normalize(Look);
            Up = Vector3.Normalize(Vector3.Cross(Look, Right));

            Right = Vector3.Cross(Up, Look);

            float x = -Vector3.Dot(Position, Right);
            float y = -Vector3.Dot(Position, Up);
            float z = -Vector3.Dot(Position, Look);

            View = new Matrix(
                Right.X, Up.X, Look.X, 0.0f,
                Right.Y, Up.Y, Look.Y, 0.0f,
                Right.Z, Up.Z, Look.Z, 0.0f,
                x, y, z, 1.0f
            );

            viewDirty = false;
        }

        //获得拾取光线，目前没有应用
        public Ray GetPickingRay(Point sp, int clientWidth, int clientHeight)
        {
            Matrix p = Proj;

            float vx = (2f * sp.X / clientWidth - 1f) / p.M11;
            float vy = (-2f * sp.Y / clientHeight + 1f) / p.M22;

            var ray = new Ray(Vector3.Zero, new Vector3(vx, vy, 1));
            Matrix v = View;
            Matrix invView = Matrix.Invert(v);

            Matrix toWorld = invView;

            ray = new Ray(
                Vector3.TransformCoordinate(ray.Position, toWorld),
                Vector3.TransformNormal(ray.Direction, toWorld));

            return ray;
        }
    }
}
