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
    public class ModernWarfare6
    {
        public static CordycepProcess Cordycep = Program.Cordycep;

        private static uint GFXMAP_POOL_IDX = 50;

        private static Dictionary<ulong, XModelMeshData[]> models = new Dictionary<ulong, XModelMeshData[]>();
        public static void DumpMap(string name, bool noStaticProps = false, Vector3 staticPropsOrigin = new(), uint range = 0)
        {
            Log.Information("Finding map {baseName}...", name);
            Cordycep.EnumerableAssetPool(GFXMAP_POOL_IDX, (asset) =>
            {
                MW6GfxWorld gfxWorld = Cordycep.ReadMemory<MW6GfxWorld>(asset.Header);
                if (gfxWorld.baseName == 0) return;
                string baseName = Cordycep.ReadString(gfxWorld.baseName).Trim();
                if (baseName == name)
                {
                    Log.Information("Found map {0}, started dumping... :)", baseName);
                    DumpMap(asset.Header, gfxWorld, baseName);
                    Log.Information("Dumped map {0}. XD", baseName);
                }
            });
        }
        public static string[] GetMapList()
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

        private static unsafe void DumpMap(nint asset, MW6GfxWorld gfxWorld, string baseName, bool noStaticProps = false, Vector3 staticPropsOrigin = new(), uint range = 0)
        {
            //Performance test
            Stopwatch stopwatch = new Stopwatch();

            ModelNode baseMeshModel = new ModelNode();
            SkeletonNode baseMeshSkeleton = new SkeletonNode();
            baseMeshModel.AddString("n", $"{baseName}_base_mesh");
            baseMeshModel.AddNode(baseMeshSkeleton);

            ModelNode staticPropsModel = new ModelNode();
            SkeletonNode staticPropsSkeleton = new SkeletonNode();
            staticPropsModel.AddString("n", $"{baseName}_static_props_mesh");
            staticPropsModel.AddNode(staticPropsSkeleton);

            MW6GfxWorldTransientZone[] transientZone = new MW6GfxWorldTransientZone[gfxWorld.transientZoneCount];

            for (int i = 0; i < gfxWorld.transientZoneCount; i++)
            {
                transientZone[i] = Cordycep.ReadMemory<MW6GfxWorldTransientZone>((nint)gfxWorld.transientZones[i]);
            }

            MW6GfxWorldSurfaces gfxWorldSurfaces = gfxWorld.surfaces;
            MeshData[] meshes = new MeshData[gfxWorldSurfaces.count];

            Log.Information("Reading {count} surfaces...", gfxWorldSurfaces.count);
            stopwatch.Start();
            for (int i = 0; i < gfxWorldSurfaces.count; i++)
            {
                Stopwatch surfaceStopwatch = new Stopwatch();
                surfaceStopwatch.Start();
                MW6GfxSurface gfxSurface = Cordycep.ReadMemory<MW6GfxSurface>(gfxWorldSurfaces.surfaces + i * sizeof(MW6GfxSurface));
                MW6GfxUgbSurfData ugbSurfData = Cordycep.ReadMemory<MW6GfxUgbSurfData>(gfxWorldSurfaces.ugbSurfData + (nint)(gfxSurface.ugbSurfDataIndex * sizeof(MW6GfxUgbSurfData)));
                MW6GfxWorldTransientZone zone = transientZone[ugbSurfData.transientZoneIndex];
                nint materialPtr = Cordycep.ReadMemory<nint>(gfxWorldSurfaces.materials + (nint)(gfxSurface.materialIndex * 8));
                MW6Material material = Cordycep.ReadMemory<MW6Material>(materialPtr);

                MeshData mesh = ReadMesh(gfxSurface, ugbSurfData, material, zone);

                baseMeshModel.AddNode(mesh.mesh);
                baseMeshModel.AddNode(mesh.material);

                meshes[i] = mesh;
                surfaceStopwatch.Stop();
                Log.Information("Read surface {i} in {time} ms.", i, surfaceStopwatch.ElapsedMilliseconds);
            }
            stopwatch.Stop();
            Log.Information("Read {count} surfaces in {time} ms.", gfxWorldSurfaces.count, stopwatch.ElapsedMilliseconds);


            if (!noStaticProps)
            {
                stopwatch.Reset();
                Log.Information("Reading {count} static models...", gfxWorld.smodels.collectionsCount);
                stopwatch.Start();

                MW6GfxWorldStaticModels smodels = gfxWorld.smodels;
                for (int i = 0; i < smodels.collectionsCount; i++)
                {
                    MW6GfxStaticModelCollection collection = Cordycep.ReadMemory<MW6GfxStaticModelCollection>(smodels.collections + i * sizeof(MW6GfxStaticModelCollection));
                    MW6GfxStaticModel staticModel = Cordycep.ReadMemory<MW6GfxStaticModel>(smodels.smodels + collection.smodelIndex * sizeof(MW6GfxStaticModel));
                    MW6GfxWorldTransientZone zone = transientZone[collection.transientGfxWorldPlaced];

                    if (zone.hash == 0) continue;

                    MW6XModel xmodel = Cordycep.ReadMemory<MW6XModel>(staticModel.xmodel);

                    ulong xmodelHash = xmodel.hash & 0x0FFFFFFFFFFFFFFF;

                    MW6XModelLodInfo lodInfo = Cordycep.ReadMemory<MW6XModelLodInfo>(xmodel.lodInfo);
                    MW6XModelSurfs xmodelSurfs = Cordycep.ReadMemory<MW6XModelSurfs>(lodInfo.modelSurfsStaging);
                    MW6XSurfaceShared shared = Cordycep.ReadMemory<MW6XSurfaceShared>(xmodelSurfs.shared);

                    XModelMeshData[] xmodelMeshes;
                    if (models.ContainsKey(xmodelHash))
                    {
                        xmodelMeshes = models[xmodelHash];
                    }
                    else
                    {
                        Stopwatch xmodelStopwatch = new Stopwatch();
                        xmodelStopwatch.Start();
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
                        xmodelStopwatch.Stop();
                        Log.Information("Read xmodel {xmodel:X} in {time} ms.", xmodelHash, xmodelStopwatch.ElapsedMilliseconds);
                        models[xmodelHash] = xmodelMeshes;
                        //pre register xmodel materials
                        foreach (var xmodelMesh in xmodelMeshes)
                        {
                            staticPropsModel.AddNode(xmodelMesh.material);
                        }
                    }

                    string xmodelName = Cordycep.ReadString(xmodel.name);
                    int instanceId = (int)collection.firstInstance;
                    while (instanceId < collection.firstInstance + collection.instanceCount)
                    {
                        MW6GfxSModelInstanceData instanceData = Cordycep.ReadMemory<MW6GfxSModelInstanceData>((nint)smodels.instanceData + instanceId * sizeof(MW6GfxSModelInstanceData));

                        Vector3 translation = new Vector3(
                            (float)instanceData.translation[0] * 0.000244140625f,
                            (float)instanceData.translation[1] * 0.000244140625f,
                            (float)instanceData.translation[2] * 0.000244140625f
                        );

                        Vector3 translationForComparison = new Vector3(translation.X, translation.Y, 0);

                        if (range != 0 && Vector3.Distance(translationForComparison, staticPropsOrigin) > range)
                        {
                            instanceId++;
                            continue;
                        }

                        Quaternion rotation = new Quaternion(
                            Math.Min(Math.Max((float)((float)instanceData.orientation[0] * 0.000030518044f) - 1.0f, -1.0f), 1.0f),
                            Math.Min(Math.Max((float)((float)instanceData.orientation[1] * 0.000030518044f) - 1.0f, -1.0f), 1.0f),
                            Math.Min(Math.Max((float)((float)instanceData.orientation[2] * 0.000030518044f) - 1.0f, -1.0f), 1.0f),
                            Math.Min(Math.Max((float)((float)instanceData.orientation[3] * 0.000030518044f) - 1.0f, -1.0f), 1.0f)
                        );

                        float scale = (float)BitConverter.UInt16BitsToHalf(instanceData.halfFloatScale);

                        Matrix4x4 transformation = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation);

                        foreach (var xmodelMesh in xmodelMeshes)
                        {
                            MeshNode meshNode = new MeshNode();
                            meshNode.AddValue("m", xmodelMesh.material.Hash);
                            meshNode.AddValue("ul", (uint)1);

                            CastArrayProperty<Vector3> positions = meshNode.AddArray<Vector3>("vp", new(xmodelMesh.positions.Count));
                            CastArrayProperty<Vector3> normals = meshNode.AddArray<Vector3>("vn", new(xmodelMesh.normals.Count));
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

                            staticPropsModel.AddNode(meshNode);
                        }
                        instanceId++;
                    }
                }
                stopwatch.Stop();
                Log.Information("Read {count} static models in {time} ms.", smodels.collectionsCount, stopwatch.ElapsedMilliseconds);
            }
            stopwatch.Reset();
            Log.Information("Exporting {baseName}...", baseName);
            stopwatch.Start();
            //Exporting cast
            string outputFolder = Path.Join(Environment.CurrentDirectory, baseName);

            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            CastNode root = new CastNode(CastNodeIdentifier.Root);
            root.AddNode(baseMeshModel);
            CastWriter.Save(Path.Join(outputFolder, $"{baseName}_base_mesh.cast"), root);

            root = new CastNode(CastNodeIdentifier.Root);
            root.AddNode(staticPropsModel);
            CastWriter.Save(Path.Join(outputFolder, $"{baseName}_static_props_mesh.cast"), root);

            List<string> exportedImages = new();

            //Exporting materials
            //Base mesh
            foreach (var mesh in meshes)
            {
                string materialName = mesh.material.Name;
                string materialPath = Path.Join(outputFolder, $"{materialName}_images.txt"); //stay consistent with greyhound naming or atleast try to...

                StringBuilder semanticTxt = new StringBuilder();
                semanticTxt.Append("# semantic, image");
                foreach (var texture in mesh.textures)
                {
                    semanticTxt.AppendLine();
                    semanticTxt.Append($"{texture.semantic}, {texture.texture}");

                    if(!exportedImages.Contains(texture.texture))
                    {
                        exportedImages.Add(texture.texture);
                    }
                }
                File.WriteAllText(materialPath, semanticTxt.ToString());
            }

            foreach (var xmodelMesh in models.Values)
            {
                foreach (var mesh in xmodelMesh)
                {
                    string materialName = mesh.material.Name;
                    string materialPath = Path.Join(outputFolder, $"{materialName}_images.txt");

                    StringBuilder semanticTxt = new StringBuilder();
                    semanticTxt.Append("# semantic, image");
                    foreach (var texture in mesh.textures)
                    {
                        semanticTxt.AppendLine();
                        semanticTxt.Append($"{texture.semantic}, {texture.texture}");

                        if (!exportedImages.Contains(texture.texture))
                        {
                            exportedImages.Add(texture.texture);
                        }
                    }
                    File.WriteAllText(materialPath, semanticTxt.ToString());
                }
            }

            //Used for greyhound mass export
            File.WriteAllText(Path.Join(outputFolder, "global_images_list.txt"), string.Join(" ,", exportedImages));

            stopwatch.Stop();
            Log.Information("Exported {baseName} in {time} ms.", baseName, stopwatch.ElapsedMilliseconds);
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

            List<TextureSemanticData> textures = new List<TextureSemanticData>();

            for (int i = 0; i < material.textureCount; i++)
            {
                MW6MaterialTextureDef textureDef = Cordycep.ReadMemory<MW6MaterialTextureDef>(material.textureTable + i * sizeof(MW6MaterialTextureDef));
                MW6GfxImage image = images[textureDef.imageIdx];

                int uvMapIndex = 0;

                ulong hash = image.hash & 0x0FFFFFFFFFFFFFFF;

                if (hash == 0xa882744bc523875 ||
                    hash == 0xc29eeff15212c37 ||
                    hash == 0x8fd10a77ef7cceb ||
                    hash == 0x29f08617872fbdd ||
                    hash == 0xcd365ba04eb6b   ||
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

        private static unsafe MeshData ReadMesh(MW6GfxSurface gfxSurface, MW6GfxUgbSurfData ugbSurfData, MW6Material material,MW6GfxWorldTransientZone zone)
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
            nint packedIndicies = zone.drawVerts.packedIndices + (nint)gfxSurface.packedIndicesOffset;

            CastArrayProperty<ushort> faceIndices = mesh.AddArray<ushort>("f", new(gfxSurface.triCount * 3));

            for (int j = 0; j < gfxSurface.triCount; j++)
            {
                ushort[] faces = FaceIndicesUnpacking.UnpackFaceIndices(tableOffsetPtr, gfxSurface.packedIndiciesTableCount, packedIndicies, indicesPtr, (uint)j);
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


                if (surface.colorOffset != 0xFFFFFFFF)
                {
                    nint colorPtr = shared + (nint)surface.colorOffset;
                    for (int j = 0; j < surface.vertCount; j++)
                    {
                        uint color = Cordycep.ReadMemory<uint>(colorPtr + j * 4, isLocal);
                        mesh.colorVertex.Add(color);
                    }
                }

                if(surface.secondUVOffset != 0xFFFFFFFF)
                {
                    nint texCoord2Ptr = shared + (nint)surface.secondUVOffset;
                    for (int j = 0; j < surface.vertCount; j++)
                    {
                        float uvu = ((float)BitConverter.UInt16BitsToHalf(Cordycep.ReadMemory<ushort>(texCoord2Ptr + j * 4, isLocal)));
                        float uvv = ((float)BitConverter.UInt16BitsToHalf(Cordycep.ReadMemory<ushort>(texCoord2Ptr + j * 4 + 2, isLocal)));
                        mesh.secondUv.Add(new Vector2(uvu, uvv));
                    }
                }

                nint tableOffsetPtr = shared + (nint)surface.packedIndiciesTableOffset;
                nint indicesPtr = shared + (nint)surface.indexDataOffset;
                nint packedIndicies = shared + (nint)surface.packedIndicesOffset;

                for (int j = 0; j < surface.triCount; j++)
                {
                    ushort[] faces = FaceIndicesUnpacking.UnpackFaceIndices(tableOffsetPtr, surface.packedIndiciesTableCount, packedIndicies, indicesPtr, (uint)j, isLocal);
                    mesh.faces.Add(new Face() { a = faces[0], b = faces[1], c = faces[2] });
                }

                meshes[i] = mesh;
            }

            return meshes;
        }
    }
}
