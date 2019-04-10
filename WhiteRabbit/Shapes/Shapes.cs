using SharpDX;
using SharpDX.Direct3D12;
using SharpDX.DXGI;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Resource = SharpDX.Direct3D12.Resource;
using WhiteRabbit.Framework;


namespace WhiteRabbit.Shapes
{
    class Shapes : D3DApp
    {
        private readonly List<FrameResource> frameResources = new List<FrameResource>(NumFrameResources);
        private readonly List<AutoResetEvent> fenceEvents = new List<AutoResetEvent>(NumFrameResources);
        private int currFrameResourceIndex;

        private DescriptorHeap cbvHeap;
        private DescriptorHeap[] descriptorHeaps;
        private RootSignature rootSignature;

        private readonly Dictionary<string, MeshGeometry> geometries = new Dictionary<string, MeshGeometry>();
        private readonly Dictionary<string, PipelineState> psos = new Dictionary<string, PipelineState>();
        private readonly Dictionary<string, ShaderBytecode> shaders = new Dictionary<string, ShaderBytecode>();

        private InputLayoutDescription inputLayout;

        private readonly List<RenderItem> allRitems = new List<RenderItem>();

        private readonly Dictionary<RenderLayer, List<RenderItem>> ritemLayers = new Dictionary<RenderLayer, List<RenderItem>>(1)
        {
            [RenderLayer.Opaque] = new List<RenderItem>()
        };

        private PassConstants mainPassCB;

        private int passCbvOffset;

        private bool isWireframe = true;

        private Vector3 eyePos;
        private Matrix proj = Matrix.Identity;
        private Matrix view = Matrix.Identity;

        private float theta = 1.5f * MathUtil.Pi;
        private float phi = 0.2f * MathUtil.Pi;
        private float radius = 15.0f;

        private Point lastMousePos;

        public Shapes()
        {
            MainWindowCaption = "Shapes";
        }

        private FrameResource CurrFrameResource => frameResources[currFrameResourceIndex];
        private AutoResetEvent CurrentFenceEvent => fenceEvents[currFrameResourceIndex];

        public override void Initialize()
        {
            base.Initialize();

            CommandList.Reset(DirectCmdListAlloc, null);

            BuildRootSignature();
            BuildShadersAndInputLayout();
            BuildShapeGeometry();
            BuildRenderItems();
            BuildFrameResources();
            BuildDescriptorHeaps();
            BuildConstantBufferViews();
            BuildPSOs();

            CommandList.Close();
            CommandQueue.ExecuteCommandList(CommandList);

            FlushCommandQueue();
        }

        protected override void OnResize()
        {
            base.OnResize();

            //调整窗口大小，更新纵横比并重新计算投影矩阵
            proj = Matrix.PerspectiveFovLH(MathUtil.PiOverFour, AspectRatio, 1.0f, 1000.0f);
        }

        protected override void Update(GameTimer gt)
        {
            UpdateCamera();

            //循环遍历框架资源数组
            currFrameResourceIndex = (currFrameResourceIndex + 1) % NumFrameResources;

            //等待GPU处理完当前帧资源的命令
            if (CurrFrameResource.Fence != 0 && Fence.CompletedValue < CurrFrameResource.Fence)
            {
                Fence.SetEventOnCompletion(CurrFrameResource.Fence, CurrentFenceEvent.SafeWaitHandle.DangerousGetHandle());
                CurrentFenceEvent.WaitOne();
            }

            UpdateObjectCBs();
            UpdateMainPassCB(gt);
        }

