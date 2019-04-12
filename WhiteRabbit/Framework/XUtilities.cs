using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using static WhiteRabbit.Framework.Mesh;
using VectorKey = System.Collections.Generic.KeyValuePair<double, SharpDX.Vector3>;
using QuatKey = System.Collections.Generic.KeyValuePair<double, SharpDX.Quaternion>;
using MatrixKey = System.Collections.Generic.KeyValuePair<double, SharpDX.Matrix>;

/// <summary>
/// X文件的自定义加载程序
/// </summary>
namespace WhiteRabbit.Framework
{
    public class XUtilities
    {
        public const uint AI_MAX_NUMBER_OF_TEXTURECOORDS = 4;
        public const uint AI_MAX_NUMBER_OF_COLOR_SETS = 4;
        const uint MSZIP_MAGIC = 0x4B43;
        const uint MSZIP_BLOCK = 32786;

        protected uint majorVersion, minorVersion;

        protected bool isBinaryFormat;

        protected uint binaryFloatSize;

        protected uint binaryNumCount;

        protected int p;

        protected byte[] buffer;

        //计数器，用于二进制格式的数组
        protected int end;

        //以文本格式 Read 时的行号
        protected uint lineNumber;

        protected Scene scene;

        //构造函数，为X文件创建数据结构
        public XUtilities(byte[] buffer)
        {
            this.buffer = buffer;
            majorVersion = 0;
            minorVersion = 0;
            isBinaryFormat = false;
            binaryNumCount = 0;
            p = -1;
            end = -1;
            lineNumber = 0;
            scene = null;

            // vector to store uncompressed file for INFLATE'd X files
            byte[] uncompressed;

            // set up memory pointers
            p = 0;
            end = buffer.Length;

            string header = Encoding.Default.GetString(buffer, 0, 16);
            // check header
            if (header.Substring(0, 4) != "xof ")
                throw new Exception("Header mismatch, file is not an XFile.");

            // read version. It comes in a four byte format such as "0302" that means version-3.2
            majorVersion = uint.Parse(header.Substring(4, 2));
            minorVersion = uint.Parse(header.Substring(6, 2));

            bool compressed = false;

            // txt - pure ASCII text format（代表接下来的.x文件是用text格式存储的）
            if (header.Substring(8, 4) == "txt ")
            {
                isBinaryFormat = false;
            }
            // bin - Binary format（代表接下来的.x文件是用二进制格式存储的）
            else if (header.Substring(8, 4) == "bin ")
            {
                isBinaryFormat = true;
            }
            // tzip - Inflate compressed text format（代表接下来的.x文件是用压缩文本格式存储的）
            else if (header.Substring(8, 4) == "tzip")
            {
                isBinaryFormat = false;
                compressed = true;
            }
            // bzip - Inflate compressed binary format（代表接下来的.x文件是用压缩二进制格式存储的）
            else if (header.Substring(8, 4) == "bzip")
            {
                isBinaryFormat = true;
                compressed = true;
            }
            else ThrowException(string.Format("Unsupported xfile format '{0}'", header.Substring(8, 4)));

            // float size
            binaryFloatSize = uint.Parse(header.Substring(12, 4));

            if (binaryFloatSize != 32 && binaryFloatSize != 64)
                ThrowException(string.Format("Unknown float size {0} specified in xfile header.", binaryFloatSize));

            //X文件以位为单位大小，但是我们以字节为单位
            binaryFloatSize /= 8;

            p += 16;

            //如果这是一个压缩后的X文件，应对应使用解压算法
            if (compressed)
            {
                //throw (new Exception("Assimp was built without compressed X support"));
                MemoryStream stream = new MemoryStream(buffer);
                stream.Position += 16;
                stream.Position += 6;
                long p = stream.Position;
                long p1 = stream.Position;
                uint estOut = 0;
                while (p1 + 3 < end)
                {
                    ushort ofs = BitConverter.ToUInt16(buffer, (int)p1);
                    p1 += 2;
                    if (ofs >= MSZIP_BLOCK)
                    {
                        throw (new Exception("X: Invalid offset to next MSZIP compressed block"));
                    }
                    ushort magic = BitConverter.ToUInt16(buffer, (int)p1);
                    p1 += 2;
                    if (magic != MSZIP_MAGIC)
                    {
                        throw (new Exception("X: Unsupported compressed format, expected MSZIP header"));
                    }
                    p1 += ofs;
                    estOut += MSZIP_BLOCK;
                }

                uncompressed = new byte[estOut + 1];
                int uncompressedEnd = 0;
                while (p + 3 < end)
                {
                    ushort ofs = BitConverter.ToUInt16(buffer, (int)p);
                    p += 4;
                    if (p + ofs > end + 2)
                    {
                        throw (new Exception("X: Unexpected EOF in compressed chunk"));
                    }
                    stream.Position = p;
                    DeflateStream uncomp = new DeflateStream(stream, CompressionMode.Decompress);
                    int readLnegth = uncomp.Read(uncompressed, 0, (int)MSZIP_BLOCK);
                    uncompressedEnd += readLnegth;
                    p += ofs;
                }
                this.buffer = uncompressed;
                this.end = uncompressedEnd;
                this.p = 0;
            }
            else
            {
                //开始读文件
                ReadUntilEndOfLine();
            }

            scene = new Scene();
            ParseFile();

            // filter the imported hierarchy for some degenerated cases
            if (scene.RootNode != null)
            {
                FilterHierarchy(scene.RootNode);
            }
        }

