using Cast.NET;
using Cast.NET.Nodes;
using Mappie.FileStorage;
using Mappie.Structures;
using Mappie.Utils;
using Serilog;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Xml.Linq;

namespace Mappie.Games
{
    public abstract class BaseGame
    {
        public abstract void DumpMap(string name, bool noProps = false, Vector3 propsOrigin = new(), uint range = 0,
            bool onlyJson = false);

        public abstract string[] GetMapList();
    }

    public abstract class BaseGame<TGfxWorld, TGfxWorldTransientZone, TWorldSurfaces, TGfxSurface, TGfxUgbSurfData,
        TMaterial, TGfxWorldStaticModels, TGfxStaticModelCollection, TGfxStaticModel, TXModel,
        TGfxSModelInstanceData, TGfxWorldDrawOffset, TGfxWorldDrawVerts, TXModelLodInfo, TXModelSurfs,
        TXSurfaceShared, TXSurface> : BaseGame
        where TGfxWorld : unmanaged, IGfxWorld<TWorldSurfaces, TGfxWorldStaticModels>
        where TGfxWorldTransientZone : unmanaged, IGfxWorldTransientZone<TGfxWorldDrawVerts>
        where TWorldSurfaces : unmanaged, IGfxWorldSurfaces
        where TGfxSurface : unmanaged, IGfxSurface
        where TGfxUgbSurfData : unmanaged, IGfxUgbSurfData<TGfxWorldDrawOffset>
        where TMaterial : unmanaged, IMaterial
        where TGfxWorldStaticModels : unmanaged, IGfxWorldStaticModels
        where TGfxStaticModelCollection : unmanaged, IGfxStaticModelCollection
        where TGfxStaticModel : unmanaged, IGfxStaticModel
        where TXModel : unmanaged, IXModel
        where TGfxSModelInstanceData : unmanaged, IGfxSModelInstanceData
        where TGfxWorldDrawOffset : unmanaged, IGfxWorldDrawOffset
        where TGfxWorldDrawVerts : unmanaged, IGfxWorldDrawVerts
        where TXModelLodInfo : unmanaged, IXModelLod
        where TXModelSurfs : unmanaged, IXModelSurfs
        where TXSurfaceShared : unmanaged, IXSurfaceShared
        where TXSurface : unmanaged, IXSurface
    {
        protected CordycepProcess Cordycep = Program.Cordycep;

        protected uint GFXMAP_POOL_IDX = 0;
        protected uint GFXMAP_TRZONE_POOL_IDX = 0;

        protected Dictionary<ulong, XModelMeshData> _models = new();

        protected abstract string GameName { get; }

        protected float MeshPositionScale = 1f;

        private TGfxWorld ReadGfxWorld(IntPtr header)
        {
            return Cordycep.ReadMemory<TGfxWorld>(header);
        }

        public override void DumpMap(string name, bool noProps = false, Vector3 propsOrigin = new(), uint range = 0,
            bool onlyJson = false)
        {
            Log.Information("[{0}] Finding map {1}...", GameName, name);
            bool found = false;

            Cordycep.EnumerableAssetPool(GFXMAP_POOL_IDX, (asset) =>
            {
                TGfxWorld gfxWorld = ReadGfxWorld(asset.Header);
                if (GetBaseName(gfxWorld) == name)
                {
                    Log.Information("[{0}] Found map {1}, started dumping...", GameName, name);
                    DumpMap(asset.Header, gfxWorld, name, noProps, propsOrigin, range, onlyJson);
                    found = true;
                }
            });

            if (!found)
            {
                Log.Error("[{0}] Map {1} not found.", GameName, name);
            }
        }

        public override string[] GetMapList()
        {
            List<string> maps = new List<string>();
            Cordycep.EnumerableAssetPool(GFXMAP_POOL_IDX, (asset) =>
            {
                TGfxWorld gfxWorld = Cordycep.ReadMemory<TGfxWorld>(asset.Header);
                if (gfxWorld.baseName == 0) return;
                string baseName = Cordycep.ReadString(gfxWorld.baseName).Trim();
                maps.Add(baseName);
            });
            return maps.ToArray();
        }

        private string GetBaseName(TGfxWorld gfxWorld)
        {
            return gfxWorld.baseName == 0 ? "" : Cordycep.ReadString(gfxWorld.baseName).Trim();
        }

        public class ModelJson
        {
            public string Name { get; set; }
            public LocationData Location { get; set; }
            public RotationData Rotation { get; set; }
            public float Scale { get; set; }
        }

        public class LocationData
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
        }

