using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Mappie.Structures
{
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public unsafe struct MW6GfxSurface : IGfxSurface
    {
        [FieldOffset(0)] public uint posOffset;
        [FieldOffset(4)] public uint baseIndex;
        [FieldOffset(8)] public uint tableIndex;
        [FieldOffset(12)] public uint ugbSurfDataIndex;
        [FieldOffset(16)] public uint materialIndex;
        [FieldOffset(20)] public ushort triCount;
        [FieldOffset(22)] public ushort packedIndicesTableCount;
        [FieldOffset(24)] public ushort vertexCount;
        [FieldOffset(26)] public ushort unk3; // is zero
        [FieldOffset(28)] public uint unk4;
        [FieldOffset(32)] public uint unk5; //FFFF
        [FieldOffset(36)] public uint packedIndicesOffset;

        uint IGfxSurface.baseIndex => baseIndex;
        uint IGfxSurface.tableIndex => tableIndex;
        uint IGfxSurface.ugbSurfDataIndex => ugbSurfDataIndex;
        uint IGfxSurface.materialIndex => materialIndex;
        ushort IGfxSurface.triCount => triCount;
        ushort IGfxSurface.packedIndicesTableCount => packedIndicesTableCount;
        ushort IGfxSurface.vertexCount => vertexCount;
        uint IGfxSurface.packedIndicesOffset => packedIndicesOffset;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public unsafe struct MW6GfxUgbSurfData : IGfxUgbSurfData<MW6GfxWorldDrawOffset>
    {
        [FieldOffset(0)] public MW6GfxWorldDrawOffset worldDrawOffset;
        [FieldOffset(16)] public uint transientZoneIndex;
        [FieldOffset(20)] public uint unk0; //0xFFFF
        [FieldOffset(24)] public uint unk1; //either 0 or 510
        [FieldOffset(28)] public uint layerCount;
        [FieldOffset(32)] public uint vertexCount;
        [FieldOffset(36)] public uint unk2; // no idea ? 
        [FieldOffset(40)] public uint xyzOffset;
        [FieldOffset(44)] public uint tangentFrameOffset;
        [FieldOffset(48)] public uint lmapOffset;
        [FieldOffset(52)] public uint colorOffset;
        [FieldOffset(56)] public uint texCoordOffset;
        [FieldOffset(60)] public uint unk3;
        [FieldOffset(64)] public uint accumulatedUnk2; //? this is sum of previous unk2
        [FieldOffset(68)] public fixed uint normalTransformOffset[7];
        [FieldOffset(96)] public fixed uint displacementOffset[8];

        MW6GfxWorldDrawOffset IGfxUgbSurfData<MW6GfxWorldDrawOffset>.worldDrawOffset => worldDrawOffset;
        uint IGfxUgbSurfData<MW6GfxWorldDrawOffset>.transientZoneIndex => transientZoneIndex;
        uint IGfxUgbSurfData<MW6GfxWorldDrawOffset>.layerCount => layerCount;
        uint IGfxUgbSurfData<MW6GfxWorldDrawOffset>.xyzOffset => xyzOffset;
        uint IGfxUgbSurfData<MW6GfxWorldDrawOffset>.tangentFrameOffset => tangentFrameOffset;
        uint IGfxUgbSurfData<MW6GfxWorldDrawOffset>.colorOffset => colorOffset;
        uint IGfxUgbSurfData<MW6GfxWorldDrawOffset>.texCoordOffset => texCoordOffset;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public unsafe struct MW6GfxWorldDrawOffset : IGfxWorldDrawOffset
    {
        [FieldOffset(0)] public float x;
        [FieldOffset(4)] public float y;
        [FieldOffset(8)] public float z;
        [FieldOffset(12)] public float scale;
        float IGfxWorldDrawOffset.x => x;
        float IGfxWorldDrawOffset.y => y;
        float IGfxWorldDrawOffset.z => z;
        float IGfxWorldDrawOffset.scale => scale;
    }

    [StructLayout(LayoutKind.Explicit, Size = 392)]
    public unsafe struct MW6GfxWorldSurfaces : IGfxWorldSurfaces
    {
        [FieldOffset(0)] public uint count;
        [FieldOffset(8)] public uint ugbSurfDataCount;
        [FieldOffset(12)] public uint materialCount;
        [FieldOffset(112)] public nint surfaces;
        [FieldOffset(152)] public nint materials;
        [FieldOffset(168)] public nint ugbSurfData;
        [FieldOffset(176)] public nint worldDrawOffsets;
        [FieldOffset(296)] public uint btndSurfacesCount;
        [FieldOffset(304)] public nint btndSurfaces;

        uint IGfxWorldSurfaces.count => count;
        nint IGfxWorldSurfaces.surfaces => surfaces;
        nint IGfxWorldSurfaces.materials => materials;
        nint IGfxWorldSurfaces.ugbSurfData => ugbSurfData;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public unsafe struct MW6GfxWorldDrawVerts : IGfxWorldDrawVerts
    {
        [FieldOffset(0)] public uint posSize;
        [FieldOffset(4)] public uint indexCount;
        [FieldOffset(8)] public uint tableCount;
        [FieldOffset(12)] public uint packedIndicesSize;
        [FieldOffset(16)] public nint posData;
        [FieldOffset(24)] public nint indices;
        [FieldOffset(32)] public nint tableData;
        [FieldOffset(40)] public nint packedIndices;

        uint IGfxWorldDrawVerts.posSize => posSize;
        nint IGfxWorldDrawVerts.posData => posData;
        nint IGfxWorldDrawVerts.indices => indices;
        nint IGfxWorldDrawVerts.tableData => tableData;
        nint IGfxWorldDrawVerts.packedIndices => packedIndices;
    }

    [StructLayout(LayoutKind.Explicit, Size = 376)]
    public unsafe struct MW6GfxWorldTransientZone : IGfxWorldTransientZone<MW6GfxWorldDrawVerts>
    {
        [FieldOffset(0)] public ulong hash;
        [FieldOffset(20)] public uint transientZoneIndex;
        [FieldOffset(24)] public MW6GfxWorldDrawVerts drawVerts;

        ulong IGfxWorldTransientZone<MW6GfxWorldDrawVerts>.hash => hash;
        MW6GfxWorldDrawVerts IGfxWorldTransientZone<MW6GfxWorldDrawVerts>.drawVerts => drawVerts;
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct MW6GfxWorld : IGfxWorld<MW6GfxWorldSurfaces, MW6GfxWorldStaticModels>
    {
        [FieldOffset(0)] public ulong hash;
        [FieldOffset(8)] public nint baseName;
        [FieldOffset(192)] public MW6GfxWorldSurfaces surfaces;
        [FieldOffset(560)] public MW6GfxWorldStaticModels smodels;
        [FieldOffset(5660)] public uint transientZoneCount;
        [FieldOffset(5664)] public fixed ulong transientZones[1536];

        nint IGfxWorld<MW6GfxWorldSurfaces, MW6GfxWorldStaticModels>.baseName => baseName;
        uint IGfxWorld<MW6GfxWorldSurfaces, MW6GfxWorldStaticModels>.transientZoneCount => transientZoneCount;

        ulong[] IGfxWorld<MW6GfxWorldSurfaces, MW6GfxWorldStaticModels>.transientZones
        {
            get
            {
                ulong[] zones = new ulong[1536];
                for (int i = 0; i < 1536; i++)
                {
                    zones[i] = transientZones[i];
                }

                return zones;
            }
        }

        MW6GfxWorldSurfaces IGfxWorld<MW6GfxWorldSurfaces, MW6GfxWorldStaticModels>.surfaces => surfaces;
        MW6GfxWorldStaticModels IGfxWorld<MW6GfxWorldSurfaces, MW6GfxWorldStaticModels>.smodels => smodels;
    }

    [StructLayout(LayoutKind.Explicit, Size = 120)]
    public unsafe struct MW6Material : IMaterial
    {
        [FieldOffset(0)] public ulong hash;
        [FieldOffset(24)] public byte textureCount;
        [FieldOffset(26)] public byte layerCount;
        [FieldOffset(27)] public byte imageCount;
        [FieldOffset(40)] public nint textureTable;
        [FieldOffset(48)] public nint imageTable;

        ulong IMaterial.hash => hash;
        byte IMaterial.textureCount => textureCount;
        byte IMaterial.imageCount => imageCount;
        nint IMaterial.textureTable => textureTable;
        nint IMaterial.imageTable => imageTable;
    }

    [StructLayout(LayoutKind.Explicit, Size = 2)]
    public unsafe struct MW6MaterialTextureDef
    {
        [FieldOffset(0)] public byte index;
        [FieldOffset(1)] public byte imageIdx;
    }

    [StructLayout(LayoutKind.Explicit, Size = 384)]
    public unsafe struct MW6GfxWorldStaticModels : IGfxWorldStaticModels
    {
        [FieldOffset(4)] public uint smodelCount;
        [FieldOffset(8)] public uint collectionsCount;
        [FieldOffset(12)] public uint instanceCount;
        [FieldOffset(80)] public nint surfaces;
        [FieldOffset(88)] public nint smodels;
        [FieldOffset(104)] public nint collections;
        [FieldOffset(168)] public nint instanceData;

        uint IGfxWorldStaticModels.collectionsCount => collectionsCount;
        nint IGfxWorldStaticModels.collections => collections;
        nint IGfxWorldStaticModels.smodels => smodels;
        nint IGfxWorldStaticModels.instanceData => instanceData;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public unsafe struct MW6GfxStaticModel : IGfxStaticModel
    {
        public nint xmodel;
        public byte flags;
        public byte firstMtlSkinIndex;
        public ushort firstStaticModelSurfaceIndex;

        nint IGfxStaticModel.xmodel => xmodel;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public unsafe struct MW6GfxStaticModelCollection : IGfxStaticModelCollection
    {
        public uint firstInstance;
        public uint instanceCount;
        public ushort smodelIndex;
        public ushort transientGfxWorldPlaced;
        public ushort clutterIndex;
        public byte flags;
        public byte pad;

        uint IGfxStaticModelCollection.firstInstance => firstInstance;
        uint IGfxStaticModelCollection.instanceCount => instanceCount;
        ushort IGfxStaticModelCollection.smodelIndex => smodelIndex;
        ushort IGfxStaticModelCollection.transientGfxWorldPlaced => transientGfxWorldPlaced;
    };

    [StructLayout(LayoutKind.Sequential, Size = 24)]
    public unsafe struct MW6GfxSModelInstanceData : IGfxSModelInstanceData
    {
        public fixed int translation[3];
        public fixed ushort orientation[4];
        public ushort halfFloatScale;

        int[] IGfxSModelInstanceData.translation
        {
            get
            {
                int[] tmp = new int[3];
                for (int i = 0; i < 3; i++)
                {
                    tmp[i] = translation[i];
                }

                return tmp;
            }
        }

        ushort[] IGfxSModelInstanceData.orientation
        {
            get
            {
                ushort[] tmp = new ushort[4];
                for (int i = 0; i < 4; i++)
                {
                    tmp[i] = orientation[i];
                }

                return tmp;
            }
        }

        ushort IGfxSModelInstanceData.halfFloatScale => halfFloatScale;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public unsafe struct MW6GfxImage : IGfxImage
    {
        [FieldOffset(0)] public ulong hash;
        ulong IGfxImage.hash => hash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 232)]
    public unsafe struct MW6XModel : IXModel
    {
        [FieldOffset(0)] public ulong hash;
        [FieldOffset(8)] public nint name;
        [FieldOffset(16)] public ushort numSurfs;
        [FieldOffset(40)] public float scale;
        [FieldOffset(144)] public nint materialHandles;
        [FieldOffset(152)] public nint lodInfo;

        ulong IXModel.hash => hash;
        nint IXModel.name => name;
        nint IXModel.materialHandles => materialHandles;
        nint IXModel.lodInfo => lodInfo;
    }

    [StructLayout(LayoutKind.Explicit, Size = 72)]
    public struct MW6XModelLod : IXModelLod
    {
        [FieldOffset(0)] public nint MeshPtr;
        [FieldOffset(8)] public nint SurfsPtr;
        [FieldOffset(16)] public float distance;
        [FieldOffset(28)] public ushort numsurfs;
        [FieldOffset(30)] public ushort surfIndex;

        nint IXModelLod.MeshPtr => MeshPtr;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80, Pack = 1)]
    public unsafe struct MW6XModelSurfs : IXModelSurfs
    {
        [FieldOffset(0)] public ulong hash;
        [FieldOffset(8)] public nint surfs;
        [FieldOffset(16)] public ulong xpakKey;
        [FieldOffset(32)] public nint shared;
        [FieldOffset(40)] public ushort numsurfs;

        nint IXModelSurfs.surfs => surfs;
        ulong IXModelSurfs.xpakKey => xpakKey;
        nint IXModelSurfs.shared => shared;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public unsafe struct MW6XSurfaceShared : IXSurfaceShared
    {
        [FieldOffset(0)] public nint data;
        [FieldOffset(8)] public uint dataSize;
        [FieldOffset(12)] public int flags;

        nint IXSurfaceShared.data => data;
        uint IXSurfaceShared.dataSize => dataSize;
        ulong IXSurfaceShared.xpakKey => 0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 224)]
    public unsafe struct MW6XSurface : IXSurface
    {
        [FieldOffset(4)] public ushort packedIndicesTableCount;
        [FieldOffset(24)] public uint vertCount;
        [FieldOffset(28)] public uint triCount;
        [FieldOffset(60)] public float overrideScale;
        [FieldOffset(64)] public fixed uint Offsets[14];
        [FieldOffset(64)] public uint xyzOffset;
        [FieldOffset(68)] public uint texCoordOffset;
        [FieldOffset(72)] public uint tangentFrameOffset;
        [FieldOffset(76)] public uint indexDataOffset;
        [FieldOffset(80)] public uint packedIndiciesTableOffset;
        [FieldOffset(84)] public uint packedIndicesOffset;
        [FieldOffset(92)] public uint colorOffset;
        [FieldOffset(96)] public uint secondUVOffset;
        [FieldOffset(120)] public nint shared;
        [FieldOffset(176)] public Vector3 offsets;
        [FieldOffset(188)] public float scale;
        [FieldOffset(192)] public float min;
        [FieldOffset(196)] public float max;

        uint IXSurface.packedIndicesTableCount => packedIndicesTableCount;
        uint IXSurface.vertCount => vertCount;
        uint IXSurface.triCount => triCount;
        uint IXSurface.xyzOffset => xyzOffset;
        uint IXSurface.texCoordOffset => texCoordOffset;
        uint IXSurface.tangentFrameOffset => tangentFrameOffset;
        uint IXSurface.indexDataOffset => indexDataOffset;
        uint IXSurface.packedIndiciesTableOffset => packedIndiciesTableOffset;
        uint IXSurface.packedIndicesOffset => packedIndicesOffset;
        uint IXSurface.colorOffset => colorOffset;
        uint IXSurface.secondUVOffset => secondUVOffset;
        uint IXSurface.secondColorOffset => 0;

    }

    public enum MW6TextureIdxTable
    {
        DIFFUSE_MAP = 0,
        DIFFUSE_MAP_UV_1 = 1,
        DIFFUSE_MAP_UV_2 = 2,
        DIFFUSE_MAP_UV_3 = 3,
        NOG_MAP = 4,
        NOG_MAP_UV_1 = 5,
        NOG_MAP_UV_2 = 6,
        NOG_MAP_UV_3 = 7,
        SKYBOX = 74,
    }
}