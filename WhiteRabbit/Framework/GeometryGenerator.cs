using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using SharpDX;

/// <summary>
/// GeometryGenerator是一个工具类，用于生成栅格、球体、柱体以及长方体这类简单的几何体（程序性几何体）
/// 此类数据生成在系统内存中，使用时必须将这些数据复制到顶点缓冲区和索引缓冲区内
/// 也可以利用该类创造一些较为复杂的几何体的顶点数据
/// </summary>
namespace WhiteRabbit.Framework
{
    public static class GeometryGenerator
    {
        //顶点结构
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector3 TangentU;
            public Vector2 TexC;

            public Vertex(Vector3 p, Vector3 n, Vector3 t, Vector2 uv)
            {
                Position = p;
                Normal = n;
                TangentU = t;
                TexC = uv;
            }

            public Vertex(
                float px, float py, float pz,
                float nx, float ny, float nz,
                float tx, float ty, float tz,
                float u, float v) : this(
                    new Vector3(px, py, pz),
                    new Vector3(nx, ny, nz),
                    new Vector3(tx, ty, tz),
                    new Vector2(u, v))
            {
            }
        }

        //Mesh是一个嵌套在GeotryGenerator类中用于存储顶点列表和索引列表的简易结构体
        public class MeshData
        {
            public List<Vertex> Vertices { get; } = new List<Vertex>();
            public List<int> Indices32 { get; } = new List<int>();

            public List<short> GetIndices16() => Indices32.Select(i => (short)i).ToList();
        }

        //生成立方体及纹理
        public static MeshData CreateBox(float width, float height, float depth, int numSubdivisions)
        {
            var meshData = new MeshData();

            //创建顶点
            var w2 = 0.5f * width;
            var h2 = 0.5f * height;
            var d2 = 0.5f * depth;
            //正面的顶点数据
            meshData.Vertices.Add(new Vertex(-w2, -h2, -d2, 0, 0, -1, 1, 0, 0, 0, 1));
            meshData.Vertices.Add(new Vertex(-w2, +h2, -d2, 0, 0, -1, 1, 0, 0, 0, 0));
            meshData.Vertices.Add(new Vertex(+w2, +h2, -d2, 0, 0, -1, 1, 0, 0, 1, 0));
            meshData.Vertices.Add(new Vertex(+w2, -h2, -d2, 0, 0, -1, 1, 0, 0, 1, 1));
            //背面的顶点数据
            meshData.Vertices.Add(new Vertex(-w2, -h2, +d2, 0, 0, 1, -1, 0, 0, 1, 1));
            meshData.Vertices.Add(new Vertex(+w2, -h2, +d2, 0, 0, 1, -1, 0, 0, 0, 1));
            meshData.Vertices.Add(new Vertex(+w2, +h2, +d2, 0, 0, 1, -1, 0, 0, 0, 0));
            meshData.Vertices.Add(new Vertex(-w2, +h2, +d2, 0, 0, 1, -1, 0, 0, 1, 0));
            //顶面的顶点数据
            meshData.Vertices.Add(new Vertex(-w2, +h2, -d2, 0, 1, 0, 1, 0, 0, 0, 1));
            meshData.Vertices.Add(new Vertex(-w2, +h2, +d2, 0, 1, 0, 1, 0, 0, 0, 0));
            meshData.Vertices.Add(new Vertex(+w2, +h2, +d2, 0, 1, 0, 1, 0, 0, 1, 0));
            meshData.Vertices.Add(new Vertex(+w2, +h2, -d2, 0, 1, 0, 1, 0, 0, 1, 1));
            //底面的顶点数据
            meshData.Vertices.Add(new Vertex(-w2, -h2, -d2, 0, -1, 0, -1, 0, 0, 1, 1));
            meshData.Vertices.Add(new Vertex(+w2, -h2, -d2, 0, -1, 0, -1, 0, 0, 0, 1));
            meshData.Vertices.Add(new Vertex(+w2, -h2, +d2, 0, -1, 0, -1, 0, 0, 0, 0));
            meshData.Vertices.Add(new Vertex(-w2, -h2, +d2, 0, -1, 0, -1, 0, 0, 1, 0));
            //左侧面的顶点数据
            meshData.Vertices.Add(new Vertex(-w2, -h2, +d2, -1, 0, 0, 0, 0, -1, 0, 1));
            meshData.Vertices.Add(new Vertex(-w2, +h2, +d2, -1, 0, 0, 0, 0, -1, 0, 0));
            meshData.Vertices.Add(new Vertex(-w2, +h2, -d2, -1, 0, 0, 0, 0, -1, 1, 0));
            meshData.Vertices.Add(new Vertex(-w2, -h2, -d2, -1, 0, 0, 0, 0, -1, 1, 1));
            //右侧面的顶点数据
            meshData.Vertices.Add(new Vertex(+w2, -h2, -d2, 1, 0, 0, 0, 0, 1, 0, 1));
            meshData.Vertices.Add(new Vertex(+w2, +h2, -d2, 1, 0, 0, 0, 0, 1, 0, 0));
            meshData.Vertices.Add(new Vertex(+w2, +h2, +d2, 1, 0, 0, 0, 0, 1, 1, 0));
            meshData.Vertices.Add(new Vertex(+w2, -h2, +d2, 1, 0, 0, 0, 0, 1, 1, 1));

            //创建索引
            meshData.Indices32.AddRange(new[]
            {
                    //正面的索引数据
                    0, 1, 2, 0, 2, 3,
                    //背面的索引数据
                    4, 5, 6, 4, 6, 7,
                    //顶面的索引数据
                    8, 9, 10, 8, 10, 11,
                    //底面的索引数据
                    12, 13, 14, 12, 14, 15,
                    //左侧面的索引数据
                    16, 17, 18, 16, 18, 19,
                    //右侧面的索引数据
                    20, 21, 22, 20, 22, 23
                });

            //确定细分的次数
            numSubdivisions = Math.Min(numSubdivisions, 6);

            for (int i = 0; i < numSubdivisions; ++i)
                Subdivide(meshData);

            return meshData;
        }

