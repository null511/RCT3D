using System.Text.Json;

namespace RCT3DViewer.Render;

internal sealed class PathLampAssetLibrary
{
    private readonly Dictionary<uint, PathLampModel> _modelsByObjectIdHash;
    private readonly PathLampModel _defaultModel;

    private PathLampAssetLibrary(Dictionary<uint, PathLampModel> modelsByObjectIdHash, PathLampModel defaultModel)
    {
        _modelsByObjectIdHash = modelsByObjectIdHash;
        _defaultModel = defaultModel;
    }

    public PathLampModel GetModel(uint objectIdHash)
    {
        return _modelsByObjectIdHash.TryGetValue(objectIdHash, out var model) ? model : _defaultModel;
    }

    public static PathLampAssetLibrary Load(string assetDirectory)
    {
        var rootDirectory = Path.GetFullPath(assetDirectory) + Path.DirectorySeparatorChar;
        var manifestPath = Path.Combine(assetDirectory, "path_lamps.json");
        using var manifestStream = File.OpenRead(manifestPath);
        using var manifest = JsonDocument.Parse(manifestStream);
        var root = manifest.RootElement;
        var loader = new SharpGltfModelLoader();
        var cachedModels = new Dictionary<(string Path, float TargetHeight, float YawDegrees), PathLampModel>();

        PathLampModel ParseModel(JsonElement element, float? inheritedTargetHeight = null)
        {
            var relativePath = element.GetProperty("model").GetString();
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new InvalidDataException("Path lamp model entries must specify a model path.");

            var modelPath = Path.GetFullPath(Path.Combine(assetDirectory, relativePath));
            if (!modelPath.StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase) || !File.Exists(modelPath))
                throw new InvalidDataException($"Path lamp model path is invalid or missing: '{relativePath}'.");

            var targetHeight = element.TryGetProperty("targetHeight", out var targetHeightProperty)
                ? targetHeightProperty.GetSingle()
                : inheritedTargetHeight ?? 23.0f * (3.0f / 32.0f);
            var yawDegrees = element.TryGetProperty("yawDegrees", out var yawProperty) ? yawProperty.GetSingle() : 0.0f;
            if (!float.IsFinite(yawDegrees))
                throw new InvalidDataException($"Path lamp model '{relativePath}' has an invalid yawDegrees value.");

            var cacheKey = (modelPath, targetHeight, yawDegrees);
            if (cachedModels.TryGetValue(cacheKey, out var cachedModel))
                return cachedModel;

            var model = new PathLampModel(modelPath, yawDegrees,
                loader.Load(modelPath, new ModelImportSettings(TargetHeight: targetHeight)));
            cachedModels.Add(cacheKey, model);
            return model;
        }

        var defaultModel = ParseModel(root.GetProperty("defaultLamp"));
        var modelsByObjectIdHash = new Dictionary<uint, PathLampModel>();
        foreach (var lamp in root.GetProperty("lamps").EnumerateArray()) {
            var objectId = lamp.GetProperty("objectId").GetString();
            if (string.IsNullOrWhiteSpace(objectId))
                throw new InvalidDataException("Path lamp objectId values must not be empty.");

            var objectIdHash = TerrainMaterialLibrary.HashObjectId(objectId);
            if (!modelsByObjectIdHash.TryAdd(objectIdHash, ParseModel(lamp, defaultModel.Data.BoundsMax.Y - defaultModel.Data.BoundsMin.Y)))
                throw new InvalidDataException($"Path lamp object identifier hash collision or duplicate: '{objectId}'.");
        }

        return new PathLampAssetLibrary(modelsByObjectIdHash, defaultModel);
    }
}

internal sealed record PathLampModel(string AssetPath, float YawDegrees, ImportedModelData Data);
