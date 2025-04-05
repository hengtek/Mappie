using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Mappie.Structures
{
    [StructLayout(LayoutKind.Explicit, Size = 36)]
    public unsafe struct BO6GfxSurface : IGfxSurface
    {
        // [FieldOffset(0)]
        // public uint posOffset;
        [FieldOffset(4)] public uint baseIndex;
        [FieldOffset(8)] public uint tableIndex;
        [FieldOffset(12)] public uint ugbSurfDataIndex;
        [FieldOffset(16)] public uint materialIndex;
        [FieldOffset(20)] public ushort triCount;
        [FieldOffset(22)] public ushort packedIndicesTableCount;

        [FieldOffset(24)] public ushort vertexCount;

        // [FieldOffset(26)]
        // public ushort unk3; // is zero
        // [FieldOffset(28)]
        // public uint unk7; // ushort(?) + ushort(FFFF)
        [FieldOffset(32)] public uint packedIndicesOffset;

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
    public unsafe struct BO6GfxUgbSurfData : IGfxUgbSurfData<BO6GfxWorldDrawOffset>
    {
        [FieldOffset(0)] public BO6GfxWorldDrawOffset worldDrawOffset;

        [FieldOffset(16)] public uint transientZoneIndex;

        // [FieldOffset(20)]
        // public uint unk0; // 0xFFFF
        // [FieldOffset(24)]
        // public uint unk1;
        [FieldOffset(28)] public uint layerCount;

        [FieldOffset(32)] public uint vertexCount; // btndSurf area has this value

        // [FieldOffset(36)]
        // public uint unk2; // no idea ? 
        [FieldOffset(40)] public uint xyzOffset;

        [FieldOffset(44)] public uint tangentFrameOffset;

        // [FieldOffset(48)]
        // public uint lmapOffset;
        [FieldOffset(52)] public uint colorOffset;
        [FieldOffset(56)] public uint texCoordOffset;

        [FieldOffset(60)] public uint
            unkDataOffset; // Only btndSurf area has this value? (bufPtr = zone.drawVerts.posData + (nint)ugbSurfData.unkDataOffset; bufSize = 8 * vertexCount;)
        // [FieldOffset(64)]
        // public uint accumulatedUnk2; //? this is sum of previous unk2
        // [FieldOffset(68)]
        // public fixed uint normalTransformOffset[7];
        // [FieldOffset(96)]
        // public fixed uint displacementOffset[8];

        BO6GfxWorldDrawOffset IGfxUgbSurfData<BO6GfxWorldDrawOffset>.worldDrawOffset => worldDrawOffset;
        uint IGfxUgbSurfData<BO6GfxWorldDrawOffset>.transientZoneIndex => transientZoneIndex;
        uint IGfxUgbSurfData<BO6GfxWorldDrawOffset>.layerCount => layerCount;
        uint IGfxUgbSurfData<BO6GfxWorldDrawOffset>.xyzOffset => xyzOffset;
        uint IGfxUgbSurfData<BO6GfxWorldDrawOffset>.tangentFrameOffset => tangentFrameOffset;
        uint IGfxUgbSurfData<BO6GfxWorldDrawOffset>.colorOffset => colorOffset;
        uint IGfxUgbSurfData<BO6GfxWorldDrawOffset>.texCoordOffset => texCoordOffset;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public unsafe struct BO6GfxWorldDrawOffset : IGfxWorldDrawOffset
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

    // Note: We can get more by "GfxWorldSurfaces::surfaces"
    [StructLayout(LayoutKind.Explicit, Size = 392)]
    public unsafe struct BO6GfxWorldSurfaces : IGfxWorldSurfaces
    {
        [FieldOffset(0)] public uint count;
        [FieldOffset(8)] public uint ugbSurfDataCount;
        [FieldOffset(12)] public uint materialCount;
        [FieldOffset(16)] public uint materialFlagsCount;
        [FieldOffset(120)] public nint surfaces;
        [FieldOffset(136)] public nint surfaceBounds; // bufSize = 24 * count;
        [FieldOffset(160)] public nint materials;
        [FieldOffset(176)] public nint surfaceMaterialFlags; // bufSize = 1 * materialFlagsCount;
        [FieldOffset(192)] public nint ugbSurfData; // bufSize = ugbSurfDataCount << 7;
        [FieldOffset(200)] public nint worldDrawOffsets; // bufSize = 16 * ugbSurfDataCount;
        [FieldOffset(320)] public uint btndSurfacesCount;
        [FieldOffset(328)] public nint btndSurfaces; // bufSize = 36 * btndSurfacesCount;
        [FieldOffset(352)] public nint unkPtr0; // bufSize = 4 * btndSurfacesCount;

        uint IGfxWorldSurfaces.count => count;
        nint IGfxWorldSurfaces.surfaces => surfaces;
        nint IGfxWorldSurfaces.materials => materials;
        nint IGfxWorldSurfaces.ugbSurfData => ugbSurfData;
    }

    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public unsafe struct BO6GfxWorldDrawVerts : IGfxWorldDrawVerts
    {
        [FieldOffset(0)] public uint posDataSize;
        [FieldOffset(4)] public uint indexCount;
        [FieldOffset(8)] public uint tableCount;
        [FieldOffset(16)] public uint packedIndicesSize;
        [FieldOffset(24)] public nint posData; // bufSize = (posDataSize + 3) & 0xFFFFFFFC;
        [FieldOffset(32)] public nint indices; // bufSize = 2 * ((indexCount + 1) & 0xFFFFFFFE);
        [FieldOffset(40)] public nint tableData; // bufSize = 28 * tableCount;
        [FieldOffset(48)] public nint packedIndices;
        
        uint IGfxWorldDrawVerts.posSize => posDataSize;
        nint IGfxWorldDrawVerts.posData => posData;
        nint IGfxWorldDrawVerts.indices => indices;
        nint IGfxWorldDrawVerts.tableData => tableData;
        nint IGfxWorldDrawVerts.packedIndices => packedIndices;
    }

    [StructLayout(LayoutKind.Explicit, Size = 584)]
    public unsafe struct BO6GfxWorldTransientZone : IGfxWorldTransientZone<BO6GfxWorldDrawVerts>
    {
        [FieldOffset(0)] public ulong hash;
        [FieldOffset(8)] public nint unkPtr0;
        [FieldOffset(24)] public BO6GfxWorldDrawVerts drawVerts;

        ulong IGfxWorldTransientZone<BO6GfxWorldDrawVerts>.hash => hash;
        BO6GfxWorldDrawVerts IGfxWorldTransientZone<BO6GfxWorldDrawVerts>.drawVerts => drawVerts;
    }

    [StructLayout(LayoutKind.Explicit, Size = 2)]
    public unsafe struct BO6MaterialTextureDef 
    {
        [FieldOffset(0)] public byte index;
        [FieldOffset(1)] public byte imageIdx;
    }
    
    // Note: We can get more by "smodelSurfData"
    // TODO: "splinedModelInstanceData", "splinedDecalInstanceData"
    [StructLayout(LayoutKind.Explicit)] // 20720
    public unsafe struct BO6GfxWorldStaticModels : IGfxWorldStaticModels
    {
        [FieldOffset(4)] public uint modelCount;
        [FieldOffset(8)] public uint collectionsCount;

        [FieldOffset(12)] public uint instanceCount;

        // [FieldOffset(80)]
        // public nint surfaces;
        [FieldOffset(104)] public nint smodels;
        [FieldOffset(120)] public nint collections;
        [FieldOffset(200)] public nint instanceData;
        uint IGfxWorldStaticModels.collectionsCount => collectionsCount;
        nint IGfxWorldStaticModels.collections => collections;
        nint IGfxWorldStaticModels.smodels => smodels;
        nint IGfxWorldStaticModels.instanceData => instanceData;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public unsafe struct BO6GfxStaticModel:IGfxStaticModel
    {
        public nint xmodel;
        public byte flags;
        public byte firstMtlSkinIndex;
        public ushort firstStaticModelSurfaceIndex;
        
        nint IGfxStaticModel.xmodel => xmodel;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public unsafe struct BO6GfxStaticModelCollection : IGfxStaticModelCollection
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
    public unsafe struct BO6GfxSModelInstanceData:IGfxSModelInstanceData
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

    [StructLayout(LayoutKind.Explicit)] // 40496
    public unsafe struct BO6GfxWorld : IGfxWorld<BO6GfxWorldSurfaces, BO6GfxWorldStaticModels>
    {
        [FieldOffset(0)] public ulong hash;
        [FieldOffset(8)] public nint baseName;
        [FieldOffset(216)] public BO6GfxWorldSurfaces surfaces;
        [FieldOffset(608)] public BO6GfxWorldStaticModels smodels;
        [FieldOffset(27548)] public uint transientZoneCount;
        [FieldOffset(27552)] public fixed ulong transientZones[0x600];

        nint IGfxWorld<BO6GfxWorldSurfaces, BO6GfxWorldStaticModels>.baseName => baseName;
        uint IGfxWorld<BO6GfxWorldSurfaces, BO6GfxWorldStaticModels>.transientZoneCount => transientZoneCount;

        ulong[] IGfxWorld<BO6GfxWorldSurfaces, BO6GfxWorldStaticModels>.transientZones
        {
            get
            {
                ulong[] zones = new ulong[0x600];
                for (int i = 0; i < 0x600; i++)
                {
                    zones[i] = transientZones[i];
                }

                return zones;
            }
        }

        BO6GfxWorldSurfaces IGfxWorld<BO6GfxWorldSurfaces, BO6GfxWorldStaticModels>.surfaces => surfaces;
        BO6GfxWorldStaticModels IGfxWorld<BO6GfxWorldSurfaces, BO6GfxWorldStaticModels>.smodels => smodels;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public unsafe struct BO6Material : IMaterial // TODO:
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

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public unsafe struct BO6GfxImage : IGfxImage
    {
        [FieldOffset(0)] public ulong hash;
        ulong IGfxImage.hash => hash;
    }
    
    [StructLayout(LayoutKind.Explicit, Size = 248)]
    public unsafe struct BO6XModel:IXModel
    {
        [FieldOffset(0)] public ulong hash;
        [FieldOffset(8)] public nint name;
        [FieldOffset(16)] public ushort numSurfs;
        [FieldOffset(18)] public byte numLods;
        [FieldOffset(104)] public nint xmodelPackedDataPtr;
        [FieldOffset(144)] public nint materialHandles;
        [FieldOffset(152)] public nint lodInfo;
        
        ulong IXModel.hash => hash;
        nint IXModel.name => name;
        nint IXModel.materialHandles => materialHandles;
        nint IXModel.lodInfo => lodInfo;
    }

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    public unsafe struct BO6XModelPackedData
    {
        [FieldOffset(0)] public byte NumBones;
        [FieldOffset(1)] public ushort NumRootBones;
        [FieldOffset(6)] public ushort UnkBoneCount;
        [FieldOffset(80)] public nint ParentListPtr;
        [FieldOffset(88)] public nint RotationsPtr;
        [FieldOffset(96)] public nint TranslationsPtr;
        [FieldOffset(104)] public nint PartClassificationPtr;
        [FieldOffset(112)] public nint BaseMatriciesPtr;
        [FieldOffset(120)] public nint BoneIDsPtr;
    };

    [StructLayout(LayoutKind.Explicit, Size = 72)]
    public unsafe struct BO6XModelLod:IXModelLod
    {
        [FieldOffset(0)] public nint MeshPtr;
        [FieldOffset(8)] public nint surfs;
        [FieldOffset(16)] public fixed float distance[3];
        [FieldOffset(28)] public ushort numSurfs;
        [FieldOffset(30)] public ushort surfIndex;
        
        nint IXModelLod.MeshPtr => MeshPtr;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64, Pack = 1)]
    public unsafe struct BO6XModelSurfs:IXModelSurfs
    {
        [FieldOffset(0)] public ulong hash;
        [FieldOffset(8)] public nint surfs;
        [FieldOffset(16)] public nint shared;
        [FieldOffset(24)] public ushort numSurfs;
        
        nint IXModelSurfs.surfs => surfs;
        ulong IXModelSurfs.xpakKey => 0;
        nint IXModelSurfs.shared => shared;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public unsafe struct BO6XSurfaceShared:IXSurfaceShared
    {
        [FieldOffset(0)] public nint data;
        [FieldOffset(8)] public ulong xpakKey;
        [FieldOffset(16)] public uint dataSize;
        [FieldOffset(20)] public int flags;
        
        nint IXSurfaceShared.data => data;
        uint IXSurfaceShared.dataSize => dataSize;
        ulong IXSurfaceShared.xpakKey => xpakKey;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public unsafe struct BO6Bounds
    {
        [FieldOffset(0)] public Vector3 midPoint;
        [FieldOffset(12)] public Vector3 halfSize;
    };

    // Note: We can get (XSurfaceSubdivInfo* subdiv;) by "facePoints"
    [StructLayout(LayoutKind.Explicit, Size = 224)]
    public unsafe struct BO6XSurface : IXSurface
    {
        [FieldOffset(20)] public uint vertCount;
        [FieldOffset(24)] public uint triCount;
        [FieldOffset(28)] public uint packedIndicesTableCount;
        [FieldOffset(60)] public float overrideScale;
        [FieldOffset(64)] public fixed uint offsets[14];
        [FieldOffset(64)] public uint sharedVertDataOffset;
        [FieldOffset(68)] public uint sharedUVDataOffset;
        [FieldOffset(72)] public uint sharedTangentFrameDataOffset;
        [FieldOffset(76)] public uint sharedIndexDataOffset;
        [FieldOffset(80)] public uint sharedPackedIndicesTableOffset;
        [FieldOffset(84)] public uint sharedPackedIndicesOffset;
        [FieldOffset(88)] public uint sharedColorDataOffset;
        // [FieldOffset(92)] public uint sharedSecondUVDataOffset;
        [FieldOffset(120)] public nint shared;
        [FieldOffset(176)] public BO6Bounds surfBounds;
        
        uint IXSurface.packedIndicesTableCount => packedIndicesTableCount;
        uint IXSurface.vertCount => vertCount;
        uint IXSurface.triCount => triCount;
        uint IXSurface.xyzOffset => 0;
        uint IXSurface.texCoordOffset => 0;
        uint IXSurface.tangentFrameOffset => 0;
        uint IXSurface.indexDataOffset => 0;
        uint IXSurface.packedIndiciesTableOffset => 0;
        uint IXSurface.packedIndicesOffset => 0;
        uint IXSurface.colorOffset => 0;
        uint IXSurface.secondUVOffset => 0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public unsafe struct BO6TransientInfoUnk
    {
        [FieldOffset(0)] public nint name;
        [FieldOffset(8)] public ulong hash;
        [FieldOffset(16)] public fixed ushort unkFlags[4]; // index? type? flag?
    }

    [StructLayout(LayoutKind.Explicit, Size = 200)]
    public unsafe struct BO6TransientInfo
    {
        [FieldOffset(112)] public nint unkPtr;
        [FieldOffset(176)] public uint unkCount; // * 24
    }

    [StructLayout(LayoutKind.Explicit, Size = 152)]
    public unsafe struct BO6StreamingInfo
    {
        [FieldOffset(0)] public ulong hash;
        [FieldOffset(128)] public nint transientInfoPtr;
    }

    // Note:
    // StTerrain: "samplePoints"
}