        ~XUtilities()
        {
        }

        public Scene GetImportedData()
        {
            return scene;
        }

        protected void ParseFile()
        {
            bool running = true;
            while (running)
            {
                // read name of next object(读下一个对象的名字)
                string objectName = GetNextToken();
                if (objectName.Length == 0)
                {
                    break;
                }

                //解析特定对象
                if (objectName == "template")
                {
                    ParseDataObjectTemplate();
                }
                else if (objectName == "Frame")
                {
                    ParseDataObjectFrame(null);
                }
                else if (objectName == "Mesh")
                {
                    // some meshes have no frames at all
                    Mesh mesh;
                    ParseDataObjectMesh(out mesh);
                    scene.GlobalMeshes.Add(mesh);
                }
                else if (objectName == "AnimTicksPerSecond")
                {
                    ParseDataObjectAnimTicksPerSecond();
                }
                else if (objectName == "AnimationSet")
                {
                    ParseDataObjectAnimationSet();
                }
                else if (objectName == "Material")
                {
                    //网格或节点外部的材料
                    Material material;
                    ParseDataObjectMaterial(out material);
                    scene.GlobalMaterial.Add(material);
                }
                else if (objectName == "}")
                {
                    // whatever?
                    Debug.WriteLine("} found in dataObject");
                }
                else
                {
                    // unknown format
                    Debug.WriteLine("Unknown data object in animation of .x file");
                    ParseUnknownDataObject();
                }
            }
        }

        protected void ParseDataObjectTemplate()
        {
            //解析一个 template 数据对象，目前暂无存储
            string name;
            ReadHeadOfDataObject(out name);

            //读 GUID
            string guid = GetNextToken();

            //读取及过滤数据成员
            bool running = true;
            while (running)
            {
                string s = GetNextToken();
                if (s == "}")
                    break;
                if (s.Length == 0)
                    ThrowException("Unexpected end of file reached while parsing template definition");
            }
        }

        protected void ParseDataObjectFrame(Node parent)
        {
            string name;
            ReadHeadOfDataObject(out name);

            //创建一个命名节点，并将其放在它的父节点上
            Node node = new Node(parent);
            node.Name = name;
            if (parent != null)
            {
                parent.Children.Add(node);
            }
            else
            {
                //可能会有多个根节点
                if (scene.RootNode != null)
                {
                    if (scene.RootNode.Name != "$dummy_root")
                    {
                        // place a dummy root if not there
                        Node exroot = scene.RootNode;
                        scene.RootNode = new Node(null);
                        scene.RootNode.Name = "$dummy_root:";
                        scene.RootNode.Children.Add(exroot);
                        exroot.Parent = scene.RootNode;
                    }
                    //将新节点作为它的子节点
                    scene.RootNode.Children.Add(node);
                    node.Parent = scene.RootNode;
                }
                else
                {
                    //这是导入的第一个节点。把它作为根节点、
                    scene.RootNode = node;
                }
            }
            // Now inside a frame.
            // Read tokens until closing brace is reached.
            bool running = true;
            while (running)
            {
                string objectName = GetNextToken();
                if (objectName.Length == 0)
                {
                    ThrowException("Unexpected end of file reached while parsing frame");
                }

                if (objectName == "}")
                {
                    break; // frame finished
                }
                else if (objectName == "Frame")
                {
                    ParseDataObjectFrame(node); // child frame
                }
                else if (objectName == "FrameTransformMatrix")
                {
                    ParseDataObjectTransformationMatrix(out node.TrafoMatrix);
                }
                else if (objectName == "Mesh")
                {
                    Mesh mesh;
                    ParseDataObjectMesh(out mesh);
                    node.Meshes.Add(mesh);
                }
                else
                {
                    Debug.WriteLine("Unknown data object in frame in x file");
                    ParseUnknownDataObject();
                }
            }
        }

