namespace RCT3D.Render.Paths.Meshing;

internal sealed class PathSurfaceCatalog
{
    private readonly Dictionary<uint, string> directories;

    private PathSurfaceCatalog(Dictionary<uint, string> directories)
    {
        this.directories = directories;
    }

    public static PathSurfaceCatalog Load(string textureRoot)
    {
        var pathDirectory = Path.Combine(textureRoot, "path");
        var directories = new Dictionary<uint, string>();
        foreach (var directory in Directory.EnumerateDirectories(pathDirectory)) {
            var objectId = Path.GetFileName(directory);
            var hash = MaterialLibraryBuilder.HashObjectId(objectId);
            if (!directories.TryAdd(hash, directory))
                throw new InvalidDataException($"Path surface object identifier hash collision: {objectId}");
        }

        return new PathSurfaceCatalog(directories);
    }

    public bool TryGetVariant(uint surfaceIdHash, int spriteIndex, out string texturePath)
    {
        if (spriteIndex is > 0 and <= 51 && directories.TryGetValue(surfaceIdHash, out var directory)) {
            var candidate = Path.Combine(directory, $"{spriteIndex:D2}.png");
            if (File.Exists(candidate)) {
                texturePath = candidate;
                return true;
            }
        }

        texturePath = string.Empty;
        return false;
    }
}
