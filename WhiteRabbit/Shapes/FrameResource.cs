using System;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct3D12;
using WhiteRabbit.Framework;

namespace WhiteRabbit.Shapes
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct ObjectConstants
    {
        public Matrix World;

        public static ObjectConstants Default => new ObjectConstants
        {
            World = Matrix.Identity
        };
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct PassConstants
    {
        public Matrix View;
        public Matrix InvView;
        public Matrix Proj;
        public Matrix InvProj;
        public Matrix ViewProj;
        public Matrix InvViewProj;
        public Vector3 EyePosW;
        public float PerObjectPad1;
        public Vector2 RenderTargetSize;
        public Vector2 InvRenderTargetSize;
        public float NearZ;
        public float FarZ;
        public float TotalTime;
        public float DeltaTime;

        public static PassConstants Default => new PassConstants
        {
            View = Matrix.Identity,
            InvView = Matrix.Identity,
            Proj = Matrix.Identity,
            InvProj = Matrix.Identity,
            ViewProj = Matrix.Identity,
            InvViewProj = Matrix.Identity
        };
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct Vertex
    {
        public Vector3 Pos;
        public Vector4 Color;
    }

    internal class FrameResource : IDisposable
    {
        public FrameResource(Device device, int passCount, int objectCount)
        {
            CmdListAlloc = device.CreateCommandAllocator(CommandListType.Direct);
            PassCB = new UploadBuffer<PassConstants>(device, passCount, true);
            ObjectCB = new UploadBuffer<ObjectConstants>(device, objectCount, true);
        }

        //直到GPU完成处理命令之前都不能重置分配器
        //因此，每个帧都需要自己的分配器
        public CommandAllocator CmdListAlloc { get; }

        //在GPU处理引用cbuffer的命令之前，都不能更新cbuffer所以每个帧都需要自己的cbuffer
        public UploadBuffer<PassConstants> PassCB { get; }
        public UploadBuffer<ObjectConstants> ObjectCB { get; }

        //要将命令标记到此栅栏点的Fence值，检查这些帧资源是否仍然被GPU使用
        public long Fence { get; set; }

        public void Dispose()
        {
            ObjectCB.Dispose();
            PassCB.Dispose();
            CmdListAlloc.Dispose();
        }
    }
}
