using Cast.NET;
using Cast.NET.Nodes;
using DotnesktRemastered.Structures;
using DotnesktRemastered.Utils;
using Serilog;
using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace DotnesktRemastered.Games
{
    public class ModernWarfare6
    {
        public static CordycepProcess Cordycep = Program.Cordycep;

        private static uint GFXMAP_POOL_IDX = 50;

        public static void DumpMap(string name)
        {
            Log.Information("Finding map {baseName}...", name);
            Cordycep.EnumerableAssetPool(GFXMAP_POOL_IDX, (asset) =>
            {
                MW6GfxWorld gfxWorld = Cordycep.ReadMemory<MW6GfxWorld>(asset.Header);
                if (gfxWorld.baseName == 0) return;
                string baseName = Cordycep.ReadString(gfxWorld.baseName).Trim();
                if (baseName == name)
                {
                    DumpMap(gfxWorld, baseName);
                    Log.Information("Found map {baseName}.", baseName);
                }
            });
        }

        private static unsafe void DumpMap(MW6GfxWorld gfxWorld, string baseName)
        {
            MW6GfxWorldTransientZone[] transientZone = new MW6GfxWorldTransientZone[gfxWorld.transientZoneCount];
            for (int i = 0; i < gfxWorld.transientZoneCount; i++)
            {
                transientZone[i] = Cordycep.ReadMemory<MW6GfxWorldTransientZone>(gfxWorld.transientZones + i * sizeof(MW6GfxWorldTransientZone));
            }
            MW6GfxWorldSurfaces gfxWorldSurfaces = gfxWorld.surfaces;

            SurfaceData[] meshes = new SurfaceData[gfxWorldSurfaces.count];
            for (int i = 0; i < gfxWorldSurfaces.count; i++)
            {
                MW6GfxSurface gfxSurface = Cordycep.ReadMemory<MW6GfxSurface>(gfxWorldSurfaces.surfaces + i * sizeof(MW6GfxSurface));
                MW6GfxUgbSurfData ugbSurfData = Cordycep.ReadMemory<MW6GfxUgbSurfData>(gfxWorldSurfaces.ugbSurfData + (nint)(gfxSurface.ugbSurfDataIndex * sizeof(MW6GfxUgbSurfData)));
                MW6GfxWorldTransientZone zone = transientZone[ugbSurfData.transientZoneIndex];

                MeshNode mesh = ReadMesh(gfxSurface, ugbSurfData, zone);

                nint materialPtr = Cordycep.ReadMemory<nint>(gfxWorldSurfaces.materials + (nint)(gfxSurface.materialIndex * 8));
                MW6Material material = Cordycep.ReadMemory<MW6Material>(materialPtr);
                ulong materialHash = material.Hash & 0x0FFFFFFFFFFFFFFF;
                MaterialNode materialNode = new MaterialNode($"xmaterial_{materialHash:X}", "pbr");
                mesh.AddValue("m", materialNode.Hash);
                PopulateMaterial(material);

                SurfaceData surfaceData = new SurfaceData()
                {
                    mesh = mesh,
                    material = materialNode,
                    textures = PopulateMaterial(material)
                };

                meshes[i] = surfaceData;
            }
            MW6GfxWorldStaticModels smodels = gfxWorld.smodels;

            for (int i = 0; i < smodels.collectionsCount; i++)
            {
                MW6GfxStaticModelCollection collection = Cordycep.ReadMemory<MW6GfxStaticModelCollection>(smodels.collections + i * 16);
                MW6GfxStaticModel staticModel = Cordycep.ReadMemory<MW6GfxStaticModel>(smodels.smodels + collection.smodelIndex * 16);
                MW6GfxWorldTransientZone zone = transientZone[collection.transientGfxWorldPlaced];
                MW6XModel xmodel = Cordycep.ReadMemory<MW6XModel>(staticModel.xmodel);

                ulong xmodelHashName = xmodel.Hash & 0x0FFFFFFFFFFFFFFF;

                int instanceId = (int)collection.firstInstance;
                while (instanceId < collection.firstInstance + collection.instanceCount)
                {
                    MW6GfxSModelInstanceData instanceData = Cordycep.ReadMemory<MW6GfxSModelInstanceData>((nint)smodels.instanceData + instanceId * 24);
                    Vector3 translation = new Vector3(
                        (float)instanceData.translation[0] * 0.000244140625f,
                        (float)instanceData.translation[1] * 0.000244140625f,
                        (float)instanceData.translation[2] * 0.000244140625f
                    );

                    Quaternion rotation = new Quaternion(
                        Math.Min(Math.Max((float)((float)instanceData.orientation[0] * 0.000030518044f) - 1.0f, -1.0f), 1.0f),
                        Math.Min(Math.Max((float)((float)instanceData.orientation[1] * 0.000030518044f) - 1.0f, -1.0f), 1.0f),
                        Math.Min(Math.Max((float)((float)instanceData.orientation[2] * 0.000030518044f) - 1.0f, -1.0f), 1.0f),
                        Math.Min(Math.Max((float)((float)instanceData.orientation[3] * 0.000030518044f) - 1.0f, -1.0f), 1.0f)
                    );

                    instanceId++;
                }
            }
            //Write to file
            /*
            ModelNode model = new ModelNode();
            SkeletonNode skeleton = new SkeletonNode();
            model.AddString("n", $"{baseName}_base_mesh");
            model.AddNode(skeleton);
            foreach (SurfaceData mesh in meshes)
            {
                model.AddNode(mesh.mesh);
                model.AddNode(mesh.material);
            }
            CastNode root = new CastNode(CastNodeIdentifier.Root);
            root.AddNode(model);
            CastWriter.Save(@"D:/" + baseName + ".cast", root);
            */
        }

        private static unsafe List<TextureSemanticData> PopulateMaterial(MW6Material material)
        {
            MW6GfxImage[] images = new MW6GfxImage[material.imageCount];

            for (int i = 0; i < material.imageCount; i++)
            {
                nint imagePtr = Cordycep.ReadMemory<nint>(material.imageTable + i * 8);
                MW6GfxImage image = Cordycep.ReadMemory<MW6GfxImage>(imagePtr);
                images[i] = image;
            }

            for (int i = 0; i < material.textureCount; i++)
            {
                MW6MaterialTextureDef textureDef = Cordycep.ReadMemory<MW6MaterialTextureDef>(material.textureTable + i * sizeof(MW6MaterialTextureDef));
                MW6GfxImage image = images[textureDef.imageIdx];

                int uvMapIndex = 0;
            }
            return null;
        }

        private static unsafe MeshNode ReadMesh(MW6GfxSurface gfxSurface, MW6GfxUgbSurfData ugbSurfData, MW6GfxWorldTransientZone zone)
        {
            MW6GfxWorldDrawOffset worldDrawOffset = ugbSurfData.worldDrawOffset;
            MeshNode mesh = new MeshNode();
            mesh.AddValue("ul", ugbSurfData.layerCount);

            for (int layerIdx = 0; layerIdx < ugbSurfData.layerCount; layerIdx++)
            {
                mesh.AddArray<Vector2>($"u{layerIdx}", new(gfxSurface.vertexCount));
            }

            CastArrayProperty<Vector2> uvs = mesh.GetProperty<CastArrayProperty<Vector2>>("u0");

            nint xyzPtr = zone.drawVerts.posData + (nint)ugbSurfData.xyzOffset;
            nint tangentFramePtr = zone.drawVerts.posData + (nint)ugbSurfData.tangentFrameOffset;
            nint texCoordPtr = zone.drawVerts.posData + (nint)ugbSurfData.texCoordOffset;

            CastArrayProperty<Vector3> positions = mesh.AddArray<Vector3>("vp", new(gfxSurface.vertexCount));
            CastArrayProperty<Vector3> normals = mesh.AddArray<Vector3>("vn", new(gfxSurface.vertexCount));

            for (int j = 0; j < gfxSurface.vertexCount; j++)
            {
                ulong packedPosition = Cordycep.ReadMemory<ulong>(xyzPtr);
                Vector3 position = new Vector3(
                    ((((packedPosition >> 0) & 0x1FFFFF) * worldDrawOffset.scale) + worldDrawOffset.x),
                    ((((packedPosition >> 21) & 0x1FFFFF) * worldDrawOffset.scale) + worldDrawOffset.y),
                    ((((packedPosition >> 42) & 0x1FFFFF) * worldDrawOffset.scale) + worldDrawOffset.z));

                positions.Add(position);
                xyzPtr += 8;

                uint packedTangentFrame = Cordycep.ReadMemory<uint>(tangentFramePtr);

                //TODO: FIX ME
                //Okay for whatever reason the normal is inverted
                //i have no idea why is this happening but multiply them by -1 seems to kinda fix it
                Vector3 normal = NormalUnpacking.UnpackCoDQTangent(packedTangentFrame) * -1;

                normals.Add(normal);
                tangentFramePtr += 4;

                Vector2 uv = Cordycep.ReadMemory<Vector2>(texCoordPtr);
                uvs.Add(uv);
                texCoordPtr += 8;

                for (int layerIdx = 1; layerIdx < ugbSurfData.layerCount; layerIdx++)
                {
                    Vector2 uvExtra = Cordycep.ReadMemory<Vector2>(texCoordPtr);
                    mesh.GetProperty<CastArrayProperty<Vector2>>($"u{layerIdx}").Add(uvExtra);
                    texCoordPtr += 8;
                }
            }

            //unpack da fucking faces 
            //References: https://github.com/Scobalula/Greyhound/blob/master/src/WraithXCOD/WraithXCOD/CoDXModelMeshHelper.cpp#L37

            nint tableOffsetPtr = zone.drawVerts.tableData + (nint)(gfxSurface.tableOffset * 40);
            nint indicesPtr = zone.drawVerts.indices + (nint)(gfxSurface.baseIndex * 2);
            nint packedIndicies = zone.drawVerts.packedIndices + (nint)gfxSurface.packedIndicesOffset;

            CastArrayProperty<ushort> faceIndices = mesh.AddArray<ushort>("f", new(gfxSurface.triCount * 3));

            for (int j = 0; j < gfxSurface.triCount; j++)
            {
                ushort[] faces = MW6FaceIndices.UnpackFaceIndices(tableOffsetPtr, gfxSurface.packedIndiciesTableCount, packedIndicies, indicesPtr, (uint)j);
                faceIndices.Add(faces[0]);
                faceIndices.Add(faces[1]);
                faceIndices.Add(faces[2]);
            }
            return mesh;
        }
    }
}