        //生成球体
        public static MeshData CreateSphere(float radius, int sliceCount, int stackCount)
        {
            var meshData = new MeshData();

            //计算上极点的位置，并沿着堆栈向下移动
            //注意，当矩形纹理映射到球体上时，纹理坐标会发生失真，因为在纹理映射上没有一个唯一的点可以分配给极点

            //上极点（Top vertex）
            meshData.Vertices.Add(new Vertex(new Vector3(0, radius, 0), new Vector3(0, 1, 0), new Vector3(1, 0, 0), Vector2.Zero));

            float phiStep = MathUtil.Pi / stackCount;
            float thetaStep = 2f * MathUtil.Pi / sliceCount;

            for (int i = 1; i <= stackCount - 1; i++)
            {
                float phi = i * phiStep;
                for (int j = 0; j <= sliceCount; j++)
                {
                    float theta = j * thetaStep;

                    //球面到笛卡尔坐标系
                    var pos = new Vector3(
                        radius * MathHelper.Sinf(phi) * MathHelper.Cosf(theta),
                        radius * MathHelper.Cosf(phi),
                        radius * MathHelper.Sinf(phi) * MathHelper.Sinf(theta));

                    //p的偏导
                    var tan = new Vector3(
                        -radius * MathHelper.Sinf(phi) * MathHelper.Sinf(theta),
                        0,
                        radius * MathHelper.Sinf(phi) * MathHelper.Cosf(theta));
                    tan.Normalize();

                    Vector3 norm = pos;
                    norm.Normalize();

                    var texCoord = new Vector2(theta / (MathUtil.Pi * 2), phi / MathUtil.Pi);

                    meshData.Vertices.Add(new Vertex(pos, norm, tan, texCoord));
                }
            }

            //下极点（Bottom vertex）
            meshData.Vertices.Add(new Vertex(0, -radius, 0, 0, -1, 0, 1, 0, 0, 0, 1));

            //计算顶层堆栈的索引，顶部堆栈首先被写入到顶点缓冲区，并将上极点与上层的第一个圆环连接起来
            for (int i = 1; i <= sliceCount; i++)
            {
                meshData.Indices32.Add(0);
                meshData.Indices32.Add(i + 1);
                meshData.Indices32.Add(i);
            }

            //计算内部堆栈的索引（不连接到极点）
            int baseIndex = 1;
            int ringVertexCount = sliceCount + 1;
            for (int i = 0; i < stackCount - 2; i++)
            {
                for (int j = 0; j < sliceCount; j++)
                {
                    meshData.Indices32.Add(baseIndex + i * ringVertexCount + j);
                    meshData.Indices32.Add(baseIndex + i * ringVertexCount + j + 1);
                    meshData.Indices32.Add(baseIndex + (i + 1) * ringVertexCount + j);

                    meshData.Indices32.Add(baseIndex + (i + 1) * ringVertexCount + j);
                    meshData.Indices32.Add(baseIndex + i * ringVertexCount + j + 1);
                    meshData.Indices32.Add(baseIndex + (i + 1) * ringVertexCount + j + 1);
                }
            }

            //计算底层堆栈的索引，底部堆栈首先被写入到顶点缓冲区，并将下极点与下层的第一个圆环连接起来

            int southPoleIndex = meshData.Vertices.Count - 1;

            //将下标偏移到最后一个圆环中第一个顶点的下标
            baseIndex = southPoleIndex - ringVertexCount;

            for (int i = 0; i < sliceCount; i++)
            {
                meshData.Indices32.Add(southPoleIndex);
                meshData.Indices32.Add(baseIndex + i);
                meshData.Indices32.Add(baseIndex + i + 1);
            }
            return meshData;
        }

