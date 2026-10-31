using RCT3D.Render.Terrain;
using System.Numerics;
using System.Text.Json;

namespace RCT3D.Render.Scenery;

internal sealed class TreeSceneryAssetLibrary
{
    private readonly IReadOnlyDictionary<uint, SceneryModelDefinition> _modelsByObjectIdHash;
    private readonly Dictionary<uint, SceneryModelAsset> _loadedModelsByObjectIdHash = new();
    public VegetationBuilder Vegetation { get; }

    private TreeSceneryAssetLibrary(
        IReadOnlyDictionary<uint, SceneryModelDefinition> modelsByObjectIdHash,
        VegetationBuilder vegetation)
    {
        _modelsByObjectIdHash = modelsByObjectIdHash;
        Vegetation = vegetation;
    }

    public static TreeSceneryAssetLibrary Load(string modelDirectory, string textureDirectory)
    {
        modelDirectory = Path.GetFullPath(modelDirectory);
        var rootDirectory = modelDirectory + Path.DirectorySeparatorChar;
        var manifestPath = Path.Combine(modelDirectory, "scenery.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (manifest.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Scenery manifest root must be an object keyed by object identifier.");

        var modelsByObjectIdHash = new Dictionary<uint, SceneryModelDefinition>();
        foreach (var entry in manifest.RootElement.EnumerateObject()) {
            if (string.IsNullOrWhiteSpace(entry.Name))
                throw new InvalidDataException("Scenery manifest object identifiers must not be empty.");
            if (entry.Value.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"Scenery manifest entry '{entry.Name}' must be an object.");

            var relativePath = entry.Value.GetProperty("model").GetString();
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new InvalidDataException($"Scenery manifest entry '{entry.Name}' must specify a model path.");

            var fullModelPath = Path.GetFullPath(Path.Combine(modelDirectory, relativePath));
            if (!fullModelPath.StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullModelPath))
                throw new InvalidDataException($"Scenery model path is invalid or missing for '{entry.Name}': '{relativePath}'.");

            var offset = Vector3.Zero;
            if (entry.Value.TryGetProperty("offset", out var offsetElement)) {
                if (offsetElement.ValueKind != JsonValueKind.Array || offsetElement.GetArrayLength() != 3)
                    throw new InvalidDataException($"Scenery model offset for '{entry.Name}' must contain three numbers.");
                offset = new Vector3(offsetElement[0].GetSingle(), offsetElement[1].GetSingle(), offsetElement[2].GetSingle());
                if (!float.IsFinite(offset.X) || !float.IsFinite(offset.Y) || !float.IsFinite(offset.Z))
                    throw new InvalidDataException($"Scenery model offset for '{entry.Name}' must be finite.");
            }

            var scale = entry.Value.TryGetProperty("scale", out var scaleElement) ? scaleElement.GetSingle() : 1.0f;
            if (!float.IsFinite(scale) || scale <= 0.0f)
                throw new InvalidDataException($"Scenery model scale for '{entry.Name}' must be finite and greater than zero.");

            var objectIdHash = TerrainMaterialLibrary.HashObjectId(entry.Name);
            if (!modelsByObjectIdHash.TryAdd(objectIdHash, new SceneryModelDefinition(fullModelPath, offset, scale)))
                throw new InvalidDataException($"Scenery object identifier hash collision or duplicate: '{entry.Name}'.");
        }

        return new TreeSceneryAssetLibrary(modelsByObjectIdHash, new VegetationBuilder(textureDirectory));
    }

    public bool TryGetModelDefinition(uint objectIdHash, out SceneryModelDefinition definition) =>
        _modelsByObjectIdHash.TryGetValue(objectIdHash, out definition!);

    public bool TryLoadModel(uint objectIdHash, out SceneryModelAsset asset)
    {
        if (!_modelsByObjectIdHash.TryGetValue(objectIdHash, out var definition)) {
            asset = null!;
            return false;
        }

        if (!_loadedModelsByObjectIdHash.TryGetValue(objectIdHash, out asset!)) {
            var data = new SharpGltfModelLoader().Load(definition.AssetPath);
            asset = new SceneryModelAsset(definition, data);
            _loadedModelsByObjectIdHash.Add(objectIdHash, asset);
        }

        return true;
    }

    public void ClearCache()
    {
        _loadedModelsByObjectIdHash.Clear();
        Vegetation.ClearCache();
    }
}

internal sealed record SceneryModelDefinition(string AssetPath, Vector3 Offset, float Scale);
internal sealed record SceneryModelAsset(SceneryModelDefinition Definition, ImportedModelData Data);
