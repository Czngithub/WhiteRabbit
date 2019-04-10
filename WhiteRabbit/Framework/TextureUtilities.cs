﻿using System;
using System.Linq;
using System.Runtime.InteropServices;
using SharpDX.Direct3D12;
using SharpDX.DXGI;
using Resource = SharpDX.Direct3D12.Resource;
using Device = SharpDX.Direct3D12.Device;

/// <summary>
/// DDS文件的自定义加载程序
/// </summary>
namespace WhiteRabbit.Framework
{
    public class TextureUtilities
    {
        const int DDS_MAGIC = 0x20534444;//DDS

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct DDS_PIXELFORMAT
        {
            public int size;
            public int flags;
            public int fourCC;
            public int RGBBitCount;
            public uint RBitMask;
            public uint GBitMask;
            public uint BBitMask;
            public uint ABitMask;
        };

        const int DDS_FOURCC = 0x00000004;// DDPF_FOURCC
        const int DDS_RGB = 0x00000040;// DDPF_RGB
        const int DDS_RGBA = 0x00000041;// DDPF_RGB | DDPF_ALPHAPIXELS
        const int DDS_LUMINANCE = 0x00020000;// DDPF_LUMINANCE
        const int DDS_LUMINANCEA = 0x00020001;// DDPF_LUMINANCE | DDPF_ALPHAPIXELS
        const int DDS_ALPHA = 0x00000002;// DDPF_ALPHA
        const int DDS_PAL8 = 0x00000020;// DDPF_PALETTEINDEXED8

        const int DDS_HEADER_FLAGS_TEXTURE = 0x00001007;// DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PIXELFORMAT
        const int DDS_HEADER_FLAGS_MIPMAP = 0x00020000;// DDSD_MIPMAPCOUNT
        const int DDS_HEADER_FLAGS_VOLUME = 0x00800000;// DDSD_DEPTH
        const int DDS_HEADER_FLAGS_PITCH = 0x00000008;// DDSD_PITCH
        const int DDS_HEADER_FLAGS_LINEARSIZE = 0x00080000;// DDSD_LINEARSIZE

        const int DDS_HEIGHT = 0x00000002;// DDSD_HEIGHT
        const int DDS_WIDTH = 0x00000004;// DDSD_WIDTH

        const int DDS_SURFACE_FLAGS_TEXTURE = 0x00001000;// DDSCAPS_TEXTURE
        const int DDS_SURFACE_FLAGS_MIPMAP = 0x00400008;// DDSCAPS_COMPLEX | DDSCAPS_MIPMAP
        const int DDS_SURFACE_FLAGS_CUBEMAP = 0x00000008;// DDSCAPS_COMPLEX

        const int DDS_CUBEMAP_POSITIVEX = 0x00000600;// DDSCAPS2_CUBEMAP | DDSCAPS2_CUBEMAP_POSITIVEX
        const int DDS_CUBEMAP_NEGATIVEX = 0x00000a00;// DDSCAPS2_CUBEMAP | DDSCAPS2_CUBEMAP_NEGATIVEX
        const int DDS_CUBEMAP_POSITIVEY = 0x00001200;// DDSCAPS2_CUBEMAP | DDSCAPS2_CUBEMAP_POSITIVEY
        const int DDS_CUBEMAP_NEGATIVEY = 0x00002200;// DDSCAPS2_CUBEMAP | DDSCAPS2_CUBEMAP_NEGATIVEY
        const int DDS_CUBEMAP_POSITIVEZ = 0x00004200;// DDSCAPS2_CUBEMAP | DDSCAPS2_CUBEMAP_POSITIVEZ
        const int DDS_CUBEMAP_NEGATIVEZ = 0x00008200;// DDSCAPS2_CUBEMAP | DDSCAPS2_CUBEMAP_NEGATIVEZ

