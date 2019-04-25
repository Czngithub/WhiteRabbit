using System;
using System.Linq;
using System.Runtime.InteropServices;
using SharpDX.Direct3D12;
using SharpDX.DXGI;
using Resource = SharpDX.Direct3D12.Resource;
using Device = SharpDX.Direct3D12.Device;
using System.Drawing;
using SharpDX;

namespace WhiteRabbit.Framework
{
    public static class XTexUtilities
    {
        public static Resource LoadFromTgaFile(Device device, string filename)
        {
            var tga = new Paloma.TargaImage(filename);
            return Load(device, tga.Image);
        }

        public static Resource LoadFromWicFile(Device device, string filename)
        {

            var image = new System.Drawing.Bitmap(filename);
            return Load(device, image);
        }

        static Resource Load(Device device, Bitmap image)
        {
            var boundsRect = new System.Drawing.Rectangle(0, 0, image.Width, image.Height);
            var bitmap = image.Clone(boundsRect, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var mapSrc = bitmap.LockBits(boundsRect, System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);
            var databox = new[] { new DataBox(mapSrc.Scan0, bitmap.Width * 4, bitmap.Height) };

            ResourceDescription textureDesc = new ResourceDescription()
            {
                MipLevels = 1,
                Format = Format.R8G8B8A8_UNorm,
                Width = bitmap.Width,
                Height = bitmap.Height,
                Flags = ResourceFlags.None,
                DepthOrArraySize = 1,
                SampleDescription = new SampleDescription(1, 0),
                Dimension = ResourceDimension.Texture2D,
            };

            var buffer = device.CreateCommittedResource(new HeapProperties(HeapType.Upload), HeapFlags.None, textureDesc, ResourceStates.GenericRead);

            bitmap.UnlockBits(mapSrc);

            System.Drawing.Imaging.BitmapData data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            buffer.WriteToSubresource(0, new ResourceRegion()
            {
                Back = 1,
                Bottom = bitmap.Height,
                Right = bitmap.Width
            }, data.Scan0, 4 * bitmap.Width, 4 * bitmap.Width * bitmap.Height);
            int bufferSize = data.Height * data.Stride;
            bitmap.UnlockBits(data);

            return buffer;
        }
    }
}
