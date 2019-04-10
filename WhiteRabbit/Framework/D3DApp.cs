using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D12;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D12.Device;
using Feature = SharpDX.Direct3D12.Feature;
using Point = SharpDX.Point;
using Resource = SharpDX.Direct3D12.Resource;
using RectangleF = SharpDX.RectangleF;

/// <summary>
/// D3DApp类是一种基础的Direct3D应用程序类
/// 它提供了创建应用程序主窗口、运行程序消息循环、处理窗口消息以及初始化Direct3D等多种功能的函数
/// 该类还为应用程序定义了一组框架函数，可以根据需求实例化一个继承D3DApp的类，重写框架的虚函数，自定义用户代码
/// </summary>
namespace WhiteRabbit.Framework
{
    // TODO:该框架存在以下问题
    // TODO:进入全屏模式将崩溃
    // TODO:改变多重采样将崩溃
    public class D3DApp : IDisposable
    {
        public const int NumFrameResources = 3;       //帧资源
        public const int SwapChainBufferCount = 2;    //交换链数量

        private Form window;           //主窗口
        private bool appPaused;        //应用程序是否暂停
        private bool minimized;        //应用程序是否最小化
        private bool maximized;        //应用程序是否最大化
        private bool resizing;         //大小调整栏是否正受到拖拽
        private bool running;          //应用程序是否正在运行

        //设置4X MSAA技术
        private bool m4xMsaaState;    //是否开启4X MSAA技术
        private int m4xMsaaQuality;   //4X MSAA技术质量级别

        //窗口显示方式
        private FormWindowState lastWindowState = FormWindowState.Normal;

        private int frameCount;
        private float timeElapsed;

        private Factory4 factory;
        private readonly Resource[] swapChainBuffers = new Resource[SwapChainBufferCount];

        private AutoResetEvent fenceEvent;

        public bool M4xMsaaState
        {
            get { return m4xMsaaState; }
            set
            {
                if (m4xMsaaState != value)
                {
                    m4xMsaaState = value;

                    if (running)
                    {
                        //使用新的多重采样设置重新创建交换链和缓冲区
                        CreateSwapChain();
                        OnResize();
                    }
                }
            }
        }

        protected DescriptorHeap RtvHeap { get; private set; }    //RTV描述符堆
        protected DescriptorHeap DsvHeap { get; private set; }    //DSV描述符堆

        protected int MsaaCount => M4xMsaaState ? 4 : 1;
        protected int MsaaQuality => M4xMsaaState ? m4xMsaaQuality - 1 : 0;

        //用于记录帧之间的事件间隔
        protected GameTimer Timer { get; } = new GameTimer();

        protected Device Device { get; private set; }

        protected Fence Fence { get; private set; }
        protected long CurrentFence { get; set; }

        protected int RtvDescriptorSize { get; private set; }
        protected int DsvDescriptorSize { get; private set; }
        protected int CbvSrvUavDescriptorSize { get; private set; }

        protected CommandQueue CommandQueue { get; private set; }
        protected CommandAllocator DirectCmdListAlloc { get; private set; }
        protected GraphicsCommandList CommandList { get; private set; }

        protected SwapChain3 SwapChain { get; private set; }
        protected Resource DepthStencilBuffer { get; private set; }

        protected ViewportF Viewport { get; set; }
        protected RectangleF ScissorRectangle { get; set; }

        protected string MainWindowCaption { get; set; } = "D3D12 Application";
        protected int ClientWidth { get; set; } = 1280;
        protected int ClientHeight { get; set; } = 720;

        protected float AspectRatio => (float)ClientWidth / ClientHeight;

        protected Format BackBufferFormat { get; } = Format.R8G8B8A8_UNorm;
        protected Format DepthStencilFormat { get; } = Format.D24_UNorm_S8_UInt;

        //返回交换链中当前后台缓冲区的ID3D12Resource
        protected Resource CurrentBackBuffer => swapChainBuffers[SwapChain.CurrentBackBufferIndex];
        //返回当前后台缓冲区的RTV
        protected CpuDescriptorHandle CurrentBackBufferView
            => RtvHeap.CPUDescriptorHandleForHeapStart + SwapChain.CurrentBackBufferIndex * RtvDescriptorSize;

        protected CpuDescriptorHandle DepthStencilView => DsvHeap.CPUDescriptorHandleForHeapStart;

        //初始化如分配资源、初始化对象和建立3D场景等
        public virtual void Initialize()
        {
            InitMainWindow();
            InitDirect3D();

            //若窗口大小发生变化，与工作区大小有关的Direct3D属性也需要随之调整
            OnResize();

            running = true;
        }

