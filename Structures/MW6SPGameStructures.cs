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
    public unsafe struct MW6SPGfxWorld: IGfxWorld<MW6GfxWorldSurfaces, MW6GfxWorldStaticModels>
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

    [StructLayout(LayoutKind.Explicit, Size = 112)]
    public unsafe struct MW6SPMaterial : IMaterial
    {
        [FieldOffset(0)]
        public ulong hash;
        [FieldOffset(24)]
        public byte textureCount;
        [FieldOffset(27)]
        public byte layerCount;
        [FieldOffset(48)]
        public nint textureTable;
        
        ulong IMaterial.hash => hash;
        byte IMaterial.textureCount => textureCount;
        byte IMaterial.imageCount => 0;
        nint IMaterial.textureTable => textureTable;
        nint IMaterial.imageTable => 0;
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
    public unsafe struct MW6SPXModel:IXModel
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
        
        ulong IXModel.hash => hash;
        nint IXModel.name => name;
        nint IXModel.materialHandles => materialHandles;
        nint IXModel.lodInfo => lodInfo;
    }
}

