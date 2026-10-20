using RCT3DViewer.Render.Terrain;
using System.Text.Json;

namespace RCT3DViewer.Render.Paths.Meshing;

public sealed class PathMaterialLibrary
{
    private readonly TerrainMaterialLibrary _materials;
    private readonly uint _queueFallbackLayer;
    private readonly uint _railingFallbackLayer;
    private readonly HashSet<uint> _invisibleRailingIdHashes;
    private readonly Dictionary<uint, uint> _railingLayers;

    public int Width => _materials.Width;
    public int Height => _materials.Height;
    public byte[] Pixels => _materials.Pixels;
    public int LayerCount => _materials.LayerCount;
    internal TerrainMaterialLibrary TextureLibrary => _materials;


    private PathMaterialLibrary(
        TerrainMaterialLibrary materials, uint queueFallbackLayer, uint railingFallbackLayer,
        HashSet<uint> invisibleRailingIdHashes, Dictionary<uint, uint> railingLayers)
    {
        _materials = materials;
        _queueFallbackLayer = queueFallbackLayer;
        _railingFallbackLayer = railingFallbackLayer;
        _invisibleRailingIdHashes = invisibleRailingIdHashes;
        _railingLayers = railingLayers;
    }

    public uint GetLayer(uint surfaceIdHash, bool isQueue)
    {
        return isQueue ? _queueFallbackLayer : _materials.GetLayer(surfaceIdHash);
    }

    public uint GetRailingLayer(uint railingsIdHash)
    {
        return _railingLayers.TryGetValue(railingsIdHash, out var layer) ? layer : _railingFallbackLayer;
    }

    public bool HasVisibleRailings(uint railingsIdHash)
    {
        return railingsIdHash != 0 && !_invisibleRailingIdHashes.Contains(railingsIdHash);
    }

    public static PathMaterialLibrary Load(string assetDirectory)
    {
        var manifestPath = Path.Combine(assetDirectory, "materials.json");
        using var manifestStream = File.OpenRead(manifestPath);
        using var manifest = JsonDocument.Parse(manifestStream);
        var queueFallbackObjectId = manifest.RootElement.GetProperty("queueFallback").GetString();
        var railingFallbackObjectId = manifest.RootElement.GetProperty("railingFallback").GetString();

        if (string.IsNullOrWhiteSpace(queueFallbackObjectId))
            throw new InvalidDataException("Path material manifest queueFallback must specify an objectId.");

        if (string.IsNullOrWhiteSpace(railingFallbackObjectId))
            throw new InvalidDataException("Path material manifest railingFallback must specify an objectId.");

        var hasQueueMaterial = manifest.RootElement.GetProperty("materials").EnumerateArray()
            .Any(material => string.Equals(material.GetProperty("objectId").GetString(), queueFallbackObjectId, StringComparison.Ordinal));

        if (!hasQueueMaterial)
            throw new InvalidDataException($"Path queue fallback object '{queueFallbackObjectId}' is not defined in materials.");

        var railingEntries = manifest.RootElement.GetProperty("railings").EnumerateArray().ToArray();
        if (!railingEntries.Any(railing => string.Equals(railing.GetProperty("objectId").GetString(), railingFallbackObjectId, StringComparison.Ordinal)))
            throw new InvalidDataException($"Path railing fallback object '{railingFallbackObjectId}' is not defined in railings.");

        var materials = TerrainMaterialLibrary.Load(assetDirectory, "railings");
        var queueFallbackLayer = materials.GetLayer(TerrainMaterialLibrary.HashObjectId(queueFallbackObjectId));
        var railingFallbackLayer = materials.GetLayer(TerrainMaterialLibrary.HashObjectId(railingFallbackObjectId));
        var railingLayers = new Dictionary<uint, uint>();

        foreach (var railing in railingEntries) {
            var objectId = railing.GetProperty("objectId").GetString();
            if (string.IsNullOrWhiteSpace(objectId))
                throw new InvalidDataException("Path railing objectId values must not be empty.");

            var objectIdHash = TerrainMaterialLibrary.HashObjectId(objectId);
            if (!railingLayers.TryAdd(objectIdHash, materials.GetLayer(objectIdHash)))
                throw new InvalidDataException($"Path railing identifier hash collision: {objectId}");
        }

        var invisibleRailingIdHashes = new HashSet<uint> {
            TerrainMaterialLibrary.HashObjectId("openrct2.footpath_railings.invisible"),
            TerrainMaterialLibrary.HashObjectId("rct2tt.footpath_railings.circuitboard_invisible"),
        };

        return new PathMaterialLibrary(materials, queueFallbackLayer, railingFallbackLayer, invisibleRailingIdHashes, railingLayers);
    }
}