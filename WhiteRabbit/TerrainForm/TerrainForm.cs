using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D12;
using SharpDX.DXGI;
using WhiteRabbit.Framework;
using static WhiteRabbit.TerrainForm.RenderItem;
using Resource = SharpDX.Direct3D12.Resource;
using ShaderResourceViewDimension = SharpDX.Direct3D12.ShaderResourceViewDimension;

namespace WhiteRabbit.TerrainForm
{
    public class TerrainForm : D3DApp
    {
        private readonly List<FrameResource> frameResources = new List<FrameResource>(NumFrameResources);
        private readonly List<AutoResetEvent> fenceEvents = new List<AutoResetEvent>(NumFrameResources);
        private int currFrameResourceIndex;

        private DescriptorHeap srvDescriptorHeap;
        private DescriptorHeap[] descriptorHeaps;

        private RootSignature rootSignature;

        private readonly Dictionary<string, MeshGeometry> geometries = new Dictionary<string, MeshGeometry>();
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly Dictionary<string, Texture> textures = new Dictionary<string, Texture>();
        private readonly Dictionary<string, ShaderBytecode> shaders = new Dictionary<string, ShaderBytecode>();

        private PipelineState opaquePso;

        private InputLayoutDescription inputLayout;

        private readonly List<RenderItem> allRitems = new List<RenderItem>();

        private readonly Dictionary<RenderLayer, List<RenderItem>> ritemLayers = new Dictionary<RenderLayer, List<RenderItem>>(1)
        {
            [RenderLayer.Opaque] = new List<RenderItem>()
        };

        private PassConstants mainPassCB = PassConstants.Default;

        private Vector3 eyePos;
        private Matrix proj = Matrix.Identity;
        private Matrix view = Matrix.Identity;

        private int xCount = 5, yCount = 4;
        private float cellHeight = 1f, cellWidth = 1f;

        private readonly float angleY = 0.01f;

        public Vector3 CamPostion = new Vector3(0, 100, 100);
        public Vector3 CamTarget = new Vector3(125, 30, 125);

        private Point lastMousePos;

        private bool isRotateByMouse = false;
        private bool isMoveByMouse = false;

        public TerrainForm()
        {
            MainWindowCaption = "TerrainForm";
        }

        private FrameResource CurrFrameResource => frameResources[currFrameResourceIndex];
        private AutoResetEvent CurrentFenceEvent => fenceEvents[currFrameResourceIndex];

        public override void Initialize()
        {
            base.Initialize();

            //重置命令列表以准备初始化命令
            CommandList.Reset(DirectCmdListAlloc, null);

            LoadTextures();
            BuildRootSignature();
            BuildDescriptorHeaps();
            BuildShadersAndInputLayout();
            BuildShapeGeometry();
            BuildMaterials();
            BuildRenderItems();
            BuildFrameResources();
            BuildPSOs();

            //执行初始化命令
            CommandList.Close();
            CommandQueue.ExecuteCommandList(CommandList);

            //等待初始化完成
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

            //循环遍历循环框架资源数组
            currFrameResourceIndex = (currFrameResourceIndex + 1) % NumFrameResources;

            //GPU处理完当前帧资源的命令了吗?
            //如果没有，请等到GPU完成该栅栏点之前的命令。
            if (CurrFrameResource.Fence != 0 && Fence.CompletedValue < CurrFrameResource.Fence)
            {
                Fence.SetEventOnCompletion(CurrFrameResource.Fence, CurrentFenceEvent.SafeWaitHandle.DangerousGetHandle());
                CurrentFenceEvent.WaitOne();
            }

            UpdateObjectCBs();
            UpdateMaterialCBs();
            UpdateMainPassCB(gt);
        }

