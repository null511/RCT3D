using RCT3D.Render.Terrain;

namespace RCT3D.Render.Paths;

internal sealed class PathAccessoryAssetLibrary(IReadOnlyDictionary<uint, PathAccessoryModelDefinition> modelsByObjectIdHash)
{
    public bool TryGetModel(uint objectIdHash, out PathAccessoryModelDefinition definition) =>
        modelsByObjectIdHash.TryGetValue(objectIdHash, out definition!);

    public PathAccessoryModel LoadModel(PathAccessoryModelDefinition definition)
    {
        var model = new SharpGltfModelLoader().Load(definition.AssetPath);
        return new PathAccessoryModel(definition.AssetPath, model);
    }

    public static PathAccessoryAssetLibrary Load(string assetDirectory)
    {
        var pathDirectory = Path.Combine(Path.GetFullPath(assetDirectory), "Path");
        var models = new Dictionary<uint, PathAccessoryModelDefinition>();
        foreach (var directory in Directory.EnumerateDirectories(pathDirectory)) {
            var objectId = Path.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(objectId))
                throw new InvalidDataException("Path accessory object identifiers must not be empty.");

            var modelPath = Path.Combine(directory, "model.glb");
            if (!File.Exists(modelPath))
                throw new InvalidDataException($"Path accessory model is missing for '{objectId}': '{modelPath}'.");

            var hash = TerrainMaterialLibrary.HashObjectId(objectId);
            if (!models.TryAdd(hash, new PathAccessoryModelDefinition(modelPath)))
                throw new InvalidDataException($"Path accessory object identifier hash collision or duplicate: '{objectId}'.");
        }

        return new PathAccessoryAssetLibrary(models);
    }
}

internal sealed record PathAccessoryModelDefinition(string AssetPath);
internal sealed record PathAccessoryModel(string AssetPath, ImportedModelData Data);
