using System;
using SharpDX;

namespace WhiteRabbit.Framework
{
    public static class MathHelper
    {
        private static readonly Random random = new Random();

        public static int Rand(int minValue, int maxValue) => random.Next(minValue, maxValue);
        public static float Randf() => random.NextFloat(0.0f, 1.0f);
        public static float Randf(float minValue, float maxValue) => random.NextFloat(minValue, maxValue);

        public static float Sinf(double a) => (float)Math.Sin(a);
        public static float Cosf(double d) => (float)Math.Cos(d);
        public static float Tanf(double a) => (float)Math.Tan(a);
        public static float Atanf(double d) => (float)Math.Atan(d);
        public static float Atan2f(double y, double x) => (float)Math.Atan2(y, x);
        public static float Acosf(double d) => (float)Math.Acos(d);
        public static float Expf(double d) => (float)Math.Exp(d);
        public static float Sqrtf(double d) => (float)Math.Sqrt(d);

        public static Vector3 SphericalToCartesian(float radius, float theta, float phi) => new Vector3(
            radius * Sinf(phi) * Cosf(theta),
            radius * Cosf(phi),
            radius * Sinf(phi) * Sinf(theta));

        //逆矩阵的转置
        public static Matrix InverseTranspose(Matrix m)
        {
            m.Row4 = Vector4.UnitW;

            return Matrix.Transpose(Matrix.Invert(m));
        }
        
        //建立一个矩阵，该矩阵映射三维空间中平面的上的点
        public static void Reflection(ref Plane plane, out Matrix result)
        {
            float num1 = plane.Normal.X;
            float num2 = plane.Normal.Y;
            float num3 = plane.Normal.Z;
            float num4 = -2f * num1;
            float num5 = -2f * num2;
            float num6 = -2f * num3;
            result.M11 = (float)((double)num4 * (double)num1 + 1.0);
            result.M12 = num5 * num1;
            result.M13 = num6 * num1;
            result.M14 = 0.0f;
            result.M21 = num4 * num2;
            result.M22 = (float)((double)num5 * (double)num2 + 1.0);
            result.M23 = num6 * num2;
            result.M24 = 0.0f;
            result.M31 = num4 * num3;
            result.M32 = num5 * num3;
            result.M33 = (float)((double)num6 * (double)num3 + 1.0);
            result.M34 = 0.0f;
            result.M41 = num4 * plane.D;
            result.M42 = num5 * plane.D;
            result.M43 = num6 * plane.D;
            result.M44 = 1f;
        }

        //建立一个矩阵，该矩阵映射三维空间中平面的上的点
        public static Matrix Reflection(Plane plane)
        {
            Matrix result;
            Reflection(ref plane, out result);
            return result;
        }

        //建立一个矩阵，该矩阵能够实现平面阴影
        public static void Shadow(ref Vector4 light, ref Plane plane, out Matrix result)
        {
            float num1 = (float)((double)plane.Normal.X * (double)light.X + (double)plane.Normal.Y * (double)light.Y + (double)plane.Normal.Z * (double)light.Z + (double)plane.D * (double)light.W);
            float num2 = -plane.Normal.X;
            float num3 = -plane.Normal.Y;
            float num4 = -plane.Normal.Z;
            float num5 = -plane.D;
            result.M11 = num2 * light.X + num1;
            result.M21 = num3 * light.X;
            result.M31 = num4 * light.X;
            result.M41 = num5 * light.X;
            result.M12 = num2 * light.Y;
            result.M22 = num3 * light.Y + num1;
            result.M32 = num4 * light.Y;
            result.M42 = num5 * light.Y;
            result.M13 = num2 * light.Z;
            result.M23 = num3 * light.Z;
            result.M33 = num4 * light.Z + num1;
            result.M43 = num5 * light.Z;
            result.M14 = num2 * light.W;
            result.M24 = num3 * light.W;
            result.M34 = num4 * light.W;
            result.M44 = num5 * light.W + num1;
        }

        //建立一个矩阵，该矩阵能够实现平面阴影
        public static Matrix Shadow(Vector4 light, Plane plane)
        {
            Matrix result;
            Shadow(ref light, ref plane, out result);
            return result;
        }
    }
}