        const int DDS_CUBEMAP_ALLFACES = (DDS_CUBEMAP_POSITIVEX | DDS_CUBEMAP_NEGATIVEX | DDS_CUBEMAP_POSITIVEY | DDS_CUBEMAP_NEGATIVEY | DDS_CUBEMAP_POSITIVEZ | DDS_CUBEMAP_NEGATIVEZ);

        const int DDS_CUBEMAP = 0x00000200;// DDSCAPS2_CUBEMAP

        const int DDS_FLAGS_VOLUME = 0x00200000;// DDSCAPS2_VOLUME

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct DDS_HEADER
        {
            public int size;
            public int flags;
            public int height;
            public int width;
            public int pitchOrLinearSize;
            public int depth; //仅当DDS_HEADER_FLAGS_VOLUME在flags中时设置
            public int mipMapCount;
            //===11
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 11)]
            public int[] reserved1;

            public DDS_PIXELFORMAT ddspf;
            public int caps;
            public int caps2;
            public int caps3;
            public int caps4;
            public int reserved2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct DDS_HEADER_DXT10
        {
            public Format dxgiFormat;
            public int resourceDimension;
            public int miscFlag; //
            public int arraySize;
            public int reserved;
        }

        static int BitsPerPixel(Format fmt)
        {
            switch (fmt)
            {
                case Format.R32G32B32A32_Typeless:
                case Format.R32G32B32A32_Float:
                case Format.R32G32B32A32_UInt:
                case Format.R32G32B32A32_SInt:
                    return 128;

                case Format.R32G32B32_Typeless:
                case Format.R32G32B32_Float:
                case Format.R32G32B32_UInt:
                case Format.R32G32B32_SInt:
                    return 96;

                case Format.R16G16B16A16_Typeless:
                case Format.R16G16B16A16_Float:
                case Format.R16G16B16A16_UNorm:
                case Format.R16G16B16A16_UInt:
                case Format.R16G16B16A16_SNorm:
                case Format.R16G16B16A16_SInt:
                case Format.R32G32_Typeless:
                case Format.R32G32_Float:
                case Format.R32G32_UInt:
                case Format.R32G32_SInt:
                case Format.R32G8X24_Typeless:
                case Format.D32_Float_S8X24_UInt:
                case Format.R32_Float_X8X24_Typeless:
                case Format.X32_Typeless_G8X24_UInt:
                    return 64;

                case Format.R10G10B10A2_Typeless:
                case Format.R10G10B10A2_UNorm:
                case Format.R10G10B10A2_UInt:
                case Format.R11G11B10_Float:
                case Format.R8G8B8A8_Typeless:
                case Format.R8G8B8A8_UNorm:
                case Format.R8G8B8A8_UNorm_SRgb:
                case Format.R8G8B8A8_UInt:
                case Format.R8G8B8A8_SNorm:
                case Format.R8G8B8A8_SInt:
                case Format.R16G16_Typeless:
                case Format.R16G16_Float:
                case Format.R16G16_UNorm:
                case Format.R16G16_UInt:
                case Format.R16G16_SNorm:
                case Format.R16G16_SInt:
                case Format.R32_Typeless:
                case Format.D32_Float:
                case Format.R32_Float:
                case Format.R32_UInt:
                case Format.R32_SInt:
                case Format.R24G8_Typeless:
                case Format.D24_UNorm_S8_UInt:
                case Format.R24_UNorm_X8_Typeless:
                case Format.X24_Typeless_G8_UInt:
                case Format.R9G9B9E5_Sharedexp:
                case Format.R8G8_B8G8_UNorm:
                case Format.G8R8_G8B8_UNorm:
                case Format.B8G8R8A8_UNorm:
                case Format.B8G8R8X8_UNorm:
                case Format.R10G10B10_Xr_Bias_A2_UNorm:
                case Format.B8G8R8A8_Typeless:
                case Format.B8G8R8A8_UNorm_SRgb:
                case Format.B8G8R8X8_Typeless:
                case Format.B8G8R8X8_UNorm_SRgb:
                    return 32;

                case Format.R8G8_Typeless:
                case Format.R8G8_UNorm:
                case Format.R8G8_UInt:
                case Format.R8G8_SNorm:
                case Format.R8G8_SInt:
                case Format.R16_Typeless:
                case Format.R16_Float:
                case Format.D16_UNorm:
                case Format.R16_UNorm:
                case Format.R16_UInt:
                case Format.R16_SNorm:
                case Format.R16_SInt:
                case Format.B5G6R5_UNorm:
                case Format.B5G5R5A1_UNorm:
                case Format.B4G4R4A4_UNorm:
                    return 16;

                case Format.R8_Typeless:
                case Format.R8_UNorm:
                case Format.R8_UInt:
                case Format.R8_SNorm:
                case Format.R8_SInt:
                case Format.A8_UNorm:
                    return 8;

                case Format.R1_UNorm:
                    return 1;

                case Format.BC1_Typeless:
                case Format.BC1_UNorm:
                case Format.BC1_UNorm_SRgb:
                case Format.BC4_Typeless:
                case Format.BC4_UNorm:
                case Format.BC4_SNorm:
                    return 4;

                case Format.BC2_Typeless:
                case Format.BC2_UNorm:
                case Format.BC2_UNorm_SRgb:
                case Format.BC3_Typeless:
                case Format.BC3_UNorm:
                case Format.BC3_UNorm_SRgb:
                case Format.BC5_Typeless:
                case Format.BC5_UNorm:
                case Format.BC5_SNorm:
                case Format.BC6H_Typeless:
                case Format.BC6H_Uf16:
                case Format.BC6H_Sf16:
                case Format.BC7_Typeless:
                case Format.BC7_UNorm:
                case Format.BC7_UNorm_SRgb:
                    return 8;

                default:
                    return 0;
            }
        }

