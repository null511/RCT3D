using RCT3DViewer.Render.Terrain;
using System.Text.Json;

namespace RCT3DViewer.Render.Paths;

internal sealed class PathAccessoryAssetLibrary
{
    private readonly Dictionary<uint, PathAccessoryModel> _lampModelsByObjectIdHash;
    private readonly Dictionary<uint, PathAccessoryModel> _benchModelsByObjectIdHash;
    private readonly PathAccessoryModel _defaultLampModel;
    private readonly PathAccessoryModel _defaultBenchModel;

    private PathAccessoryAssetLibrary(
        Dictionary<uint, PathAccessoryModel> lampModelsByObjectIdHash,
        PathAccessoryModel defaultLampModel,
        Dictionary<uint, PathAccessoryModel> benchModelsByObjectIdHash,
        PathAccessoryModel defaultBenchModel)
    {
        _lampModelsByObjectIdHash = lampModelsByObjectIdHash;
        _defaultLampModel = defaultLampModel;
        _benchModelsByObjectIdHash = benchModelsByObjectIdHash;
        _defaultBenchModel = defaultBenchModel;
    }

    public PathAccessoryModel GetLampModel(uint objectIdHash)
    {
        return _lampModelsByObjectIdHash.TryGetValue(objectIdHash, out var model) ? model : _defaultLampModel;
    }

    public PathAccessoryModel GetBenchModel(uint objectIdHash)
    {
        return _benchModelsByObjectIdHash.TryGetValue(objectIdHash, out var model) ? model : _defaultBenchModel;
    }

    public static PathAccessoryAssetLibrary Load(string assetDirectory)
    {
        var rootDirectory = Path.GetFullPath(assetDirectory) + Path.DirectorySeparatorChar;
        var loader = new SharpGltfModelLoader();
        var cachedModels = new Dictionary<(string Path, float TargetHeight, float YawDegrees), PathAccessoryModel>();

        PathAccessoryModel ParseModel(JsonElement element, string accessoryName, float? inheritedTargetHeight = null)
        {
            var relativePath = element.GetProperty("model").GetString();
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new InvalidDataException($"Path {accessoryName} model entries must specify a model path.");

            var modelPath = Path.GetFullPath(Path.Combine(assetDirectory, relativePath));
            if (!modelPath.StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase) || !File.Exists(modelPath))
                throw new InvalidDataException($"Path {accessoryName} model path is invalid or missing: '{relativePath}'.");

            var targetHeight = element.TryGetProperty("targetHeight", out var targetHeightProperty)
                ? targetHeightProperty.GetSingle()
                : inheritedTargetHeight ?? (accessoryName == "bench" ? 7.0f : 23.0f) * (3.0f / 32.0f);
            var yawDegrees = element.TryGetProperty("yawDegrees", out var yawProperty) ? yawProperty.GetSingle() : 0.0f;
            if (!float.IsFinite(yawDegrees))
                throw new InvalidDataException($"Path {accessoryName} model '{relativePath}' has an invalid yawDegrees value.");

            var cacheKey = (modelPath, targetHeight, yawDegrees);
            if (cachedModels.TryGetValue(cacheKey, out var cachedModel))
                return cachedModel;

            var model = new PathAccessoryModel(modelPath, yawDegrees,
                loader.Load(modelPath, new ModelImportSettings(TargetHeight: targetHeight)));
            cachedModels.Add(cacheKey, model);
            return model;
        }

        (PathAccessoryModel DefaultModel, Dictionary<uint, PathAccessoryModel> ModelsByObjectIdHash) ParseManifest(
            string manifestName, string defaultModelProperty, string modelEntriesProperty, string accessoryName, float fallbackHeight)
        {
            var manifestPath = Path.Combine(assetDirectory, "Path", manifestName);
            using var manifestStream = File.OpenRead(manifestPath);
            using var manifest = JsonDocument.Parse(manifestStream);
            var root = manifest.RootElement;
            var defaultModel = ParseModel(root.GetProperty(defaultModelProperty), accessoryName, fallbackHeight);
            var modelsByObjectIdHash = new Dictionary<uint, PathAccessoryModel>();
            foreach (var entry in root.GetProperty(modelEntriesProperty).EnumerateArray()) {
                var objectId = entry.GetProperty("objectId").GetString();
                if (string.IsNullOrWhiteSpace(objectId))
                    throw new InvalidDataException($"Path {accessoryName} objectId values must not be empty.");

                var objectIdHash = TerrainMaterialLibrary.HashObjectId(objectId);
                if (!modelsByObjectIdHash.TryAdd(objectIdHash,
                        ParseModel(entry, accessoryName, defaultModel.Data.BoundsMax.Y - defaultModel.Data.BoundsMin.Y)))
                    throw new InvalidDataException($"Path {accessoryName} object identifier hash collision or duplicate: '{objectId}'.");
            }

            return (defaultModel, modelsByObjectIdHash);
        }

        var lamps = ParseManifest("lamp1.json", "defaultLamp", "lamps", "lamp", 23.0f * (3.0f / 32.0f));
        var benches = ParseManifest("bench1.json", "defaultBench", "benches", "bench", 7.0f * (3.0f / 32.0f));
        return new PathAccessoryAssetLibrary(lamps.ModelsByObjectIdHash, lamps.DefaultModel,
            benches.ModelsByObjectIdHash, benches.DefaultModel);
    }
}

internal sealed record PathAccessoryModel(string AssetPath, float YawDegrees, ImportedModelData Data);
