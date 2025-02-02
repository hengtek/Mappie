using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DotnesktRemastered.Structures
{
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public unsafe struct MW6SPGfxSurface
    {
        [FieldOffset(0)]
        public uint posOffset;
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
        public ushort packedIndiciesTableCount;
        [FieldOffset(24)]
        public ushort vertexCount;
        [FieldOffset(26)]
        public ushort unk3; // is zero
        [FieldOffset(28)]
        public uint unk4;
        [FieldOffset(32)]
        public uint unk5; //FFFF
        [FieldOffset(36)]
        public uint packedIndicesOffset;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public unsafe struct MW6SPGfxUgbSurfData
    {
        [FieldOffset(0)]
        public MW6SPGfxWorldDrawOffset worldDrawOffset;
        [FieldOffset(16)]
        public uint transientZoneIndex;
        [FieldOffset(20)]
        public uint unk0; //0xFFFF
        [FieldOffset(24)]
        public uint unk1; //either 0 or 510
        [FieldOffset(28)]
        public uint layerCount;
        [FieldOffset(32)]
        public uint vertexCount;
        [FieldOffset(36)]
        public uint unk2; // no idea ? 
        [FieldOffset(40)]
        public uint xyzOffset;
        [FieldOffset(44)]
        public uint tangentFrameOffset;
        [FieldOffset(48)]
        public uint lmapOffset;
        [FieldOffset(52)]
        public uint colorOffset;
        [FieldOffset(56)]
        public uint texCoordOffset;
        [FieldOffset(60)]
        public uint unk3;
        [FieldOffset(64)]
        public uint accumulatedUnk2; //? this is sum of previous unk2
        [FieldOffset(68)]
        public fixed uint normalTransformOffset[7];
        [FieldOffset(96)]
        public fixed uint displacementOffset[8];
    }
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public unsafe struct MW6SPGfxWorldDrawOffset
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

    [StructLayout(LayoutKind.Explicit, Size = 392)]
    public unsafe struct MW6SPGfxWorldSurfaces
    {
        [FieldOffset(0)]
        public uint count;
        [FieldOffset(8)]
        public uint ugbSurfDataCount;
        [FieldOffset(12)]
        public uint materialCount;
        [FieldOffset(112)]
        public nint surfaces;
        [FieldOffset(152)]
        public nint materials;
        [FieldOffset(168)]
        public nint ugbSurfData;
        [FieldOffset(176)]
        public nint worldDrawOffsets;
        [FieldOffset(296)]
        public uint btndSurfacesCount;
        [FieldOffset(304)]
        public nint btndSurfaces;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public unsafe struct MW6SPGfxWorldDrawVerts
    {
        [FieldOffset(0)]
        public uint posSize;
        [FieldOffset(4)]
        public uint indexCount;
        [FieldOffset(8)]
        public uint tableCount;
        [FieldOffset(12)]
        public uint packedIndicesSize;
        [FieldOffset(16)]
        public nint posData;
        [FieldOffset(24)]
        public nint indices;
        [FieldOffset(32)]
        public nint tableData;
        [FieldOffset(40)]
        public nint packedIndices;
    }

    [StructLayout(LayoutKind.Explicit, Size = 376)]
    public unsafe struct MW6SPGfxWorldTransientZone
    {
        [FieldOffset(0)]
        public ulong hash;
        [FieldOffset(8)]
        public nint unkPtr0; // 16 bytes of yap, seems empty
        [FieldOffset(16)]
        public ulong transientZoneIndex;
        [FieldOffset(24)]
        public MW6SPGfxWorldDrawVerts drawVerts;
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct MW6SPGfxWorld
    {
        [FieldOffset(0)]
        public ulong hash;
        [FieldOffset(8)]
        public nint baseName;
        [FieldOffset(192)]
        public MW6SPGfxWorldSurfaces surfaces;
        [FieldOffset(560)]
        public MW6SPGfxWorldStaticModels smodels;
        [FieldOffset(5652)]
        public uint transientZoneCount;
        [FieldOffset(5656)]
        public fixed ulong transientZones[1536];
    }

    [StructLayout(LayoutKind.Explicit, Size = 112)]
    public unsafe struct MW6SPMaterial
    {
        [FieldOffset(0)]
        public ulong hash;
        [FieldOffset(24)]
        public byte textureCount;
        [FieldOffset(27)]
        public byte layerCount;
        [FieldOffset(48)]
        public nint textureTable;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public unsafe struct MW6SPMaterialTextureDef
    {
        [FieldOffset(0)]
        public byte index;
        [FieldOffset(1)]
        public fixed byte padding[7];
        [FieldOffset(8)]
        public nint imagePtr;
    }

    [StructLayout(LayoutKind.Explicit, Size = 384)]
    public unsafe struct MW6SPGfxWorldStaticModels
    {
        [FieldOffset(4)]
        public uint smodelCount;
        [FieldOffset(8)]
        public uint collectionsCount;
        [FieldOffset(12)]
        public uint instanceCount;
        [FieldOffset(80)]
        public nint surfaces;
        [FieldOffset(88)]
        public nint smodels;
        [FieldOffset(104)]
        public nint collections;
        [FieldOffset(168)]
        public nint instanceData;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public unsafe struct MW6SPGfxStaticModel
    {
        public nint xmodel;
        public byte flags;
        public byte firstMtlSkinIndex;
        public ushort firstStaticModelSurfaceIndex;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public unsafe struct MW6SPGfxStaticModelCollection
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
    public unsafe struct MW6SPGfxSModelInstanceData
    {
        public fixed int translation[3];
        public fixed ushort orientation[4];
        public ushort halfFloatScale;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public unsafe struct MW6SPGfxImage
    {
        [FieldOffset(0)]
        public ulong hash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 232)]
    public unsafe struct MW6SPXModel
    {
        [FieldOffset(0)]
        public ulong hash;
        [FieldOffset(8)]
        public nint name;
        [FieldOffset(16)]
        public ushort numSurfs;
        [FieldOffset(40)]
        public float scale;
        [FieldOffset(264)]
        public nint materialHandles;
        [FieldOffset(272)]
        public nint lodInfo;
    }

    [StructLayout(LayoutKind.Explicit, Size = 72)]
    public unsafe struct MW6SPXModelLodInfo
    {
        [FieldOffset(0)]
        public nint modelSurfsStaging;
        [FieldOffset(8)]
        public nint surfs;
        [FieldOffset(16)]
        public float distance;
        [FieldOffset(28)]
        public ushort numsurfs;
        [FieldOffset(30)]
        public ushort surfIndex;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80, Pack = 1)]
    public unsafe struct MW6SPXModelSurfs
    {
        [FieldOffset(0)]
        public ulong hash;
        [FieldOffset(8)]
        public nint surfs;
        [FieldOffset(16)]
        public ulong xpakKey;
        [FieldOffset(32)]
        public nint shared;
        [FieldOffset(40)]
        public ushort numsurfs;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public unsafe struct MW6SPXSurfaceShared
    {
        [FieldOffset(0)]
        public nint data;
        [FieldOffset(8)]
        public uint dataSize;
        [FieldOffset(12)]
        public int flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 224)]
    public unsafe struct MW6SPXSurface
    {
        [FieldOffset(4)]
        public ushort packedIndiciesTableCount;
        [FieldOffset(24)]
        public uint vertCount;
        [FieldOffset(28)]
        public uint triCount;
        [FieldOffset(60)]
        public float overrideScale;
        [FieldOffset(64)]
        public fixed uint Offsets[14];
        [FieldOffset(64)]
        public uint xyzOffset;
        [FieldOffset(68)]
        public uint texCoordOffset;
        [FieldOffset(72)]
        public uint tangentFrameOffset;
        [FieldOffset(76)]
        public uint indexDataOffset;
        [FieldOffset(80)]
        public uint packedIndiciesTableOffset;
        [FieldOffset(84)]
        public uint packedIndicesOffset;
        [FieldOffset(88)]
        public uint colorOffset;
        [FieldOffset(96)]
        public uint secondUVOffset;
        [FieldOffset(120)]
        public nint shared;
        [FieldOffset(176)]
        public Vector3 offsets;
        [FieldOffset(188)]
        public float scale;
        [FieldOffset(192)]
        public float min;
        [FieldOffset(196)]
        public float max;
    }

    public enum MW6SPTextureIdxTable
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

