using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DotnesktRemastered
{
    [StructLayout(LayoutKind.Explicit, Size = 36)]
    public unsafe struct BO6GfxSurface
    {
        [FieldOffset(0)]
        public uint unk0;
        [FieldOffset(4)]
        public uint unk1;
        [FieldOffset(8)]
        public uint unk2;
        [FieldOffset(12)]
        public uint unk3;
        [FieldOffset(16)]
        public uint unk4;
        [FieldOffset(20)]
        public uint unk5;
        [FieldOffset(24)]
        public uint unk6;
        [FieldOffset(28)]
        public uint unk7;
        [FieldOffset(32)]
        public uint unk8;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public unsafe struct BO6GfxUgbSurfData
    {
        [FieldOffset(0)]
        public fixed byte worldDrawOffset[16];
        [FieldOffset(16)]
        public uint unk0; //00000000
        [FieldOffset(20)]
        public ushort unk1; //FFFF
    }
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public unsafe struct BO6GfxWorldDrawOffset
    {
        [FieldOffset(0)]
        public fixed byte data[16];
    }

    [StructLayout(LayoutKind.Explicit, Size = 392)]
    public unsafe struct BO6GfxWorldSurfaces
    {
        [FieldOffset(0)]
        public uint count;
        [FieldOffset(8)]
        public uint surfDataCount;
        [FieldOffset(120)]
        public nint surfaces;
        [FieldOffset(192)]
        public nint ugbSurfData;
        [FieldOffset(200)]
        public nint worldDrawOffsets;
        [FieldOffset(320)]
        public uint btndSurfacesCount;
        [FieldOffset(328)]
        public nint btndSurfaces;
    }

    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public unsafe struct BO6GfxWorldDrawVerts
    {
        [FieldOffset(3)]
        public uint posSize;
        [FieldOffset(4)]
        public uint indexCount;
        [FieldOffset(8)]
        public uint unk2;
        [FieldOffset(16)]
        public nint unk3;
        [FieldOffset(24)]
        public nint posData;
        [FieldOffset(32)]
        public nint indices;
        [FieldOffset(40)]
        public nint unk2Ptr;
        [FieldOffset(48)]
        public nint unk3Ptr;
    }

    [StructLayout(LayoutKind.Explicit, Size = 584)]
    public unsafe struct BO6GfxWorldTransientZone
    {
        [FieldOffset(0)]
        public ulong Hash;
        [FieldOffset(8)]
        public nint unkPtr0;
        [FieldOffset(24)]
        public BO6GfxWorldDrawVerts drawVerts;
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct BO6GfxWorld
    {
        [FieldOffset(0)]
        public ulong Hash;
        [FieldOffset(8)]
        public nint baseName;
        [FieldOffset(200)]
        public BO6GfxWorldSurfaces surfaces;
        [FieldOffset(27580)]
        public uint transientZoneCount;
        [FieldOffset(27584)]
        public nint transientZones;
    }
}
