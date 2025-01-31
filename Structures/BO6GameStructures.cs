using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DotnesktRemastered.Structures
{
    [StructLayout(LayoutKind.Explicit, Size = 36)]
    public unsafe struct BO6GfxSurface
    {
        // [FieldOffset(0)]
        // public uint posOffset;
        [FieldOffset(4)]
        public uint baseIndex;
        [FieldOffset(8)]
        public uint tableIndex;
        [FieldOffset(12)]
        public uint ugbSurfDataIndex;
        [FieldOffset(16)]
        public uint materialIndex;
        [FieldOffset(20)]
        public ushort triCount;
        [FieldOffset(22)]
        public ushort packedIndicesTableCount;
        [FieldOffset(24)]
        public ushort vertexCount;
        // [FieldOffset(26)]
        // public ushort unk3; // is zero
        // [FieldOffset(28)]
        // public uint unk7; // ushort(?) + ushort(FFFF)
        [FieldOffset(32)]
        public uint packedIndicesOffset;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public unsafe struct BO6GfxUgbSurfData
    {
        [FieldOffset(0)]
        public BO6GfxWorldDrawOffset worldDrawOffset;
        [FieldOffset(16)]
        public uint transientZoneIndex;
        // [FieldOffset(20)]
        // public uint unk0; // 0xFFFF
        // [FieldOffset(24)]
        // public uint unk1;
        [FieldOffset(28)]
        public uint layerCount;
        [FieldOffset(32)]
        public uint vertexCount; // btndSurf area has this value
        // [FieldOffset(36)]
        // public uint unk2; // no idea ? 
        [FieldOffset(40)]
        public uint xyzOffset;
        [FieldOffset(44)]
        public uint tangentFrameOffset;
        // [FieldOffset(48)]
        // public uint lmapOffset;
        [FieldOffset(52)]
        public uint colorOffset;
        [FieldOffset(56)]
        public uint texCoordOffset;
        [FieldOffset(60)]
        public uint unkDataOffset; // Only btndSurf area has this value? (bufPtr = zone.drawVerts.posData + (nint)ugbSurfData.unkDataOffset; bufSize = 8 * vertexCount;)
        // [FieldOffset(64)]
        // public uint accumulatedUnk2; //? this is sum of previous unk2
        // [FieldOffset(68)]
        // public fixed uint normalTransformOffset[7];
        // [FieldOffset(96)]
        // public fixed uint displacementOffset[8];
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public unsafe struct BO6GfxWorldDrawOffset
    {
        [FieldOffset(0)]
        public float x;
        [FieldOffset(4)]
        public float y;
        [FieldOffset(8)]
        public float z;
        [FieldOffset(12)]
        public float scale;
    }

    // Note: We can get more by "GfxWorldSurfaces::surfaces"
    [StructLayout(LayoutKind.Explicit, Size = 392)]
    public unsafe struct BO6GfxWorldSurfaces
    {
        [FieldOffset(0)]
        public uint count;
        [FieldOffset(8)]
        public uint ugbSurfDataCount;
        [FieldOffset(12)]
        public uint materialCount;
        [FieldOffset(16)]
        public uint materialFlagsCount;
        [FieldOffset(120)]
        public nint surfaces;
        [FieldOffset(136)]
        public nint surfaceBounds; // bufSize = 24 * count;
        [FieldOffset(160)]
        public nint materials;
        [FieldOffset(176)]
        public nint surfaceMaterialFlags; // bufSize = 1 * materialFlagsCount;
        [FieldOffset(192)]
        public nint ugbSurfData; // bufSize = ugbSurfDataCount << 7;
        [FieldOffset(200)]
        public nint worldDrawOffsets; // bufSize = 16 * ugbSurfDataCount;
        [FieldOffset(320)]
        public uint btndSurfacesCount;
        [FieldOffset(328)]
        public nint btndSurfaces; // bufSize = 36 * btndSurfacesCount;
        [FieldOffset(352)]
        public nint unkPtr0; // bufSize = 4 * btndSurfacesCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public unsafe struct BO6GfxWorldDrawVerts
    {
        [FieldOffset(0)]
        public uint posDataSize;
        [FieldOffset(4)]
        public uint indexCount;
        [FieldOffset(8)]
        public uint tableCount;
        [FieldOffset(16)]
        public uint packedIndicesSize;
        [FieldOffset(24)]
        public nint posData; // bufSize = (posDataSize + 3) & 0xFFFFFFFC;
        [FieldOffset(32)]
        public nint indices; // bufSize = 2 * ((indexCount + 1) & 0xFFFFFFFE);
        [FieldOffset(40)]
        public nint tableData; // bufSize = 28 * tableCount;
        [FieldOffset(48)]
        public nint packedIndices;
    }

    [StructLayout(LayoutKind.Explicit, Size = 584)]
    public unsafe struct BO6GfxWorldTransientZone
    {
        [FieldOffset(0)]
        public ulong hash;
        [FieldOffset(8)]
        public nint unkPtr0;
        [FieldOffset(24)]
        public BO6GfxWorldDrawVerts drawVerts;
    }

    // Note: We can get more by "smodelSurfData"
    // TODO: "splinedModelInstanceData", "splinedDecalInstanceData"
    [StructLayout(LayoutKind.Explicit)] // 20720
    public unsafe struct BO6GfxWorldStaticModels
    {
        [FieldOffset(4)]
        public uint modelCount;
        [FieldOffset(8)]
        public uint collectionsCount;
        [FieldOffset(12)]
        public uint instanceCount;
        // [FieldOffset(80)]
        // public nint surfaces;
        [FieldOffset(104)]
        public nint models;
        [FieldOffset(120)]
        public nint collections;
        [FieldOffset(200)]
        public nint instanceData;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public unsafe struct BO6GfxStaticModel
    {
        public nint model;
        public byte flags;
        public byte firstMtlSkinIndex;
        public ushort firstStaticModelSurfaceIndex;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public unsafe struct BO6GfxStaticModelCollection
    {
        public uint firstInstance;
        public uint instanceCount;
        public ushort smodelIndex;
        public ushort transientGfxWorldPlaced;
        public ushort clutterIndex;
        public byte flags;
        public byte pad;
    };

    [StructLayout(LayoutKind.Sequential, Size = 24)]
    public unsafe struct BO6GfxSModelInstanceData
    {
        public fixed int translation[3];
        public fixed ushort orientation[4];
        public ushort halfFloatScale;
    }

    [StructLayout(LayoutKind.Explicit)] // 40496
    public unsafe struct BO6GfxWorld
    {
        [FieldOffset(0)]
        public ulong hash;
        [FieldOffset(8)]
        public nint baseName;
        [FieldOffset(216)]
        public BO6GfxWorldSurfaces surfaces;
        [FieldOffset(608)]
        public BO6GfxWorldStaticModels smodels;
        [FieldOffset(27508)]
        public uint transientZoneCount;
        [FieldOffset(27512)]
        public fixed ulong transientZones[1536];
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public unsafe struct BO6Material // TODO:
    {
        [FieldOffset(0)]
        public ulong hash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 248)]
    public unsafe struct BO6XModel
    {
        [FieldOffset(0)]
        public ulong hash;
        [FieldOffset(8)]
        public nint name;
        [FieldOffset(16)]
        public ushort numSurfs;
        [FieldOffset(18)]
        public byte numLods;
        [FieldOffset(104)]
        public nint xmodelPackedDataPtr;
        [FieldOffset(144)]
        public nint materialHandles;
        [FieldOffset(152)]
        public nint lodInfo;
    }

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    public unsafe struct BO6XModelPackedData
    {
        [FieldOffset(0)]
        public byte NumBones;
        [FieldOffset(1)]
        public ushort NumRootBones;
        [FieldOffset(6)]
        public ushort UnkBoneCount;
        [FieldOffset(80)]
        public nint ParentListPtr;
        [FieldOffset(88)]
        public nint RotationsPtr;
        [FieldOffset(96)]
        public nint TranslationsPtr;
        [FieldOffset(104)]
        public nint PartClassificationPtr;
        [FieldOffset(112)]
        public nint BaseMatriciesPtr;
        [FieldOffset(120)]
        public nint BoneIDsPtr;
    };

    [StructLayout(LayoutKind.Explicit, Size = 72)]
    public unsafe struct BO6XModelLodInfo
    {
        [FieldOffset(0)]
        public nint modelSurfsStaging;
        [FieldOffset(8)]
        public nint surfs;
        [FieldOffset(16)]
        public fixed float distance[3];
        [FieldOffset(28)]
        public ushort numSurfs;
        [FieldOffset(30)]
        public ushort surfIndex;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64, Pack = 1)]
    public unsafe struct BO6XModelSurfs
    {
        [FieldOffset(0)]
        public ulong hash;
        [FieldOffset(8)]
        public nint surfs;
        [FieldOffset(16)]
        public nint shared;
        [FieldOffset(24)]
        public ushort numSurfs;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public unsafe struct BO6XSurfaceShared
    {
        [FieldOffset(0)]
        public nint data;
        [FieldOffset(8)]
        public ulong xpakKey;
        [FieldOffset(16)]
        public uint dataSize;
        [FieldOffset(20)]
        public int flags;
    }
    
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public unsafe struct BO6Bounds
    {
        [FieldOffset(0)]
        public Vector3 midPoint;
        [FieldOffset(12)]
        public Vector3 halfSize;
    };

    // Note: We can get (XSurfaceSubdivInfo* subdiv;) by "facePoints"
    [StructLayout(LayoutKind.Explicit, Size = 224)]
    public unsafe struct BO6XSurface
    {
        [FieldOffset(20)]
        public uint vertCount;
        [FieldOffset(24)]
        public uint triCount;
        [FieldOffset(28)]
        public uint packedIndicesTableCount;
        [FieldOffset(60)]
        public float overrideScale;

        [FieldOffset(64)]
        public fixed uint offsets[14];
        [FieldOffset(64)]
        public uint sharedVertDataOffset;
        [FieldOffset(68)]
        public uint sharedUVDataOffset;
        [FieldOffset(72)]
        public uint sharedTangentFrameDataOffset;
        [FieldOffset(76)]
        public uint sharedIndexDataOffset;
        [FieldOffset(80)]
        public uint sharedPackedIndicesTableOffset;
        [FieldOffset(84)]
        public uint sharedPackedIndicesOffset;
        [FieldOffset(88)]
        public uint sharedColorDataOffset;
        // [FieldOffset(92)]
        // public uint sharedSecondUVDataOffset;

        [FieldOffset(120)]
        public nint shared;
        [FieldOffset(176)]
        public BO6Bounds surfBounds;
    }

    // Note:
    // StTerrain: "samplePoints"
}
