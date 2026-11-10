using RCT3D.Render.Terrain;
using StbImageSharp;
using System.Numerics;

namespace RCT3D.Render.Scenery;

internal sealed class VegetationBuilder
{
    private const float SurfaceBias = -0.12f;
    private const bool UseUpNormals = true;

    private readonly string textureDirectory;
    private readonly Dictionary<uint, string> objectIdsByHash = new();
    private readonly Dictionary<uint, ImportedModelMesh[]?> builtByObjectIdHash = new();


    public VegetationBuilder(string textureDirectory)
    {
        this.textureDirectory = Path.GetFullPath(textureDirectory);
        foreach (var path in Directory.EnumerateFiles(this.textureDirectory, "*.png")) {
            var objectId = Path.GetFileNameWithoutExtension(path);
            if (objectId.EndsWith("_n", StringComparison.OrdinalIgnoreCase)
                || objectId.EndsWith("_s", StringComparison.OrdinalIgnoreCase))
                continue;

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

        var textures = LoadTexture($"{objectId}.png");
        if (textures is null) {
            builtByObjectIdHash.Add(objectIdHash, null);
            meshes = [];
            return false;
        }

        meshes = [CreateMesh(objectId, textures)];

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

    private ImportedModelMesh CreateMesh(string name, VegetationTexture textures)
    {
        if (textures.BaseColor.Width % 2 != 0)
            throw new InvalidDataException($"Vegetation atlas '{name}' must have two equal-width direction panels.");

        var material = new ImportedModelMaterial(
            Vector4.One,
            Vector3.Zero,
            textures.BaseColor,
            null,
            DoubleSided: true,
            NormalTexture: textures.Normal,
            MetallicRoughnessTexture: textures.Specular,
            HasPackedPbrTextures: true);

        var halfWidth = textures.BaseColor.Width * 0.25f * WorldUnits.NativeUnit;
        var height = textures.BaseColor.Height * WorldUnits.NativeUnit;
        var uInset = 0.5f / textures.BaseColor.Width;
        var vInset = 0.5f / textures.BaseColor.Height;
        var northUMin = uInset;
        var northUMax = 0.5f - uInset;
        var westUMin = 0.5f + uInset;
        var westUMax = 1.0f - uInset;
        var vertices = new List<ImportedModelVertex>(16);
        var indices = new List<uint>(24);

        var minY = SurfaceBias;
        var maxY = minY + height;

        AddPlane(vertices, indices, [
            new ImportedModelVertex(new Vector3(-halfWidth, minY, 0), -Vector3.UnitZ, new Vector2(northUMin, 1.0f - vInset)),
            new ImportedModelVertex(new Vector3(halfWidth, minY, 0), -Vector3.UnitZ, new Vector2(northUMax, 1.0f - vInset)),
            new ImportedModelVertex(new Vector3(halfWidth, maxY, 0), -Vector3.UnitZ, new Vector2(northUMax, vInset)),
            new ImportedModelVertex(new Vector3(-halfWidth, maxY, 0), -Vector3.UnitZ, new Vector2(northUMin, vInset)),
        ], northUMin, northUMax);

        AddPlane(vertices, indices, [
            new ImportedModelVertex(new Vector3(0, minY, -halfWidth), Vector3.UnitX, new Vector2(westUMin, 1.0f - vInset)),
            new ImportedModelVertex(new Vector3(0, minY, halfWidth), Vector3.UnitX, new Vector2(westUMax, 1.0f - vInset)),
            new ImportedModelVertex(new Vector3(0, maxY, halfWidth), Vector3.UnitX, new Vector2(westUMax, vInset)),
            new ImportedModelVertex(new Vector3(0, maxY, -halfWidth), Vector3.UnitX, new Vector2(westUMin, vInset)),
        ], westUMin, westUMax);

        return new ImportedModelMesh(name, vertices.ToArray(), indices.ToArray(), material);
    }

    private static void AddPlane(List<ImportedModelVertex> vertices, List<uint> indices, ImportedModelVertex[] frontVertices, float uMin, float uMax)
    {
        if (UseUpNormals) {
            for (var i = 0; i < frontVertices.Length; i++)
                frontVertices[i].Normal = Vector3.UnitY;
        }

        var firstVertex = (uint)vertices.Count;
        vertices.AddRange(frontVertices);

        foreach (var frontVertex in frontVertices) {
            var n = UseUpNormals ? Vector3.UnitY : -frontVertex.Normal;
            vertices.Add(new ImportedModelVertex(
                frontVertex.Position,
                n,
                new Vector2(uMin + uMax - frontVertex.TextureCoordinate.X, frontVertex.TextureCoordinate.Y)));
        }

        indices.AddRange([
            firstVertex, firstVertex + 1, firstVertex + 2, firstVertex, firstVertex + 2, firstVertex + 3,
            firstVertex + 6, firstVertex + 5, firstVertex + 4, firstVertex + 7, firstVertex + 6, firstVertex + 4,
        ]);
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
