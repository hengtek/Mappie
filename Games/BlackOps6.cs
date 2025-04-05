using DotnesktRemastered.Structures;
using System.Numerics;

namespace DotnesktRemastered.Games
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
    }
}