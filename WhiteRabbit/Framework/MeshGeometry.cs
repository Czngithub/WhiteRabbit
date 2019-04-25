using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SharpDX;
using SharpDX.Direct3D12;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D12.Device;
using Resource = SharpDX.Direct3D12.Resource;

/// <summary>
/// 当需要定义多个几何体时，使用MeshGeometry类
/// 借此结构体还可以将顶点和索引数据置于系统内存中，以供CPU读取，如执行拾取（picking）和碰撞检测（collision detection）
/// 该结构体还缓存了顶点缓冲区和索引缓冲区的一些重要属性，如格式和每个顶点所占用的字节数，并提供了返回缓冲区视图的方法
/// </summary>
namespace WhiteRabbit.Framework
{
    //利用SubmeshGeometry定义MeshGeometry存储的单个几何体
    //此结构适用于将单个几何体数据存于一个顶点缓冲区和一个索引缓冲区的情况
    //他提供了对存于顶点缓冲区和索引缓冲区中的单个几何体进行绘制所需的数据和偏移量
    public class SubmeshGeometry
    {
        public int IndexCount { get; set; } //每个实例要绘制的索引数量
        public int StartIndexLocation { get; set; } //指向索引缓冲区中的某个元素，将其标记为预读取的起始索引
        public int BaseVertexLocation { get; set; } //基准顶点地址，在本次绘制调用读取顶点之前，要为每个索引都加上此整数值

        //通过此子网格来定义当前SubmeshGeometry结构体中所存几何体的包围盒（bounding box）
        public BoundingBox Bounds { get; set; }
    }

    public class MeshGeometry : IDisposable
    {
        private readonly List<IDisposable> toDispose = new List<IDisposable>();

        //使用MeshGeometry.New工厂方法构造一个MeshGeometry实例
        private MeshGeometry() { }

        //指定几何体网格集合的名称，这样就能根据此名找到它
        public string Name { get; set; }

        //系统内存中的缓冲区副本
        public Resource VertexBufferGPU { get; set; }
        public Resource IndexBufferGPU { get; set; }

        public object VertexBufferCPU { get; set; }
        public object IndexBufferCPU { get; set; }

        //与缓冲区相关的数据
        public int VertexByteStride { get; set; }
        public int VertexBufferByteSize { get; set; }
        public Format IndexFormat { get; set; }
        public int IndexBufferByteSize { get; set; }
        public int IndexCount { get; set; }

        //一个MeshGeometry结构体能够存储一组顶点/索引缓冲区的多个几何体
        //利用下列容器来定义子网格几何体，就能单独绘制出其中的子网格（单个几何体）
        public Dictionary<string, SubmeshGeometry> DrawArgs { get; } = new Dictionary<string, SubmeshGeometry>();

        public VertexBufferView VertexBufferView => new VertexBufferView
        {
            BufferLocation = VertexBufferGPU.GPUVirtualAddress,
            StrideInBytes = VertexByteStride,
            SizeInBytes = VertexBufferByteSize
        };

        public IndexBufferView IndexBufferView => new IndexBufferView
        {
            BufferLocation = IndexBufferGPU.GPUVirtualAddress,
            Format = IndexFormat,
            SizeInBytes = IndexBufferByteSize
        };

        public void Dispose()
        {
            foreach (IDisposable disposable in toDispose)
                disposable.Dispose();
        }

        //创建MeshGeometry的泛型类型
        public static MeshGeometry New<TVertex, TIndex>(
            Device device,
            GraphicsCommandList commandList,
            IEnumerable<TVertex> vertices,
            IEnumerable<TIndex> indices,
            string name = "Default")
            where TVertex : struct
            where TIndex : struct
        {
            TVertex[] vertexArray = vertices.ToArray();
            TIndex[] indexArray = indices.ToArray();

            int vertexBufferByteSize = Utilities.SizeOf(vertexArray);
            Resource vertexBuffer = D3DUtil.CreateDefaultBuffer(
                device,
                commandList,
                vertexArray,
                vertexBufferByteSize,
                out Resource vertexBufferUploader);

            int indexBufferByteSize = Utilities.SizeOf(indexArray);
            Resource indexBuffer = D3DUtil.CreateDefaultBuffer(
                device, commandList,
                indexArray,
                indexBufferByteSize,
                out Resource indexBufferUploader);

            return new MeshGeometry
            {
                Name = name,
                VertexByteStride = Utilities.SizeOf<TVertex>(),
                VertexBufferByteSize = vertexBufferByteSize,
                VertexBufferGPU = vertexBuffer,
                VertexBufferCPU = vertexArray,
                IndexCount = indexArray.Length,
                IndexFormat = GetIndexFormat<TIndex>(),
                IndexBufferByteSize = indexBufferByteSize,
                IndexBufferGPU = indexBuffer,
                IndexBufferCPU = indexArray,
                toDispose =
                {
                    vertexBuffer, vertexBufferUploader,
                    indexBuffer, indexBufferUploader
                }
            };
        }

        private static Format GetIndexFormat<TIndex>()
        {
            var format = Format.Unknown;
            if (typeof(TIndex) == typeof(int))
                format = Format.R32_UInt;
            else if (typeof(TIndex) == typeof(short))
                format = Format.R16_UInt;

            //Debug.Assert(format != Format.Unknown);

            return format;
        }
    }
}