        protected override void Draw(GameTimer gt)
        {
            CommandAllocator cmdListAlloc = CurrFrameResource.CmdListAlloc;

            //重置命令列表
            //只有GPU执行完相关的命令列表后，才能重置
            cmdListAlloc.Reset();

            //命令列表通过ExecuteCommandList添加到命令队列后可以重置
            //重置命令列表会重用内存。
            CommandList.Reset(cmdListAlloc, isWireframe ? psos["opaque_wireframe"] : psos["opaque"]);

            CommandList.SetViewport(Viewport);
            CommandList.SetScissorRectangles(ScissorRectangle);

            //状态转换
            CommandList.ResourceBarrierTransition(CurrentBackBuffer, ResourceStates.Present, ResourceStates.RenderTarget);

            //清除后台缓冲区和深度缓冲区
            CommandList.ClearRenderTargetView(CurrentBackBufferView, Color.LightSteelBlue);
            CommandList.ClearDepthStencilView(DepthStencilView, ClearFlags.FlagsDepth | ClearFlags.FlagsStencil, 1.0f, 0);

            //指明所要呈现的缓冲区
            CommandList.SetRenderTargets(CurrentBackBufferView, DepthStencilView);

            CommandList.SetDescriptorHeaps(descriptorHeaps.Length, descriptorHeaps);

            CommandList.SetGraphicsRootSignature(rootSignature);

            int passCbvIndex = passCbvOffset + currFrameResourceIndex;
            GpuDescriptorHandle passCbvHandle = cbvHeap.GPUDescriptorHandleForHeapStart;
            passCbvHandle += passCbvIndex * CbvSrvUavDescriptorSize;
            CommandList.SetGraphicsRootDescriptorTable(1, passCbvHandle);

            DrawRenderItems(CommandList, ritemLayers[RenderLayer.Opaque]);

            //状态转换
            CommandList.ResourceBarrierTransition(CurrentBackBuffer, ResourceStates.RenderTarget, ResourceStates.Present);

            //执行完命令列表后
            CommandList.Close();

            //将命令列表添加到队列中以便执行
            CommandQueue.ExecuteCommandList(CommandList);

            //交换缓冲区
            SwapChain.Present(0, PresentFlags.None);

            CurrFrameResource.Fence = ++CurrentFence;

            CommandQueue.Signal(Fence, CurrentFence);
        }

        protected override void OnMouseDown(MouseButtons button, Point location)
        {
            base.OnMouseDown(button, location);
            lastMousePos = location;
        }

        protected override void OnMouseMove(MouseButtons button, Point location)
        {
            if ((button & MouseButtons.Left) != 0)
            {
                //使每个像素对应四分之一度
                float dx = MathUtil.DegreesToRadians(0.25f * (location.X - lastMousePos.X));
                float dy = MathUtil.DegreesToRadians(0.25f * (location.Y - lastMousePos.Y));

                //更新摄像机角度
                theta += dx;
                phi += dy;

                //限制角度mPhi
                phi = MathUtil.Clamp(phi, 0.1f, MathUtil.Pi - 0.1f);
            }
            else if ((button & MouseButtons.Right) != 0)
            {
                //使每个像素对应四分之一度
                float dx = 0.05f * (location.X - lastMousePos.X);
                float dy = 0.05f * (location.Y - lastMousePos.Y);

                //更新摄像机角度
                radius += dx - dy;

                //限制角度mPhi
                radius = MathUtil.Clamp(radius, 5.0f, 150.0f);
            }

            lastMousePos = location;
        }

        protected override void OnKeyDown(Keys keyCode)
        {
            if (keyCode == Keys.D1)
                isWireframe = false;
        }

        protected override void OnKeyUp(Keys keyCode)
        {
            base.OnKeyUp(keyCode);
            if (keyCode == Keys.D1)
                isWireframe = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                rootSignature?.Dispose();
                cbvHeap?.Dispose();
                foreach (FrameResource frameResource in frameResources) frameResource.Dispose();
                foreach (MeshGeometry geometry in geometries.Values) geometry.Dispose();
                foreach (PipelineState pso in psos.Values) pso.Dispose();
            }
            base.Dispose(disposing);
        }

        private void UpdateCamera()
        {
            //将球面坐标转换为笛卡尔坐标
            eyePos.X = radius * MathHelper.Sinf(phi) * MathHelper.Cosf(theta);
            eyePos.Z = radius * MathHelper.Sinf(phi) * MathHelper.Sinf(theta);
            eyePos.Y = radius * MathHelper.Cosf(phi);

            //建立视图矩阵
            view = Matrix.LookAtLH(eyePos, Vector3.Zero, Vector3.Up);
        }

        //更新常量缓冲区数据
        private void UpdateObjectCBs()
        {
            foreach (RenderItem e in allRitems)
            {
                //只有在常量发生更改时才更新cbuffer数据
                //需要对每个帧资源进行跟踪
                if (e.NumFramesDirty > 0)
                {
                    var objConstants = new ObjectConstants { World = Matrix.Transpose(e.World) };
                    CurrFrameResource.ObjectCB.CopyData(e.ObjCBIndex, ref objConstants);

                    //下一帧也需要更新
                    e.NumFramesDirty--;
                }
            }
        }

