using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D12;
using Device = SharpDX.Direct3D12.Device;
using Resource = SharpDX.Direct3D12.Resource;
using ShaderBytecode = SharpDX.Direct3D12.ShaderBytecode;

namespace WhiteRabbit.Framework
{
    public static class D3DUtil
    {
        public const int DefaultShader4ComponentMapping = 5768;

        public static Resource CreateDefaultBuffer<T>(
            Device device,
            GraphicsCommandList cmdList,
            T[] initData,
            long byteSize,
            out Resource uploadBuffer) where T : struct
        {
            //创建实际的默认缓冲区资源
            Resource defaultBuffer = device.CreateCommittedResource(
                new HeapProperties(HeapType.Default),
                HeapFlags.None,
                ResourceDescription.Buffer(byteSize),
                ResourceStates.Common);

            //为了将CPU内存数据复制到默认缓冲区中，需要创建一个中间上传堆
            uploadBuffer = device.CreateCommittedResource(
                new HeapProperties(HeapType.Upload),
                HeapFlags.None,
                ResourceDescription.Buffer(byteSize),
                ResourceStates.GenericRead);

            //将数据复制到上传缓冲区
            IntPtr ptr = uploadBuffer.Map(0);
            Utilities.Write(ptr, initData, 0, initData.Length);
            uploadBuffer.Unmap(0);

            //将数据复制到默认缓冲区资源
            cmdList.ResourceBarrierTransition(defaultBuffer, ResourceStates.Common, ResourceStates.CopyDestination);
            cmdList.CopyResource(defaultBuffer, uploadBuffer);
            cmdList.ResourceBarrierTransition(defaultBuffer, ResourceStates.CopyDestination, ResourceStates.GenericRead);

            //注意:uploadBuffer必须在上述函数调用之后保持活动状态，因为复制的命令列表尚未执行
            //在执行了副本之后释放uploadBuffer

            return defaultBuffer;
        }

        //常量缓冲区必须是最小硬件分配大小(通常为256字节)的倍数,因此四舍五入到256的整数倍
        //通过加上255,然后屏蔽存储所有< 256位的下2个字节来实现
        //Example: Suppose byteSize = 300.
        //(300 + 255) & ~255
        //555 & ~255
        //0x022B & ~0x00ff
        //0x022B & 0xff00
        //0x0200
        //512
        public static int CalcConstantBufferByteSize<T>() where T : struct => (Marshal.SizeOf(typeof(T)) + 255) & ~255;