        //获取特定格式的表面信息
        static void GetSurfaceInfo(int width, int height, Format fmt, out int outNumBytes, out int outRowBytes, out int outNumRows)
        {
            int numBytes = 0;
            int rowBytes = 0;
            int numRows = 0;

            bool bc = false;
            bool packed = false;
            int bcnumBytesPerBlock = 0;
            switch (fmt)
            {
                case Format.BC1_Typeless:
                case Format.BC1_UNorm:
                case Format.BC1_UNorm_SRgb:
                case Format.BC4_Typeless:
                case Format.BC4_UNorm:
                case Format.BC4_SNorm:
                    bc = true;
                    bcnumBytesPerBlock = 8;
                    break;

                case Format.BC2_Typeless:
                case Format.BC2_UNorm:
                case Format.BC2_UNorm_SRgb:
                case Format.BC3_Typeless:
                case Format.BC3_UNorm:
                case Format.BC3_UNorm_SRgb:
                case Format.BC5_Typeless:
                case Format.BC5_UNorm:
                case Format.BC5_SNorm:
                case Format.BC6H_Typeless:
                case Format.BC6H_Uf16:
                case Format.BC6H_Sf16:
                case Format.BC7_Typeless:
                case Format.BC7_UNorm:
                case Format.BC7_UNorm_SRgb:
                    bc = true;
                    bcnumBytesPerBlock = 16;
                    break;

                case Format.R8G8_B8G8_UNorm:
                case Format.G8R8_G8B8_UNorm:
                    packed = true;
                    break;
            }

            if (bc)
            {
                int numBlocksWide = 0;
                if (width > 0)
                {
                    numBlocksWide = Math.Max(1, (width + 3) / 4);
                }
                int numBlocksHigh = 0;
                if (height > 0)
                {
                    numBlocksHigh = Math.Max(1, (height + 3) / 4);
                }
                rowBytes = numBlocksWide * bcnumBytesPerBlock;
                numRows = numBlocksHigh;
            }
            else if (packed)
            {
                rowBytes = ((width + 1) >> 1) * 4;
                numRows = height;
            }
            else
            {
                int bpp = BitsPerPixel(fmt);
                rowBytes = (width * bpp + 7) / 8; //四舍五入
                numRows = height;
            }

            numBytes = rowBytes * numRows;

            outNumBytes = numBytes;
            outRowBytes = rowBytes;
            outNumRows = numRows;
        }

