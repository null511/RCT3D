using System.Text.Json;
using RCT3D.Render.Terrain;

namespace RCT3D.Render.Paths;

internal sealed class PathAccessoryAssetLibrary
{
    private readonly IReadOnlyDictionary<uint, PathAccessoryModelDefinition> _lampModelsByObjectIdHash;
    private readonly IReadOnlyDictionary<uint, PathAccessoryModelDefinition> _benchModelsByObjectIdHash;
    private readonly PathAccessoryModelDefinition _defaultLampModel;
    private readonly PathAccessoryModelDefinition _defaultBenchModel;

    private PathAccessoryAssetLibrary(
        IReadOnlyDictionary<uint, PathAccessoryModelDefinition> lampModelsByObjectIdHash,
        PathAccessoryModelDefinition defaultLampModel,
        IReadOnlyDictionary<uint, PathAccessoryModelDefinition> benchModelsByObjectIdHash,
        PathAccessoryModelDefinition defaultBenchModel)
    {
        _lampModelsByObjectIdHash = lampModelsByObjectIdHash;
        _defaultLampModel = defaultLampModel;
        _benchModelsByObjectIdHash = benchModelsByObjectIdHash;
        _defaultBenchModel = defaultBenchModel;
    }

    public PathAccessoryModelDefinition GetLampModel(uint objectIdHash)
    {
        return _lampModelsByObjectIdHash.TryGetValue(objectIdHash, out var model) ? model : _defaultLampModel;
    }

    public PathAccessoryModelDefinition GetBenchModel(uint objectIdHash)
    {
        return _benchModelsByObjectIdHash.TryGetValue(objectIdHash, out var model) ? model : _defaultBenchModel;
    }

    public PathAccessoryModel LoadModel(PathAccessoryModelDefinition definition)
    {
        var model = new SharpGltfModelLoader().Load(definition.AssetPath,
            new ModelImportSettings(TargetHeight: definition.TargetHeight));
        return new PathAccessoryModel(definition.AssetPath, definition.YawDegrees, model);
    }

    public static PathAccessoryAssetLibrary Load(string assetDirectory)
    {
        assetDirectory = Path.GetFullPath(assetDirectory);
        var rootDirectory = assetDirectory + Path.DirectorySeparatorChar;

        PathAccessoryModelDefinition ParseModel(JsonElement element, string accessoryName, float inheritedTargetHeight)
        {
            var relativePath = element.GetProperty("model").GetString();
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new InvalidDataException($"Path {accessoryName} model entries must specify a model path.");

            var modelPath = Path.GetFullPath(Path.Combine(assetDirectory, relativePath));
            if (!modelPath.StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase) || !File.Exists(modelPath))
                throw new InvalidDataException($"Path {accessoryName} model path is invalid or missing: '{relativePath}'.");

            var targetHeight = element.TryGetProperty("targetHeight", out var targetHeightProperty)
                ? targetHeightProperty.GetSingle()
                : inheritedTargetHeight;
            var yawDegrees = element.TryGetProperty("yawDegrees", out var yawProperty) ? yawProperty.GetSingle() : 0.0f;
            if (!float.IsFinite(yawDegrees))
                throw new InvalidDataException($"Path {accessoryName} model '{relativePath}' has an invalid yawDegrees value.");

            return new PathAccessoryModelDefinition(modelPath, targetHeight, yawDegrees);
        }

        (PathAccessoryModelDefinition DefaultModel, Dictionary<uint, PathAccessoryModelDefinition> ModelsByObjectIdHash) ParseManifest(
            string manifestName, string defaultModelProperty, string modelEntriesProperty, string accessoryName, float fallbackHeight)
        {
            var manifestPath = Path.Combine(assetDirectory, "Path", manifestName);
            using var manifestStream = File.OpenRead(manifestPath);
            using var manifest = JsonDocument.Parse(manifestStream);
            var root = manifest.RootElement;
            var defaultModel = ParseModel(root.GetProperty(defaultModelProperty), accessoryName, fallbackHeight);
            var modelsByObjectIdHash = new Dictionary<uint, PathAccessoryModelDefinition>();
            foreach (var entry in root.GetProperty(modelEntriesProperty).EnumerateArray()) {
                var objectId = entry.GetProperty("objectId").GetString();
                if (string.IsNullOrWhiteSpace(objectId))
                    throw new InvalidDataException($"Path {accessoryName} objectId values must not be empty.");

                var objectIdHash = TerrainMaterialLibrary.HashObjectId(objectId);
                if (!modelsByObjectIdHash.TryAdd(objectIdHash,
                        ParseModel(entry, accessoryName, defaultModel.TargetHeight)))
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

internal sealed record PathAccessoryModelDefinition(string AssetPath, float TargetHeight, float YawDegrees);
internal sealed record PathAccessoryModel(string AssetPath, float YawDegrees, ImportedModelData Data);
