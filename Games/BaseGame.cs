using Cast.NET;
using Cast.NET.Nodes;
using DotnesktRemastered.FileStorage;
using DotnesktRemastered.Structures;
using DotnesktRemastered.Utils;
using Serilog;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;

namespace DotnesktRemastered.Games
{
    public abstract class BaseGame
    {
        public abstract void DumpMap(string name, bool noProps = false, Vector3 propsOrigin = new(), uint range = 0,
            bool onlyJson = false);

        public abstract string[] GetMapList();
    }

    public abstract class BaseGame<T1, T2> : BaseGame
        where T1 : struct
        where T2 : struct
    {
        protected CordycepProcess Cordycep = Program.Cordycep;

        protected uint GFXMAP_POOL_IDX = 0;
        protected uint GFXMAP_TRZONE_POOL_IDX = 0;

        protected Dictionary<ulong, XModelMeshData[]> _models = new();

        protected abstract string GameName { get; }
        protected abstract T1 ReadGfxWorld(IntPtr header);

        public override void DumpMap(string name, bool noProps = false, Vector3 propsOrigin = new(), uint range = 0,
            bool onlyJson = false)
        {
            Log.Information("[{0}] Finding map {1}...", GameName, name);
            bool found = false;

            Cordycep.EnumerableAssetPool(GFXMAP_POOL_IDX, (asset) =>
            {
                T1 gfxWorld = ReadGfxWorld(asset.Header);
                if (GetBaseName(gfxWorld) == name)
                {
                    Log.Information("[{0}] Found map {1}, started dumping...", GameName, name);
                    DumpMap(asset.Header, gfxWorld, name, noProps, propsOrigin, range, onlyJson);
                    Log.Information("[{0}] Dumped map {1}.", GameName, name);
                    found = true;
                }
            });

            if (!found)
            {
                Log.Error("[{0}] Map {1} not found.", GameName, name);
            }
        }

        protected abstract string GetBaseName(T1 gfxWorld);

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
            public T1 GfxWorld { get; set; }
            public string BaseName { get; set; }
            public bool NoProps { get; set; }
            public Vector3 StaticPropsOrigin { get; set; }
            public uint Range { get; set; }
            public T2[] TransientZones { get; set; }
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
            InitializeBaseModel(context);
            ReadTransientZones(context);
            ProcessSurfaces(context);

            if (!context.NoProps)
            {
                modelProcessor(context);
            }

            exportHandler(context);
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

        protected abstract void ReadTransientZones(MapProcessingContext context);

        protected abstract void ProcessSurfaces(MapProcessingContext context);

        protected abstract void ProcessStaticModelsForJson(MapProcessingContext context);

        protected abstract void ProcessStaticModelsForCast(MapProcessingContext context);

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
            CastWriter.Save(Path.Join(outputFolder, $"{context.BaseName}__props_mesh.cast"), root);

            List<string> exportedPropsImages = new();

            foreach (var xmodelMesh in _models.Values)
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

                        if (!exportedPropsImages.Contains(texture.texture))
                        {
                            exportedPropsImages.Add(texture.texture);
                        }
                    }

                    File.WriteAllText(materialPath, semanticTxt.ToString());
                }
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

        private void MapToJson(nint asset, T1 gfxWorld, string baseName,
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

        private void DumpMap(nint asset, T1 gfxWorld, string baseName,
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