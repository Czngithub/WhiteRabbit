using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using VectorKey = System.Collections.Generic.KeyValuePair<double, SharpDX.Vector3>;
using QuatKey = System.Collections.Generic.KeyValuePair<double, SharpDX.Quaternion>;
using MatrixKey = System.Collections.Generic.KeyValuePair<double, SharpDX.Matrix>;

/// <summary>
/// X文件的辅助结构
/// </summary>
namespace WhiteRabbit.Framework
{
    //X文件的网格（Mesh）
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Face
    {
        public List<uint> Indices;
    }

    //X文件的纹理（Texture）
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct TexEntry
    {
        public string Name;

        public bool IsNormalMap;

        public TexEntry(string name, bool isNormalMap = false)
        {
            Name = name;
            IsNormalMap = isNormalMap;

        }
    }

    //X文件的材质（Material）
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct MatEntry
    {
        public string Name;

        public bool IsReference;

        public Color4 Diffuse;

        public Color3 Specular;

        public float SpecularExponent;

        public Color3 Emissive;

        public List<TexEntry> Textures;

        public int SceneIndex;
    }

    //X文件的骨架重量
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct BoneWeight
    {
        public uint Vertex;

        public float Weight;
    }

    public class Bone
    {
        public string Name;

        public List<BoneWeight> Weights;

        public Matrix OffsetMatrix;

        public Bone()
        {
            Weights = new List<BoneWeight>();
        }
    }

    public class Mesh
    {
        public String Name;

        public List<Vector3> Positions;

        public List<Face> PosFaces;

        public List<Vector3> Normals;

        public List<Face> NormalFaces;

        public uint NumTextures;

        public List<Vector2>[] TexCoords;

        public uint NumColorSets;

        public List<Color4>[] Colors;

        public List<uint> FaceMaterials;

        public List<MatEntry> Materials;

        public List<Bone> Bones;

        public Mesh(string name = "")
        {
            uint AI_MAX_NUMBER_OF_TEXTURECOORDS = 4;
            uint AI_MAX_NUMBER_OF_COLOR_SETS = 4;
            Name = name;
            Positions = new List<Vector3>();
            PosFaces = new List<Face>();
            Normals = new List<Vector3>();
            NormalFaces = new List<Face>();
            TexCoords = new List<Vector2>[AI_MAX_NUMBER_OF_TEXTURECOORDS];
            Colors = new List<Color4>[AI_MAX_NUMBER_OF_COLOR_SETS];
            FaceMaterials = new List<uint>();
            Materials = new List<MatEntry>();
            Bones = new List<Bone>();
            NumTextures = 0;
            NumColorSets = 0;
        }

        //X文件框架
        public class Node
        {
            public string Name;

            public Matrix TrafoMatrix;

            public Node Parent;

            public List<Node> Children = new List<Node>();

            public List<Mesh> Meshes = new List<Mesh>();

            public Node(Node parent = null)
            {
                this.Parent = parent;
            }
        }

        public class AnimBone
        {
            public string BoneName;

            public List<VectorKey> PosKeys = new List<VectorKey>();

            public List<QuatKey> RotKeys = new List<QuatKey>();

            public List<VectorKey> ScaleKeys = new List<VectorKey>();

            public List<MatrixKey> TrafoKeys = new List<MatrixKey>();
        }

        public class Animation
        {
            public string Name;

            public List<AnimBone> Anims = new List<AnimBone>();
        }

        public class Scene
        {
            public Node RootNode;

            public List<Mesh> GlobalMeshes = new List<Mesh>();

            public List<MatEntry> GlobalMaterial = new List<MatEntry>();

            public List<Animation> Anims = new List<Animation>();

            public uint AnimTicksPerSecond;
        }
    }
}
