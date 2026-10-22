using System.Text.Json;
using RCT3D.Render.Terrain;

namespace RCT3D.Render.Paths.Meshing;

public sealed class PathMaterialLibrary
{
    private readonly TerrainMaterialLibrary _materials;
    private readonly HashSet<uint> _invisibleRailingIdHashes;
    private readonly Dictionary<uint, uint> _railingLayers;

    public int Width => _materials.Width;
    public int Height => _materials.Height;
    public byte[] Pixels => _materials.Pixels;
    public int LayerCount => _materials.LayerCount;
    internal TerrainMaterialLibrary TextureLibrary => _materials;


    private PathMaterialLibrary(
        TerrainMaterialLibrary materials, HashSet<uint> invisibleRailingIdHashes, Dictionary<uint, uint> railingLayers)
    {
        _materials = materials;
        _invisibleRailingIdHashes = invisibleRailingIdHashes;
        _railingLayers = railingLayers;
    }

    public uint GetLayer(uint surfaceIdHash)
    {
        return _materials.GetLayer(surfaceIdHash);
    }

    public uint GetRailingLayer(uint railingsIdHash)
    {
        return _railingLayers.TryGetValue(railingsIdHash, out var layer) ? layer : _materials.FallbackLayer;
    }

    public bool HasVisibleRailings(uint railingsIdHash)
    {
        return railingsIdHash != 0 && !_invisibleRailingIdHashes.Contains(railingsIdHash);
    }

    public static PathMaterialLibrary Load(string assetDirectory, string missingTexturePath)
    {
        var manifestPath = Path.Combine(assetDirectory, "materials.json");
        using var manifestStream = File.OpenRead(manifestPath);
        using var manifest = JsonDocument.Parse(manifestStream);
        var railingEntries = manifest.RootElement.GetProperty("railings").EnumerateArray().ToArray();

        var materials = TerrainMaterialLibrary.Load(assetDirectory, "railings", missingTexturePath);
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

        return new PathMaterialLibrary(materials, invisibleRailingIdHashes, railingLayers);
    }
}