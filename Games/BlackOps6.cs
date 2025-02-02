using Cast.NET;
using Cast.NET.Nodes;
using DotnesktRemastered.FileStorage;
using DotnesktRemastered.Structures;
using DotnesktRemastered.Utils;
using Serilog;
using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;

namespace DotnesktRemastered.Games
{
    public class BlackOps6
    {
        public static CordycepProcess Cordycep = Program.Cordycep;

        private static uint GFXMAP_POOL_IDX = 43;
        private static uint STREAMINGINFO_POOL_IDX = 0x4F;

        private static Dictionary<ulong, XModelMeshData[]> models = new();
        public static void DumpMap(string name, bool noStaticProps = false, Vector3 staticPropsOrigin = new(), uint range = 0)
        {
            Log.Information("Finding map {0}...", name);
            Cordycep.EnumerableAssetPool(GFXMAP_POOL_IDX, (asset) =>
            {
                BO6GfxWorld gfxWorld = Cordycep.ReadMemory<BO6GfxWorld>(asset.Header);
                if (gfxWorld.baseName == 0) return;
                string baseName = Cordycep.ReadString(gfxWorld.baseName).Trim();
                if (baseName == name)
                {
                    Log.Information("Found map {0}, started dumping... :)", baseName);
                    DumpMap(gfxWorld, baseName);
                    Log.Information("Dumped map {0}. XD", baseName);
                }
            });
        }
        public static string[] GetMapList()
        {
            List<string> maps = new List<string>();
            Cordycep.EnumerableAssetPool(GFXMAP_POOL_IDX, (asset) =>
            {
                BO6GfxWorld gfxWorld = Cordycep.ReadMemory<BO6GfxWorld>(asset.Header);
                if (gfxWorld.baseName == 0) return;
                string baseName = Cordycep.ReadString(gfxWorld.baseName).Trim();
                maps.Add(baseName);
            });
            return maps.ToArray();
        }
        public static unsafe void TestLmao()
        {
            Cordycep.EnumerableAssetPool(STREAMINGINFO_POOL_IDX, (asset) =>
            {
                var streamingInfo = Cordycep.ReadMemory<BO6StreamingInfo>(asset.Header);
                var transientInfo = Cordycep.ReadMemory<BO6TransientInfo>(streamingInfo.transientInfoPtr);
                for (uint i = 0; i < transientInfo.unkCount; i++)
                {
                    var unk = Cordycep.ReadMemory<BO6TransientInfoUnk>(transientInfo.unkPtr + (nint)i * sizeof(BO6TransientInfoUnk));
                    var assetName = Cordycep.ReadString(unk.name);

                    Log.Information("[{0}] Asset: 0x{1:x} - {2} - ({3}, {4}, {5}, {6}).",
                        i, unk.hash, assetName,
                        unk.unkFlags[0], unk.unkFlags[1], unk.unkFlags[2], unk.unkFlags[3]);
                }
            });
        }

