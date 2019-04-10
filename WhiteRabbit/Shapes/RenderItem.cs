using SharpDX;
using SharpDX.Direct3D;
using WhiteRabbit.Framework;

namespace WhiteRabbit.Shapes
{
    /// <summary>
    /// 包含绘制形状的参数的轻量级结构，该结构因应用程序不同而不同
    /// </summary>
    internal class RenderItem
    {
        //描述对象相对于世界空间的局部空间的形状的世界矩阵，它定义了对象在世界中的位置、方向和比例
        public Matrix World { get; set; } = Matrix.Identity;

        //dirty flag指示对象数据已更改，需要更新常量缓冲区
        //因为每一个FrameResource都有一个cbuffer，所以必须对每个FrameResource更新
        //因此当修改obect数据时应设置NumFramesDirty = gNumFrameResources，以便每个帧资源都得到更新
        public int NumFramesDirty { get; set; } = D3DApp.NumFrameResources;

        //索引到与此呈现项的ObjectCB对应的GPU常量缓冲区中
        public int ObjCBIndex { get; set; } = -1;

        public MeshGeometry Geo { get; set; }

        //拓扑
        public PrimitiveTopology PrimitiveType { get; set; } = PrimitiveTopology.TriangleList;

        //DrawIndexedInstanced参数
        public int IndexCount { get; set; }
        public int StartIndexLocation { get; set; }
        public int BaseVertexLocation { get; set; }
    }

    internal enum RenderLayer
    {
        Opaque
    }
}
