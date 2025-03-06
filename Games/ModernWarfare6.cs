using DotnesktRemastered.Structures;
using System.Numerics;

namespace DotnesktRemastered.Games
{
    public class ModernWarfare6 : BaseGame<MW6GfxWorld, MW6GfxWorldTransientZone, MW6GfxWorldSurfaces, MW6GfxSurface,
        MW6GfxUgbSurfData, MW6Material, MW6GfxWorldStaticModels, MW6GfxStaticModelCollection, MW6GfxStaticModel,
        MW6XModel, MW6GfxSModelInstanceData, MW6GfxWorldDrawOffset, MW6GfxWorldDrawVerts, MW6XModelLodInfo,
        MW6XModelSurfs, MW6XSurfaceShared, MW6XSurface>
    {
        public ModernWarfare6()
        {
            GFXMAP_POOL_IDX = 50;
            GFXMAP_TRZONE_POOL_IDX = 51;
        }

        protected override string GameName => "ModernWarfare6";

        protected override unsafe List<TextureSemanticData> PopulateMaterial(MW6Material material)
        {
            MW6GfxImage[] images = new MW6GfxImage[material.imageCount];

            for (int i = 0; i < material.imageCount; i++)
            {
                nint imagePtr = Cordycep.ReadMemory<nint>(material.imageTable + i * 8);
                MW6GfxImage image = Cordycep.ReadMemory<MW6GfxImage>(imagePtr);
                images[i] = image;
            }

            List<TextureSemanticData> textures = new List<TextureSemanticData>();

            for (int i = 0; i < material.textureCount; i++)
            {
                MW6MaterialTextureDef textureDef =
                    Cordycep.ReadMemory<MW6MaterialTextureDef>(
                        material.textureTable + i * sizeof(MW6MaterialTextureDef));
                MW6GfxImage image = images[textureDef.imageIdx];

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
    }
}