        //生成几何球体
        public static MeshData CreateGeosphere(float radius, int numSubdivisions)
        {
            //为了生成几何球体，以一个正二十面体为基础，细分其上的三角形，
            //再根据给定的半径向球面投影新生成的顶点，反复重复这个过程，就
            //可以提高几何球体的曲面细分程度

            var meshData = new MeshData();

            //确定细分的次数
            numSubdivisions = Math.Min(numSubdivisions, 6);

            //通过对一个正二十面体的细分来逼近一个球体
            const float x = 0.525731f;
            const float z = 0.850651f;

            Vector3[] positions =
            {
                new Vector3(-x, 0, z), new Vector3(x, 0, z),
                new Vector3(-x, 0, -z), new Vector3(x, 0, -z),
                new Vector3(0, z, x), new Vector3(0, z, -x),
                new Vector3(0, -z, x), new Vector3(0, -z, -x),
                new Vector3(z, x, 0), new Vector3(-z, x, 0),
                new Vector3(z, -x, 0), new Vector3(-z, -x, 0)
                };

            int[] indices =
            {
                1,4,0, 4,9,0, 4,5,9, 8,5,4, 1,8,4,
                1,10,8, 10,3,8, 8,3,5, 3,2,5, 3,7,2,
                3,10,7, 10,6,7, 6,11,7, 6,0,11, 6,1,0,
                10,1,6, 11,0,9, 2,11,9, 5,2,9, 11,2,7
                };

            meshData.Vertices.AddRange(positions.Select(position => new Vertex { Position = position }));
            meshData.Indices32.AddRange(indices);

            for (int i = 0; i < numSubdivisions; i++)
                Subdivide(meshData);

            //将每一个顶点都投影到球面上，并推导其对应的纹理坐标
            for (int i = 0; i < positions.Length; i++)
            {
                //投影到单位球面上
                Vector3 normal = Vector3.Normalize(positions[i]);

                //投射到球面上
                Vector3 position = radius * normal;

                //根据球面坐标推导纹理坐标
                float theta = MathHelper.Atan2f(positions[i].Z, positions[i].X) + MathUtil.Pi;

                float phi = MathHelper.Acosf(positions[i].Y / radius);

                Vector2 texCoord = new Vector2(
                    theta / MathUtil.TwoPi,
                    phi / MathUtil.TwoPi);

                //求出p关于theta的偏导数
                Vector3 tangentU = new Vector3(
                    -radius * MathHelper.Sinf(phi) * MathHelper.Sinf(theta),
                    0.0f,
                    radius * MathHelper.Sinf(phi) * MathHelper.Cosf(theta));

                meshData.Vertices.Add(new Vertex(position, normal, tangentU, texCoord));
            }

            return meshData;
        }