        private void UpdateMainPassCB(GameTimer gt)
        {
            Matrix viewProj = view * proj;
            Matrix invView = Matrix.Invert(view);
            Matrix invProj = Matrix.Invert(proj);
            Matrix invViewProj = Matrix.Invert(viewProj);

            mainPassCB.View = Matrix.Transpose(view);
            mainPassCB.InvView = Matrix.Transpose(invView);
            mainPassCB.Proj = Matrix.Transpose(proj);
            mainPassCB.InvProj = Matrix.Transpose(invProj);
            mainPassCB.ViewProj = Matrix.Transpose(viewProj);
            mainPassCB.InvViewProj = Matrix.Transpose(invViewProj);
            mainPassCB.EyePosW = eyePos;
            mainPassCB.RenderTargetSize = new Vector2(ClientWidth, ClientHeight);
            mainPassCB.InvRenderTargetSize = 1.0f / mainPassCB.RenderTargetSize;
            mainPassCB.NearZ = 1.0f;
            mainPassCB.FarZ = 1000.0f;
            mainPassCB.TotalTime = gt.TotalTime;
            mainPassCB.DeltaTime = gt.DeltaTime;

            CurrFrameResource.PassCB.CopyData(0, ref mainPassCB);
        }

        //建立描述符堆
        private void BuildDescriptorHeaps()
        {
            int objCount = allRitems.Count;

            //需要一个常量缓冲区视图描述符堆来描述每一个帧资源的对象
            int numDescriptors = (objCount + 1) * NumFrameResources;

            //保存一个偏移量到上传cbv的起点，这是最后三个描述符
            passCbvOffset = objCount * NumFrameResources;

            var cbvHeapDesc = new DescriptorHeapDescription
            {
                DescriptorCount = numDescriptors,
                Type = DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView,
                Flags = DescriptorHeapFlags.ShaderVisible
            };
            cbvHeap = Device.CreateDescriptorHeap(cbvHeapDesc);
            descriptorHeaps = new[] { cbvHeap };
        }

        //建立常量缓冲区视图
        private void BuildConstantBufferViews()
        {
            int objCBByteSize = D3DUtil.CalcConstantBufferByteSize<ObjectConstants>();

            int objCount = allRitems.Count;

            //每一帧资源都需要常量缓冲区视图描述符
            for (int frameIndex = 0; frameIndex < NumFrameResources; frameIndex++)
            {
                Resource objectCB = frameResources[frameIndex].ObjectCB.Resource;
                for (int i = 0; i < objCount; i++)
                {
                    long cbAddress = objectCB.GPUVirtualAddress;

                    //偏移到缓冲区中的第i个对象常量缓冲区
                    cbAddress += i * objCBByteSize;

                    //描述符堆中对象cbv的偏移量
                    int heapIndex = frameIndex * objCount + i;
                    CpuDescriptorHandle handle = cbvHeap.CPUDescriptorHandleForHeapStart;
                    handle += heapIndex * CbvSrvUavDescriptorSize;

                    var cbvDesc = new ConstantBufferViewDescription
                    {
                        BufferLocation = cbAddress,
                        SizeInBytes = objCBByteSize
                    };

                    Device.CreateConstantBufferView(cbvDesc, handle);
                }
            }

            int passCBByteSize = D3DUtil.CalcConstantBufferByteSize<PassConstants>();

            //最后三个描述符是每个帧资源的上传cbv
            for (int frameIndex = 0; frameIndex < NumFrameResources; frameIndex++)
            {
                Resource passCB = frameResources[frameIndex].PassCB.Resource;
                long cbAddress = passCB.GPUVirtualAddress;

                //上传cbv在描述符堆中的偏移量
                int heapIndex = passCbvOffset + frameIndex;
                CpuDescriptorHandle handle = cbvHeap.CPUDescriptorHandleForHeapStart;
                handle += heapIndex * CbvSrvUavDescriptorSize;

                var cbvDesc = new ConstantBufferViewDescription
                {
                    BufferLocation = cbAddress,
                    SizeInBytes = passCBByteSize
                };

                Device.CreateConstantBufferView(cbvDesc, handle);
            }
        }

