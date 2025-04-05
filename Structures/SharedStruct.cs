using Cast.NET.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Mappie.Structures
{
    public struct MeshData
    {
        public MeshNode mesh;
        public MaterialNode material;
        public List<TextureSemanticData> textures;
    }

    public struct TextureSemanticData
    {
        public string semantic;
        public string texture;
    }

    public struct Face
    {
        public ushort a;
        public ushort b;
        public ushort c;
    }

    public struct XModelMeshData
    {
        public List<Vector3> positions;
        public List<Vector3> normals;
        public List<Vector2> uv;
        public List<Vector2> secondUv;
        public List<uint> colorVertex;
        public List<Face> faces;

        public MaterialNode material;
        public List<TextureSemanticData> textures;
    }
}
