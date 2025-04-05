using Mappie.Structures;
using Mappie.Utils;
using System.Numerics;

namespace Mappie.Games
{
    public class ModernWarfare6SP : BaseGame<MW6SPGfxWorld, MW6GfxWorldTransientZone, MW6GfxWorldSurfaces, MW6GfxSurface
        , MW6GfxUgbSurfData, MW6SPMaterial, MW6GfxWorldStaticModels, MW6GfxStaticModelCollection, MW6GfxStaticModel,
        MW6SPXModel,
        MW6GfxSModelInstanceData, MW6GfxWorldDrawOffset, MW6GfxWorldDrawVerts, MW6XModelLod, MW6XModelSurfs,
        MW6XSurfaceShared, MW6XSurface>
    {
        public ModernWarfare6SP()
        {
            GFXMAP_POOL_IDX = 50;
            GFXMAP_TRZONE_POOL_IDX = 51;
        }

        protected override string GameName => "ModernWarfare6SP";

        protected override unsafe List<TextureSemanticData> PopulateMaterial(MW6SPMaterial material)
        {
            List<TextureSemanticData> textures = new List<TextureSemanticData>();

            for (int i = 0; i < material.textureCount; i++)
            {
                MW6SPMaterialTextureDef textureDef =
                    Cordycep.ReadMemory<MW6SPMaterialTextureDef>(material.textureTable +
                                                                 i * sizeof(MW6SPMaterialTextureDef));
                MW6GfxImage image = Cordycep.ReadMemory<MW6GfxImage>(textureDef.imagePtr);

                int uvMapIndex = 0;

                ulong hash = image.hash & 0x0FFFFFFFFFFFFFFF;

                string imageName = $"ximage_{hash:X}".ToLower();

                string textureSemantic;
                textureSemantic = $"unk_semantic_0x{textureDef.index:X}";

                textures.Add(new()
                {
                    semantic = textureSemantic,
                    texture = imageName
                });
            }

            return textures;
        }

        protected override float GetSurfaceScale(MW6XSurface surface)
        {
            return surface.overrideScale != -1
                ? surface.overrideScale
                : Math.Max(Math.Max(surface.min, surface.scale), surface.max);
        }

        protected override Vector3 GetSurfaceOffset(MW6XSurface surface)
        {
            return surface.overrideScale != -1 ? Vector3.Zero : surface.offsets;
        }

        protected override ushort[] UnpackFaceIndices(nint tables, uint tableCount, nint packedIndices, nint indices, uint faceIndex, bool isLocal = false)
        {
            uint currentFaceIndex = faceIndex;
            for (int i = 0; i < tableCount; i++)
            {
                nint tablePtr = tables + (i * 40);
                nint tableIndicesPtr = packedIndices + (nint)Cordycep.ReadMemory<uint>(tablePtr + 36, isLocal);
                byte count = Cordycep.ReadMemory<byte>(tablePtr + 35, isLocal);
                if (currentFaceIndex < count)
                {
                    ushort[] faceIndices = new ushort[3];
                    byte bits = (byte)(Cordycep.ReadMemory<byte>(tablePtr + 34, isLocal) - 1);
                    faceIndex = Cordycep.ReadMemory<uint>(tablePtr + 28, isLocal);

                    uint faceIndex1Offset = FindFaceIndex(tableIndicesPtr, currentFaceIndex * 3 + 0, bits, isLocal) + faceIndex;
                    uint faceIndex2Offset = FindFaceIndex(tableIndicesPtr, currentFaceIndex * 3 + 1, bits, isLocal) + faceIndex;
                    uint faceIndex3Offset = FindFaceIndex(tableIndicesPtr, currentFaceIndex * 3 + 2, bits, isLocal) + faceIndex;

                    faceIndices[0] = Cordycep.ReadMemory<ushort>(indices + (nint)(faceIndex1Offset * 2), isLocal);
                    faceIndices[1] = Cordycep.ReadMemory<ushort>(indices + (nint)(faceIndex2Offset * 2), isLocal);
                    faceIndices[2] = Cordycep.ReadMemory<ushort>(indices + (nint)(faceIndex3Offset * 2), isLocal);
                    return faceIndices;
                }
                currentFaceIndex -= count;
            }
            return null;
        }

        protected byte FindFaceIndex(nint packedIndices, uint index, byte bits, bool isLocal = false)
        {
            int bitIndex;

            if (!Bits.BitScanReverse64(bits, out bitIndex))
                bitIndex = 64;
            else
                bitIndex ^= 0x3F;

            ushort offset = (ushort)(index * (byte)(64 - bitIndex));
            byte bitCount = (byte)(64 - bitIndex);
            nint packedIndicesPtr = packedIndices + (offset >> 3);
            byte bitOffset = (byte)(offset & 7);

            byte packedIndice = Cordycep.ReadMemory<byte>(packedIndicesPtr, isLocal);

            if (bitOffset == 0)
                return (byte)(packedIndice & ((1 << bitCount) - 1));

            if (8 - bitOffset < bitCount)
            {
                byte nextPackedIndice = Cordycep.ReadMemory<byte>(packedIndicesPtr + 1, isLocal);
                return (byte)((packedIndice >> bitOffset) & ((1 << (8 - bitOffset)) - 1) | ((nextPackedIndice & ((1 << (64 - bitIndex - (8 - bitOffset))) - 1)) << (8 - bitOffset)));
            }

            return (byte)((packedIndice >> bitOffset) & ((1 << bitCount) - 1));
        }
    }
}