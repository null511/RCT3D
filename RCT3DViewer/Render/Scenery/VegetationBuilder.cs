using RCT3D.Render.Terrain;
using StbImageSharp;
using System.Numerics;

namespace RCT3D.Render.Scenery;

internal sealed class VegetationBuilder
{
    private const float PixelToWorld = WorldUnits.TileSize / 32.0f;

    private readonly string textureDirectory;
    private readonly Dictionary<uint, string> objectIdsByHash = new();
    private readonly Dictionary<uint, ImportedModelMesh[]?> builtByObjectIdHash = new();


    public VegetationBuilder(string textureDirectory)
    {
        this.textureDirectory = Path.GetFullPath(textureDirectory);
        foreach (var path in Directory.EnumerateFiles(this.textureDirectory, "*-north.png")) {
            var objectId = Path.GetFileNameWithoutExtension(path)[..^"-north".Length];
            var hash = TerrainMaterialLibrary.HashObjectId(objectId);

            if (objectIdsByHash.TryGetValue(hash, out var existing) && !string.Equals(existing, objectId, StringComparison.Ordinal))
                throw new InvalidDataException($"Vegetation object identifier hash collision: '{existing}' and '{objectId}'.");

            objectIdsByHash[hash] = objectId;
        }
    }

    public bool TryBuild(uint objectIdHash, out ImportedModelMesh[] meshes)
    {
        if (builtByObjectIdHash.TryGetValue(objectIdHash, out var cachedMeshes)) {
            meshes = cachedMeshes ?? [];
            return cachedMeshes is not null;
        }

        if (!objectIdsByHash.TryGetValue(objectIdHash, out var objectId)) {
            builtByObjectIdHash.Add(objectIdHash, null);
            meshes = [];
            return false;
        }

        var north = LoadTexture($"{objectId}-north.png");
        var west = LoadTexture($"{objectId}-west.png");

        if (north is null || west is null) {
            builtByObjectIdHash.Add(objectIdHash, null);
            meshes = [];
            return false;
        }

        meshes = [
            CreatePlane("VegetationNorth", north, alongX: true),
            CreatePlane("VegetationWest", west, alongX: false),
        ];

        builtByObjectIdHash.Add(objectIdHash, meshes);
        return true;
    }

    public void ClearCache() => builtByObjectIdHash.Clear();

    private VegetationTexture? LoadTexture(string filename)
    {
        var path = Path.GetFullPath(Path.Combine(textureDirectory, filename));
        if (!path.StartsWith(textureDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            return null;

        using var stream = File.OpenRead(path);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        var baseTexture = new ImportedModelTexture(image.Width, image.Height, image.Data);

        return new VegetationTexture(baseTexture,
            LoadCompanion(path, image.Width, image.Height, "_n", [128, 128, 255, 255]),
            LoadCompanion(path, image.Width, image.Height, "_s", [255, 0, 0, 0]));
    }

    private ImportedModelMesh CreatePlane(string name, VegetationTexture textures, bool alongX)
    {
        var material = new ImportedModelMaterial(
            Vector4.One,
            Vector3.Zero,
            textures.BaseColor,
            null,
            DoubleSided: true,
            NormalTexture: textures.Normal,
            MetallicRoughnessTexture: textures.Specular,
            HasPackedPbrTextures: true);

        var halfWidth = textures.BaseColor.Width * PixelToWorld * 0.5f;
        var height = textures.BaseColor.Height * PixelToWorld;

        var frontVertices = alongX
            ? new[] {
                new ImportedModelVertex(new Vector3(-halfWidth, 0, 0), -Vector3.UnitZ, new Vector2(0, 1)),
                new ImportedModelVertex(new Vector3(halfWidth, 0, 0), -Vector3.UnitZ, new Vector2(1, 1)),
                new ImportedModelVertex(new Vector3(halfWidth, height, 0), -Vector3.UnitZ, new Vector2(1, 0)),
                new ImportedModelVertex(new Vector3(-halfWidth, height, 0), -Vector3.UnitZ, new Vector2(0, 0)),
            }
            : new[] {
                new ImportedModelVertex(new Vector3(0, 0, -halfWidth), Vector3.UnitX, new Vector2(0, 1)),
                new ImportedModelVertex(new Vector3(0, 0, halfWidth), Vector3.UnitX, new Vector2(1, 1)),
                new ImportedModelVertex(new Vector3(0, height, halfWidth), Vector3.UnitX, new Vector2(1, 0)),
                new ImportedModelVertex(new Vector3(0, height, -halfWidth), Vector3.UnitX, new Vector2(0, 0)),
            };

        var vertices = new ImportedModelVertex[frontVertices.Length * 2];
        frontVertices.CopyTo(vertices, 0);
        for (var index = 0; index < frontVertices.Length; index++) {
            var frontVertex = frontVertices[index];
            vertices[index + frontVertices.Length] = new ImportedModelVertex(
                frontVertex.Position,
                -frontVertex.Normal,
                new Vector2(1.0f - frontVertex.TextureCoordinate.X, frontVertex.TextureCoordinate.Y));
        }

        uint[] indices = [
            0, 1, 2, 0, 2, 3,
            6, 5, 4, 7, 6, 4,
        ];

        return new ImportedModelMesh(name, vertices, indices, material);
    }

    private ImportedModelTexture LoadCompanion(string basePath, int width, int height, string suffix, byte[] fallback)
    {
        var companionPath = Path.Combine(Path.GetDirectoryName(basePath)!, Path.GetFileNameWithoutExtension(basePath) + $"{suffix}.png");
        if (!File.Exists(companionPath))
            return CreateSolidTexture(width, height, fallback);

        using var companionStream = File.OpenRead(companionPath);
        var companion = ImageResult.FromStream(companionStream, ColorComponents.RedGreenBlueAlpha);
        if (companion.Width != width || companion.Height != height)
            throw new InvalidDataException($"Vegetation {suffix} texture '{Path.GetFileName(companionPath)}' has dimensions {companion.Width}x{companion.Height}, expected {width}x{height}.");
        
        return new ImportedModelTexture(companion.Width, companion.Height, companion.Data);
    }

    private static ImportedModelTexture CreateSolidTexture(int width, int height, byte[] color)
    {
        var pixels = new byte[checked(width * height * 4)];

        for (var offset = 0; offset < pixels.Length; offset += 4)
            color.CopyTo(pixels, offset);

        return new ImportedModelTexture(width, height, pixels);
    }

    private sealed record VegetationTexture(
        ImportedModelTexture BaseColor,
        ImportedModelTexture Normal,
        ImportedModelTexture Specular);
}
