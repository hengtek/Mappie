using Mappie.Structures;
using Mappie.Utils;
using System.Numerics;
using System.Reflection.PortableExecutable;

namespace Mappie.Games
{
    public class BlackOps6 : BaseGame<BO6GfxWorld, BO6GfxWorldTransientZone, BO6GfxWorldSurfaces, BO6GfxSurface,
        BO6GfxUgbSurfData, BO6Material, BO6GfxWorldStaticModels, BO6GfxStaticModelCollection, BO6GfxStaticModel,
        BO6XModel, BO6GfxSModelInstanceData, BO6GfxWorldDrawOffset, BO6GfxWorldDrawVerts, BO6XModelLod,
        BO6XModelSurfs, BO6XSurfaceShared, BO6XSurface>
    {
        public BlackOps6()
        {
            GFXMAP_POOL_IDX = 43;
            GFXMAP_TRZONE_POOL_IDX = 0x4F;
            MeshPositionScale = 0.0254f;
        }

        protected override string GameName => "BlackOps6";

        protected override unsafe List<TextureSemanticData> PopulateMaterial(BO6Material material)
        {
            BO6GfxImage[] images = new BO6GfxImage[material.imageCount];

            for (int i = 0; i < material.imageCount; i++)
            {
                nint imagePtr = Cordycep.ReadMemory<nint>(material.imageTable + i * 8);
                BO6GfxImage image = Cordycep.ReadMemory<BO6GfxImage>(imagePtr);
                images[i] = image;
            }

            List<TextureSemanticData> textures = new List<TextureSemanticData>();

            for (int i = 0; i < material.textureCount; i++)
            {
                BO6MaterialTextureDef textureDef =
                    Cordycep.ReadMemory<BO6MaterialTextureDef>(
                        material.textureTable + i * sizeof(BO6MaterialTextureDef));
                BO6GfxImage image = images[textureDef.imageIdx];

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

        protected override float GetSurfaceScale(BO6XSurface surface)
        {
            return surface.overrideScale != -1f
                ? surface.overrideScale
                : Math.Max(Math.Max(surface.surfBounds.halfSize.Y, surface.surfBounds.halfSize.X),
                    surface.surfBounds.halfSize.Z);
        }

        protected override Vector3 GetSurfaceOffset(BO6XSurface surface)
        {
            return surface.overrideScale != -1f ? Vector3.Zero : surface.surfBounds.midPoint;
        }

        protected override ushort[] UnpackFaceIndices(nint tables, uint tableCount, nint packedIndices, nint indices, uint faceIndex, bool isLocal = false)
        {
            for (int i = 0; i < tableCount; i++)
            {
                nint tablePtr = tables + (i * 32);
                byte count = (byte)(Cordycep.ReadMemory<byte>(tablePtr + 19, isLocal) + 1);
                byte flag = Cordycep.ReadMemory<byte>(tablePtr + 28, isLocal);

                if (faceIndex < count)
                {
                    ushort[] faceIndices = new ushort[3];
                    uint tripleFaceIndex = faceIndex * 3;
                    uint baseFaceIndex = Cordycep.ReadMemory<uint>(tablePtr + 20, isLocal) & 0xFFFFFF;
                    byte bits = Cordycep.ReadMemory<byte>(tablePtr + 23, isLocal);
                    nint tableIndicesPtr = packedIndices + (nint)Cordycep.ReadMemory<uint>(tablePtr + 24, isLocal);

                    faceIndices[0] = Cordycep.ReadMemory<ushort>(indices + (nint)(FindFaceIndex(tableIndicesPtr, tripleFaceIndex + 0, bits, flag, isLocal) + baseFaceIndex) * 2, isLocal);
                    faceIndices[1] = Cordycep.ReadMemory<ushort>(indices + (nint)(FindFaceIndex(tableIndicesPtr, tripleFaceIndex + 1, bits, flag, isLocal) + baseFaceIndex) * 2, isLocal);
                    faceIndices[2] = Cordycep.ReadMemory<ushort>(indices + (nint)(FindFaceIndex(tableIndicesPtr, tripleFaceIndex + 2, bits, flag, isLocal) + baseFaceIndex) * 2, isLocal);

                    return faceIndices;
                }

                faceIndex -= count;
            }
            return null;
        }

        private byte FindFaceIndex(nint packedIndices, uint index, uint bits, byte flag, bool isLocal = false)
        {
            if (!Bits.BitScanReverse32(bits, out int highestBit))
                highestBit = 32;
            else
                highestBit = 31 - highestBit;

            byte bitCount = (byte)(32 - highestBit);

            if ((flag & 0x10) != 0)
            {
                return ExtractPackedBits(packedIndices, index * bitCount, (uint)192 * bitCount, bitCount, isLocal);
            }

            uint tripleIndex = index;
            byte effectiveBitCount = (byte)(bitCount + 2 * flag);
            ulong bitOffset = (ulong)(tripleIndex / 3 * effectiveBitCount);
            ulong byteOffset = bitOffset >> 3;

            uint extractedValue;
            if ((bitOffset & 7) != 0)
            {
                ulong bitStart = bitOffset - byteOffset * 8;
                nint dataOffset = packedIndices + (nint)byteOffset;

                ulong packedBits = Cordycep.ReadMemory<ulong>(dataOffset, isLocal);
                extractedValue = (uint)((packedBits >> (int)bitStart) & ((1UL << effectiveBitCount) - 1));
            }
            else
            {
                nint fullOffset = packedIndices + (nint)byteOffset;
                extractedValue = Cordycep.ReadMemory<ushort>(fullOffset, isLocal);
                extractedValue |= (uint)(Cordycep.ReadMemory<byte>(fullOffset + 2, isLocal) << 16);
            }

            byte baseVertex = (byte)(extractedValue & ((1 << bitCount) - 1));
            uint componentIndex = index % 3;

            if (componentIndex != 0)
            {
                byte adjustedBitCount = (byte)(componentIndex == 1 ? bitCount : bitCount + flag);
                uint offsetValue = (uint)((extractedValue >> adjustedBitCount) & ((1 << flag) - 1));

                if ((offsetValue & (1 << (flag - 1))) != 0)
                {
                    offsetValue = (uint)(-((int)offsetValue & ((1 << (flag - 1)) - 1)));
                }

                baseVertex = (byte)(baseVertex + offsetValue);
            }

            return baseVertex;
        }

        private byte ExtractPackedBits(nint packedIndices, uint offset, uint bitLimit, byte bitCount, bool isLocal)
        {
            ulong byteOffset = offset >> 3;
            ulong bitStart = offset - (byteOffset * 8);
            ulong currentBitPos = bitStart;
            nint currentByteOffset = packedIndices + (nint)byteOffset;

            if (bitStart >= bitLimit)
                return 0;

            ulong packedBits = Cordycep.ReadMemory<ulong>(currentByteOffset, isLocal);
            return (byte)((packedBits >> (int)(currentBitPos & 7)) & ((1UL << bitCount) - 1));
        }
    }
}