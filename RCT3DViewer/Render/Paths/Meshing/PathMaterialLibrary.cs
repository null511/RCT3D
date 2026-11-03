using RCT3D.Render.Terrain;
using System.Text.Json;

namespace RCT3D.Render.Paths.Meshing;

public sealed class PathMaterialLibrary : IMaterialLibrary
{
    private readonly IReadOnlyDictionary<uint, uint> _layersByObjectIdHash;

    public int Width { get; }
    public int Height { get; }
    public byte[] ColorPixels { get; }
    public byte[] NormalPixels { get; }
    public byte[] SpecularPixels { get; }
    public uint FallbackLayer {get;}

    public int LayerCount => ColorPixels.Length / (Width * Height * 4);


    private PathMaterialLibrary(int width, int height, byte[] colorPixels, byte[] normalPixels, byte[] specularPixels, IReadOnlyDictionary<uint, uint> layersByObjectIdHash, uint fallbackLayer)
    {
        Width = width;
        Height = height;
        ColorPixels = colorPixels;
        NormalPixels = normalPixels;
        SpecularPixels = specularPixels;
        FallbackLayer = fallbackLayer;

        _layersByObjectIdHash = layersByObjectIdHash;
    }

    public uint GetLayer(uint surfaceIdHash)
    {
        return _layersByObjectIdHash.TryGetValue(surfaceIdHash, out var layer) ? layer : FallbackLayer;
    }

    public bool HasVisibleRailings(uint railingsIdHash)
    {
        return railingsIdHash != 0 && !invisibleRailingIdHashes.Contains(railingsIdHash);
    }

    public static PathMaterialLibrary Load(string textureRoot, string missingTexturePath)
    {
        var builder = new MaterialLibraryBuilder();
        
        // missing/fallback image
        var missingTextureFullPath = Path.GetFullPath(missingTexturePath);
        var missingTextureDirectory = Path.GetDirectoryName(missingTextureFullPath)
            ?? throw new InvalidDataException($"Missing texture path has no directory: {missingTexturePath}");

        var fallbackLayer = 0u; //(uint)images.Count;
        builder.AddImage(string.Empty, Path.GetFileName(missingTextureFullPath), missingTextureDirectory);

        // load data
        LoadSurfaces(textureRoot, builder);
        LoadRailings(textureRoot, builder);

        var (color, normal, specular) = builder.Build();

        return new PathMaterialLibrary(builder.Width!.Value, builder.Height!.Value, color, normal, specular, builder.Layers, fallbackLayer);
    }

    private static void LoadSurfaces(string textureRoot, MaterialLibraryBuilder builder)
    {
        var pathDirectory = Path.Combine(textureRoot, "path");
        var manifestPath = Path.Combine(pathDirectory, "materials.json");

        using var manifestStream = File.OpenRead(manifestPath);
        using var manifest = JsonDocument.Parse(manifestStream);

        foreach (var entry in manifest.RootElement.EnumerateObject()) {
            var objectId = entry.Name;
            var texture = entry.Value.GetString();

            if (string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(texture))
                throw new InvalidDataException("Path surface objectId and texture values must not be empty.");

            builder.AddImage(objectId, texture, pathDirectory);
        }
    }

    private static void LoadRailings(string textureRoot, MaterialLibraryBuilder builder)
    {
        var pathDirectory = Path.Combine(textureRoot, "path_railing");
        var manifestPath = Path.Combine(pathDirectory, "materials.json");

        using var manifestStream = File.OpenRead(manifestPath);
        using var manifest = JsonDocument.Parse(manifestStream);

        foreach (var entry in manifest.RootElement.EnumerateObject()) {
            var objectId = entry.Name;
            var texture = entry.Value.GetString();

            if (string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(texture))
                throw new InvalidDataException("Path railing objectId and texture values must not be empty.");

            builder.AddImage(objectId, texture, pathDirectory);
        }
    }

    private static readonly HashSet<uint> invisibleRailingIdHashes = [
        TerrainMaterialLibrary.HashObjectId("openrct2.footpath_railings.invisible"),
        TerrainMaterialLibrary.HashObjectId("rct2tt.footpath_railings.circuitboard_invisible")
    ];
}