        public static ShaderBytecode CompileShader(string fileName, string entryPoint, string profile, ShaderMacro[] defines = null)
        {
            var shaderFlags = ShaderFlags.None;
#if DEBUG
            shaderFlags |= ShaderFlags.Debug | ShaderFlags.SkipOptimization;
#endif
            CompilationResult result = SharpDX.D3DCompiler.ShaderBytecode.CompileFromFile(
                fileName,
                entryPoint,
                profile,
                shaderFlags,
                include: FileIncludeHandler.Default,
                defines: defines);
            return new ShaderBytecode(result);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Light
    {
        public const int MaxLights = 16;

        public Vector3 Strength;
        public float FalloffStart;
        public Vector3 Direction;
        public float FalloffEnd;
        public Vector3 Position;
        public float SpotPower;

        public static Light Default => new Light
        {
            Strength = new Vector3(0.5f),
            FalloffStart = 1.0f,
            Direction = -Vector3.UnitY,
            FalloffEnd = 10.0f,
            Position = Vector3.Zero,
            SpotPower = 64.0f
        };

        public static Light[] DefaultArray => Enumerable.Repeat(Default, MaxLights).ToArray();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct MaterialConstants
    {
        public Vector4 DiffuseAlbedo;
        public Vector3 FresnelR0;
        public float Roughness;

        //用于纹理映射
        public Matrix MatTransform;

        public static MaterialConstants Default => new MaterialConstants
        {
            DiffuseAlbedo = Vector4.One,
            FresnelR0 = new Vector3(0.01f),
            Roughness = 0.25f,
            MatTransform = Matrix.Identity
        };
    };

    //材质Material类，这里的结构较为简单，一个3D引擎可能会创建多个材质的类层次结构
    public class Material
    {
        //用于查找的唯一材料名称
        public string Name { get; set; }

        //索引到与此材料对应的常数缓冲区中
        public int MatCBIndex { get; set; } = -1;

        //索引到SRV堆中的漫反射纹理
        public int DiffuseSrvHeapIndex { get; set; } = -1;

        //索引到SRV堆中以获得正常纹理
        public int NormalSrvHeapIndex { get; set; } = -1;

        //更新帧资源
        public int NumFramesDirty { get; set; } = D3DApp.NumFrameResources;

        //材质常数缓冲数据，用于着色
        public Vector4 DiffuseAlbedo { get; set; } = Vector4.One;
        public Vector3 FresnelR0 { get; set; } = new Vector3(0.01f);
        public float Roughness { get; set; } = 0.25f;
        public Matrix MatTransform { get; set; } = Matrix.Identity;
    }

    public class Texture : IDisposable
    {
        //用于查找的唯一材料名称
        public string Name { get; set; }

        public string Filename { get; set; }

        public Resource Resource { get; set; }
        public Resource UploadHeap { get; set; }

        public void Dispose()
        {
            Resource?.Dispose();
            UploadHeap?.Dispose();
        }
    }

    public static class D3DExtensions
    {
        public static GraphicsPipelineStateDescription Copy(this GraphicsPipelineStateDescription desc)
        {
            var newDesc = new GraphicsPipelineStateDescription
            {
                BlendState = desc.BlendState,
                CachedPSO = desc.CachedPSO,
                DepthStencilFormat = desc.DepthStencilFormat,
                DepthStencilState = desc.DepthStencilState,
                SampleDescription = desc.SampleDescription,
                DomainShader = desc.DomainShader,
                Flags = desc.Flags,
                GeometryShader = desc.GeometryShader,
                HullShader = desc.HullShader,
                IBStripCutValue = desc.IBStripCutValue,
                InputLayout = desc.InputLayout,
                NodeMask = desc.NodeMask,
                PixelShader = desc.PixelShader,
                PrimitiveTopologyType = desc.PrimitiveTopologyType,
                RasterizerState = desc.RasterizerState,
                RenderTargetCount = desc.RenderTargetCount,
                SampleMask = desc.SampleMask,
                StreamOutput = desc.StreamOutput,
                VertexShader = desc.VertexShader,
                RootSignature = desc.RootSignature
            };
            for (int i = 0; i < desc.RenderTargetFormats.Length; i++)
                newDesc.RenderTargetFormats[i] = desc.RenderTargetFormats[i];
            return newDesc;
        }
    }

    //ShaderBytecode
    //相当于D3D_COMPILE_STANDARD_FILE_INCLUDE.
    internal class FileIncludeHandler : CallbackBase, Include
    {
        public static FileIncludeHandler Default { get; } = new FileIncludeHandler();

        public Stream Open(IncludeType type, string fileName, Stream parentStream)
        {
            string filePath = fileName;

            if (!Path.IsPathRooted(filePath))
            {
                string selectedFile = Path.Combine(Environment.CurrentDirectory, fileName);
                if (File.Exists(selectedFile))
                    filePath = selectedFile;
            }

            return new FileStream(filePath, FileMode.Open, FileAccess.Read);
        }

        public void Close(Stream stream) => stream.Close();
    }

    //定义枚举，为键鼠输入提供与System.Windows.Form相同的值
    //防止直接依赖System.Windows.Form和System.Drawing

    // Ref: System.Windows.Forms.MouseButtons
    [Flags]
    public enum MouseButtons
    {
        Left = 1048576,
        None = 0,
        Right = 2097152,
        Middle = 4194304,
        XButton1 = 8388608,
        XButton2 = 16777216
    }

    // Ref: System.Windows.Forms.Keys
    public enum Keys
    {
        KeyCode = 65535,
        Modifiers = -65536,
        None = 0,
        LButton = 1,
        RButton = 2,
        Cancel = 3,
        MButton = 4,
        XButton1 = 5,
        XButton2 = 6,
        Back = 8,
        Tab = 9,
        LineFeed = 10,
        Clear = 12,
        Return = 13,
        Enter = 13,
        ShiftKey = 16,
        ControlKey = 17,
        Menu = 18,
        Pause = 19,
        Capital = 20,
        CapsLock = 20,
        KanaMode = 21,
        HanguelMode = 21,
        HangulMode = 21,
        JunjaMode = 23,
        FinalMode = 24,
        HanjaMode = 25,
        KanjiMode = 25,
        Escape = 27,
        IMEConvert = 28,
        IMENonconvert = 29,
        IMEAccept = 30,
        IMEAceept = 30,
        IMEModeChange = 31,
        Space = 32,
        Prior = 33,
        PageUp = 33,
        Next = 34,
        PageDown = 34,
        End = 35,
        Home = 36,
        Left = 37,
        Up = 38,
        Right = 39,
        Down = 40,
        Select = 41,
        Print = 42,
        Execute = 43,
        Snapshot = 44,
        PrintScreen = 44,
        Insert = 45,
        Delete = 46,
        Help = 47,
        D0 = 48,
        D1 = 49,
        D2 = 50,
        D3 = 51,
        D4 = 52,
        D5 = 53,
        D6 = 54,
        D7 = 55,
        D8 = 56,
        D9 = 57,
        A = 65,
        B = 66,
        C = 67,
        D = 68,
        E = 69,
        F = 70,
        G = 71,
        H = 72,
        I = 73,
        J = 74,
        K = 75,
        L = 76,
        M = 77,
        N = 78,
        O = 79,
        P = 80,
        Q = 81,
        R = 82,
        S = 83,
        T = 84,
        U = 85,
        V = 86,
        W = 87,
        X = 88,
        Y = 89,
        Z = 90,
        LWin = 91,
        RWin = 92,
        Apps = 93,
        Sleep = 95,
        NumPad0 = 96,
        NumPad1 = 97,
        NumPad2 = 98,
        NumPad3 = 99,
        NumPad4 = 100,
        NumPad5 = 101,
        NumPad6 = 102,
        NumPad7 = 103,
        NumPad8 = 104,
        NumPad9 = 105,
        Multiply = 106,
        Add = 107,
        Separator = 108,
        Subtract = 109,
        Decimal = 110,
        Divide = 111,
        F1 = 112,
        F2 = 113,
        F3 = 114,
        F4 = 115,
        F5 = 116,
        F6 = 117,
        F7 = 118,
        F8 = 119,
        F9 = 120,
        F10 = 121,
        F11 = 122,
        F12 = 123,
        F13 = 124,
        F14 = 125,
        F15 = 126,
        F16 = 127,
        F17 = 128,
        F18 = 129,
        F19 = 130,
        F20 = 131,
        F21 = 132,
        F22 = 133,
        F23 = 134,
        F24 = 135,
        NumLock = 144,
        Scroll = 145,
        LShiftKey = 160,
        RShiftKey = 161,
        LControlKey = 162,
        RControlKey = 163,
        LMenu = 164,
        RMenu = 165,
        BrowserBack = 166,
        BrowserForward = 167,
        BrowserRefresh = 168,
        BrowserStop = 169,
        BrowserSearch = 170,
        BrowserFavorites = 171,
        BrowserHome = 172,
        VolumeMute = 173,
        VolumeDown = 174,
        VolumeUp = 175,
        MediaNextTrack = 176,
        MediaPreviousTrack = 177,
        MediaStop = 178,
        MediaPlayPause = 179,
        LaunchMail = 180,
        SelectMedia = 181,
        LaunchApplication1 = 182,
        LaunchApplication2 = 183,
        OemSemicolon = 186,
        Oem1 = 186,
        Oemplus = 187,
        Oemcomma = 188,
        OemMinus = 189,
        OemPeriod = 190,
        OemQuestion = 191,
        Oem2 = 191,
        Oemtilde = 192,
        Oem3 = 192,
        OemOpenBrackets = 219,
        Oem4 = 219,
        OemPipe = 220,
        Oem5 = 220,
        OemCloseBrackets = 221,
        Oem6 = 221,
        OemQuotes = 222,
        Oem7 = 222,
        Oem8 = 223,
        OemBackslash = 226,
        Oem102 = 226,
        ProcessKey = 229,
        Packet = 231,
        Attn = 246,
        Crsel = 247,
        Exsel = 248,
        EraseEof = 249,
        Play = 250,
        Zoom = 251,
        NoName = 252,
        Pa1 = 253,
        OemClear = 254,
        Shift = 65536,
        Control = 131072,
        Alt = 262144
    }
}