        //创建根签名
        private void BuildRootSignature()
        {
            var cbvTable0 = new DescriptorRange(DescriptorRangeType.ConstantBufferView, 1, 0);
            var cbvTable1 = new DescriptorRange(DescriptorRangeType.ConstantBufferView, 1, 1);

            //根参数可以是表，根描述符或者根常量
            var slotRootParameters = new[]
            {
                new RootParameter(ShaderVisibility.Vertex, cbvTable0),
                new RootParameter(ShaderVisibility.Vertex, cbvTable1)
            };

            //根签名是根参数的数组
            var rootSigDesc = new RootSignatureDescription(
                RootSignatureFlags.AllowInputAssemblerInputLayout,
                slotRootParameters);

            //一个槽创建一个根签名，该槽指向由一个常量缓冲区组成的描述符范围
            rootSignature = Device.CreateRootSignature(rootSigDesc.Serialize());
        }

        //建立着色器和输入布局
        private void BuildShadersAndInputLayout()
        {
            shaders["standardVS"] = D3DUtil.CompileShader(@"C:\Users\yulanli\source\repos\WhiteRabbit\WhiteRabbit\Shapes\Shaders\Color.hlsl", "VS", "vs_5_0");
            shaders["opaquePS"] = D3DUtil.CompileShader(@"C:\Users\yulanli\source\repos\WhiteRabbit\WhiteRabbit\Shapes\Shaders\Color.hlsl", "PS", "ps_5_0");

            inputLayout = new InputLayoutDescription(new[]
            {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 12, 0)
            });
        }

        //建立图形
        private void BuildShapeGeometry()
        {
            //要把所有的几何图形连接到一个大的顶点/索引缓冲区中，因此，定义每个子网格覆盖的缓冲区中的区域

            var vertices = new List<Vertex>();
            var indices = new List<short>();

            SubmeshGeometry box = AppendMeshData(GeometryGenerator.CreateBox(1.5f, 0.5f, 1.5f, 3), Color.DarkGreen, vertices, indices);
            SubmeshGeometry grid = AppendMeshData(GeometryGenerator.CreateGrid(20.0f, 30.0f, 60, 40), Color.ForestGreen, vertices, indices);
            SubmeshGeometry sphere = AppendMeshData(GeometryGenerator.CreateSphere(0.5f, 20, 20), Color.Crimson, vertices, indices);
            SubmeshGeometry cylinder = AppendMeshData(GeometryGenerator.CreateCylinder(0.5f, 0.3f, 3.0f, 20, 20), Color.SteelBlue, vertices, indices);

            var geo = MeshGeometry.New(Device, CommandList, vertices, indices.ToArray(), "shapeGeo");

            geo.DrawArgs["box"] = box;
            geo.DrawArgs["grid"] = grid;
            geo.DrawArgs["sphere"] = sphere;
            geo.DrawArgs["cylinder"] = cylinder;

            geometries[geo.Name] = geo;
        }

        private SubmeshGeometry AppendMeshData(GeometryGenerator.MeshData meshData, Color color, List<Vertex> vertices, List<short> indices)
        {
            //定义覆盖顶点/索引缓冲区不同区域的子网格几何

            var submesh = new SubmeshGeometry
            {
                IndexCount = meshData.Indices32.Count,
                StartIndexLocation = indices.Count,
                BaseVertexLocation = vertices.Count
            };

            //提取顶点元素，并将所有网格的顶点和索引打包到一个顶点/索引缓冲区中

            vertices.AddRange(meshData.Vertices.Select(vertex => new Vertex
            {
                Pos = vertex.Position,
                Color = color.ToVector4()
            }));
            indices.AddRange(meshData.GetIndices16());

            return submesh;
        }

        //建立PSO
        private void BuildPSOs()
        {
            //PSO用于不透明对象

            var opaquePsoDesc = new GraphicsPipelineStateDescription
            {
                InputLayout = inputLayout,
                RootSignature = rootSignature,
                VertexShader = shaders["standardVS"],
                PixelShader = shaders["opaquePS"],
                RasterizerState = RasterizerStateDescription.Default(),
                BlendState = BlendStateDescription.Default(),
                DepthStencilState = DepthStencilStateDescription.Default(),
                SampleMask = int.MaxValue,
                PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
                RenderTargetCount = 1,
                Flags = PipelineStateFlags.None,
                SampleDescription = new SampleDescription(MsaaCount, MsaaQuality),
                DepthStencilFormat = DepthStencilFormat,
                StreamOutput = new StreamOutputDescription()
            };
            opaquePsoDesc.RenderTargetFormats[0] = BackBufferFormat;

            psos["opaque"] = Device.CreateGraphicsPipelineState(opaquePsoDesc);

            //PSO用于不透明的线框对象

            var opaqueWireframePsoDesc = opaquePsoDesc;
            opaqueWireframePsoDesc.RasterizerState.FillMode = FillMode.Wireframe;

            psos["opaque_wireframe"] = Device.CreateGraphicsPipelineState(opaqueWireframePsoDesc);
        }

        //建立帧资源
        private void BuildFrameResources()
        {
            for (int i = 0; i < NumFrameResources; i++)
            {
                frameResources.Add(new FrameResource(Device, 1, allRitems.Count));
                fenceEvents.Add(new AutoResetEvent(false));
            }
        }

        private void BuildRenderItems()
        {
            AddRenderItem(RenderLayer.Opaque, 0, "shapeGeo", "box",
                world: Matrix.Scaling(2.0f, 2.0f, 2.0f) * Matrix.Translation(0.0f, 0.5f, 0.0f));
            AddRenderItem(RenderLayer.Opaque, 1, "shapeGeo", "grid");

            int objCBIndex = 2;
            for (int i = 0; i < 5; ++i)
            {
                AddRenderItem(RenderLayer.Opaque, objCBIndex++, "shapeGeo", "cylinder",
                    world: Matrix.Translation(-5.0f, 1.5f, -10.0f + i * 5.0f));
                AddRenderItem(RenderLayer.Opaque, objCBIndex++, "shapeGeo", "cylinder",
                    world: Matrix.Translation(+5.0f, 1.5f, -10.0f + i * 5.0f));

                AddRenderItem(RenderLayer.Opaque, objCBIndex++, "shapeGeo", "sphere",
                    world: Matrix.Translation(-5.0f, 3.5f, -10.0f + i * 5.0f));
                AddRenderItem(RenderLayer.Opaque, objCBIndex++, "shapeGeo", "sphere",
                    world: Matrix.Translation(+5.0f, 3.5f, -10.0f + i * 5.0f));
            }
        }

        private void AddRenderItem(RenderLayer layer, int objCBIndex, string geoName, string submeshName, Matrix? world = null)
        {
            MeshGeometry geo = geometries[geoName];
            SubmeshGeometry submesh = geo.DrawArgs[submeshName];
            var renderItem = new RenderItem
            {
                ObjCBIndex = objCBIndex,
                Geo = geo,
                IndexCount = submesh.IndexCount,
                StartIndexLocation = submesh.StartIndexLocation,
                BaseVertexLocation = submesh.BaseVertexLocation,
                World = world ?? Matrix.Identity
            };
            ritemLayers[layer].Add(renderItem);
            allRitems.Add(renderItem);
        }

        private void DrawRenderItems(GraphicsCommandList cmdList, List<RenderItem> ritems)
        {
            //对于每个渲染项…
            foreach (RenderItem ri in ritems)
            {
                cmdList.SetVertexBuffer(0, ri.Geo.VertexBufferView);
                cmdList.SetIndexBuffer(ri.Geo.IndexBufferView);
                cmdList.PrimitiveTopology = ri.PrimitiveType;

                //该对象和该帧资源在描述符堆中的CBV偏移量
                int cbvIndex = currFrameResourceIndex * allRitems.Count + ri.ObjCBIndex;
                GpuDescriptorHandle cbvHandle = cbvHeap.GPUDescriptorHandleForHeapStart;
                cbvHandle += cbvIndex * CbvSrvUavDescriptorSize;

                cmdList.SetGraphicsRootDescriptorTable(0, cbvHandle);

                cmdList.DrawIndexedInstanced(ri.IndexCount, 1, ri.StartIndexLocation, ri.BaseVertexLocation, 0);
            }
        }
    }
}
 