        static void CreateDirectoryIfNotExists(string? path)
        {
            if (path == null)
                throw new Exception("The path cannot be null.");

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static unsafe void DumpMap(BO6GfxWorld gfxWorld, string mapBaseName, bool noStaticProps = false, Vector3 staticPropsOrigin = new(), uint range = 0)
        {
            // Create a root for map
            CastNode mapRoot = new CastNode(CastNodeIdentifier.Root);

            // Create a model for map geo
            ModelNode mapGeoModel = new ModelNode();
            SkeletonNode skeleton = new SkeletonNode();
            mapGeoModel.AddString("n", $"{mapBaseName}_base_mesh");
            mapGeoModel.AddNode(skeleton);

            BO6GfxWorldTransientZone[] transientZone = new BO6GfxWorldTransientZone[gfxWorld.transientZoneCount];
            for (int i = 0; i < gfxWorld.transientZoneCount; i++)
            {
                var trZonePtr = (nint)gfxWorld.transientZones[i];
                if (trZonePtr == 0)
                    throw new Exception($"The transient zone with index {i} was missing.");

                transientZone[i] = Cordycep.ReadMemory<BO6GfxWorldTransientZone>(trZonePtr); // TODO:
            }
            BO6GfxWorldSurfaces gfxWorldSurfaces = gfxWorld.surfaces;

            MeshData[] meshes = new MeshData[gfxWorldSurfaces.count];
            for (int i = 0; i < gfxWorldSurfaces.count; i++)
            {
                BO6GfxSurface gfxSurface = Cordycep.ReadMemory<BO6GfxSurface>(gfxWorldSurfaces.surfaces + i * sizeof(BO6GfxSurface));
                BO6GfxUgbSurfData ugbSurfData = Cordycep.ReadMemory<BO6GfxUgbSurfData>(gfxWorldSurfaces.ugbSurfData + (nint)(gfxSurface.ugbSurfDataIndex * sizeof(BO6GfxUgbSurfData)));
                BO6GfxWorldTransientZone zone = transientZone[ugbSurfData.transientZoneIndex];
                nint materialPtr = Cordycep.ReadMemory<nint>(gfxWorldSurfaces.materials + (nint)(gfxSurface.materialIndex * 8));
                BO6Material material = Cordycep.ReadMemory<BO6Material>(materialPtr);

                MeshData mesh = ReadMesh(gfxSurface, ugbSurfData, material, zone);

                mapGeoModel.AddNode(mesh.mesh);
                mapGeoModel.AddNode(mesh.material);

                meshes[i] = mesh;
            }
            mapRoot.AddNode(mapGeoModel);

            /*MeshData[] meshes = new MeshData[gfxWorldSurfaces.btndSurfacesCount];
            for (int i = 0; i < gfxWorldSurfaces.btndSurfacesCount; i++)
            {
                BO6GfxSurface gfxSurface = Cordycep.ReadMemory<BO6GfxSurface>(gfxWorldSurfaces.btndSurfaces + i * sizeof(BO6GfxSurface));
                BO6GfxUgbSurfData ugbSurfData = Cordycep.ReadMemory<BO6GfxUgbSurfData>(gfxWorldSurfaces.ugbSurfData + (nint)(gfxSurface.ugbSurfDataIndex * sizeof(BO6GfxUgbSurfData)));
                BO6GfxWorldTransientZone zone = transientZone[ugbSurfData.transientZoneIndex];
                nint materialPtr = Cordycep.ReadMemory<nint>(gfxWorldSurfaces.materials + (nint)(gfxSurface.materialIndex * 8));
                BO6Material material = Cordycep.ReadMemory<BO6Material>(materialPtr);

                MeshData mesh = ReadMesh(gfxSurface, ugbSurfData, material, zone);

                mapGeoModel.AddNode(mesh.mesh);
                mapGeoModel.AddNode(mesh.material);

                meshes[i] = mesh;
            }
            mapRoot.AddNode(mapGeoModel);*/

            // Static prop xmodels
            BO6GfxWorldStaticModels smodels = gfxWorld.smodels;
            for (int i = 0; i < smodels.collectionsCount; i++)
            {
                BO6GfxStaticModelCollection collection = Cordycep.ReadMemory<BO6GfxStaticModelCollection>(smodels.collections + i * sizeof(BO6GfxStaticModelCollection));
                BO6GfxStaticModel staticModel = Cordycep.ReadMemory<BO6GfxStaticModel>(smodels.models + collection.smodelIndex * sizeof(BO6GfxStaticModel));
                // BO6GfxWorldTransientZone zone = transientZone[collection.transientGfxWorldPlaced];
                BO6XModel xmodel = Cordycep.ReadMemory<BO6XModel>(staticModel.model);

                BO6XModelLodInfo lodInfo = Cordycep.ReadMemory<BO6XModelLodInfo>(xmodel.lodInfo);
                BO6XModelSurfs xmodelSurfs = Cordycep.ReadMemory<BO6XModelSurfs>(lodInfo.modelSurfsStaging);
                BO6XSurfaceShared shared = Cordycep.ReadMemory<BO6XSurfaceShared>(xmodelSurfs.shared);

                XModelMeshData[] xmodelMeshes;
                if (models.TryGetValue(xmodel.hash, out var existingMeshes))
                {
                    xmodelMeshes = existingMeshes;
                }
                else
                {
                    if (shared.data == 0)
                    {
                        byte[] buffer = XSub.ExtractXSubPackage(shared.xpakKey, shared.dataSize);
                        nint sharedPtr = Marshal.AllocHGlobal((int)shared.dataSize);
                        Marshal.Copy(buffer, 0, sharedPtr, (int)shared.dataSize);
                        xmodelMeshes = ReadXModelMeshes(xmodel, sharedPtr, true);
                        Marshal.FreeHGlobal(sharedPtr);
                    }
                    else
                    {
                        xmodelMeshes = ReadXModelMeshes(xmodel, shared.data, false);
                    }

                    models[xmodel.hash] = xmodelMeshes;

                    // Pre-register xmodel materials
                    foreach (var xmodelMesh in xmodelMeshes)
                    {
                        mapGeoModel.AddNode(xmodelMesh.material);
                    }
                }

                string xmodelName = Cordycep.ReadString(xmodel.name);
                xmodelName = xmodelName.Replace("::", "_"); // TODO: Make strings safer
                string propModelPath = @$"{Configuration.EXPORT_PATH}/{mapBaseName}/Props/{xmodelName}.cast";
                CreateDirectoryIfNotExists(Path.GetDirectoryName(propModelPath));

                int lodIdx = -1;
                foreach (var xmodelMesh in xmodelMeshes)
                {
                    lodIdx++;

                    ModelNode propModel = new ModelNode();
                    SkeletonNode propSkeleton = new SkeletonNode();
                    propModel.AddString("n", xmodelName);
                    propModel.AddNode(propSkeleton);
                    propModel.AddNode(xmodelMesh.material);

                    MeshNode meshNode = new MeshNode();
                    meshNode.AddString("n", $"{xmodelName}_lod{lodIdx}");
                    meshNode.AddValue("m", xmodelMesh.material.Hash);
                    CastArrayProperty<Vector3> positions = meshNode.AddArray<Vector3>("vp", new(xmodelMesh.positions.Count));
                    CastArrayProperty<Vector3> normals = meshNode.AddArray<Vector3>("vn", new(xmodelMesh.normals.Count));
                    CastArrayProperty<Vector2> uvs = meshNode.AddArray<Vector2>($"u0", xmodelMesh.uv); // TODO:

                    foreach (var position in xmodelMesh.positions)
                    {
                        positions.Add(position);
                    }

                    foreach (var normal in xmodelMesh.normals)
                    {
                        normals.Add(normal);
                    }

                    CastArrayProperty<ushort> f = meshNode.AddArray<ushort>("f", new(xmodelMesh.faces.Count));
                    foreach (var face in xmodelMesh.faces)
                    {
                        f.Add(face.c); f.Add(face.b); f.Add(face.a);
                    }

                    propModel.AddNode(meshNode);
                    CastNode propModelRoot = new CastNode(CastNodeIdentifier.Root);
                    propModelRoot.AddNode(propModel);
                    CastWriter.Save(propModelPath, propModelRoot);
                }

                int instIdx = -1;
                int instanceId = (int)collection.firstInstance;
                while (instanceId < collection.firstInstance + collection.instanceCount)
                {
                    instIdx++;

                    BO6GfxSModelInstanceData instanceData = Cordycep.ReadMemory<BO6GfxSModelInstanceData>(smodels.instanceData + instanceId * sizeof(BO6GfxSModelInstanceData));

                    // Log.Information("Raw instance data: {instanceData}", BitConverter.ToString(Cordycep.ReadRawMemory((nint)smodels.instanceData + instanceId * sizeof(BO6GfxSModelInstanceData), 24)).Replace("-", ""));
                    Vector3 translation = new Vector3(
                        (float)instanceData.translation[0] * 0.000244140625f,
                        (float)instanceData.translation[1] * 0.000244140625f,
                        (float)instanceData.translation[2] * 0.000244140625f
                    ) * 0.0254f;

                    Quaternion rotation = new Quaternion(
                        Math.Min(Math.Max((float)((float)instanceData.orientation[0] * 0.000030518044f) - 1.0f, -1.0f), 1.0f),
                        Math.Min(Math.Max((float)((float)instanceData.orientation[1] * 0.000030518044f) - 1.0f, -1.0f), 1.0f),
                        Math.Min(Math.Max((float)((float)instanceData.orientation[2] * 0.000030518044f) - 1.0f, -1.0f), 1.0f),
                        Math.Min(Math.Max((float)((float)instanceData.orientation[3] * 0.000030518044f) - 1.0f, -1.0f), 1.0f)
                    );

                    float scale = (float)BitConverter.UInt16BitsToHalf(instanceData.halfFloatScale);

                    // Log.Information("Translation: {translation}, Rotation: {rotation}, Scale: {scale}", translation, rotation, scale);

                    FileNode modelFile = new FileNode();
                    modelFile.Hash = xmodel.hash & 0x0FFFFFFFFFFFFFFF;
                    modelFile.AddString("p", @$"{xmodelName}.cast");

                    InstanceNode inst = new InstanceNode();
                    inst.AddString("n", $"{xmodelName}_{instIdx}");
                    inst.AddValue("rf", modelFile.Hash);
                    inst.AddValue("p", translation);
                    inst.AddValue("r", new Vector4(rotation.X, rotation.Y, rotation.Z, rotation.W));
                    inst.AddValue("s", new Vector3(scale));
                    inst.AddNode(modelFile);
                    mapRoot.AddNode(inst);

                    instanceId++;
                }
            }

            string rootPath = @$"{Configuration.EXPORT_PATH}/{mapBaseName}/{mapBaseName}.cast";
            CreateDirectoryIfNotExists(Path.GetDirectoryName(rootPath));
            CastWriter.Save(rootPath, mapRoot);
        }

        // private static unsafe List<TextureSemanticData> PopulateMaterial(BO6Material material)
        // {
        //     BO6GfxImage[] images = new BO6GfxImage[material.imageCount];
        //
        //     for (int i = 0; i < material.imageCount; i++)
        //     {
        //         nint imagePtr = Cordycep.ReadMemory<nint>(material.imageTable + i * 8);
        //         BO6GfxImage image = Cordycep.ReadMemory<BO6GfxImage>(imagePtr);
        //         images[i] = image;
        //     }
        //
        //     for (int i = 0; i < material.textureCount; i++)
        //     {
        //         BO6MaterialTextureDef textureDef = Cordycep.ReadMemory<BO6MaterialTextureDef>(material.textureTable + i * sizeof(BO6MaterialTextureDef));
        //         BO6GfxImage image = images[textureDef.imageIdx];
        //
        //         int uvMapIndex = 0;
        //     }
        //     return null;
        // }

        private static unsafe MeshData ReadMesh(BO6GfxSurface gfxSurface, BO6GfxUgbSurfData ugbSurfData, BO6Material material, BO6GfxWorldTransientZone zone)
        {
            BO6GfxWorldDrawOffset worldDrawOffset = ugbSurfData.worldDrawOffset;

            MeshNode mesh = new MeshNode();

            ulong materialHash = material.hash & 0x0FFFFFFFFFFFFFFF;
            MaterialNode materialNode = new MaterialNode($"xmaterial_{materialHash:X}", "pbr");
            mesh.AddValue("m", materialNode.Hash);

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
                ulong packedPosition = Cordycep.ReadMemory<ulong>(xyzPtr + j * 8);
                Vector3 position = new Vector3(
                    ((((packedPosition >> 0) & 0x1FFFFF) * worldDrawOffset.scale) + worldDrawOffset.x),
                    ((((packedPosition >> 21) & 0x1FFFFF) * worldDrawOffset.scale) + worldDrawOffset.y),
                    ((((packedPosition >> 42) & 0x1FFFFF) * worldDrawOffset.scale) + worldDrawOffset.z));
                positions.Add(position * 0.0254f);

                uint packedTangentFrame = Cordycep.ReadMemory<uint>(tangentFramePtr + j * 4);
                Vector3 normal = NormalUnpacking.UnpackCoDQTangent(packedTangentFrame);
                normals.Add(normal);

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

            if (ugbSurfData.colorOffset != 0)
            {
                mesh.AddValue("cl", (uint)1);
                CastArrayProperty<uint> colors = mesh.AddArray<uint>("c0", new(gfxSurface.vertexCount));
                nint colorPtr = (nint)(zone.drawVerts.posDataSize + ugbSurfData.colorOffset);
                for (int j = 0; j < gfxSurface.vertexCount; j++)
                {
                    uint color = Cordycep.ReadMemory<uint>(colorPtr + j * 4);
                    colors.Add(color);
                }
            }

            // Unpack face indices
            nint tableOffsetPtr = zone.drawVerts.tableData + (nint)(gfxSurface.tableIndex * 28);
            nint indicesPtr = zone.drawVerts.indices + (nint)(gfxSurface.baseIndex * 2);
            nint packedIndices = zone.drawVerts.packedIndices + (nint)gfxSurface.packedIndicesOffset;

            CastArrayProperty<ushort> faceIndices = mesh.AddArray<ushort>("f", new(gfxSurface.triCount * 3));

            for (int j = 0; j < gfxSurface.triCount; j++)
            {
                ushort[] faces = FaceIndicesUnpacking.UnpackFaceIndicesEx(tableOffsetPtr, gfxSurface.packedIndicesTableCount, packedIndices, indicesPtr, (uint)j);
                faceIndices.Add(faces[2]); faceIndices.Add(faces[1]); faceIndices.Add(faces[0]);
            }

            return new MeshData()
            {
                mesh = mesh,
                material = materialNode,
                // textures = PopulateMaterial(material)
            };
        }

        private static unsafe XModelMeshData[] ReadXModelMeshes(BO6XModel xmodel, nint shared, bool isLocal = false)
        {
            BO6XModelLodInfo lodInfo = Cordycep.ReadMemory<BO6XModelLodInfo>(xmodel.lodInfo);
            BO6XModelSurfs xmodelSurfs = Cordycep.ReadMemory<BO6XModelSurfs>(lodInfo.modelSurfsStaging);
            XModelMeshData[] meshes = new XModelMeshData[/*xmodelSurfs.numSurfs*/1]; // TODO: Only export the biggest lod level now

            // for (int i = 0; i < lodInfo.numSurfs; i++)
            {
                int i = 0; // TODO: Only export the biggest lod level now

                BO6XSurface surface = Cordycep.ReadMemory<BO6XSurface>((nint)xmodelSurfs.surfs + i * sizeof(BO6XSurface));
                BO6Material material = Cordycep.ReadMemory<BO6Material>(Cordycep.ReadMemory<nint>(xmodel.materialHandles + i * 8));

                XModelMeshData mesh = new XModelMeshData() {
                    positions = new(),
                    normals = new(),
                    uv = new(),
                    secondUv = new(),
                    faces = new()
                };

                ulong materialHash = material.hash & 0x0FFFFFFFFFFFFFFF;
                MaterialNode materialNode = new MaterialNode($"xmaterial_{materialHash:X}", "pbr");
                mesh.material = materialNode;
                // mesh.textures = PopulateMaterial(material);

                nint xyzPtr = (nint)(shared + surface.sharedVertDataOffset);
                nint tangentFramePtr = (nint)(shared + surface.sharedTangentFrameDataOffset);
                nint texCoordPtr = (nint)(shared + surface.sharedUVDataOffset);

                float scale = surface.overrideScale != -1f ? surface.overrideScale : Math.Max(Math.Max(surface.surfBounds.halfSize.Y, surface.surfBounds.halfSize.X), surface.surfBounds.halfSize.Z);
                Vector3 offset = surface.overrideScale != -1f ? Vector3.Zero : surface.surfBounds.midPoint;
                for (int j = 0; j < surface.vertCount; j++)
                {
                    ulong packedPosition = Cordycep.ReadMemory<ulong>(xyzPtr + j * 8, isLocal);
                    Vector3 position = new Vector3(
                        (((((packedPosition >> 00) & 0x1FFFFF) * ((1.0f / 0x1FFFFF) * 2.0f)) - 1.0f) * scale) + offset.X,
                        (((((packedPosition >> 21) & 0x1FFFFF) * ((1.0f / 0x1FFFFF) * 2.0f)) - 1.0f) * scale) + offset.Y,
                        (((((packedPosition >> 42) & 0x1FFFFF) * ((1.0f / 0x1FFFFF) * 2.0f)) - 1.0f) * scale) + offset.Z);
                    mesh.positions.Add(position * 0.0254f);

                    uint packedTangentFrame = Cordycep.ReadMemory<uint>(tangentFramePtr + j * 4, isLocal);
                    Vector3 normal = NormalUnpacking.UnpackCoDQTangent(packedTangentFrame);
                    mesh.normals.Add(normal);

                    float uvu = ((float)BitConverter.UInt16BitsToHalf(Cordycep.ReadMemory<ushort>(texCoordPtr + j * 4, isLocal)));
                    float uvv = ((float)BitConverter.UInt16BitsToHalf(Cordycep.ReadMemory<ushort>(texCoordPtr + j * 4 + 2, isLocal)));
                    mesh.uv.Add(new Vector2(uvu, uvv));
                }

                nint tableOffsetPtr = shared + (nint)surface.sharedPackedIndicesTableOffset;
                nint indicesPtr = shared + (nint)surface.sharedIndexDataOffset;
                nint packedIndices = shared + (nint)surface.sharedPackedIndicesOffset;

                for (int j = 0; j < surface.triCount; j++)
                {
                    ushort[] faces = FaceIndicesUnpacking.UnpackFaceIndicesEx(tableOffsetPtr, surface.packedIndicesTableCount, packedIndices, indicesPtr, (uint)j, isLocal);
                    mesh.faces.Add(new Face() { a = faces[0], b = faces[1], c = faces[2] });
                }

                meshes[i] = mesh;
            }

            return meshes;
        }

    }
}
