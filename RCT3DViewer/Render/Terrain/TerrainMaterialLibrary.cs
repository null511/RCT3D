using System.Text;
using System.Text.Json;

namespace RCT3D.Render.Terrain;

public sealed class TerrainMaterialLibrary : IMaterialLibrary
{
    private readonly IReadOnlyDictionary<uint, uint> _surfaceLayersByObjectIdHash;
    private readonly IReadOnlyDictionary<uint, uint> _sidingLayersByObjectIdHash;

    public int Width { get; }
    public int Height { get; }
    public byte[] ColorPixels { get; }
    public byte[] NormalPixels { get; }
    public byte[] SpecularPixels { get; }
    public uint FallbackLayer {get;}

    public int LayerCount => ColorPixels.Length / (Width * Height * 4);


    private TerrainMaterialLibrary(int width, int height, byte[] colorPixels, byte[] normalPixels, byte[] specularPixels, IReadOnlyDictionary<uint, uint> surfaceLayersByObjectIdHash, IReadOnlyDictionary<uint, uint> sidingLayersByObjectIdHash, uint fallbackLayer)
    {
        Width = width;
        Height = height;
        ColorPixels = colorPixels;
        NormalPixels = normalPixels;
        SpecularPixels = specularPixels;
        FallbackLayer = fallbackLayer;

        _surfaceLayersByObjectIdHash = surfaceLayersByObjectIdHash;
        _sidingLayersByObjectIdHash = sidingLayersByObjectIdHash;
    }

    public uint GetSurfaceLayer(uint objectIdHash)
    {
        return _surfaceLayersByObjectIdHash.TryGetValue(objectIdHash, out var layer) ? layer : FallbackLayer;
    }

    public uint GetSidingLayer(uint objectIdHash)
    {
        return _sidingLayersByObjectIdHash.TryGetValue(objectIdHash, out var layer) ? layer : FallbackLayer;
    }

    public static TerrainMaterialLibrary Load(string textureDirectory, string? additionalArrayProperty, string missingTexturePath)
    {
        var surfaceLayers = new Dictionary<uint, uint>();
        var sidingLayers = new Dictionary<uint, uint>();

        var builder = new MaterialLibraryBuilder();

        // missing/fallback image
        var missingTextureFullPath = Path.GetFullPath(missingTexturePath);
        var missingTextureDirectory = Path.GetDirectoryName(missingTextureFullPath)
            ?? throw new InvalidDataException($"Missing texture path has no directory: {missingTexturePath}");

        var fallbackLayer = 0u; //(uint)images.Count;
        builder.AddImage(string.Empty, Path.GetFileName(missingTextureFullPath), missingTextureDirectory);

        // load data
        LoadSurfaces(textureDirectory, builder, surfaceLayers);
        LoadSiding(textureDirectory, builder, sidingLayers);

        var (color, normal, specular) = builder.Build();

        return new TerrainMaterialLibrary(builder.Width!.Value, builder.Height!.Value, color, normal, specular, surfaceLayers, sidingLayers, fallbackLayer);
    }

    private static void LoadSurfaces(string textureDirectory, MaterialLibraryBuilder builder, Dictionary<uint, uint> layers)
    {
        var surfaceDirectory = Path.Combine(textureDirectory, "Terrain");
        var manifestPath = Path.Combine(surfaceDirectory, "materials.json");

        using var manifestStream = File.OpenRead(manifestPath);
        using var manifest = JsonDocument.Parse(manifestStream);
        
        foreach (var entry in manifest.RootElement.EnumerateObject()) {
            var objectId = entry.Name;
            var texture = entry.Value.GetString();

            if (string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(texture))
                throw new InvalidDataException("Terrain materials must have non-empty objectId and texture values.");

            builder.AddImage(objectId, texture, surfaceDirectory, layers);
        }
    }

    private static void LoadSiding(string textureDirectory, MaterialLibraryBuilder builder, Dictionary<uint, uint> layers)
    {
        var sideDirectory = Path.Combine(textureDirectory, "terrain_side");
        var manifestPath = Path.Combine(sideDirectory, "materials.json");

        using var manifestStream = File.OpenRead(manifestPath);
        using var manifest = JsonDocument.Parse(manifestStream);

        foreach (var entry in manifest.RootElement.EnumerateObject()) {
            var objectId = entry.Name;
            var texture = entry.Value.GetString();

            if (string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(texture))
                throw new InvalidDataException("Terrain materials must have non-empty objectId and texture values.");

            builder.AddImage(objectId, texture, sideDirectory, layers);
        }
    }

    //private static string GetSharedMissingTexturePath(string assetDirectory)
    //{
    //    var textureRoot = Directory.GetParent(Path.GetFullPath(assetDirectory))?.FullName
    //        ?? throw new InvalidDataException($"Texture asset directory has no parent: {assetDirectory}");

    //    return Path.Combine(textureRoot, "missing.png");
    //}

    public static uint HashObjectId(string objectId)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;

        foreach (var value in Encoding.UTF8.GetBytes(objectId)) {
            hash ^= value;
            hash *= prime;
        }

        return hash;
    }
}