        protected override void Draw(GameTimer gt)
        {
            CommandAllocator cmdListAlloc = CurrFrameResource.CmdListAlloc;

            //重置与命令记录关联的内存
            //只有相关的命令列表在GPU中执行完成，才能重置
            cmdListAlloc.Reset();

            //命令列表通过ExecuteCommandList添加到命令队列后可以重置
            //重置命令列表将重置内存
            CommandList.Reset(cmdListAlloc, opaquePso);

            CommandList.SetViewport(Viewport);
            CommandList.SetScissorRectangles(ScissorRectangle);

            //状态转换
            CommandList.ResourceBarrierTransition(CurrentBackBuffer, ResourceStates.Present, ResourceStates.RenderTarget);

            //清除后台缓冲区和深度缓冲区
            CommandList.ClearRenderTargetView(CurrentBackBufferView, Color.LightSteelBlue);
            CommandList.ClearDepthStencilView(DepthStencilView, ClearFlags.FlagsDepth | ClearFlags.FlagsStencil, 1.0f, 0);

            //指定要渲染的缓冲区。
            CommandList.SetRenderTargets(CurrentBackBufferView, DepthStencilView);

            CommandList.SetDescriptorHeaps(1, descriptorHeaps);

            CommandList.SetGraphicsRootSignature(rootSignature);

            Resource passCB = CurrFrameResource.PassCB.Resource;
            CommandList.SetGraphicsRootConstantBufferView(2, passCB.GPUVirtualAddress);

            DrawRenderItems(CommandList, ritemLayers[RenderLayer.Opaque]);

            //状态转换
            CommandList.ResourceBarrierTransition(CurrentBackBuffer, ResourceStates.RenderTarget, ResourceStates.Present);

            //执行完成后关闭命令列表
            CommandList.Close();

            //将命令列表添加到队列中以便执行
            CommandQueue.ExecuteCommandList(CommandList);

            //将缓冲区呈现给屏幕，将自动交换前后缓冲区
            SwapChain.Present(0, PresentFlags.None);

            //++fence值，将命令标记到此fence点
            CurrFrameResource.Fence = ++CurrentFence;

            //向命令队列中添加一条指令，以设置一个新的栅栏点
            CommandQueue.Signal(Fence, CurrentFence);
        }

        protected override void OnKeyDown(Keys keyCode)
        {
            Vector4 tempV4;
            Matrix currentView = mainPassCB.View;
            switch (keyCode)
            {
                case Keys.Left:
                    CamPostion = Vector3.Subtract(CamPostion, CamTarget);
                    tempV4 = Vector3.Transform(CamPostion, Matrix.RotationQuaternion(
                            Quaternion.RotationAxis(new Vector3(currentView.M12, currentView.M22, currentView.M32), -angleY)));
                    CamPostion.X = tempV4.X + CamTarget.X;
                    CamPostion.Y = tempV4.Y + CamTarget.Y;
                    CamPostion.Z = tempV4.Z + CamTarget.Z;
                    break;
                case Keys.Right:
                    CamPostion = Vector3.Subtract(CamPostion, CamTarget);
                    tempV4 = Vector3.Transform(CamPostion, Matrix.RotationQuaternion(
                            Quaternion.RotationAxis(new Vector3(currentView.M12, currentView.M22, currentView.M32), angleY)));
                    CamPostion.X = tempV4.X + CamTarget.X;
                    CamPostion.Y = tempV4.Y + CamTarget.Y;
                    CamPostion.Z = tempV4.Z + CamTarget.Z;
                    break;
                case Keys.Up:
                    CamPostion = Vector3.Subtract(CamPostion, CamTarget);
                    tempV4 = Vector3.Transform(CamPostion, Matrix.RotationQuaternion(
                       Quaternion.RotationAxis(new Vector3(mainPassCB.View.M11
                       , mainPassCB.View.M21, mainPassCB.View.M31), -angleY)));
                    CamPostion.X = tempV4.X + CamTarget.X;
                    CamPostion.Y = tempV4.Y + CamTarget.Y;
                    CamPostion.Z = tempV4.Z + CamTarget.Z;
                    break;
                case Keys.Down:
                    CamPostion = Vector3.Subtract(CamPostion, CamTarget);
                    tempV4 = Vector3.Transform(CamPostion, Matrix.RotationQuaternion(
                       Quaternion.RotationAxis(new Vector3(mainPassCB.View.M11
                       , mainPassCB.View.M21, mainPassCB.View.M31), angleY)));
                    CamPostion.X = tempV4.X + CamTarget.X;
                    CamPostion.Y = tempV4.Y + CamTarget.Y;
                    CamPostion.Z = tempV4.Z + CamTarget.Z;
                    break;
                case Keys.Add:
                    CamPostion = Vector3.Subtract(CamPostion, CamTarget);
                    
                    CamPostion.X = CamPostion.X * 0.95f;
                    CamPostion.Y = CamPostion.Y * 0.95f;
                    CamPostion.Z = CamPostion.Z * 0.95f;
                    CamPostion = Vector3.Add(CamPostion, CamTarget);
                    break;
                case Keys.Subtract:
                    CamPostion = Vector3.Subtract(CamPostion, CamTarget);
                    CamPostion.X = CamPostion.X * 1.05f;
                    CamPostion.Y = CamPostion.Y * 1.05f;
                    CamPostion.Z = CamPostion.Z * 1.05f;
                    CamPostion = Vector3.Add(CamPostion, CamTarget);
                    break;
            }

            eyePos = CamPostion;

            Matrix viewMatrix = Matrix.LookAtLH(eyePos, CamTarget, Vector3.Up);
            mainPassCB.View = viewMatrix;
        }

