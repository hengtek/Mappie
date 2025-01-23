using Cast.NET.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotnesktRemastered.Structures
{
    public struct SurfaceData
    {
        public MeshNode mesh;
        public MaterialNode material;
        public List<TextureSemanticData> textures;
    }

    public struct TextureSemanticData
    {
        public string semantic;
        public byte uvLayer;
        public string texture;
    }
}