        //生成圆柱体
        public static MeshData CreateCylinder(float bottomRadius, float topRadius,
        float height, int sliceCount, int stackCount)
        {
            var meshData = new MeshData();

            BuildCylinderSide(bottomRadius, topRadius, height, sliceCount, stackCount, meshData);
            BuildCylinderTopCap(topRadius, height, sliceCount, meshData);
            BuildCylinderBottomCap(bottomRadius, height, sliceCount, meshData);

            return meshData;
        }

        //生成网格
        public static MeshData CreateGrid(float width, float depth, int m, int n)
        {
            var meshData = new MeshData();

            //创建顶点

            float halfWidth = 0.5f * width;
            float halfDepth = 0.5f * depth;

            float dx = width / (n - 1);
            float dz = depth / (m - 1);

            float du = 1f / (n - 1);
            float dv = 1f / (m - 1);

            for (int i = 0; i < m; i++)
            {
                float z = halfDepth - i * dz;
                for (int j = 0; j < n; j++)
                {
                    float x = -halfWidth + j * dx;

                    meshData.Vertices.Add(new Vertex(
                        new Vector3(x, 0, z),
                        new Vector3(0, 1, 0),
                        new Vector3(1, 0, 0),
                        new Vector2(j * du, i * dv))); // Stretch texture over grid.
                }
            }

            //创建索引

            //遍历每个quad并计算索引。
            for (int i = 0; i < m - 1; i++)
            {
                for (int j = 0; j < n - 1; j++)
                {
                    meshData.Indices32.Add(i * n + j);
                    meshData.Indices32.Add(i * n + j + 1);
                    meshData.Indices32.Add((i + 1) * n + j);

                    meshData.Indices32.Add((i + 1) * n + j);
                    meshData.Indices32.Add(i * n + j + 1);
                    meshData.Indices32.Add((i + 1) * n + j + 1);
                }
            }

            return meshData;
        }

        public static MeshData CreateQuad(float x, float y, float w, float h, float depth)
        {
            var meshData = new MeshData();

            meshData.Vertices.Add(new Vertex(
                x, y - h, depth,
                0.0f, 0.0f, -1.0f,
                1.0f, 0.0f, 0.0f,
                0.0f, 1.0f));

            meshData.Vertices.Add(new Vertex(
                x, y, depth,
                0.0f, 0.0f, -1.0f,
                1.0f, 0.0f, 0.0f,
                0.0f, 0.0f));

            meshData.Vertices.Add(new Vertex(
                x + w, y, depth,
                0.0f, 0.0f, -1.0f,
                1.0f, 0.0f, 0.0f,
                1.0f, 0.0f));

            meshData.Vertices.Add(new Vertex(
                x + w, y - h, depth,
                0.0f, 0.0f, -1.0f,
                1.0f, 0.0f, 0.0f,
                1.0f, 1.0f));

            meshData.Indices32.Add(0);
            meshData.Indices32.Add(1);
            meshData.Indices32.Add(2);

            meshData.Indices32.Add(0);
            meshData.Indices32.Add(2);
            meshData.Indices32.Add(3);

            return meshData;
        }

