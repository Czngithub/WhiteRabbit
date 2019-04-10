using System;
using System.Runtime.InteropServices;
using SharpDX.Direct3D12;

namespace WhiteRabbit.Framework
{
    public class UploadBuffer<T> : IDisposable where T : struct
    {
        private readonly int elementByteSize;
        private readonly IntPtr resourcePointer;

        public UploadBuffer(Device device, int elementCount, bool isConstantBuffer)
        {
            elementByteSize = isConstantBuffer
                   ? D3DUtil.CalcConstantBufferByteSize<T>()
                   : Marshal.SizeOf(typeof(T));

            Resource = device.CreateCommittedResource(
                new HeapProperties(HeapType.Upload),
                HeapFlags.None,
                ResourceDescription.Buffer(elementByteSize * elementCount),
                ResourceStates.GenericRead);

            resourcePointer = Resource.Map(0);
        }

        public Resource Resource { get; }

        public void CopyData(int elementIndex, ref T data)
        {
            Marshal.StructureToPtr(data, resourcePointer + elementIndex * elementByteSize, true);
        }

        public void Dispose()
        {
            Resource.Unmap(0);
            Resource.Dispose();
        }
    }
}
