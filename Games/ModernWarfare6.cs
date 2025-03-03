using Cast.NET;
using Cast.NET.Nodes;
using DotnesktRemastered.FileStorage;
using DotnesktRemastered.Structures;
using DotnesktRemastered.Utils;
using Serilog;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace DotnesktRemastered.Games
{
    public class ModernWarfare6 : BaseGame<MW6GfxWorld, MW6GfxWorldTransientZone>
    {
        public ModernWarfare6()
        {
            GFXMAP_POOL_IDX = 50;
            GFXMAP_TRZONE_POOL_IDX = 51;
        }

        protected override string GameName => "ModernWarfare6";

        protected override string GetBaseName(MW6GfxWorld gfxWorld)
        {
            return gfxWorld.baseName == 0 ? "" : Cordycep.ReadString(gfxWorld.baseName).Trim();
        }

        protected override MW6GfxWorld ReadGfxWorld(IntPtr header)
        {
            return Cordycep.ReadMemory<MW6GfxWorld>(header);
        }

        public override string[] GetMapList()
        {
            List<string> maps = new List<string>();
            Cordycep.EnumerableAssetPool(GFXMAP_POOL_IDX, (asset) =>
            {
                MW6GfxWorld gfxWorld = Cordycep.ReadMemory<MW6GfxWorld>(asset.Header);
                if (gfxWorld.baseName == 0) return;
                string baseName = Cordycep.ReadString(gfxWorld.baseName).Trim();
                maps.Add(baseName);
            });
            return maps.ToArray();
        }

        protected override unsafe void ReadTransientZones(MapProcessingContext context)
        {
            context.TransientZones = new MW6GfxWorldTransientZone[context.GfxWorld.transientZoneCount];
            for (int i = 0; i < context.GfxWorld.transientZoneCount; i++)
            {
                MW6GfxWorld gfxWorld = context.GfxWorld;
                context.TransientZones[i] = Cordycep.ReadMemory<MW6GfxWorldTransientZone>(
                    (nint)gfxWorld.transientZones[i]);
            }
        }

        protected override unsafe void ProcessSurfaces(MapProcessingContext context)
        {
            MW6GfxWorldSurfaces surfaces = context.GfxWorld.surfaces;
            context.Meshes.Capacity = (int)surfaces.count;

            Log.Information("Reading {count} surfaces...", surfaces.count);
            var stopwatch = Stopwatch.StartNew();

            Parallel.For(0, surfaces.count, i =>
            {
                MW6GfxWorldSurfaces gfxWorldSurfaces = context.GfxWorld.surfaces;
                MW6GfxSurface gfxSurface = Cordycep.ReadMemory<MW6GfxSurface>((nint)(gfxWorldSurfaces.surfaces +
                    i * sizeof(MW6GfxSurface)));
                MW6GfxUgbSurfData ugbSurfData = Cordycep.ReadMemory<MW6GfxUgbSurfData>(
                    gfxWorldSurfaces.ugbSurfData +
                    (nint)(gfxSurface.ugbSurfDataIndex * sizeof(MW6GfxUgbSurfData)));
                MW6GfxWorldTransientZone zone = context.TransientZones[ugbSurfData.transientZoneIndex];
                if (zone.hash == 0) return;
                nint materialPtr =
                    Cordycep.ReadMemory<nint>(gfxWorldSurfaces.materials + (nint)(gfxSurface.materialIndex * 8));
                MW6Material material = Cordycep.ReadMemory<MW6Material>(materialPtr);
                MeshData mesh = ReadMesh(gfxSurface, ugbSurfData, material, zone);

                lock (context.BaseMeshModel)
                {
                    context.BaseMeshModel.AddNode(mesh.mesh);
                    context.BaseMeshModel.AddNode(mesh.material);
                }

                lock (context.Meshes)
                {
                    context.Meshes.Add(mesh);
                }
            });

            stopwatch.Stop();
            Log.Information("Read {count} surfaces in {time} ms.", surfaces.count, stopwatch.ElapsedMilliseconds);
        }

        protected override void ProcessStaticModelsForJson(MapProcessingContext context)
        {
            MW6GfxWorldStaticModels smodels = context.GfxWorld.smodels;
            Log.Information("Reading {count} static models...", smodels.collectionsCount);
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < smodels.collectionsCount; i++)
            {
                ProcessStaticModelJson(smodels, i, context);
            }

            stopwatch.Stop();
            Log.Information("Read {count} static models in {time} ms.",
                smodels.collectionsCount, stopwatch.ElapsedMilliseconds);
        }

        protected override void ProcessStaticModelsForCast(MapProcessingContext context)
        {
            MW6GfxWorldStaticModels smodels = context.GfxWorld.smodels;

            Log.Information("Reading {count} static models...", smodels.collectionsCount);
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < smodels.collectionsCount; i++)
            {
                ProcessStaticModelMesh(smodels, i, context);
            }

            stopwatch.Stop();
            Log.Information("Read {count} static models in {time} ms.",
                smodels.collectionsCount, stopwatch.ElapsedMilliseconds);
        }

        private unsafe void ProcessStaticModelJson(MW6GfxWorldStaticModels smodels, int index,
            MapProcessingContext context)
        {
            MW6GfxStaticModelCollection collection =
                Cordycep.ReadMemory<MW6GfxStaticModelCollection>(smodels.collections +
                                                                 index * sizeof(MW6GfxStaticModelCollection));
            MW6GfxStaticModel staticModel =
                Cordycep.ReadMemory<MW6GfxStaticModel>(smodels.smodels +
                                                       collection.smodelIndex * sizeof(MW6GfxStaticModel));
            MW6GfxWorldTransientZone zone = context.TransientZones[collection.transientGfxWorldPlaced];

            if (zone.hash == 0) return;

            MW6XModel xmodel = Cordycep.ReadMemory<MW6XModel>(staticModel.xmodel);

            string xmodelName = Cordycep.ReadString(xmodel.name);
            string cleanedName = xmodelName.Trim();
            if (cleanedName.Contains("/"))
            {
                cleanedName = xmodelName.Substring(xmodelName.LastIndexOf('/') + 1);
            }

            if (cleanedName.Contains("::"))
            {
                cleanedName = cleanedName.Substring(cleanedName.LastIndexOf("::") + 2);
            }

            int instanceId = (int)collection.firstInstance;
            while (instanceId < collection.firstInstance + collection.instanceCount)
            {
                MW6GfxSModelInstanceData instanceData =
                    Cordycep.ReadMemory<MW6GfxSModelInstanceData>((nint)smodels.instanceData +
                                                                  instanceId *
                                                                  sizeof(MW6GfxSModelInstanceData));

                Vector3 translation = new Vector3(
                    (float)instanceData.translation[0] * 0.000244140625f,
                    (float)instanceData.translation[1] * 0.000244140625f,
                    (float)instanceData.translation[2] * 0.000244140625f
                );

                Vector3 translationForComparison = new Vector3(translation.X, translation.Y, 0);

                if (context.Range != 0 && Vector3.Distance(translationForComparison, context.StaticPropsOrigin) >
                    context.Range)
                {
                    instanceId++;
                    continue;
                }

                Quaternion rotation = new Quaternion(
                    Math.Min(
                        Math.Max((float)((float)instanceData.orientation[0] * 0.000030518044f) - 1.0f, -1.0f),
                        1.0f),
                    Math.Min(
                        Math.Max((float)((float)instanceData.orientation[1] * 0.000030518044f) - 1.0f, -1.0f),
                        1.0f),
                    Math.Min(
                        Math.Max((float)((float)instanceData.orientation[2] * 0.000030518044f) - 1.0f, -1.0f),
                        1.0f),
                    Math.Min(
                        Math.Max((float)((float)instanceData.orientation[3] * 0.000030518044f) - 1.0f, -1.0f),
                        1.0f)
                );
                rotation = Quaternion.Normalize(rotation);

                float scale = (float)BitConverter.UInt16BitsToHalf(instanceData.halfFloatScale);

                ModelJson modelJson = new ModelJson
                {
                    Name = cleanedName,
                    Location = new LocationData
                    {
                        X = translation.X,
                        Y = translation.Y,
                        Z = translation.Z
                    },
                    Rotation = new RotationData
                    {
                        X = rotation.X,
                        Y = rotation.Y,
                        Z = rotation.Z,
                        W = rotation.W
                    },
                    Scale = scale
                };

                context.ModelsList.Add(modelJson);

                instanceId++;
            }
        }

        private unsafe void ProcessStaticModelMesh(MW6GfxWorldStaticModels smodels, int index,
            MapProcessingContext context)
        {
            MW6GfxStaticModelCollection collection =
                Cordycep.ReadMemory<MW6GfxStaticModelCollection>(smodels.collections +
                                                                 index * sizeof(MW6GfxStaticModelCollection));
            MW6GfxStaticModel staticModel =
                Cordycep.ReadMemory<MW6GfxStaticModel>(smodels.smodels +
                                                       collection.smodelIndex * sizeof(MW6GfxStaticModel));
            MW6GfxWorldTransientZone zone = context.TransientZones[collection.transientGfxWorldPlaced];

            if (zone.hash == 0) return;

            MW6XModel xmodel = Cordycep.ReadMemory<MW6XModel>(staticModel.xmodel);

            ulong xmodelHash = xmodel.hash & 0x0FFFFFFFFFFFFFFF;

            MW6XModelLodInfo lodInfo = Cordycep.ReadMemory<MW6XModelLodInfo>(xmodel.lodInfo);
            MW6XModelSurfs xmodelSurfs = Cordycep.ReadMemory<MW6XModelSurfs>(lodInfo.modelSurfsStaging);
            MW6XSurfaceShared shared = Cordycep.ReadMemory<MW6XSurfaceShared>(xmodelSurfs.shared);

            XModelMeshData[] xmodelMeshes = new XModelMeshData[0];

            if (_models.ContainsKey(xmodelHash))
            {
                xmodelMeshes = _models[xmodelHash];
            }
            else
            {
                // See if we have data from memory
                if (shared.data != 0)
                {
                    xmodelMeshes = ReadXModelMeshes(xmodel, shared.data, false);

                    _models[xmodelHash] = xmodelMeshes;
                }
                // If not, Check XSub Cache
                else if (shared.data == 0 && XSub.CacheObjects.ContainsKey(xmodelSurfs.xpakKey))
                {
                    byte[] buffer = XSub.ExtractXSubPackage(xmodelSurfs.xpakKey, shared.dataSize);
                    nint sharedPtr = Marshal.AllocHGlobal((int)shared.dataSize);
                    Marshal.Copy(buffer, 0, sharedPtr, (int)shared.dataSize);
                    xmodelMeshes = ReadXModelMeshes(xmodel, (nint)sharedPtr, true);
                    Marshal.FreeHGlobal(sharedPtr);

                    _models[xmodelHash] = xmodelMeshes;
                }
                // If XSub wasn't successful, Check CASC Cache
                else if (shared.data == 0 && CASCPackage.Assets.ContainsKey(xmodelSurfs.xpakKey))
                {
                    byte[] buffer = CASCPackage.ExtractXSubPackage(xmodelSurfs.xpakKey, shared.dataSize);
                    nint sharedPtr = Marshal.AllocHGlobal((int)shared.dataSize);
                    Marshal.Copy(buffer, 0, sharedPtr, (int)shared.dataSize);
                    xmodelMeshes = ReadXModelMeshes(xmodel, (nint)sharedPtr, true);
                    Marshal.FreeHGlobal(sharedPtr);

                    _models[xmodelHash] = xmodelMeshes;
                }
            }

            string xmodelName = Cordycep.ReadString(xmodel.name);
            int instanceId = (int)collection.firstInstance;
            while (instanceId < collection.firstInstance + collection.instanceCount)
            {
                MW6GfxSModelInstanceData instanceData =
                    Cordycep.ReadMemory<MW6GfxSModelInstanceData>((nint)smodels.instanceData +
                                                                  instanceId *
                                                                  sizeof(MW6GfxSModelInstanceData));

                Vector3 translation = new Vector3(
                    (float)instanceData.translation[0] * 0.000244140625f,
                    (float)instanceData.translation[1] * 0.000244140625f,
                    (float)instanceData.translation[2] * 0.000244140625f
                );

                Vector3 translationForComparison = new Vector3(translation.X, translation.Y, 0);

                if (context.Range != 0 && Vector3.Distance(translationForComparison, context.StaticPropsOrigin) >
                    context.Range)
                {
                    instanceId++;
                    continue;
                }

                Quaternion rotation = new Quaternion(
                    Math.Min(
                        Math.Max((float)((float)instanceData.orientation[0] * 0.000030518044f) - 1.0f, -1.0f),
                        1.0f),
                    Math.Min(
                        Math.Max((float)((float)instanceData.orientation[1] * 0.000030518044f) - 1.0f, -1.0f),
                        1.0f),
                    Math.Min(
                        Math.Max((float)((float)instanceData.orientation[2] * 0.000030518044f) - 1.0f, -1.0f),
                        1.0f),
                    Math.Min(
                        Math.Max((float)((float)instanceData.orientation[3] * 0.000030518044f) - 1.0f, -1.0f),
                        1.0f)
                );

                float scale = (float)BitConverter.UInt16BitsToHalf(instanceData.halfFloatScale);

                Matrix4x4 transformation = Matrix4x4.CreateScale(scale) *
                                           Matrix4x4.CreateFromQuaternion(rotation) *
                                           Matrix4x4.CreateTranslation(translation);

                foreach (var xmodelMesh in xmodelMeshes)
                {
                    MeshNode meshNode = new MeshNode();
                    meshNode.AddValue("m", xmodelMesh.material.Hash);
                    meshNode.AddValue("ul", (uint)1);

                    CastArrayProperty<Vector3> positions =
                        meshNode.AddArray<Vector3>("vp", new(xmodelMesh.positions.Count));
                    CastArrayProperty<Vector3> normals =
                        meshNode.AddArray<Vector3>("vn", new(xmodelMesh.normals.Count));
                    CastArrayProperty<Vector2> uvs = meshNode.AddArray<Vector2>($"u0", xmodelMesh.uv);

                    if (xmodelMesh.secondUv.Count > 0)
                    {
                        meshNode.AddValue("ul", (uint)2);
                        meshNode.AddArray<Vector2>($"u1", xmodelMesh.secondUv);
                    }

                    if (xmodelMesh.colorVertex.Count > 0)
                    {
                        meshNode.AddValue("cl", (uint)1);
                        meshNode.AddArray<uint>($"c0", xmodelMesh.colorVertex);
                    }

                    foreach (var position in xmodelMesh.positions)
                    {
                        positions.Add(Vector3.Transform(position, transformation));
                    }

                    foreach (var normal in xmodelMesh.normals)
                    {
                        normals.Add(Vector3.TransformNormal(normal, transformation));
                    }

                    CastArrayProperty<ushort> f = meshNode.AddArray<ushort>("f", new(xmodelMesh.faces.Count));
                    foreach (var face in xmodelMesh.faces)
                    {
                        f.Add(face.c);
                        f.Add(face.b);
                        f.Add(face.a);
                    }

                    context.StaticModel.AddNode(meshNode);
                    context.StaticModel.AddNode(xmodelMesh.material);
                }

                instanceId++;
            }
        }

        private unsafe List<TextureSemanticData> PopulateMaterial(MW6Material material)
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

                if (hash == 0xa882744bc523875 ||
                    hash == 0xc29eeff15212c37 ||
                    hash == 0x8fd10a77ef7cceb ||
                    hash == 0x29f08617872fbdd ||
                    hash == 0xcd365ba04eb6b ||
                    hash == 0xc2d1c3e952cb190 ||
                    hash == 0x2ca20d05140bbf8 ||
                    hash == 0xc979d3a4845195f ||
                    hash == 0xcdfbff57d64fc0d ||
                    hash == 0x8b3d69e4258c738 ||
                    hash == 0x859d988746fc4e8 ||
                    hash == 0xebe9c97e3c8c029) continue; //pretty sure its prob a null texture lol

                string imageName = $"ximage_{hash:X}".ToLower();

                //instead of using actual semantic value, we can guess them base on the texture index, hf

                string textureSemantic;
                if (!Enum.IsDefined(typeof(MW6TextureIdxTable), (int)textureDef.index))
                {
                    textureSemantic = $"unknown_texture_{textureDef.index}";
                }
                else
                {
                    textureSemantic = ((MW6TextureIdxTable)textureDef.index).ToString().ToLower();
                }

                textures.Add(new()
                {
                    semantic = textureSemantic,
                    texture = imageName
                });
            }

            return textures;
        }

        private MeshData ReadMesh(MW6GfxSurface gfxSurface, MW6GfxUgbSurfData ugbSurfData,
            MW6Material material, MW6GfxWorldTransientZone zone)
        {
            MW6GfxWorldDrawOffset worldDrawOffset = ugbSurfData.worldDrawOffset;

            MeshNode mesh = new MeshNode();

            ulong materialHash = material.hash & 0x0FFFFFFFFFFFFFFF;
            MaterialNode materialNode = new MaterialNode($"xmaterial_{materialHash:X}".ToLower(), "pbr");
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

                positions.Add(position);

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
                nint colorPtr = (nint)(zone.drawVerts.posSize + ugbSurfData.colorOffset);
                for (int j = 0; j < gfxSurface.vertexCount; j++)
                {
                    uint color = Cordycep.ReadMemory<uint>(colorPtr + j * 4);
                    colors.Add(color);
                }
            }

            //unpack da fucking faces 
            //References: https://github.com/Scobalula/Greyhound/blob/master/src/WraithXCOD/WraithXCOD/CoDXModelMeshHelper.cpp#L37

            nint tableOffsetPtr = zone.drawVerts.tableData + (nint)(gfxSurface.tableIndex * 40);
            nint indicesPtr = zone.drawVerts.indices + (nint)(gfxSurface.baseIndex * 2);
            nint packedIndices = zone.drawVerts.packedIndices + (nint)gfxSurface.packedIndicesOffset;

            CastArrayProperty<ushort> faceIndices = mesh.AddArray<ushort>("f", new(gfxSurface.triCount * 3));
            ushort[] faceIndicesArray = new ushort[gfxSurface.triCount * 3];

            Parallel.For(0, gfxSurface.triCount, j =>
            {
                ushort[] faces = FaceIndicesUnpacking.UnpackFaceIndices(tableOffsetPtr,
                    gfxSurface.packedIndicesTableCount, packedIndices, indicesPtr, (uint)j);
                int index = j * 3;
                faceIndicesArray[index] = faces[2];
                faceIndicesArray[index + 1] = faces[1];
                faceIndicesArray[index + 2] = faces[0];
            });

            faceIndices.AddRange(faceIndicesArray);

            return new MeshData()
            {
                mesh = mesh,
                material = materialNode,
                textures = PopulateMaterial(material)
            };
        }

        private unsafe XModelMeshData[] ReadXModelMeshes(MW6XModel xmodel, nint shared, bool isLocal = false)
        {
            MW6XModelLodInfo lodInfo = Cordycep.ReadMemory<MW6XModelLodInfo>(xmodel.lodInfo);
            MW6XModelSurfs xmodelSurfs = Cordycep.ReadMemory<MW6XModelSurfs>(lodInfo.modelSurfsStaging);
            XModelMeshData[] meshes = new XModelMeshData[xmodelSurfs.numsurfs];

            for (int i = 0; i < lodInfo.numsurfs; i++)
            {
                MW6XSurface surface =
                    Cordycep.ReadMemory<MW6XSurface>((nint)xmodelSurfs.surfs + i * sizeof(MW6XSurface));
                MW6Material material =
                    Cordycep.ReadMemory<MW6Material>(Cordycep.ReadMemory<nint>(xmodel.materialHandles + i * 8));

                XModelMeshData mesh = new XModelMeshData()
                {
                    positions = new(),
                    normals = new(),
                    uv = new(),
                    secondUv = new(),
                    faces = new(),
                    colorVertex = new()
                };

                ulong materialHash = material.hash & 0x0FFFFFFFFFFFFFFF;
                MaterialNode materialNode = new MaterialNode($"xmaterial_{materialHash:X}".ToLower(), "pbr");
                mesh.material = materialNode;
                mesh.textures = PopulateMaterial(material);

                nint xyzPtr = (nint)(shared + surface.xyzOffset);
                nint tangentFramePtr = (nint)(shared + surface.tangentFrameOffset);
                nint texCoordPtr = (nint)(shared + surface.texCoordOffset);

                float scale = surface.overrideScale != -1
                    ? surface.overrideScale
                    : Math.Max(Math.Max(surface.min, surface.scale), surface.max);
                Vector3 offset = surface.overrideScale != -1 ? Vector3.Zero : surface.offsets;
                for (int j = 0; j < surface.vertCount; j++)
                {
                    ulong packedPosition = Cordycep.ReadMemory<ulong>(xyzPtr + j * 8, isLocal);
                    Vector3 position = new Vector3(
                        (((((packedPosition >> 00) & 0x1FFFFF) * ((1.0f / 0x1FFFFF) * 2.0f)) - 1.0f) * scale) +
                        offset.X,
                        (((((packedPosition >> 21) & 0x1FFFFF) * ((1.0f / 0x1FFFFF) * 2.0f)) - 1.0f) * scale) +
                        offset.Y,
                        (((((packedPosition >> 42) & 0x1FFFFF) * ((1.0f / 0x1FFFFF) * 2.0f)) - 1.0f) * scale) +
                        offset.Z);

                    mesh.positions.Add(position);

                    uint packedTangentFrame = Cordycep.ReadMemory<uint>(tangentFramePtr + j * 4, isLocal);
                    Vector3 normal = NormalUnpacking.UnpackCoDQTangent(packedTangentFrame);

                    mesh.normals.Add(normal);

                    float uvu = ((float)BitConverter.UInt16BitsToHalf(
                        Cordycep.ReadMemory<ushort>(texCoordPtr + j * 4, isLocal)));
                    float uvv = ((float)BitConverter.UInt16BitsToHalf(
                        Cordycep.ReadMemory<ushort>(texCoordPtr + j * 4 + 2, isLocal)));

                    mesh.uv.Add(new Vector2(uvu, uvv));
                }


                if (surface.colorOffset != 0xFFFFFFFF)
                {
                    nint colorPtr = shared + (nint)surface.colorOffset;
                    for (int j = 0; j < surface.vertCount; j++)
                    {
                        uint color = Cordycep.ReadMemory<uint>(colorPtr + j * 4, isLocal);
                        mesh.colorVertex.Add(color);
                    }
                }

                if (surface.secondUVOffset != 0xFFFFFFFF)
                {
                    nint texCoord2Ptr = shared + (nint)surface.secondUVOffset;
                    for (int j = 0; j < surface.vertCount; j++)
                    {
                        float uvu = ((float)BitConverter.UInt16BitsToHalf(
                            Cordycep.ReadMemory<ushort>(texCoord2Ptr + j * 4, isLocal)));
                        float uvv = ((float)BitConverter.UInt16BitsToHalf(
                            Cordycep.ReadMemory<ushort>(texCoord2Ptr + j * 4 + 2, isLocal)));
                        mesh.secondUv.Add(new Vector2(uvu, uvv));
                    }
                }

                nint tableOffsetPtr = shared + (nint)surface.packedIndiciesTableOffset;
                nint indicesPtr = shared + (nint)surface.indexDataOffset;
                nint packedIndicies = shared + (nint)surface.packedIndicesOffset;

                for (int j = 0; j < surface.triCount; j++)
                {
                    ushort[] faces = FaceIndicesUnpacking.UnpackFaceIndices(tableOffsetPtr,
                        surface.packedIndicesTableCount, packedIndicies, indicesPtr, (uint)j, isLocal);
                    mesh.faces.Add(new Face() { a = faces[0], b = faces[1], c = faces[2] });
                }

                meshes[i] = mesh;
            }

            return meshes;
        }
    }
}