        static bool ISBITMASK(DDS_PIXELFORMAT ddpf, uint r, uint g, uint b, uint a)
        {
            return (ddpf.RBitMask == r && ddpf.GBitMask == g && ddpf.BBitMask == b && ddpf.ABitMask == a);
        }

        static int MAKEFOURCC(int ch0, int ch1, int ch2, int ch3)
        {
            return ((int)(byte)(ch0) | ((int)(byte)(ch1) << 8) | ((int)(byte)(ch2) << 16) | ((int)(byte)(ch3) << 24));
        }

        static Format GetDXGIFormat(DDS_PIXELFORMAT ddpf)
        {

            if ((ddpf.flags & DDS_RGB) > 0)
            {
                //注意sRGB格式是使用DX10 head编写的

                switch (ddpf.RGBBitCount)
                {
                    case 32:
                        if (ISBITMASK(ddpf, 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000))
                        {
                            return Format.R8G8B8A8_UNorm;
                        }

                        if (ISBITMASK(ddpf, 0x00ff0000, 0x0000ff00, 0x000000ff, 0xff000000))
                        {
                            return Format.B8G8R8A8_UNorm;
                        }

                        if (ISBITMASK(ddpf, 0x00ff0000, 0x0000ff00, 0x000000ff, 0x00000000))
                        {
                            return Format.B8G8R8X8_UNorm;
                        }

                        if (ISBITMASK(ddpf, 0x3ff00000, 0x000ffc00, 0x000003ff, 0xc0000000))
                        {
                            return Format.R10G10B10A2_UNorm;
                        }

                        if (ISBITMASK(ddpf, 0x0000ffff, 0xffff0000, 0x00000000, 0x00000000))
                        {
                            return Format.R16G16_UNorm;
                        }

                        if (ISBITMASK(ddpf, 0xffffffff, 0x00000000, 0x00000000, 0x00000000))
                        {
                            //D3D9中只有R32F是32位的彩色通道格式
                            return Format.R32_Float;
                        }
                        break;

                    case 24:
                        break;

                    case 16:
                        if (ISBITMASK(ddpf, 0x7c00, 0x03e0, 0x001f, 0x8000))
                        {
                            return Format.B5G5R5A1_UNorm;
                        }
                        if (ISBITMASK(ddpf, 0xf800, 0x07e0, 0x001f, 0x0000))
                        {
                            return Format.B5G6R5_UNorm;
                        }

                        if (ISBITMASK(ddpf, 0x0f00, 0x00f0, 0x000f, 0xf000))
                        {
                            return Format.B4G4R4A4_UNorm;
                        }

                        break;
                }
            }
            else if ((ddpf.flags & DDS_LUMINANCE) > 0)
            {
                if (8 == ddpf.RGBBitCount)
                {
                    if (ISBITMASK(ddpf, 0x000000ff, 0x00000000, 0x00000000, 0x00000000))
                    {
                        return Format.R8_UNorm; //D3DX10/11将其写成DX10扩展名
                    }
                }

                if (16 == ddpf.RGBBitCount)
                {
                    if (ISBITMASK(ddpf, 0x0000ffff, 0x00000000, 0x00000000, 0x00000000))
                    {
                        return Format.R16_UNorm; //D3DX10/11将其写成DX10扩展名
                    }
                    if (ISBITMASK(ddpf, 0x000000ff, 0x00000000, 0x00000000, 0x0000ff00))
                    {
                        return Format.R8G8_UNorm; //D3DX10/11将其写成DX10扩展名
                    }
                }
            }
            else if ((ddpf.flags & DDS_ALPHA) > 0)
            {
                if (8 == ddpf.RGBBitCount)
                {
                    return Format.A8_UNorm;
                }
            }
            else if ((ddpf.flags & DDS_FOURCC) > 0)
            {
                if (MAKEFOURCC('D', 'X', 'T', '1') == ddpf.fourCC)
                {
                    return Format.BC1_UNorm;
                }
                if (MAKEFOURCC('D', 'X', 'T', '3') == ddpf.fourCC)
                {
                    return Format.BC2_UNorm;
                }
                if (MAKEFOURCC('D', 'X', 'T', '5') == ddpf.fourCC)
                {
                    return Format.BC3_UNorm;
                }

                //虽然DXGI格式不直接支持pre-mulitplied alpha，但它们基本上与这些BC格式相同，因此可以映射它们
                if (MAKEFOURCC('D', 'X', 'T', '2') == ddpf.fourCC)
                {
                    return Format.BC2_UNorm;
                }
                if (MAKEFOURCC('D', 'X', 'T', '4') == ddpf.fourCC)
                {
                    return Format.BC3_UNorm;
                }

                if (MAKEFOURCC('A', 'T', 'I', '1') == ddpf.fourCC)
                {
                    return Format.BC4_UNorm;
                }
                if (MAKEFOURCC('B', 'C', '4', 'U') == ddpf.fourCC)
                {
                    return Format.BC4_UNorm;
                }
                if (MAKEFOURCC('B', 'C', '4', 'S') == ddpf.fourCC)
                {
                    return Format.BC4_SNorm;
                }

                if (MAKEFOURCC('A', 'T', 'I', '2') == ddpf.fourCC)
                {
                    return Format.BC5_UNorm;
                }
                if (MAKEFOURCC('B', 'C', '5', 'U') == ddpf.fourCC)
                {
                    return Format.BC5_UNorm;
                }
                if (MAKEFOURCC('B', 'C', '5', 'S') == ddpf.fourCC)
                {
                    return Format.BC5_SNorm;
                }

                //BC6H和BC7是使用“DX10”扩展头编写的

                if (MAKEFOURCC('R', 'G', 'B', 'G') == ddpf.fourCC)
                {
                    return Format.R8G8_B8G8_UNorm;
                }
                if (MAKEFOURCC('G', 'R', 'G', 'B') == ddpf.fourCC)
                {
                    return Format.G8R8_G8B8_UNorm;
                }

                //检查这里是否设置了D3DFORMAT枚举
                switch (ddpf.fourCC)
                {
                    case 36: // D3DFMT_A16B16G16R16
                        return Format.R16G16B16A16_UNorm;

                    case 110: // D3DFMT_Q16W16V16U16
                        return Format.R16G16B16A16_SNorm;

                    case 111: // D3DFMT_R16F
                        return Format.R16_Float;

                    case 112: // D3DFMT_G16R16F
                        return Format.R16G16_Float;

                    case 113: // D3DFMT_A16B16G16R16F
                        return Format.R16G16B16A16_Float;

                    case 114: // D3DFMT_R32F
                        return Format.R32_Float;

                    case 115: // D3DFMT_G32R32F
                        return Format.R32G32_Float;

                    case 116: // D3DFMT_A32B32G32R32F
                        return Format.R32G32B32A32_Float;
                }
            }

            return Format.Unknown;
        }