        protected override void OnMouseDown(MouseButtons button, Point location)
        {
            if (button == MouseButtons.Left)
            {
                lastMousePos = location;
                isRotateByMouse = true;
            }
            else if (button == MouseButtons.Middle)
            {
                lastMousePos = location;
                isMoveByMouse = true;
            }
        }

        protected override void OnMouseUp(MouseButtons button, Point location)
        {
            isRotateByMouse = false;
            isMoveByMouse = false;
        }

        protected override void OnMouseMove(MouseButtons button, Point location)
        {
            if (isRotateByMouse)
            {
                Matrix currentView = mainPassCB.View;
                float tempAngleY = 2 * (float)(location.X - lastMousePos.X) / this.ClientWidth;
                CamPostion = Vector3.Subtract(CamPostion, CamTarget);
                Vector4 tempV4 = Vector3.Transform(CamPostion, Matrix.RotationQuaternion(
                    Quaternion.RotationAxis(new Vector3(currentView.M12, currentView.M22, currentView.M32), tempAngleY)));
                CamPostion.X = tempV4.X;
                CamPostion.Y = tempV4.Y;
                CamPostion.Z = tempV4.Z;

                float tempAngleX = 4 * (float)(location.Y - lastMousePos.Y) / this.ClientHeight;
                tempV4 = Vector3.Transform(CamPostion, Matrix.RotationQuaternion(
                    Quaternion.RotationAxis(new Vector3(currentView.M11, currentView.M21, currentView.M31), tempAngleX)));
                CamPostion.X = tempV4.X + CamTarget.X;
                CamPostion.Y = tempV4.Y + CamTarget.Y;
                CamPostion.Z = tempV4.Z + CamTarget.Z;

                eyePos = CamPostion;

                Matrix viewMatrix = Matrix.LookAtLH(eyePos, CamTarget, Vector3.Up);
                mainPassCB.View = viewMatrix;
            }
            else if (isMoveByMouse)
            {
                Matrix currentView = mainPassCB.View;
                float moveFactor = 0.01f;
                CamPostion.X += -moveFactor * ((location.X - lastMousePos.X) * currentView.M11 - (location.Y - lastMousePos.Y) * currentView.M12);
                CamPostion.Y += -moveFactor * ((location.X - lastMousePos.X) * currentView.M21 - (location.Y - lastMousePos.Y) * currentView.M22);
                CamPostion.Z += -moveFactor * ((location.X - lastMousePos.X) * currentView.M31 - (location.Y - lastMousePos.Y) * currentView.M32);

                CamPostion.X += -moveFactor * ((location.X - lastMousePos.X) * currentView.M11 - (location.Y - lastMousePos.Y) * currentView.M12);
                CamPostion.Y += -moveFactor * ((location.X - lastMousePos.X) * currentView.M21 - (location.Y - lastMousePos.Y) * currentView.M22);
                CamPostion.Z += -moveFactor * ((location.X - lastMousePos.X) * currentView.M31 - (location.Y - lastMousePos.Y) * currentView.M32);

                eyePos = CamPostion;

                Matrix viewMatrix = Matrix.LookAtLH(eyePos, CamTarget, Vector3.Up);
                mainPassCB.View = viewMatrix;
            }

            lastMousePos = location;
        }
        
