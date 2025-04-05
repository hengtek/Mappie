using Mappie.Structures;
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
    }
}