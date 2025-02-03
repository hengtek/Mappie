using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DotnesktRemastered.Structures
{
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct MW6SPGfxWorld
    {
        [FieldOffset(0)]
        public ulong hash;
        [FieldOffset(8)]
        public nint baseName;
        [FieldOffset(192)]
        public MW6GfxWorldSurfaces surfaces;
        [FieldOffset(560)]
        public MW6GfxWorldStaticModels smodels;
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
}