        public class RotationData
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public float W { get; set; }
        }

        public class MapProcessingContext
        {
            public TGfxWorld GfxWorld { get; set; }
            public string BaseName { get; set; }
            public bool NoProps { get; set; }
            public Vector3 StaticPropsOrigin { get; set; }
            public uint Range { get; set; }
            public TGfxWorldTransientZone[] TransientZones { get; set; }
            public ModelNode BaseMeshModel { get; set; }
            public ModelNode StaticModel { get; set; }

            public SkeletonNode BaseMeshSkeleton { get; set; }
            public List<ModelJson> ModelsList { get; } = new();
            public List<MeshData> Meshes { get; } = new();
        }

        private void ProcessMapCommon(
            MapProcessingContext context, Action<MapProcessingContext> modelProcessor,
            Action<MapProcessingContext> exportHandler)
        {
            try
            {
                InitializeBaseModel(context);
                ReadTransientZones(context);
                ProcessSurfaces(context);

                if (!context.NoProps)
                {
                    modelProcessor(context);
                }

                exportHandler(context);
                Log.Information("[{0}] Dumped map {1}.", GameName, context.BaseName);
            }
            catch(Exception e)
            {
                Log.Error(e, "Error processing map {name}", context.BaseName);
            }
        }

        private void InitializeBaseModel(MapProcessingContext context)
        {
            context.BaseMeshModel = InitializeModel($"{context.BaseName}_base_mesh");
            context.StaticModel = InitializeModel($"{context.BaseName}__props_mesh");
        }

        private ModelNode InitializeModel(string baseName)
        {
            ModelNode PropsModel = new ModelNode();
            SkeletonNode PropsSkeleton = new SkeletonNode();
            PropsModel.AddString("n", baseName);
            PropsModel.AddNode(PropsSkeleton);
            return PropsModel;
        }

        protected void ReadTransientZones(MapProcessingContext context)
        {
            context.TransientZones = new TGfxWorldTransientZone[context.GfxWorld.transientZoneCount];
            for (int i = 0; i < context.GfxWorld.transientZoneCount; i++)
            {
                TGfxWorld gfxWorld = context.GfxWorld;
                context.TransientZones[i] =
                    Cordycep.ReadMemory<TGfxWorldTransientZone>((nint)gfxWorld.transientZones[i]);
            }
        }

        private unsafe void ProcessSurfaces(MapProcessingContext context)
        {
            TWorldSurfaces gfxWorldSurfaces = context.GfxWorld.surfaces;
            context.Meshes.Capacity = (int)gfxWorldSurfaces.count;

            Log.Information("Reading {count} surfaces...", gfxWorldSurfaces.count);
            var stopwatch = Stopwatch.StartNew();

            var exceptions = new ConcurrentQueue<Exception>();

            Parallel.For(0, gfxWorldSurfaces.count, i =>
            {
                try
                {
                    TGfxSurface gfxSurface = Cordycep.ReadMemory<TGfxSurface>((nint)(gfxWorldSurfaces.surfaces +
                    i * sizeof(TGfxSurface)));
                    TGfxUgbSurfData ugbSurfData = Cordycep.ReadMemory<TGfxUgbSurfData>(
                        gfxWorldSurfaces.ugbSurfData +
                        (nint)(gfxSurface.ugbSurfDataIndex * sizeof(TGfxUgbSurfData)));
                    TGfxWorldTransientZone zone = context.TransientZones[ugbSurfData.transientZoneIndex];
                    if (zone.hash == 0) return;
                    nint materialPtr =
                        Cordycep.ReadMemory<nint>(gfxWorldSurfaces.materials + (nint)(gfxSurface.materialIndex * 8));
                    TMaterial material = Cordycep.ReadMemory<TMaterial>(materialPtr);
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
                }
                catch (Exception e)
                {
                    exceptions.Enqueue(e);
                }
            });

            if (!exceptions.IsEmpty)
            {
                throw new AggregateException("Error processing surfaces.", exceptions.First());
            }

            stopwatch.Stop();
            Log.Information("Read {count} surfaces in {time} ms.", gfxWorldSurfaces.count, stopwatch.ElapsedMilliseconds);
        }

        private void ProcessStaticModelsForJson(MapProcessingContext context)
        {
            TGfxWorldStaticModels smodels = context.GfxWorld.smodels;
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

        private unsafe void ProcessStaticModelJson(TGfxWorldStaticModels smodels, int index,
            MapProcessingContext context)
        {
            TGfxStaticModelCollection collection =
                Cordycep.ReadMemory<TGfxStaticModelCollection>(smodels.collections +
                                                               index * sizeof(TGfxStaticModelCollection));
            TGfxStaticModel staticModel =
                Cordycep.ReadMemory<TGfxStaticModel>(smodels.smodels +
                                                     collection.smodelIndex * sizeof(TGfxStaticModel));
            TGfxWorldTransientZone zone = context.TransientZones[collection.transientGfxWorldPlaced];

            if (zone.hash == 0) return;

            TXModel xmodel = Cordycep.ReadMemory<TXModel>(staticModel.xmodel);

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
                TGfxSModelInstanceData instanceData =
                    Cordycep.ReadMemory<TGfxSModelInstanceData>((nint)smodels.instanceData +
                                                                instanceId *
                                                                sizeof(TGfxSModelInstanceData));

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

        private void ProcessStaticModelsForCast(MapProcessingContext context)
        {
            TGfxWorldStaticModels smodels = context.GfxWorld.smodels;

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

        private unsafe void ProcessStaticModelMesh(TGfxWorldStaticModels smodels, int index,
            MapProcessingContext context)
        {
            TGfxStaticModelCollection collection =
                Cordycep.ReadMemory<TGfxStaticModelCollection>(smodels.collections +
                                                               index * sizeof(TGfxStaticModelCollection));
            TGfxStaticModel staticModel =
                Cordycep.ReadMemory<TGfxStaticModel>(smodels.smodels +
                                                     collection.smodelIndex * sizeof(TGfxStaticModel));
            TGfxWorldTransientZone zone = context.TransientZones[collection.transientGfxWorldPlaced];

            if (zone.hash == 0) return;

            TXModel xmodel = Cordycep.ReadMemory<TXModel>(staticModel.xmodel);

            ulong xmodelHash = xmodel.hash & 0x0FFFFFFFFFFFFFFF;

            TXModelLodInfo lodInfo = Cordycep.ReadMemory<TXModelLodInfo>(xmodel.lodInfo);
            TXModelSurfs xmodelSurfs = Cordycep.ReadMemory<TXModelSurfs>(lodInfo.MeshPtr);
            TXSurfaceShared shared = Cordycep.ReadMemory<TXSurfaceShared>(xmodelSurfs.shared);

            XModelMeshData xmodelMesh = new XModelMeshData();
            xmodelMesh.loaded = false;

            if (_models.ContainsKey(xmodelHash))
            {
                xmodelMesh = _models[xmodelHash];
            }
            else
            {
                ulong pakKey = xmodelSurfs.xpakKey;
                if (pakKey == 0)
                {
                    pakKey = shared.xpakKey;
                }

                if (shared.data != 0)
                {
                    xmodelMesh = ReadXModelMeshes(xmodel, shared.data, false);
                    xmodelMesh.loaded = true;
                }
                else if (shared.data == 0 && XSub.CacheObjects.ContainsKey(pakKey))
                {
                    byte[] buffer = XSub.ExtractXSubPackage(pakKey, shared.dataSize);
                    nint sharedPtr = Marshal.AllocHGlobal((int)shared.dataSize);
                    Marshal.Copy(buffer, 0, sharedPtr, (int)shared.dataSize);
                    xmodelMesh = ReadXModelMeshes(xmodel, (nint)sharedPtr, true);
                    Marshal.FreeHGlobal(sharedPtr);
                    xmodelMesh.loaded = true;
                }
                else if (shared.data == 0 && CASCPackage.Assets.ContainsKey(pakKey))
                {
                    byte[] buffer = CASCPackage.ExtractXSubPackage(pakKey, shared.dataSize);
                    nint sharedPtr = Marshal.AllocHGlobal((int)shared.dataSize);
                    Marshal.Copy(buffer, 0, sharedPtr, (int)shared.dataSize);
                    xmodelMesh = ReadXModelMeshes(xmodel, (nint)sharedPtr, true);
                    Marshal.FreeHGlobal(sharedPtr);
                    xmodelMesh.loaded = true;
                }
                if (xmodelMesh.loaded)
                {
                    _models[xmodelHash] = xmodelMesh;
                }
            }

            if (!xmodelMesh.loaded)
            {
                Log.Error($"Failed to load xmodel {Cordycep.ReadString(xmodel.name)}. XSUB: {shared.xpakKey}");
                return;
            }

            string xmodelName = Cordycep.ReadString(xmodel.name);
            int instanceId = (int)collection.firstInstance;
            while (instanceId < collection.firstInstance + collection.instanceCount)
            {
                TGfxSModelInstanceData instanceData =
                    Cordycep.ReadMemory<TGfxSModelInstanceData>((nint)smodels.instanceData +
                                                                instanceId *
                                                                sizeof(TGfxSModelInstanceData));

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

                instanceId++;
            }
        }

        protected abstract float GetSurfaceScale(TXSurface surface);
        protected abstract Vector3 GetSurfaceOffset(TXSurface surface);

        private unsafe XModelMeshData ReadXModelMeshes(TXModel xmodel, nint shared, bool isLocal = false)
        {
            TXModelLodInfo lodInfo = Cordycep.ReadMemory<TXModelLodInfo>(xmodel.lodInfo);
            TXModelSurfs xmodelSurfs = Cordycep.ReadMemory<TXModelSurfs>(lodInfo.MeshPtr);

            TXSurface surface =
                Cordycep.ReadMemory<TXSurface>((nint)xmodelSurfs.surfs);
            TMaterial material =
                Cordycep.ReadMemory<TMaterial>(Cordycep.ReadMemory<nint>(xmodel.materialHandles));

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

            float scale = GetSurfaceScale(surface);
            Vector3 offset = GetSurfaceOffset(surface);
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

                mesh.positions.Add(position * MeshPositionScale);

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
            nint packedIndices = shared + (nint)surface.packedIndicesOffset;

            for (int j = 0; j < surface.triCount; j++)
            {
                ushort[] faces = UnpackFaceIndices(tableOffsetPtr,
                    surface.packedIndicesTableCount, packedIndices, indicesPtr, (uint)j, isLocal);
                mesh.faces.Add(new Face() { a = faces[0], b = faces[1], c = faces[2] });
            }

            return mesh;
        }

        protected virtual MeshData ReadMesh(TGfxSurface gfxSurface, TGfxUgbSurfData ugbSurfData, TMaterial material,
            TGfxWorldTransientZone zone)
        {
            TGfxWorldDrawOffset worldDrawOffset = ugbSurfData.worldDrawOffset;

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
                positions.Add(position * MeshPositionScale);

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

            nint tableOffsetPtr = zone.drawVerts.tableData + (nint)(gfxSurface.tableIndex * 32);
            nint indicesPtr = zone.drawVerts.indices + (nint)(gfxSurface.baseIndex * 2);
            nint packedIndices = zone.drawVerts.packedIndices + (nint)gfxSurface.packedIndicesOffset;

            CastArrayProperty<ushort> faceIndices = mesh.AddArray<ushort>("f", new(gfxSurface.triCount * 3));
            ushort[] faceIndicesArray = new ushort[gfxSurface.triCount * 3];

            Parallel.For(0, gfxSurface.triCount, j =>
            {
                ushort[] faces = UnpackFaceIndices(tableOffsetPtr,
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

        protected abstract List<TextureSemanticData> PopulateMaterial(TMaterial material);
        protected abstract ushort[] UnpackFaceIndices(nint tables, uint tableCount, nint packedIndices, nint indices, uint faceIndex, bool isLocal = false);

        private string EnsureOutputDirectory(string baseName)
        {
            string outputFolder = Path.Join(Environment.CurrentDirectory, baseName);

            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);
            return outputFolder;
        }

        private void ExportBaseMesh(MapProcessingContext context, string outputFolder)
        {
            CastNode root = new CastNode(CastNodeIdentifier.Root);
            root.AddNode(context.BaseMeshModel);
            CastWriter.Save(Path.Join(outputFolder, $"{context.BaseName}_base_mesh.cast"), root);

            List<string> exportedBaseImages = new();

            foreach (var mesh in context.Meshes)
            {
                if (mesh.mesh == null) continue;
                string materialName = mesh.material.Name;
                string materialPath =
                    Path.Join(outputFolder,
                        $"{materialName}_images.txt");

                StringBuilder semanticTxt = new StringBuilder();
                semanticTxt.Append("semantic,image_name");
                foreach (var texture in mesh.textures)
                {
                    semanticTxt.AppendLine();
                    semanticTxt.Append($"{texture.semantic},{texture.texture}");

                    if (!exportedBaseImages.Contains(texture.texture))
                    {
                        exportedBaseImages.Add(texture.texture);
                    }
                }

                File.WriteAllText(materialPath, semanticTxt.ToString());
            }
        }

        private void ExportPropsMesh(MapProcessingContext context, string outputFolder)
        {
            CastNode root = new CastNode(CastNodeIdentifier.Root);
            root.AddNode(context.StaticModel);
            CastWriter.Save(Path.Join(outputFolder, $"{context.BaseName}_props_mesh.cast"), root);

            List<string> exportedPropsImages = new();

            foreach (var xmodelMesh in _models.Values)
            {
                string materialName = xmodelMesh.material.Name;
                string materialPath = Path.Join(outputFolder, $"{materialName}_images.txt");

                StringBuilder semanticTxt = new StringBuilder();
                semanticTxt.Append("# semantic, image");
                foreach (var texture in xmodelMesh.textures)
                {
                    semanticTxt.AppendLine();
                    semanticTxt.Append($"{texture.semantic}, {texture.texture}");

                    if (!exportedPropsImages.Contains(texture.texture))
                    {
                        exportedPropsImages.Add(texture.texture);
                    }
                }

                File.WriteAllText(materialPath, semanticTxt.ToString());
            }
        }

        private void ExportJson(MapProcessingContext context, string outputFolder)
        {
            string jsonOutput = JsonConvert.SerializeObject(context.ModelsList, Formatting.Indented);
            string outputFile = Path.Join(outputFolder, $"{context.BaseName}.json");
            File.WriteAllText(outputFile, jsonOutput);

            Console.WriteLine($"JSON exported to {outputFile}");
        }

        private void CommonExport(MapProcessingContext context, bool exportCast)
        {
            var outputFolder = EnsureOutputDirectory(context.BaseName);

            ExportBaseMesh(context, outputFolder);

            if (exportCast)
            {
                ExportPropsMesh(context, outputFolder);
            }

            ExportJson(context, outputFolder);
        }

        private void MapToJson(nint asset, TGfxWorld gfxWorld, string baseName,
            bool noProps = false, Vector3 PropsOrigin = new(), uint range = 0)
        {
            var context = new MapProcessingContext
            {
                GfxWorld = gfxWorld,
                BaseName = baseName,
                NoProps = noProps,
                StaticPropsOrigin = PropsOrigin,
                Range = range
            };

            ProcessMapCommon(
                context,
                ctx => ProcessStaticModelsForJson(ctx),
                ctx => CommonExport(ctx, false)
            );
        }

        private void DumpMap(nint asset, TGfxWorld gfxWorld, string baseName,
            bool noProps = false, Vector3 staticPropsOrigin = new(), uint range = 0, bool onlyJson = false)
        {
            if (onlyJson)
            {
                MapToJson(asset, gfxWorld, baseName, noProps, staticPropsOrigin);
                return;
            }

            var context = new MapProcessingContext
            {
                GfxWorld = gfxWorld,
                BaseName = baseName,
                NoProps = noProps,
                StaticPropsOrigin = staticPropsOrigin,
                Range = range
            };

            ProcessMapCommon(
                context,
                ctx => ProcessStaticModelsForCast(ctx),
                ctx => CommonExport(ctx, true)
            );
        }
    }
}