        //将一个三角形细分为4个等面积的三角形的过程
        private static void Subdivide(MeshData meshData)
        {
            //保存输入几何图形的副本
            Vertex[] verticesCopy = meshData.Vertices.ToArray();
            int[] indicesCopy = meshData.Indices32.ToArray();

            meshData.Vertices.Clear();
            meshData.Indices32.Clear();

            //       v1
            //       *
            //      / \
            //     /   \
            //  m0*-----*m1
            //   / \   / \
            //  /   \ /   \
            // *-----*-----*
            // v0    m2     v2

            int numTriangles = indicesCopy.Length / 3;
            for (int i = 0; i < numTriangles; i++)
            {
                Vertex v0 = verticesCopy[indicesCopy[i * 3 + 0]];
                Vertex v1 = verticesCopy[indicesCopy[i * 3 + 1]];
                Vertex v2 = verticesCopy[indicesCopy[i * 3 + 2]];

                //生成中点

                Vertex m0 = MidPoint(v0, v1);
                Vertex m1 = MidPoint(v1, v2);
                Vertex m2 = MidPoint(v0, v2);

                //构件图形

                meshData.Vertices.Add(v0);   // 0
                meshData.Vertices.Add(v1);   // 1
                meshData.Vertices.Add(v2);   // 2
                meshData.Vertices.Add(m0);   // 3
                meshData.Vertices.Add(m1);   // 4
                meshData.Vertices.Add(m2);   // 5

                meshData.Indices32.Add(i * 6 + 0);
                meshData.Indices32.Add(i * 6 + 3);
                meshData.Indices32.Add(i * 6 + 5);

                meshData.Indices32.Add(i * 6 + 3);
                meshData.Indices32.Add(i * 6 + 4);
                meshData.Indices32.Add(i * 6 + 5);

                meshData.Indices32.Add(i * 6 + 5);
                meshData.Indices32.Add(i * 6 + 4);
                meshData.Indices32.Add(i * 6 + 2);

                meshData.Indices32.Add(i * 6 + 3);
                meshData.Indices32.Add(i * 6 + 1);
                meshData.Indices32.Add(i * 6 + 4);
            }
        }

        //计算中点
        private static Vertex MidPoint(Vertex v0, Vertex v1)
        {
            //计算所有属性的中点，向量需要归一化
            Vector3 pos = 0.5f * (v0.Position + v1.Position);
            Vector3 normal = Vector3.Normalize(0.5f * (v0.Normal + v1.Normal));
            Vector3 tangent = Vector3.Normalize(0.5f * (v0.TangentU + v1.TangentU));
            Vector2 tex = 0.5f * (v0.TexC + v1.TexC);

            return new Vertex(pos, normal, tangent, tex);
        }

        //构建圆柱体侧面
        private static void BuildCylinderSide(float bottomRadius, float topRadius,
        float height, int sliceCount, int stackCount, MeshData meshData)
        {
            float stackHeight = height / stackCount;

            //当从栈底向上移动到栈顶时，等于递增半径
            float radiusStep = (topRadius - bottomRadius) / stackCount;

            int ringCount = stackCount + 1;

            //计算每个堆栈圆环的顶点，从底部开始向上移动
            for (int i = 0; i < ringCount; i++)
            {
                float y = -0.5f * height + i * stackHeight;
                float r = bottomRadius + i * radiusStep;

                //圆环的顶点
                float dTheta = 2.0f * MathUtil.Pi / sliceCount;
                for (int j = 0; j <= sliceCount; j++)
                {
                    float c = MathHelper.Cosf(j * dTheta);
                    float s = MathHelper.Sinf(j * dTheta);

                    var pos = new Vector3(r * c, y, r * s);
                    var uv = new Vector2((float)j / sliceCount, 1f - (float)i / stackCount);
                    var tangent = new Vector3(-s, 0.0f, c);

                    float dr = bottomRadius - topRadius;
                    var bitangent = new Vector3(dr * c, -height, dr * s);

                    var normal = Vector3.Cross(tangent, bitangent);
                    normal.Normalize();
                    meshData.Vertices.Add(new Vertex(pos, normal, tangent, uv));
                }
            }

            //加1是因为复制了每个环的第一个和最后一个顶点，因此纹理坐标不同
            int ringVertexCount = sliceCount + 1;

            //计算每个堆栈的索引
            for (int i = 0; i < stackCount; i++)
            {
                for (int j = 0; j < sliceCount; j++)
                {
                    meshData.Indices32.Add(i * ringVertexCount + j);
                    meshData.Indices32.Add((i + 1) * ringVertexCount + j);
                    meshData.Indices32.Add((i + 1) * ringVertexCount + j + 1);

                    meshData.Indices32.Add(i * ringVertexCount + j);
                    meshData.Indices32.Add((i + 1) * ringVertexCount + j + 1);
                    meshData.Indices32.Add(i * ringVertexCount + j + 1);
                }
            }
        }