        protected override void OnMouseWheel(MouseButtons button, Point wheel)
        {
            //这里的wheel.X并不是滚轮的X坐标，而是指鼠标滚轮的delta值
            float scaleFactor = -(float) wheel.X/ 2000 + 1f;

            CamPostion = Vector3.Subtract(CamPostion, CamTarget);
            CamPostion.X = CamPostion.X * scaleFactor;
            CamPostion.Y = CamPostion.Y * scaleFactor;
            CamPostion.Z = CamPostion.Z * scaleFactor;
            CamPostion = Vector3.Add(CamPostion, CamTarget);

            eyePos = CamPostion;

            Matrix viewMatrix = Matrix.LookAtLH(eyePos, CamTarget, Vector3.Up);
            mainPassCB.View = viewMatrix;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                srvDescriptorHeap?.Dispose();
                opaquePso?.Dispose();
                rootSignature?.Dispose();
                foreach (Texture texture in textures.Values) texture.Dispose();
                foreach (FrameResource frameResource in frameResources) frameResource.Dispose();
                foreach (MeshGeometry geometry in geometries.Values) geometry.Dispose();
            }
            base.Dispose(disposing);
        }

        private void UpdateCamera()
        {
            eyePos = CamPostion;

            //建立观察矩阵
            view = Matrix.LookAtLH(eyePos, CamTarget, Vector3.Up);
        }

        private void UpdateObjectCBs()
        {
            foreach (RenderItem e in allRitems)
            {
                //只有在常量发生更改时才更新cbuffer数据
                //需要对每个帧资源进行跟踪
                if (e.NumFramesDirty > 0)
                {
                    var objConstants = new ObjectConstants
                    {
                        World = Matrix.Transpose(e.World),
                        TexTransform = Matrix.Transpose(e.TexTransform)
                    };
                    CurrFrameResource.ObjectCB.CopyData(e.ObjCBIndex, ref objConstants);

                    //下一个FrameResource也需要更新
                    e.NumFramesDirty--;
                }
            }
        }