        protected void ParseDataObjectTransformationMatrix(out Matrix matrix)
        {
            //读头文件
            // we're not interested if it has a name
            ReadHeadOfDataObject();

            //读取组件
            matrix = new Matrix();
            matrix.M11 = ReadFloat(); matrix.M21 = ReadFloat();
            matrix.M31 = ReadFloat(); matrix.M41 = ReadFloat();
            matrix.M12 = ReadFloat(); matrix.M22 = ReadFloat();
            matrix.M33 = ReadFloat(); matrix.M42 = ReadFloat();
            matrix.M13 = ReadFloat(); matrix.M23 = ReadFloat();
            matrix.M33 = ReadFloat(); matrix.M43 = ReadFloat();
            matrix.M14 = ReadFloat(); matrix.M24 = ReadFloat();
            matrix.M33 = ReadFloat(); matrix.M44 = ReadFloat();

            //尾部符号
            CheckForSemicolon();
            CheckForClosingBrace();
        }

        protected void ParseDataObjectMesh(out Mesh mesh)
        {
            mesh = new Mesh();
            string name;
            ReadHeadOfDataObject(out name);

            //读顶点数量
            uint numVertices = ReadInt();
            mesh.Positions = new List<Vector3>((int)numVertices);

            //读顶点
            for (int a = 0; a < numVertices; a++)
                mesh.Positions.Add(ReadVector3());

            //读 position faces
            uint numPosFaces = ReadInt();
            mesh.PosFaces = new List<Face>((int)numPosFaces);
            for (uint a = 0; a < numPosFaces; a++)
            {
                uint numIndices = ReadInt();
                if (numIndices < 3)
                    ThrowException(string.Format("Invalid index count {0} for face {1}.", numIndices, a));

                //读索引
                Face face = new Face();
                face.Indices = new List<uint>();
                for (uint b = 0; b < numIndices; b++)
                    face.Indices.Add(ReadInt());
                mesh.PosFaces.Add(face);
                TestForSeparator();
            }

            //在这里，其他数据对象也可能随之出现
            bool running = true;
            while (running)
            {
                string objectName = GetNextToken();

                if (objectName.Length == 0)
                    ThrowException("Unexpected end of file while parsing mesh structure");
                else if (objectName == "}")
                    break; // mesh finished
                else if (objectName == "MeshNormals")
                    ParseDataObjectMeshNormals(ref mesh);
                else if (objectName == "MeshTextureCoords")
                    ParseDataObjectMeshTextureCoords(ref mesh);
                else if (objectName == "MeshVertexColors")
                    ParseDataObjectMeshVertexColors(ref mesh);
                else if (objectName == "MeshMaterialList")
                    ParseDataObjectMeshMaterialList(ref mesh);
                else if (objectName == "VertexDuplicationIndices")
                    ParseUnknownDataObject(); // we'll ignore vertex duplication indices
                else if (objectName == "XSkinMeshHeader")
                    ParseDataObjectSkinMeshHeader(ref mesh);
                else if (objectName == "SkinWeights")
                    ParseDataObjectSkinWeights(ref mesh);
                else
                {
                    Debug.WriteLine("Unknown data object in mesh in x file");
                    ParseUnknownDataObject();
                }
            }
        }

        protected void ParseDataObjectSkinWeights(ref Mesh mesh)
        {
            ReadHeadOfDataObject();

            string transformNodeName;
            GetNextTokenAsString(out transformNodeName);

            Bone bone = new Bone();
            mesh.Bones.Add(bone);
            bone.Name = transformNodeName;

            // read vertex weights
            uint numWeights = ReadInt();

            for (uint a = 0; a < numWeights; a++)
            {
                BoneWeight weight = new BoneWeight();
                weight.Vertex = ReadInt();
                bone.Weights.Add(weight);
            }

            // read vertex weights
            for (int a = 0; a < numWeights; a++)
            {
                BoneWeight bw = bone.Weights[a];
                bw.Weight = ReadFloat();
                bone.Weights[a] = bw;
            }

            // read matrix offset
            bone.OffsetMatrix = new Matrix4x4();
            bone.OffsetMatrix.M11 = ReadFloat(); bone.OffsetMatrix.M21 = ReadFloat();
            bone.OffsetMatrix.M31 = ReadFloat(); bone.OffsetMatrix.M41 = ReadFloat();
            bone.OffsetMatrix.M12 = ReadFloat(); bone.OffsetMatrix.M22 = ReadFloat();
            bone.OffsetMatrix.M33 = ReadFloat(); bone.OffsetMatrix.M42 = ReadFloat();
            bone.OffsetMatrix.M13 = ReadFloat(); bone.OffsetMatrix.M23 = ReadFloat();
            bone.OffsetMatrix.M33 = ReadFloat(); bone.OffsetMatrix.M43 = ReadFloat();
            bone.OffsetMatrix.M14 = ReadFloat(); bone.OffsetMatrix.M24 = ReadFloat();
            bone.OffsetMatrix.M33 = ReadFloat(); bone.OffsetMatrix.M44 = ReadFloat();

            CheckForSemicolon();
            CheckForClosingBrace();
        }

