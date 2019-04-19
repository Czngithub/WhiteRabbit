using System;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct3D12;
using WhiteRabbit.Framework;

namespace WhiteRabbit.XLoader
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct ObjectConstants
    {
        public Matrix World;
        public Matrix TexTransform;

        public static ObjectConstants Default => new ObjectConstants
        {
            World = Matrix.Identity,
            TexTransform = Matrix.Identity
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

        //环境光
        public Vector4 AmbientLight;

        //光源
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = Light.MaxLights)]
        public Light[] Lights;

        public static PassConstants Default => new PassConstants
        {
            View = Matrix.Identity,
            InvView = Matrix.Identity,
            Proj = Matrix.Identity,
            InvProj = Matrix.Identity,
            ViewProj = Matrix.Identity,
            InvViewProj = Matrix.Identity,
            AmbientLight = Vector4.UnitW,
            Lights = Light.DefaultArray
        };
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct Vertex
    {
        public Vector4 Pos;
        public Vector4 Normal;
        public Vector2 TexC;
    }

    internal class FrameResource : IDisposable
    {
        public FrameResource(Device device, int passCount, int objectCount, int materialCount)
        {
            CmdListAlloc = device.CreateCommandAllocator(CommandListType.Direct);

            PassCB = new UploadBuffer<PassConstants>(device, passCount, true);
            MaterialCB = new UploadBuffer<MaterialConstants>(device, materialCount, true);
            ObjectCB = new UploadBuffer<ObjectConstants>(device, objectCount, true);

        }

        //直到GPU完成处理命令之前都不能重置分配器，因此，每个帧都需要自己的分配器。
        public CommandAllocator CmdListAlloc { get; }

        //直到GPU处理引用的cbuffer的命令之前都不能更新cbuffer，所以每个帧都需要自己的cbuffer
        public UploadBuffer<PassConstants> PassCB { get; }
        public UploadBuffer<MaterialConstants> MaterialCB { get; }
        public UploadBuffer<ObjectConstants> ObjectCB { get; }

        //将命令标记到此栅栏点的Fence值以检查这些帧资源是否仍然被GPU使用
        public long Fence { get; set; }

        public void Dispose()
        {
            ObjectCB.Dispose();
            MaterialCB.Dispose();
            PassCB.Dispose();
            CmdListAlloc.Dispose();
        }
    }
}
