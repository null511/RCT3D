using RCT3D.Interop.Models;
using Serilog;

namespace RCT3D.Render.Paths.Meshing;

internal readonly record struct PathSurfaceMaterial(uint DescriptorIndex, uint Layer);

internal sealed record PathSurfaceTextureArray(int Width, int Height, byte[] ColorPixels, byte[] NormalPixels, byte[] SpecularPixels)
{
    public int LayerCount => ColorPixels.Length / checked(Width * Height * 4);
}

internal sealed class PathSurfaceMaterials
{
    private readonly Dictionary<(uint SurfaceIdHash, int SpriteIndex), PathSurfaceMaterial> materials;

    public IReadOnlyList<PathSurfaceTextureArray> Arrays { get; }

    private PathSurfaceMaterials(List<PathSurfaceTextureArray> arrays, Dictionary<(uint, int), PathSurfaceMaterial> materials)
    {
        Arrays = arrays;
        this.materials = materials;
    }

    public PathSurfaceMaterial GetMaterial(RCTTilePathData path)
    {
        if (PathSurfaceVariant.TryGetSpriteIndex(path, out var spriteIndex)
            && materials.TryGetValue((path.SurfaceIdHash, spriteIndex), out var material))
            return material;

        return default;
    }

    public static PathSurfaceMaterials Load(string textureRoot, ReadOnlySpan<RCTTilePathData> paths)
    {
        var catalog = PathSurfaceCatalog.Load(textureRoot);
        var requested = new HashSet<(uint SurfaceIdHash, int SpriteIndex)>();
        var invalid = new HashSet<(uint SurfaceIdHash, byte Edges, byte Corners, bool IsQueue)>();
        foreach (var path in paths) {
            if (PathSurfaceVariant.TryGetSpriteIndex(path, out var spriteIndex))
                requested.Add((path.SurfaceIdHash, spriteIndex));
            else
                invalid.Add((path.SurfaceIdHash, path.Edges, path.Corners, path.IsQueue));
        }

        foreach (var value in invalid)
            Log.Warning("Unsupported path connectivity for surface {SurfaceIdHash:X8}: edges {Edges:X2}, corners {Corners:X2}, queue {IsQueue}; using missing texture", value.SurfaceIdHash, value.Edges, value.Corners, value.IsQueue);

        var arrays = new List<PathSurfaceTextureArray>();
        var materials = new Dictionary<(uint, int), PathSurfaceMaterial>();
        var fallback = new MaterialLibraryBuilder();
        fallback.AddImage("missing", "missing.png", Path.Combine(textureRoot, "path"));
        arrays.Add(BuildArray(fallback));

        foreach (var surface in requested.OrderBy(key => key.SurfaceIdHash).ThenBy(key => key.SpriteIndex).GroupBy(key => key.SurfaceIdHash)) {
            var builder = new MaterialLibraryBuilder();
            var added = 0u;
            var descriptorIndex = checked((uint)arrays.Count);
            foreach (var key in surface) {
                if (!catalog.TryGetVariant(key.SurfaceIdHash, key.SpriteIndex, out var texturePath)) {
                    Log.Warning("Missing path texture for surface {SurfaceIdHash:X8}, sprite {SpriteIndex:D2}; using missing texture", key.SurfaceIdHash, key.SpriteIndex);
                    continue;
                }

                builder.AddImage(key.SpriteIndex.ToString(), Path.GetFileName(texturePath), Path.GetDirectoryName(texturePath)!);
                materials.Add(key, new PathSurfaceMaterial(descriptorIndex, added++));
            }

            if (added > 0)
                arrays.Add(BuildArray(builder));
        }

        return new PathSurfaceMaterials(arrays, materials);
    }

    private static PathSurfaceTextureArray BuildArray(MaterialLibraryBuilder builder)
    {
        if (builder.Width != 32 || builder.Height != 32)
            throw new InvalidDataException($"Path surface textures must be 32x32, got {builder.Width}x{builder.Height}.");

        var (color, normal, specular) = builder.Build();
        return new PathSurfaceTextureArray(builder.Width.Value, builder.Height.Value, color, normal, specular);
    }
}
