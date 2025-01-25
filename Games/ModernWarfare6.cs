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
    public class ModernWarfare6
    {
        public static CordycepProcess Cordycep = Program.Cordycep;

        private static uint GFXMAP_POOL_IDX = 50;

        private static Dictionary<ulong, XModelMeshData[]> models = new Dictionary<ulong, XModelMeshData[]>();
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

            ModelNode model = new ModelNode();
            SkeletonNode skeleton = new SkeletonNode();
            model.AddString("n", $"{baseName}_base_mesh");
            model.AddNode(skeleton);

            MW6GfxWorldTransientZone[] transientZone = new MW6GfxWorldTransientZone[gfxWorld.transientZoneCount];
            for (int i = 0; i < gfxWorld.transientZoneCount; i++)
            {
                transientZone[i] = Cordycep.ReadMemory<MW6GfxWorldTransientZone>(gfxWorld.transientZones + i * sizeof(MW6GfxWorldTransientZone));
            }
            MW6GfxWorldSurfaces gfxWorldSurfaces = gfxWorld.surfaces;

            MeshData[] meshes = new MeshData[gfxWorldSurfaces.count];
            for (int i = 0; i < gfxWorldSurfaces.count; i++)
            {
                MW6GfxSurface gfxSurface = Cordycep.ReadMemory<MW6GfxSurface>(gfxWorldSurfaces.surfaces + i * sizeof(MW6GfxSurface));
                MW6GfxUgbSurfData ugbSurfData = Cordycep.ReadMemory<MW6GfxUgbSurfData>(gfxWorldSurfaces.ugbSurfData + (nint)(gfxSurface.ugbSurfDataIndex * sizeof(MW6GfxUgbSurfData)));
                MW6GfxWorldTransientZone zone = transientZone[ugbSurfData.transientZoneIndex];
                nint materialPtr = Cordycep.ReadMemory<nint>(gfxWorldSurfaces.materials + (nint)(gfxSurface.materialIndex * 8));
                MW6Material material = Cordycep.ReadMemory<MW6Material>(materialPtr);

                MeshData mesh = ReadMesh(gfxSurface, ugbSurfData, material, zone);

                model.AddNode(mesh.mesh);
                model.AddNode(mesh.material);

                meshes[i] = mesh;
            }

            MW6GfxWorldStaticModels smodels = gfxWorld.smodels;

            for (int i = 0; i < smodels.collectionsCount; i++)
            {
                MW6GfxStaticModelCollection collection = Cordycep.ReadMemory<MW6GfxStaticModelCollection>(smodels.collections + i * sizeof(MW6GfxStaticModelCollection));
                MW6GfxStaticModel staticModel = Cordycep.ReadMemory<MW6GfxStaticModel>(smodels.smodels + collection.smodelIndex * sizeof(MW6GfxStaticModel));
                MW6GfxWorldTransientZone zone = transientZone[collection.transientGfxWorldPlaced];
                MW6XModel xmodel = Cordycep.ReadMemory<MW6XModel>(staticModel.xmodel);

                MW6XModelLodInfo lodInfo = Cordycep.ReadMemory<MW6XModelLodInfo>(xmodel.lodInfo);
                MW6XModelSurfs xmodelSurfs = Cordycep.ReadMemory<MW6XModelSurfs>(lodInfo.modelSurfsStaging);
                MW6XSurfaceShared shared = Cordycep.ReadMemory<MW6XSurfaceShared>(xmodelSurfs.shared);

                XModelMeshData[] xmodelMeshes;
                if (models.ContainsKey(xmodel.hash))
                {
                    xmodelMeshes = models[xmodel.hash];
                }
                else
                {
                    if (shared.data == 0)
                    {
                        byte[] buffer = XSub.ExtractXSubPackage(xmodelSurfs.xpakKey, shared.dataSize);
                        nint sharedPtr = Marshal.AllocHGlobal((int)shared.dataSize);
                        Marshal.Copy(buffer, 0, sharedPtr, (int)shared.dataSize);
                        xmodelMeshes = ReadXModelMeshes(xmodel, (nint)sharedPtr, true);
                        Marshal.FreeHGlobal(sharedPtr);
                    }
                    else
                    {
                        xmodelMeshes = ReadXModelMeshes(xmodel, shared.data, false);
                    }
                    models[xmodel.hash] = xmodelMeshes;
                    //pre register xmodel materials
                    foreach (var xmodelMesh in xmodelMeshes)
                    {
                        model.AddNode(xmodelMesh.material);
                    }
                }

                string xmodelName = Cordycep.ReadString(xmodel.name);
                int instanceId = (int)collection.firstInstance;
                while (instanceId < collection.firstInstance + collection.instanceCount)
                {
                    MW6GfxSModelInstanceData instanceData = Cordycep.ReadMemory<MW6GfxSModelInstanceData>((nint)smodels.instanceData + instanceId * sizeof(MW6GfxSModelInstanceData));

                   // Log.Information("Raw instance data: {instanceData}", BitConverter.ToString(Cordycep.ReadRawMemory((nint)smodels.instanceData + instanceId * sizeof(MW6GfxSModelInstanceData), 24)).Replace("-", ""));
                    Vector3 translation = new Vector3(
                        (float)instanceData.translation[0] * 0.000244140625f,
                        (float)instanceData.translation[1] * 0.000244140625f,
                        (float)instanceData.translation[2] * 0.000244140625f
                    );

                    /*
                     * tf
                    float scaleUint;

                    var v23 = (instanceData.packedScale & 0xFFFF8000) << 16;
                    var v24 = instanceData.packedScale & 0x3FF;
                    var result = (instanceData.packedScale >> 10) & 31;

                    if (result != 0)
                    {
                        result = v23 | (v24 << 13) | ((result << 23) + 0x38000000);
                        scaleUint = result;
                    }
                    else
                    {
                        scaleUint = v23 | 0x38800000;
                    }
                    */
                    Quaternion rotation = new Quaternion(
                        Math.Min(Math.Max((float)((float)instanceData.orientation[0] * 0.000030518044f) - 1.0f, -1.0f), 1.0f),
                        Math.Min(Math.Max((float)((float)instanceData.orientation[1] * 0.000030518044f) - 1.0f, -1.0f), 1.0f),
                        Math.Min(Math.Max((float)((float)instanceData.orientation[2] * 0.000030518044f) - 1.0f, -1.0f), 1.0f),
                        Math.Min(Math.Max((float)((float)instanceData.orientation[3] * 0.000030518044f) - 1.0f, -1.0f), 1.0f)
                    );

                    Matrix4x4 transformation = Matrix4x4.CreateScale(xmodel.scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation);

                    foreach (var xmodelMesh in xmodelMeshes)
                    {
                        MeshNode meshNode = new MeshNode();
                        meshNode.AddValue("m", xmodelMesh.material.Hash);
                        CastArrayProperty<Vector3> positions = meshNode.AddArray<Vector3>("vp", new(xmodelMesh.positions.Count));
                        CastArrayProperty<Vector3> normals = meshNode.AddArray<Vector3>("vn", new(xmodelMesh.normals.Count));
                        CastArrayProperty<Vector2> uvs = meshNode.AddArray<Vector2>($"u0", xmodelMesh.uv);

                        foreach (var position in xmodelMesh.positions)
                        {
                            positions.Add(Vector3.Transform(position, transformation));
                        }

                        foreach (var normal in xmodelMesh.normals)
                        {
                            normals.Add(Vector3.TransformNormal(normal, transformation));
                        }

                        CastArrayProperty<ushort> f =meshNode.AddArray<ushort>("f", new(xmodelMesh.faces.Count));
                        foreach (var face in xmodelMesh.faces)
                        {
                            f.Add(face.c);
                            f.Add(face.b);
                            f.Add(face.a);
                        }

                        model.AddNode(meshNode);
                    }
                    instanceId++;
                }
            }
            CastNode root = new CastNode(CastNodeIdentifier.Root);
            root.AddNode(model);
            CastWriter.Save(@"D:/" + baseName + ".cast", root);
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

        private static unsafe MeshData ReadMesh(MW6GfxSurface gfxSurface, MW6GfxUgbSurfData ugbSurfData, MW6Material material,MW6GfxWorldTransientZone zone)
        {
            MW6GfxWorldDrawOffset worldDrawOffset = ugbSurfData.worldDrawOffset;

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

            nint tableOffsetPtr = zone.drawVerts.tableData + (nint)(gfxSurface.tableOffset * 40);
            nint indicesPtr = zone.drawVerts.indices + (nint)(gfxSurface.baseIndex * 2);
            nint packedIndicies = zone.drawVerts.packedIndices + (nint)gfxSurface.packedIndicesOffset;

            CastArrayProperty<ushort> faceIndices = mesh.AddArray<ushort>("f", new(gfxSurface.triCount * 3));

            for (int j = 0; j < gfxSurface.triCount; j++)
            {
                ushort[] faces = MW6FaceIndices.UnpackFaceIndices(tableOffsetPtr, gfxSurface.packedIndiciesTableCount, packedIndicies, indicesPtr, (uint)j);
                faceIndices.Add(faces[2]);
                faceIndices.Add(faces[1]);
                faceIndices.Add(faces[0]);
            }

            return new MeshData()
            {
                mesh = mesh,
                material = materialNode,
                textures = PopulateMaterial(material)
            };
        }

        private static unsafe XModelMeshData[] ReadXModelMeshes(MW6XModel xmodel, nint shared, bool isLocal = false)
        {
            MW6XModelLodInfo lodInfo = Cordycep.ReadMemory<MW6XModelLodInfo>(xmodel.lodInfo);
            MW6XModelSurfs xmodelSurfs = Cordycep.ReadMemory<MW6XModelSurfs>(lodInfo.modelSurfsStaging);
            XModelMeshData[] meshes = new XModelMeshData[xmodelSurfs.numsurfs];

            for (int i = 0; i < lodInfo.numsurfs; i++)
            {
                MW6XSurface surface = Cordycep.ReadMemory<MW6XSurface>((nint)xmodelSurfs.surfs + i * sizeof(MW6XSurface));
                MW6Material material = Cordycep.ReadMemory<MW6Material>(Cordycep.ReadMemory<nint>(xmodel.materialHandles + i * 8));

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
                mesh.textures = PopulateMaterial(material);

                nint xyzPtr = (nint)(shared + surface.xyzOffset);
                nint tangentFramePtr = (nint)(shared + surface.tangentFrameOffset);
                nint texCoordPtr = (nint)(shared + surface.texCoordOffset);

                float scale = surface.overrideScale != -1 ? surface.overrideScale : Math.Max(Math.Max(surface.min, surface.scale), surface.max);
                Vector3 offset = surface.overrideScale != -1 ? Vector3.Zero : surface.offsets;
                for (int j = 0; j < surface.vertCount; j++)
                {
                    ulong packedPosition = Cordycep.ReadMemory<ulong>(xyzPtr + j * 8, isLocal);
                    Vector3 position = new Vector3(
                        (((((packedPosition >> 00) & 0x1FFFFF) * ((1.0f / 0x1FFFFF) * 2.0f)) - 1.0f) * scale) + offset.X,
                        (((((packedPosition >> 21) & 0x1FFFFF) * ((1.0f / 0x1FFFFF) * 2.0f)) - 1.0f) * scale) + offset.Y,
                        (((((packedPosition >> 42) & 0x1FFFFF) * ((1.0f / 0x1FFFFF) * 2.0f)) - 1.0f) * scale) + offset.Z);

                    mesh.positions.Add(position);

                    uint packedTangentFrame = Cordycep.ReadMemory<uint>(tangentFramePtr + j * 4, isLocal);
                    Vector3 normal = NormalUnpacking.UnpackCoDQTangent(packedTangentFrame);

                    mesh.normals.Add(normal);

                    float uvu = ((float)BitConverter.UInt16BitsToHalf(Cordycep.ReadMemory<ushort>(texCoordPtr + j * 4, isLocal)));
                    float uvv = ((float)BitConverter.UInt16BitsToHalf(Cordycep.ReadMemory<ushort>(texCoordPtr + j * 4 + 2, isLocal)));

                    mesh.uv.Add(new Vector2(uvu, uvv));
                }

                nint tableOffsetPtr = shared + (nint)surface.packedIndiciesTableOffset;
                nint indicesPtr = shared + (nint)surface.indexDataOffset;
                nint packedIndicies = shared + (nint)surface.packedIndicesOffset;

                for (int j = 0; j < surface.triCount; j++)
                {
                    ushort[] faces = MW6FaceIndices.UnpackFaceIndices(tableOffsetPtr, surface.packedIndiciesTableCount, packedIndicies, indicesPtr, (uint)j, isLocal);
                    mesh.faces.Add(new Face() { a = faces[0], b = faces[1], c = faces[2] });
                }

                meshes[i] = mesh;
            }

            return meshes;
        }
    }
}