        static T ByteArrayToStructure<T>(byte[] bytes, int start, int count) where T : struct
        {

            byte[] temp = bytes.Skip(start).Take(count).ToArray();
            GCHandle handle = GCHandle.Alloc(temp, GCHandleType.Pinned);
            T stuff = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return stuff;
        }

        static void FillInitData(Resource texture, int width, int height, int depth, int mipCount, int arraySize, Format format, int maxsize, int bitSize, byte[] bitData, int offset)
        {
            int NumBytes = 0;
            int RowBytes = 0;
            int NumRows = 0;
            byte[] pSrcBits = bitData;
            byte[] pEndBits = bitData;// + bitSize;

            int index = 0;
            int k = offset;


            for (int j = 0; j < arraySize; j++)
            {
                int w = width;
                int h = height;
                int d = depth;
                for (int i = 0; i < mipCount; i++)
                {
                    GetSurfaceInfo(w, h, format, out NumBytes, out RowBytes, out NumRows);

                    GCHandle handle = GCHandle.Alloc(bitData, GCHandleType.Pinned);
                    IntPtr ptr = Marshal.UnsafeAddrOfPinnedArrayElement(bitData, k);
                    texture.WriteToSubresource(index, null, ptr, RowBytes, NumBytes);
                    handle.Free();

                    index++;

                    k += NumBytes * d;

                    w = w >> 1;
                    h = h >> 1;
                    d = d >> 1;
                    if (w == 0)
                    {
                        w = 1;
                    }
                    if (h == 0)
                    {
                        h = 1;
                    }
                    if (d == 0)
                    {
                        d = 1;
                    }
                }
            }
        }