        //构建圆柱体顶面
        private static void BuildCylinderTopCap(float topRadius, float height,
        int sliceCount, MeshData meshData)
        {
            int baseIndex = meshData.Vertices.Count;

            float y = 0.5f * height;
            float dTheta = 2.0f * MathUtil.Pi / sliceCount;

            //复制顶面圆环顶点，因为纹理坐标和法线不同
            for (int i = 0; i <= sliceCount; i++)
            {
                float x = topRadius * MathHelper.Cosf(i * dTheta);
                float z = topRadius * MathHelper.Sinf(i * dTheta);

                //按高度向下缩放，使顶面圆环纹理面积与底面成比例
                float u = x / height + 0.5f;
                float v = z / height + 0.5f;

                meshData.Vertices.Add(new Vertex(
                    new Vector3(x, y, z), new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector2(u, v)));
            }

            //顶面圆环中心顶点
            meshData.Vertices.Add(new Vertex(
                new Vector3(0, y, 0), new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector2(0.5f, 0.5f)));

            //中心顶点的下标
            int centerIndex = meshData.Vertices.Count - 1;

            for (int i = 0; i < sliceCount; i++)
            {
                meshData.Indices32.Add(centerIndex);
                meshData.Indices32.Add(baseIndex + i + 1);
                meshData.Indices32.Add(baseIndex + i);
            }
        }

        //构建圆柱体底面
        private static void BuildCylinderBottomCap(float bottomRadius, float height,
            int sliceCount, MeshData meshData)
        {
            int baseIndex = meshData.Vertices.Count;
            float y = -0.5f * height;

            //圆环的顶点
            float dTheta = 2.0f * MathUtil.Pi / sliceCount;
            for (int i = 0; i <= sliceCount; i++)
            {
                float x = bottomRadius * MathHelper.Cosf(i * dTheta);
                float z = bottomRadius * MathHelper.Sinf(i * dTheta);

                //按高度向下缩放，使顶面圆环纹理面积与底面成比例
                float u = x / height + 0.5f;
                float v = z / height + 0.5f;

                meshData.Vertices.Add(new Vertex(new Vector3(x, y, z), new Vector3(0, -1, 0), new Vector3(1, 0, 0), new Vector2(u, v)));
            }

            //顶面圆环中心顶点
            meshData.Vertices.Add(new Vertex(new Vector3(0, y, 0), new Vector3(0, -1, 0), new Vector3(1, 0, 0), new Vector2(0.5f, 0.5f)));

            //缓存中心顶点的索引
            int centerIndex = meshData.Vertices.Count - 1;

            for (int i = 0; i < sliceCount; i++)
            {
                meshData.Indices32.Add(centerIndex);
                meshData.Indices32.Add(baseIndex + i + 1);
                meshData.Indices32.Add(baseIndex + i);
            }
        }

    }
}