        //封装了应用程序的消息循环
        public void Run()
        {
            Timer.Reset();
            while (running)
            {
                Application.DoEvents();
                Timer.Tick();
                if (!appPaused)
                {
                    CalculateFrameRateStats();
                    Update(Timer);
                    Draw(Timer);
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            //资源回收模式
            if (disposing)
            {
                FlushCommandQueue();
                RtvHeap?.Dispose();
                DsvHeap?.Dispose();
                SwapChain?.Dispose();
                foreach (Resource buffer in swapChainBuffers)
                    buffer?.Dispose();
                DepthStencilBuffer?.Dispose();
                CommandList?.Dispose();
                DirectCmdListAlloc?.Dispose();
                CommandQueue?.Dispose();
                Fence?.Dispose();
                Device?.Dispose();
            }
        }

        protected virtual void OnResize()
        {
            Debug.Assert(Device != null);
            Debug.Assert(SwapChain != null);
            Debug.Assert(DirectCmdListAlloc != null);

            //在更改资源前刷新
            FlushCommandQueue();

            CommandList.Reset(DirectCmdListAlloc, null);

            //释放将要重新创建的前面的资源
            foreach (Resource buffer in swapChainBuffers)
                buffer?.Dispose();
            DepthStencilBuffer?.Dispose();

            //调整交换链的大小
            SwapChain.ResizeBuffers(
                SwapChainBufferCount,
                ClientWidth, ClientHeight,
                BackBufferFormat,
                SwapChainFlags.AllowModeSwitch);

            CpuDescriptorHandle rtvHeapHandle = RtvHeap.CPUDescriptorHandleForHeapStart;
            for (int i = 0; i < SwapChainBufferCount; i++)
            {
                Resource backBuffer = SwapChain.GetBackBuffer<Resource>(i);
                swapChainBuffers[i] = backBuffer;
                Device.CreateRenderTargetView(backBuffer, null, rtvHeapHandle);
                rtvHeapHandle += RtvDescriptorSize;
            }

            //创建深度/模板缓冲区及视图
            var depthStencilDesc = new ResourceDescription
            {
                Dimension = ResourceDimension.Texture2D,
                Alignment = 0,
                Width = ClientWidth,
                Height = ClientHeight,
                DepthOrArraySize = 1,
                MipLevels = 1,
                Format = Format.R24G8_Typeless,
                SampleDescription = new SampleDescription
                {
                    Count = MsaaCount,
                    Quality = MsaaQuality
                },
                Layout = TextureLayout.Unknown,
                Flags = ResourceFlags.AllowDepthStencil
            };
            var optClear = new ClearValue
            {
                Format = DepthStencilFormat,
                DepthStencil = new DepthStencilValue
                {
                    Depth = 1.0f,
                    Stencil = 0
                }
            };
            DepthStencilBuffer = Device.CreateCommittedResource(
                new HeapProperties(HeapType.Default),
                HeapFlags.None,
                depthStencilDesc,
                ResourceStates.Common,
                optClear);

            var depthStencilViewDesc = new DepthStencilViewDescription
            {
                Dimension = M4xMsaaState
                    ? DepthStencilViewDimension.Texture2DMultisampled
                    : DepthStencilViewDimension.Texture2D,
                Format = DepthStencilFormat
            };
            //将资源描述符的mip级别设为0
            CpuDescriptorHandle dsvHeapHandle = DsvHeap.CPUDescriptorHandleForHeapStart;
            Device.CreateDepthStencilView(DepthStencilBuffer, depthStencilViewDesc, dsvHeapHandle);

            //更新深度缓冲区
            CommandList.ResourceBarrierTransition(DepthStencilBuffer, ResourceStates.Common, ResourceStates.DepthWrite);

            //执行调整命令
            CommandList.Close();
            CommandQueue.ExecuteCommandList(CommandList);

            //等待执行完成
            FlushCommandQueue();

            Viewport = new ViewportF(0, 0, ClientWidth, ClientHeight, 0.0f, 1.0f);
            ScissorRectangle = new RectangleF(0, 0, ClientWidth, ClientHeight);
        }

        //绘制每一帧时都会调用的抽象方法
        protected virtual void Update(GameTimer gt) { }
        protected virtual void Draw(GameTimer gt) { }

        //初始化应用程序主窗口
        protected void InitMainWindow()
        {
            window = new Form
            {
                Text = MainWindowCaption,
                Name = "D3DWndClassName",
                FormBorderStyle = FormBorderStyle.Sizable,
                ClientSize = new Size(ClientWidth, ClientHeight),
                StartPosition = FormStartPosition.CenterScreen,
                MinimumSize = new Size(200, 200)
            };

            window.MouseDown += (sender, e) => OnMouseDown((MouseButtons)e.Button, new Point(e.X, e.Y));
            window.MouseUp += (sender, e) => OnMouseUp((MouseButtons)e.Button, new Point(e.X, e.Y));
            window.MouseMove += (sender, e) => OnMouseMove((MouseButtons)e.Button, new Point(e.X, e.Y));
            window.MouseWheel += (sender, e) => OnMouseWheel((MouseButtons)e.Button, new Point(e.Delta, e.Delta));
            window.KeyDown += (sender, e) => OnKeyDown((Keys)e.KeyCode);
            window.KeyUp += (sender, e) => OnKeyUp((Keys)e.KeyCode);
            window.ResizeBegin += (sender, e) =>
            {
                appPaused = true;
                resizing = true;
                Timer.Stop();
            };
            window.ResizeEnd += (sender, e) =>
            {
                appPaused = false;
                resizing = false;
                Timer.Start();
                OnResize();
            };
            window.Activated += (sender, e) =>
            {
                appPaused = false;
                Timer.Start();
            };
            window.Deactivate += (sender, e) =>
            {
                appPaused = true;
                Timer.Stop();
            };
            window.HandleDestroyed += (sender, e) => running = false;
            window.Resize += (sender, e) =>
            {
                ClientWidth = window.ClientSize.Width;
                ClientHeight = window.ClientSize.Height;
                //当窗口状态改变
                if (window.WindowState != lastWindowState)
                {
                    lastWindowState = window.WindowState;
                    if (window.WindowState == FormWindowState.Maximized)
                    {
                        appPaused = false;
                        minimized = false;
                        maximized = true;
                        OnResize();
                    }
                    else if (window.WindowState == FormWindowState.Minimized)
                    {
                        appPaused = true;
                        minimized = true;
                        maximized = false;
                    }
                    else if (window.WindowState == FormWindowState.Normal)
                    {
                        if (minimized) //从最小化状态恢复？
                        {
                            appPaused = false;
                            minimized = false;
                            OnResize();
                        }
                        else if (maximized) //从最大化状态恢复？
                        {
                            appPaused = false;
                            maximized = false;
                            OnResize();
                        }
                        else if (resizing)
                        {
                        }
                        else //API调用
                        {
                            OnResize();
                        }
                    }
                }
                else if (!resizing)
                {
                    OnResize();
                }
            };

            window.Show();
            window.Update();
        }

        protected virtual void OnMouseDown(MouseButtons button, Point location)
        {
            window.Capture = true;
        }

        protected virtual void OnMouseUp(MouseButtons button, Point location)
        {
            window.Capture = false;
        }

        protected virtual void OnMouseMove(MouseButtons button, Point location)
        {
        }

        protected virtual void OnMouseWheel(MouseButtons button, Point wheel)
        {
        }

        protected virtual void OnKeyDown(Keys keyCode)
        {
        }

        protected virtual void OnKeyUp(Keys keyCode)
        {
            switch (keyCode)
            {
                case Keys.Escape:
                    running = false;
                    break;
                case Keys.F2:
                    M4xMsaaState = !M4xMsaaState;
                    break;
            }
        }

        protected bool IsKeyDown(Keys keyCode) => Keyboard.IsKeyDown(KeyInterop.KeyFromVirtualKey((int)keyCode));

        //Direct3D的初始化
        protected void InitDirect3D()
        {
#if DEBUG
            try
            {
                DebugInterface.Get().EnableDebugLayer();
            }
            catch (SharpDXException ex) when (ex.Descriptor.NativeApiCode == "DXGI_ERROR_SDK_COMPONENT_MISSING")
            {
                Debug.WriteLine("Failed to enable debug layer. Please ensure \"Graphics Tools\" feature is enabled in Windows \"Manage optional feature\" settings page");
            }
#endif

            factory = new Factory4();

            try
            {
                //尝试创建硬件设备
                //传递NULL以使用默认适配器，默认适配器是 Factory.Adapters 枚举的第一个适配器
                Device = new Device(null, FeatureLevel.Level_11_0);
            }
            catch (SharpDXException)
            {
                // Fallback to WARP device.
                Adapter warpAdapter = factory.GetWarpAdapter();
                Device = new Device(warpAdapter, FeatureLevel.Level_11_0);
            }

            Fence = Device.CreateFence(0, FenceFlags.None);
            fenceEvent = new AutoResetEvent(false);

            RtvDescriptorSize = Device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView);
            DsvDescriptorSize = Device.GetDescriptorHandleIncrementSize(DescriptorHeapType.DepthStencilView);
            CbvSrvUavDescriptorSize = Device.GetDescriptorHandleIncrementSize(
                DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView);

            //检查后缓冲区格式的4X MSAA质量支持
            //所有支持Direct3D 11的设备都支持4X MSAA的所有的渲染目标格式，所以只需要检查质量支持

            FeatureDataMultisampleQualityLevels msQualityLevels;
            msQualityLevels.Format = BackBufferFormat;
            msQualityLevels.SampleCount = 4;
            msQualityLevels.Flags = MultisampleQualityLevelFlags.None;
            msQualityLevels.QualityLevelCount = 0;
            Debug.Assert(Device.CheckFeatureSupport(Feature.MultisampleQualityLevels, ref msQualityLevels));
            m4xMsaaQuality = msQualityLevels.QualityLevelCount;

#if DEBUG
            LogAdapters();
#endif

            CreateCommandObjects();
            CreateSwapChain();
            CreateRtvAndDsvDescriptorHeaps();
        }

        //强制CPU等待GPU，直到GPU处理完队列中的所有的命令
        protected void FlushCommandQueue()
        {
            CurrentFence++;

            CommandQueue.Signal(Fence, CurrentFence);

            if (Fence.CompletedValue < CurrentFence)
            {
                Fence.SetEventOnCompletion(CurrentFence, fenceEvent.SafeWaitHandle.DangerousGetHandle());

                fenceEvent.WaitOne();
            }
        }

        protected virtual int RtvDescriptorCount => SwapChainBufferCount;
        protected virtual int DsvDescriptorCount => 1;

        //创建命令队列、命令列表分配器和命令列表
        private void CreateCommandObjects()
        {
            var queueDesc = new CommandQueueDescription(CommandListType.Direct);
            CommandQueue = Device.CreateCommandQueue(queueDesc);

            DirectCmdListAlloc = Device.CreateCommandAllocator(CommandListType.Direct);

            CommandList = Device.CreateCommandList(
                0,
                CommandListType.Direct,
                DirectCmdListAlloc, //相关的命令分配器
                null);              //初始化PSO

            CommandList.Close();
        }

        //创建交换链
        private void CreateSwapChain()
        {
            //释放前一个交换链
            SwapChain?.Dispose();

            var sd = new SwapChainDescription
            {
                ModeDescription = new ModeDescription
                {
                    Width = ClientWidth,
                    Height = ClientHeight,
                    Format = BackBufferFormat,
                    RefreshRate = new Rational(60, 1),
                    Scaling = DisplayModeScaling.Unspecified,
                    ScanlineOrdering = DisplayModeScanlineOrder.Unspecified
                },
                SampleDescription = new SampleDescription
                {
                    Count = 1,
                    Quality = 0
                },
                Usage = Usage.RenderTargetOutput,
                BufferCount = SwapChainBufferCount,
                SwapEffect = SwapEffect.FlipDiscard,
                Flags = SwapChainFlags.AllowModeSwitch,
                OutputHandle = window.Handle,
                IsWindowed = true
            };

            using (var tempSwapChain = new SwapChain(factory, CommandQueue, sd))
            {
                SwapChain = tempSwapChain.QueryInterface<SwapChain3>();
            }
        }

        //创建应用程序所需的RTV和DSV描述符堆
        private void CreateRtvAndDsvDescriptorHeaps()
        {
            var rtvHeapDesc = new DescriptorHeapDescription
            {
                DescriptorCount = RtvDescriptorCount,
                Type = DescriptorHeapType.RenderTargetView
            };
            RtvHeap = Device.CreateDescriptorHeap(rtvHeapDesc);

            var dsvHeapDesc = new DescriptorHeapDescription
            {
                DescriptorCount = DsvDescriptorCount,
                Type = DescriptorHeapType.DepthStencilView
            };
            DsvHeap = Device.CreateDescriptorHeap(dsvHeapDesc);
        }

        //枚举系统中所有的适配器
        private void LogAdapters()
        {
            foreach (Adapter adapter in factory.Adapters)
            {
                Debug.WriteLine($"***Adapter: {adapter.Description.Description}");
                LogAdapterOutputs(adapter);
            }
        }

        //枚举指定适配器的全部显示输出
        private void LogAdapterOutputs(Adapter adapter)
        {
            foreach (Output output in adapter.Outputs)
            {
                Debug.WriteLine($"***Output: {output.Description.DeviceName}");
                LogOutputDisplayModes(output, BackBufferFormat);
            }
        }

        //枚举某个显示输出对特定格式支持的所有显示模式
        private void LogOutputDisplayModes(Output output, Format format)
        {
            foreach (ModeDescription displayMode in output.GetDisplayModeList(format, 0))
                Debug.WriteLine($"Width = {displayMode.Width} Height = {displayMode.Height} Refresh = {displayMode.RefreshRate}");
        }

        //计算每秒的平均帧数以及每帧平均的毫秒时长
        private void CalculateFrameRateStats()
        {
            frameCount++;

            if (Timer.TotalTime - timeElapsed >= 1.0f)
            {
                float fps = frameCount;
                float mspf = 1000.0f / fps;

                window.Text = $"{MainWindowCaption}    fps: {fps}   mspf: {mspf}";

                //重置
                frameCount = 0;
                timeElapsed += 1.0f;
            }
        }
    }
}
