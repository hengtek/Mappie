using Cast.NET;
using Cast.NET.Nodes;
using DotnesktRemastered.FileStorage;
using DotnesktRemastered.Structures;
using DotnesktRemastered.Utils;
using Serilog;
using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;

namespace DotnesktRemastered.Games
{
    public class BlackOps6 : BaseGame<BO6GfxWorld, BO6GfxWorldTransientZone>
    {
        public BlackOps6()
        {
            GFXMAP_POOL_IDX = 43;
            GFXMAP_TRZONE_POOL_IDX = 0x4F;
        }

        protected override string GameName => "BlackOps6";

        protected override BO6GfxWorld ReadGfxWorld(IntPtr header)
        {
            return Cordycep.ReadMemory<BO6GfxWorld>(header);
        }

        protected override string GetBaseName(BO6GfxWorld gfxWorld)
        {
            return gfxWorld.baseName == 0 ? "" : Cordycep.ReadString(gfxWorld.baseName).Trim();
        }

        public override string[] GetMapList()
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

        protected override unsafe void ReadTransientZones(MapProcessingContext context)
        {
            context.TransientZones = new BO6GfxWorldTransientZone[context.GfxWorld.transientZoneCount];
            for (int i = 0; i < context.GfxWorld.transientZoneCount; i++)
            {
                BO6GfxWorld gfxWorld = context.GfxWorld;
                context.TransientZones[i] = Cordycep.ReadMemory<BO6GfxWorldTransientZone>(
                    (nint)gfxWorld.transientZones[i]);
            }
        }

