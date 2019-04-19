using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D12;
using SharpDX.DXGI;
using WhiteRabbit.Framework;
using static WhiteRabbit.XLoader.RenderItem;
using Resource = SharpDX.Direct3D12.Resource;
using ShaderResourceViewDimension = SharpDX.Direct3D12.ShaderResourceViewDimension;


namespace WhiteRabbit.XLoader
{
    public class XLoader : D3DApp
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

        private readonly float angleY;

        public Vector3 CamPostion;
        public Vector3 CamTarget;

        private Point lastMousePos;

        private bool isRotateByMouse = false;
        private bool isMoveByMouse = false;

        Mesh mesh;

        private string filename;
        private string dirpath;
        int triangleCount;

        public XLoader()
        {
            MainWindowCaption = "XLoader";
        }

        private FrameResource CurrFrameResource => frameResources[currFrameResourceIndex];
        private AutoResetEvent CurrentFenceEvent => fenceEvents[currFrameResourceIndex];

        public override void Initialize()
        {
            base.Initialize();

            //重置命令列表以准备初始化命令
            CommandList.Reset(DirectCmdListAlloc, null);

            LoadMesh(filename);
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

        private void LoadMesh(string filename)
        {
            byte[] buffer;
            filename = Path.GetFullPath(filename);
            dirpath = Path.GetDirectoryName(filename);           

            using (BinaryReader br = new BinaryReader(File.OpenRead(filename)))
            {
                buffer = br.ReadBytes((int)br.BaseStream.Length);
            }

            XUtilities xu = new XUtilities(buffer);
            Mesh.Scene scene = xu.GetImportedData();

            if (scene.GlobalMeshes.Count > 0)
            {
                mesh = scene.GlobalMeshes[0];
            }
            else if (scene.RootNode.Meshes.Count > 0)
            {
                mesh = scene.RootNode.Meshes[0];
            }
            else
            {
                mesh = scene.RootNode.Children[0].Meshes[0];
            }
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
                slotRootParameters);

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
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                new InputElement("NORMAL", 0, Format.R32G32B32A32_Float, 16, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 32, 0)
            });
        }

        private void BuildShapeGeometry()
        {
            List<Vertex> vertices = new List<Vertex>();
            List<uint> materialIndices = new List<uint>();
            triangleCount = 0;

            for (int i = 0; i < mesh.PosFaces.Count; i++)
            {
                if (mesh.PosFaces[i].Indices.Count == 3)
                {
                    // Triangle
                    for (int j = 0; j < 3; j++)
                    {
                        Vector3 pos = mesh.Positions[(int)mesh.PosFaces[i].Indices[j]];
                        Vector3 nor = mesh.Normals[(int)mesh.NormalFaces[i].Indices[j]];
                        nor.Normalize();
                        Vector2 tex = mesh.NumTextures > 0 ? mesh.TexCoords[0][(int)mesh.PosFaces[i].Indices[j]] : Vector2.Zero;

                        var vertex = new Vertex()
                        {
                            Pos = new Vector4(pos, 1),
                            Normal = new Vector4(nor, 1),
                            TexC = tex
                        };

                        vertices.Add(vertex);
                    }
                    materialIndices.Add(mesh.FaceMaterials[i]);
                    triangleCount++;
                }
                else if (mesh.PosFaces[i].Indices.Count == 4)
                {
                    // Quadrilateral
                    int[] indexLine = new int[] { 0, 1, 2, 0, 2, 3 };
                    foreach (int j in indexLine)
                    {
                        Vector3 pos = mesh.Positions[(int)mesh.PosFaces[i].Indices[j]];
                        Vector3 nor = mesh.Normals[(int)mesh.NormalFaces[i].Indices[j]];
                        Vector2 tex = mesh.NumTextures > 0 ? mesh.TexCoords[0][(int)mesh.PosFaces[i].Indices[j]] : Vector2.Zero;

                        var vertex = new Vertex()
                        {
                            Pos = new Vector4(pos, 1),
                            Normal = new Vector4(nor, 1),
                            TexC = tex
                        };

                        vertices.Add(vertex);
                    }
                    materialIndices.Add(mesh.FaceMaterials[i]);
                    materialIndices.Add(mesh.FaceMaterials[i]);
                    triangleCount += 2;
                }
                else
                {
                    Console.Error.WriteLine("Polygon is neither triangle nor quadrilateral.");
                }
            }

            List<uint>[] indicesPerMaterial = new List<uint>[mesh.Materials.Count];

            for (int i = 0; i < indicesPerMaterial.Length; i++)
            {
                indicesPerMaterial[i] = new List<uint>();
            }

            for (uint i = 0; i < materialIndices.Count; i++)
            {
                indicesPerMaterial[(int)materialIndices[(int)i]].Add(i);
            }

            SubmeshGeometry[] geoSubmesh = null;
            
            for (int i = 0; i < indicesPerMaterial.Length; i++)
            {
                geoSubmesh[i] = new SubmeshGeometry
                {
                    //待修改
                    IndexCount = (int)indicesPerMaterial[i].Count,
                    StartIndexLocation = indicesPerMaterial[i].Count,
                    BaseVertexLocation = vertices.Count
                };
            }

            var geo = MeshGeometry.New(Device, CommandList, vertices, materialIndices, "XLoader");

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

    }
}