        static Resource CreateTextureFromDDS(Device d3dDevice, DDS_HEADER header, DDS_HEADER_DXT10? header10, byte[] bitData, int offset, int maxsize, out bool isCubeMap)
        {
            int width = header.width;
            int height = header.height;
            int depth = header.depth;

            ResourceDimension resDim = ResourceDimension.Unknown;
            int arraySize = 1;
            Format format = Format.Unknown;
            isCubeMap = false;

            int mipCount = header.mipMapCount;
            if (0 == mipCount)
            {
                mipCount = 1;
            }

            if (((header.ddspf.flags & DDS_FOURCC) > 0) && (MAKEFOURCC('D', 'X', '1', '0') == header.ddspf.fourCC))
            {
                DDS_HEADER_DXT10 d3d10ext = header10.Value;

                arraySize = d3d10ext.arraySize;
                if (arraySize == 0)
                {
                    throw new Exception();
                }

                if (BitsPerPixel(d3d10ext.dxgiFormat) == 0)
                {
                    throw new Exception();
                }

                format = d3d10ext.dxgiFormat;

                switch ((ResourceDimension)d3d10ext.resourceDimension)
                {
                    case ResourceDimension.Texture1D:
                        if ((header.flags & DDS_HEIGHT) > 0 && height != 1)
                        {
                            throw new Exception();
                        }
                        height = depth = 1;
                        break;

                    case ResourceDimension.Texture2D:
                        //D3D11_RESOURCE_MISC_TEXTURECUBE
                        if ((d3d10ext.miscFlag & 0x4) > 0)
                        {
                            arraySize *= 6;
                            isCubeMap = true;
                        }
                        depth = 1;
                        break;

                    case ResourceDimension.Texture3D:
                        if (!((header.flags & DDS_HEADER_FLAGS_VOLUME) > 0))
                        {
                            throw new Exception();
                        }

                        if (arraySize > 1)
                        {
                            throw new Exception();
                        }
                        break;

                    default:
                        throw new Exception();
                }

                resDim = (ResourceDimension)d3d10ext.resourceDimension;
            }
            else
            {
                format = GetDXGIFormat(header.ddspf);

                if (format == Format.Unknown)
                {
                    throw new Exception();
                }

                if ((header.flags & DDS_HEADER_FLAGS_VOLUME) > 0)
                {
                    resDim = ResourceDimension.Texture3D;
                }
                else
                {
                    if ((header.caps2 & DDS_CUBEMAP) > 0)
                    {
                        //定义所有六个面
                        if ((header.caps2 & DDS_CUBEMAP_ALLFACES) != DDS_CUBEMAP_ALLFACES)
                        {
                            throw new Exception();
                        }

                        arraySize = 6;
                        isCubeMap = true;
                    }

                    depth = 1;
                    resDim = ResourceDimension.Texture2D;
                }
            }
            var resource = d3dDevice.CreateCommittedResource(new HeapProperties(CpuPageProperty.WriteBack, MemoryPool.L0), HeapFlags.None,
                        new ResourceDescription()
                        {
                            //Alignment = -1,
                            Dimension = resDim,
                            DepthOrArraySize = (short)arraySize,
                            Flags = ResourceFlags.None,
                            Format = format,
                            Height = height,
                            Layout = TextureLayout.Unknown,
                            MipLevels = (short)mipCount,
                            SampleDescription = new SampleDescription(1, 0),
                            Width = width,

                        },
                        ResourceStates.GenericRead);

            FillInitData(resource, width, height, depth, mipCount, arraySize, format, 0, 0, bitData, offset);

            return resource;
        }