        protected override unsafe void ProcessSurfaces(MapProcessingContext context)
        {
            BO6GfxWorldSurfaces surfaces = context.GfxWorld.surfaces;
            context.Meshes.Capacity = (int)surfaces.count;

            Log.Information("Reading {count} surfaces...", surfaces.count);
            var stopwatch = Stopwatch.StartNew();

            Parallel.For(0, surfaces.count, i =>
            {
                BO6GfxWorldSurfaces gfxWorldSurfaces = context.GfxWorld.surfaces;
                BO6GfxSurface gfxSurface = Cordycep.ReadMemory<BO6GfxSurface>((nint)(gfxWorldSurfaces.surfaces +
                    i * sizeof(BO6GfxSurface)));
                BO6GfxUgbSurfData ugbSurfData = Cordycep.ReadMemory<BO6GfxUgbSurfData>(
                    gfxWorldSurfaces.ugbSurfData +
                    (nint)(gfxSurface.ugbSurfDataIndex * sizeof(BO6GfxUgbSurfData)));
                BO6GfxWorldTransientZone zone = context.TransientZones[ugbSurfData.transientZoneIndex];
                if (zone.hash == 0) return;
                nint materialPtr =
                    Cordycep.ReadMemory<nint>(gfxWorldSurfaces.materials + (nint)(gfxSurface.materialIndex * 8));
                BO6Material material = Cordycep.ReadMemory<BO6Material>(materialPtr);
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
            BO6GfxWorldStaticModels smodels = context.GfxWorld.smodels;
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
            BO6GfxWorldStaticModels smodels = context.GfxWorld.smodels;

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

        private unsafe void ProcessStaticModelJson(BO6GfxWorldStaticModels smodels, int index,
            MapProcessingContext context)
        {
            BO6GfxStaticModelCollection collection =
                Cordycep.ReadMemory<BO6GfxStaticModelCollection>(smodels.collections +
                                                                 index * sizeof(BO6GfxStaticModelCollection));
            BO6GfxStaticModel staticModel =
                Cordycep.ReadMemory<BO6GfxStaticModel>(smodels.smodels +
                                                       collection.smodelIndex * sizeof(BO6GfxStaticModel));
            BO6GfxWorldTransientZone zone = context.TransientZones[collection.transientGfxWorldPlaced];

            if (zone.hash == 0) return;

            BO6XModel xmodel = Cordycep.ReadMemory<BO6XModel>(staticModel.xmodel);

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
                BO6GfxSModelInstanceData instanceData =
                    Cordycep.ReadMemory<BO6GfxSModelInstanceData>((nint)smodels.instanceData +
                                                                  instanceId *
                                                                  sizeof(BO6GfxSModelInstanceData));

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

        private unsafe void ProcessStaticModelMesh(BO6GfxWorldStaticModels smodels, int index,
            MapProcessingContext context)
        {
            BO6GfxStaticModelCollection collection =
                Cordycep.ReadMemory<BO6GfxStaticModelCollection>(smodels.collections +
                                                                 index * sizeof(BO6GfxStaticModelCollection));
            BO6GfxStaticModel staticModel =
                Cordycep.ReadMemory<BO6GfxStaticModel>(smodels.smodels +
                                                       collection.smodelIndex * sizeof(BO6GfxStaticModel));
            BO6GfxWorldTransientZone zone = context.TransientZones[collection.transientGfxWorldPlaced];

            if (zone.hash == 0) return;

            BO6XModel xmodel = Cordycep.ReadMemory<BO6XModel>(staticModel.xmodel);

            ulong xmodelHash = xmodel.hash & 0x0FFFFFFFFFFFFFFF;

            BO6XModelLodInfo lodInfo = Cordycep.ReadMemory<BO6XModelLodInfo>(xmodel.lodInfo);
            BO6XModelSurfs xmodelSurfs = Cordycep.ReadMemory<BO6XModelSurfs>(lodInfo.modelSurfsStaging);
            BO6XSurfaceShared shared = Cordycep.ReadMemory<BO6XSurfaceShared>(xmodelSurfs.shared);

            XModelMeshData[] xmodelMeshes = new XModelMeshData[0];

            if (_models.ContainsKey(xmodelHash))
            {
                xmodelMeshes = _models[xmodelHash];
            }
            else
            {
                if (shared.data != 0)
                {
                    xmodelMeshes = ReadXModelMeshes(xmodel, shared.data, false);
                }
                else if (shared.data == 0 && XSub.CacheObjects.ContainsKey(shared.xpakKey))
                {
                    byte[] buffer = XSub.ExtractXSubPackage(shared.xpakKey, shared.dataSize);
                    nint sharedPtr = Marshal.AllocHGlobal((int)shared.dataSize);
                    Marshal.Copy(buffer, 0, sharedPtr, (int)shared.dataSize);
                    xmodelMeshes = ReadXModelMeshes(xmodel, (nint)sharedPtr, true);
                    Marshal.FreeHGlobal(sharedPtr);
                }
                else if (shared.data == 0 && CASCPackage.Assets.ContainsKey(shared.xpakKey))
                {
                    byte[] buffer = CASCPackage.ExtractXSubPackage(shared.xpakKey, shared.dataSize);
                    nint sharedPtr = Marshal.AllocHGlobal((int)shared.dataSize);
                    Marshal.Copy(buffer, 0, sharedPtr, (int)shared.dataSize);
                    xmodelMeshes = ReadXModelMeshes(xmodel, (nint)sharedPtr, true);
                    Marshal.FreeHGlobal(sharedPtr);
                }

                _models[xmodelHash] = xmodelMeshes;
            }

            string xmodelName = Cordycep.ReadString(xmodel.name);
            int instanceId = (int)collection.firstInstance;
            while (instanceId < collection.firstInstance + collection.instanceCount)
            {
                BO6GfxSModelInstanceData instanceData =
                    Cordycep.ReadMemory<BO6GfxSModelInstanceData>((nint)smodels.instanceData +
                                                                  instanceId *
                                                                  sizeof(BO6GfxSModelInstanceData));

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


        private MeshData ReadMesh(BO6GfxSurface gfxSurface, BO6GfxUgbSurfData ugbSurfData, BO6Material material,
            BO6GfxWorldTransientZone zone)
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
                // textures = PopulateMaterial(material)
            };
        }

        private unsafe XModelMeshData[] ReadXModelMeshes(BO6XModel xmodel, nint shared, bool isLocal = false)
        {
            BO6XModelLodInfo lodInfo = Cordycep.ReadMemory<BO6XModelLodInfo>(xmodel.lodInfo);
            BO6XModelSurfs xmodelSurfs = Cordycep.ReadMemory<BO6XModelSurfs>(lodInfo.modelSurfsStaging);
            XModelMeshData[]
                meshes = new XModelMeshData[ /*xmodelSurfs.numSurfs*/1]; // TODO: Only export the biggest lod level now

            // for (int i = 0; i < lodInfo.numSurfs; i++)
            {
                int i = 0; // TODO: Only export the biggest lod level now

                BO6XSurface surface =
                    Cordycep.ReadMemory<BO6XSurface>((nint)xmodelSurfs.surfs + i * sizeof(BO6XSurface));
                BO6Material material =
                    Cordycep.ReadMemory<BO6Material>(Cordycep.ReadMemory<nint>(xmodel.materialHandles + i * 8));

                XModelMeshData mesh = new XModelMeshData()
                {
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

                float scale = surface.overrideScale != -1f
                    ? surface.overrideScale
                    : Math.Max(Math.Max(surface.surfBounds.halfSize.Y, surface.surfBounds.halfSize.X),
                        surface.surfBounds.halfSize.Z);
                Vector3 offset = surface.overrideScale != -1f ? Vector3.Zero : surface.surfBounds.midPoint;
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
                    mesh.positions.Add(position * 0.0254f);

                    uint packedTangentFrame = Cordycep.ReadMemory<uint>(tangentFramePtr + j * 4, isLocal);
                    Vector3 normal = NormalUnpacking.UnpackCoDQTangent(packedTangentFrame);
                    mesh.normals.Add(normal);

                    float uvu = ((float)BitConverter.UInt16BitsToHalf(
                        Cordycep.ReadMemory<ushort>(texCoordPtr + j * 4, isLocal)));
                    float uvv = ((float)BitConverter.UInt16BitsToHalf(
                        Cordycep.ReadMemory<ushort>(texCoordPtr + j * 4 + 2, isLocal)));
                    mesh.uv.Add(new Vector2(uvu, uvv));
                }

                nint tableOffsetPtr = shared + (nint)surface.sharedPackedIndicesTableOffset;
                nint indicesPtr = shared + (nint)surface.sharedIndexDataOffset;
                nint packedIndices = shared + (nint)surface.sharedPackedIndicesOffset;

                for (int j = 0; j < surface.triCount; j++)
                {
                    ushort[] faces = FaceIndicesUnpacking.UnpackFaceIndicesEx(tableOffsetPtr,
                        surface.packedIndicesTableCount, packedIndices, indicesPtr, (uint)j, isLocal);
                    mesh.faces.Add(new Face() { a = faces[0], b = faces[1], c = faces[2] });
                }

                meshes[i] = mesh;
            }

            return meshes;
        }
    }
}