        private void UpdateMaterialCBs()
        {
            UploadBuffer<MaterialConstants> currMaterialCB = CurrFrameResource.MaterialCB;
            foreach (Material mat in materials.Values)
            {
                //只有在常量发生更改时才更新cbuffer数据
                //如果cbuffer数据发生更改，则需要为每个FrameResource更新它。
                if (mat.NumFramesDirty > 0)
                {
                    var matConstants = new MaterialConstants
                    {
                        DiffuseAlbedo = mat.DiffuseAlbedo,
                        FresnelR0 = mat.FresnelR0,
                        Roughness = mat.Roughness,
                        MatTransform = Matrix.Transpose(mat.MatTransform)
                    };

                    currMaterialCB.CopyData(mat.MatCBIndex, ref matConstants);

                    //下一个FrameResource也需要更新
                    mat.NumFramesDirty--;
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
            mainPassCB.AmbientLight = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            //mainPassCB.Lights[0].Direction = new Vector3(0.57735f, -0.57735f, 0.57735f);
            //mainPassCB.Lights[0].Strength = new Vector3(0.9f);
            //mainPassCB.Lights[1].Direction = new Vector3(-0.57735f, -0.57735f, 0.57735f);
            //mainPassCB.Lights[1].Strength = new Vector3(0.5f);
            //mainPassCB.Lights[2].Direction = new Vector3(0.0f, -0.707f, -0.707f);
            //mainPassCB.Lights[2].Strength = new Vector3(0.2f);

            CurrFrameResource.PassCB.CopyData(0, ref mainPassCB);
        }

        private void LoadTextures()
        {
            var CrateTex = new Texture
            {
                Name = "CrateTex",
                Filename = @"C:\Users\yulanli\Desktop\WhiteRabbit-master\WhiteRabbit\TerrainForm\Textures\colorMap.dds"
            };
            CrateTex.Resource = TextureUtilities.CreateTextureFromDDS(Device, CrateTex.Filename);
            textures[CrateTex.Name] = CrateTex;
        }

        private void BuildRootSignature()
        {
            var texTable = new DescriptorRange(DescriptorRangeType.ShaderResourceView, 1, 0);

            var descriptor1 = new RootDescriptor(0, 0);
            var descriptor2 = new RootDescriptor(1, 0);
            var descriptor3 = new RootDescriptor(2, 0);

            //根参数可以是表、根描述符或根常量
            var slotRootParameters = new[]
            {
                new RootParameter(ShaderVisibility.Pixel, texTable),
                new RootParameter(ShaderVisibility.Vertex, descriptor1, RootParameterType.ConstantBufferView),
                new RootParameter(ShaderVisibility.All, descriptor2, RootParameterType.ConstantBufferView),
                new RootParameter(ShaderVisibility.All, descriptor3, RootParameterType.ConstantBufferView)
            };

            //根签名是根参数的数组
            var rootSigDesc = new RootSignatureDescription(
                RootSignatureFlags.AllowInputAssemblerInputLayout,
                slotRootParameters,
                GetStaticSamplers());

            rootSignature = Device.CreateRootSignature(rootSigDesc.Serialize());
        }

        private void BuildDescriptorHeaps()
        {
            //创建SRV符堆
            var srvHeapDesc = new DescriptorHeapDescription
            {
                DescriptorCount = 1,
                Type = DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView,
                Flags = DescriptorHeapFlags.ShaderVisible
            };
            srvDescriptorHeap = Device.CreateDescriptorHeap(srvHeapDesc);
            descriptorHeaps = new[] { srvDescriptorHeap };

            //将创建的纹理资源与创建的SRV绑定
            var hDescriptor = srvDescriptorHeap.CPUDescriptorHandleForHeapStart;

            Resource crateTexture = textures["CrateTex"].Resource;

            var srvDesc = new ShaderResourceViewDescription
            {
                Shader4ComponentMapping = D3DUtil.DefaultShader4ComponentMapping,
                Format = crateTexture.Description.Format,
                Dimension = ShaderResourceViewDimension.Texture2D,
                Texture2D = new ShaderResourceViewDescription.Texture2DResource
                {
                    MostDetailedMip = 0,
                    MipLevels = crateTexture.Description.MipLevels,
                    ResourceMinLODClamp = 0.0f
                }
            };

            Device.CreateShaderResourceView(crateTexture, srvDesc, hDescriptor);
        }

        private void BuildShadersAndInputLayout()
        {
            shaders["standardVS"] = D3DUtil.CompileShader(@"C:\Users\yulanli\Desktop\WhiteRabbit-master\WhiteRabbit\TerrainForm\Shaders\Default.hlsl", "VS", "vs_5_0");
            shaders["opaquePS"] = D3DUtil.CompileShader(@"C:\Users\yulanli\Desktop\WhiteRabbit-master\WhiteRabbit\TerrainForm\Shaders\Default.hlsl", "PS", "ps_5_0");

            inputLayout = new InputLayoutDescription(new[]
            {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElement("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 24, 0)
            });
        }

        private void BuildShapeGeometry()
        {
            string bitmapPath = @"C:\Users\yulanli\Desktop\WhiteRabbit-master\WhiteRabbit\TerrainForm\Textures\heightMap.BMP";
            System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(bitmapPath);
            xCount = (bitmap.Width - 1) / 2;
            yCount = (bitmap.Height - 1) / 2;
            cellWidth = bitmap.Width / xCount;
            cellHeight = bitmap.Height / yCount;

            var vertices = new Vertex[(xCount + 1) * (yCount + 1)];
            for (int i = 0; i < yCount; i++)
            {
                for (int j = 0; j < xCount; j++)
                {
                    System.Drawing.Color color = bitmap.GetPixel((int)(j * cellWidth), (int)(i * cellHeight));
                    float height = float.Parse(color.R.ToString()) + float.Parse(color.G.ToString()) + float.Parse(color.B.ToString());
                    height /= 10;
                    vertices[j + i * (xCount + 1)].Pos = new Vector3(j * cellWidth, height, i * cellHeight);
                    vertices[j + i * (xCount + 1)].TexC = new Vector2((float)j / (xCount + 1), (float)i / (yCount + 1));
                    vertices[j + i * (xCount + 1)].Normal = GetNormal(vertices[j + i * (xCount + 1)].Pos.X, vertices[j + i * (xCount + 1)].Pos.Z);
                }
            }

            CamTarget = new Vector3(bitmap.Width / 2, 0f, bitmap.Height / 2);

            var indices = new int[6 * xCount * yCount];
            for (int i = 0; i < yCount; i++)
            {
                for (int j = 0; j < xCount; j++)
                {
                    indices[6 * (j + i * xCount)] = j + i * (xCount + 1);
                    indices[6 * (j + i * xCount) + 1] = j + (i + 1) * (xCount + 1);
                    indices[6 * (j + i * xCount) + 2] = j + i * (xCount + 1) + 1;
                    indices[6 * (j + i * xCount) + 3] = j + i * (xCount + 1) + 1;
                    indices[6 * (j + i * xCount) + 4] = j + (i + 1) * (xCount + 1);
                    indices[6 * (j + i * xCount) + 5] = j + (i + 1) * (xCount + 1) + 1;
                }
            }

            var geoSubmesh = new SubmeshGeometry
            {
                IndexCount = 6 * xCount * yCount,
                StartIndexLocation = 0,
                BaseVertexLocation = 0
            };

            var geo = MeshGeometry.New(Device, CommandList, vertices, indices, "TerrainForm");

            geo.DrawArgs["TerrainForm"] = geoSubmesh;

            geometries[geo.Name] = geo;
        }

        private void BuildPSOs()
        {
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
                SampleDescription = new SampleDescription(MsaaCount, MsaaQuality),
                DepthStencilFormat = DepthStencilFormat,
                StreamOutput = new StreamOutputDescription()
            };
            opaquePsoDesc.RenderTargetFormats[0] = BackBufferFormat;

            opaquePso = Device.CreateGraphicsPipelineState(opaquePsoDesc);
        }

        private void BuildFrameResources()
        {
            for (int i = 0; i < NumFrameResources; i++)
            {
                frameResources.Add(new FrameResource(Device, 1, allRitems.Count, materials.Count));
                fenceEvents.Add(new AutoResetEvent(false));
            }
        }

        private void BuildMaterials()
        {
            AddMaterial(new Material
            {
                Name = "CrateMat",
                MatCBIndex = 0,
                DiffuseSrvHeapIndex = 0,
                DiffuseAlbedo = Color.White.ToVector4(),
                FresnelR0 = Color.LightGray.ToVector3(),
                Roughness = 0.85f
            });
        }

        private void AddMaterial(Material mat) => materials[mat.Name] = mat;

        private void BuildRenderItems()
        {
            MeshGeometry geo = geometries["TerrainForm"];
            SubmeshGeometry submesh = geo.DrawArgs["TerrainForm"];
            var renderItem = new RenderItem
            {
                ObjCBIndex = 0,
                Mat = materials["CrateMat"],
                Geo = geo,
                PrimitiveType = PrimitiveTopology.TriangleList,
                IndexCount = submesh.IndexCount,
                StartIndexLocation = submesh.StartIndexLocation,
                BaseVertexLocation = submesh.BaseVertexLocation
            };

            allRitems.Add(renderItem);
            //所有渲染项都是不透明的
            ritemLayers[RenderLayer.Opaque].AddRange(allRitems);
        }

        private void DrawRenderItems(GraphicsCommandList cmdList, List<RenderItem> ritems)
        {
            int objCBByteSize = D3DUtil.CalcConstantBufferByteSize<ObjectConstants>();
            int matCBByteSize = D3DUtil.CalcConstantBufferByteSize<MaterialConstants>();

            Resource objectCB = CurrFrameResource.ObjectCB.Resource;
            Resource matCB = CurrFrameResource.MaterialCB.Resource;

            foreach (RenderItem ri in ritems)
            {
                cmdList.SetVertexBuffer(0, ri.Geo.VertexBufferView);
                cmdList.SetIndexBuffer(ri.Geo.IndexBufferView);
                cmdList.PrimitiveTopology = ri.PrimitiveType;

                GpuDescriptorHandle tex = srvDescriptorHeap.GPUDescriptorHandleForHeapStart +
                    ri.Mat.DiffuseSrvHeapIndex * CbvSrvUavDescriptorSize;

                long objCBAddress = objectCB.GPUVirtualAddress + ri.ObjCBIndex * objCBByteSize;
                long matCBAddress = matCB.GPUVirtualAddress + ri.Mat.MatCBIndex * matCBByteSize;

                cmdList.SetGraphicsRootDescriptorTable(0, tex);
                cmdList.SetGraphicsRootConstantBufferView(1, objCBAddress);
                cmdList.SetGraphicsRootConstantBufferView(3, matCBAddress);

                cmdList.DrawIndexedInstanced(ri.IndexCount, 1, ri.StartIndexLocation, ri.BaseVertexLocation, 0);
            }
        }

        private static Vector3 GetNormal(float x, float z) => Vector3.Normalize(new Vector3(
            -0.03f * z * MathHelper.Cosf(0.1f * x) - 0.3f * MathHelper.Cosf(0.1f * z),
            1.0f,
            -0.3f * MathHelper.Sinf(0.1f * x) + 0.03f * x * MathHelper.Sinf(0.1f * z)));

        //应用程序通常只需要少量的采样器
        //因此，只需预先定义它们，并将它们作为根签名的一部分保持可用
        private static StaticSamplerDescription[] GetStaticSamplers() => new[]
        {
            // PointWrap
            new StaticSamplerDescription(ShaderVisibility.All, 0, 0)
            {
                Filter = Filter.MinMagMipPoint,
                AddressUVW = TextureAddressMode.Wrap
            },
            // PointClamp
            new StaticSamplerDescription(ShaderVisibility.All, 1, 0)
            {
                Filter = Filter.MinMagMipPoint,
                AddressUVW = TextureAddressMode.Clamp
            },
            // LinearWrap
            new StaticSamplerDescription(ShaderVisibility.All, 2, 0)
            {
                Filter = Filter.MinMagMipLinear,
                AddressUVW = TextureAddressMode.Wrap
            },
            // LinearClamp
            new StaticSamplerDescription(ShaderVisibility.All, 3, 0)
            {
                Filter = Filter.MinMagMipLinear,
                AddressUVW = TextureAddressMode.Clamp
            },
            // AnisotropicWrap
            new StaticSamplerDescription(ShaderVisibility.All, 4, 0)
            {
                Filter = Filter.Anisotropic,
                AddressUVW = TextureAddressMode.Wrap,
                MipLODBias = 0.0f,
                MaxAnisotropy = 8
            },
            // AnisotropicClamp
            new StaticSamplerDescription(ShaderVisibility.All, 5, 0)
            {
                Filter = Filter.Anisotropic,
                AddressUVW = TextureAddressMode.Clamp,
                MipLODBias = 0.0f,
                MaxAnisotropy = 8
            }
        };
    }
}