        //从内存中加载纹理
        public static Resource CreateTextureFromDDS(Device device, byte[] data, out bool isCubeMap)
        {
            //在内存中验证DDS文件
            DDS_HEADER header = new DDS_HEADER();

            int ddsHeaderSize = Marshal.SizeOf(header);
            int ddspfSize = Marshal.SizeOf(new DDS_PIXELFORMAT());
            int ddsHeader10Size = Marshal.SizeOf(new DDS_HEADER_DXT10());

            if (data.Length < (sizeof(uint) + ddsHeaderSize))
            {
                throw new Exception();
            }

            int dwMagicNumber = BitConverter.ToInt32(data, 0);
            if (dwMagicNumber != DDS_MAGIC)
            {
                throw new Exception();
            }

            header = ByteArrayToStructure<DDS_HEADER>(data, 4, ddsHeaderSize);

            //验证头文件以确认DDS文件
            if (header.size != ddsHeaderSize ||
                header.ddspf.size != ddspfSize)
            {
                throw new Exception();
            }

            //检查DX10扩展名
            DDS_HEADER_DXT10? dx10Header = null;
            if (((header.ddspf.flags & DDS_FOURCC) > 0) && (MAKEFOURCC('D', 'X', '1', '0') == header.ddspf.fourCC))
            {
                if (data.Length < (ddsHeaderSize + 4 + ddsHeader10Size))
                {
                    throw new Exception();
                }

                dx10Header = ByteArrayToStructure<DDS_HEADER_DXT10>(data, 4 + ddsHeaderSize, ddsHeader10Size);
            }

            int offset = 4 + ddsHeaderSize + (dx10Header.HasValue ? ddsHeader10Size : 0);

            return CreateTextureFromDDS(device, header, dx10Header, data, offset, 0, out isCubeMap);
        }

        //从DDS文件中加载纹理
        public static Resource CreateTextureFromDDS(Device device, string filename)
        {
            bool isCube;
            return CreateTextureFromDDS(device, System.IO.File.ReadAllBytes(filename), out isCube);
        }

        //从bmp文件中加载纹理
        public static Resource CreateTextureFromBitmap(Device device, string filename)
        {
            System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(filename);

            int width = bitmap.Width;
            int height = bitmap.Height;

            //描述并创建一个Texture2D纹理.
            ResourceDescription textureDesc = new ResourceDescription()
            {
                MipLevels = 1,
                Format = Format.B8G8R8A8_UNorm,
                Width = width,
                Height = height,
                Flags = ResourceFlags.None,
                DepthOrArraySize = 1,
                SampleDescription = new SampleDescription(1, 0),
                Dimension = ResourceDimension.Texture2D,
            };

            var buffer = device.CreateCommittedResource(new HeapProperties(HeapType.Upload), HeapFlags.None, textureDesc, ResourceStates.GenericRead);


            System.Drawing.Imaging.BitmapData data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            buffer.WriteToSubresource(0, new ResourceRegion()
            {
                Back = 1,
                Bottom = height,
                Right = width
            }, data.Scan0, 4 * width, 4 * width * height);
            int bufferSize = data.Height * data.Stride;
            bitmap.UnlockBits(data);

            return buffer;
        }
    }
}