        protected void ParseDataObjectSkinMeshHeader(ref Mesh mesh)
        {
            ReadHeadOfDataObject();
            ReadInt();
            ReadInt();
            ReadInt();
            CheckForClosingBrace();
        }

        protected void ParseDataObjectMeshNormals(ref Mesh mesh)
        {
            ReadHeadOfDataObject();

            // read count
            uint numNormals = ReadInt();
            mesh.Normals = new List<Vector3>((int)numNormals);

            // read normal vectors
            for (uint a = 0; a < numNormals; a++)
                mesh.Normals.Add(ReadVector3());

            // read normal indices
            uint numFaces = ReadInt();
            if (numFaces != mesh.PosFaces.Count)
                ThrowException("Normal face count does not match vertex face count.");

            for (uint a = 0; a < numFaces; a++)
            {
                uint numIndices = ReadInt();
                Face face = new Face();
                face.Indices = new List<uint>();
                for (uint b = 0; b < numIndices; b++)
                    face.Indices.Add(ReadInt());
                mesh.NormalFaces.Add(face);

                TestForSeparator();
            }

            CheckForClosingBrace();
        }

        protected void ParseDataObjectMeshTextureCoords(ref Mesh mesh)
        {
            ReadHeadOfDataObject();
            if (mesh.NumTextures + 1 > AI_MAX_NUMBER_OF_TEXTURECOORDS)
            {
                ThrowException("Too many sets of texture coordinates");
            }

            uint numCoords = ReadInt();
            if (numCoords != mesh.Positions.Count)
            {
                ThrowException("Texture coord count does not match vertex count");
            }

            List<Vector2> coords = new List<Vector2>((int)numCoords);
            for (int a = 0; a < numCoords; a++)
            {
                coords.Add(ReadVector2());
            }
            mesh.TexCoords[mesh.NumTextures++] = coords;

            CheckForClosingBrace();
        }

        protected void ParseDataObjectMeshVertexColor(ref Mesh mesh)
        {
            ReadHeadOfDataObject();

            if (mesh.NumColorSets + 1 > AI_MAX_NUMBER_OF_COLOR_SETS)
                ThrowException("Too many colorsets");
            Color4[] colors;

            uint numColors = ReadInt();
            if (numColors != mesh.Positions.Count)
                ThrowException("Vertex color count does not match vertex count");

            //colors.resize( numColors, aiColor4D( 0, 0, 0, 1));
            colors = new Color4[numColors];

            for (uint a = 0; a < numColors; a++)
            {
                uint index = ReadInt();
                if (index >= mesh.Positions.Count)
                    ThrowException("Vertex color index out of bounds");

                colors[(int)index] = ReadRGBA();
                // HACK: (thom) Maxon Cinema XPort plugin puts a third separator here, kwxPort puts a comma.
                // Ignore gracefully.
                if (!isBinaryFormat)
                {
                    FindNextNoneWhiteSpace();
                    if (buffer[p] == ';' || buffer[p] == ',')
                        p++;
                }
            }
            mesh.Colors[(int)mesh.NumColorSets] = new List<Color4>(colors);
            CheckForClosingBrace();
        }

        protected void ParseDataObjectMeshMaterialList(ref Mesh mesh)
        {
            ReadHeadOfDataObject();

            // 读取材质的数量
            /*unsigned int numMaterials =*/
            ReadInt();

            //读取非三角面的材质索引数
            uint numMatIndices = ReadInt();

            //有些模型的材质数为1，为了能够读入它们，在每个 face 上复制这个单一的材料
            if (numMatIndices != mesh.PosFaces.Count && numMatIndices != 1)
                ThrowException("Per-Face material index count does not match face count.");
        }
    }
}
