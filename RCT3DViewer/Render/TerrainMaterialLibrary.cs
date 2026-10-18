using System.Text;
using StbImageSharp;
using System.Text.Json;

namespace RCT3DViewer.Render;

public sealed class TerrainMaterialLibrary
{
    private readonly Dictionary<uint, uint> _layersByObjectIdHash;
    private readonly Dictionary<uint, GrassTextureLayers> _grassLayersByObjectIdHash;

    private sealed record GrassTextureLayers(uint[] Mowed, uint[] Short, uint[] Long);

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }
    public uint FallbackLayer {get;}

    public int LayerCount => Pixels.Length / (Width * Height * 4);


    private TerrainMaterialLibrary(
        int width, int height, byte[] pixels, Dictionary<uint, uint> layersByObjectIdHash,
        Dictionary<uint, GrassTextureLayers> grassLayersByObjectIdHash, uint fallbackLayer)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
        FallbackLayer = fallbackLayer;

        _layersByObjectIdHash = layersByObjectIdHash;
        _grassLayersByObjectIdHash = grassLayersByObjectIdHash;
    }

    public uint GetLayer(uint objectIdHash)
    {
        return _layersByObjectIdHash.TryGetValue(objectIdHash, out var layer) ? layer : FallbackLayer;
    }

    private static uint HashTerrainVariant(uint objectIdHash, byte grassState, int tileX, int tileY)
    {
        unchecked {
            var hash = objectIdHash
                ^ ((uint)tileX * 0x9E3779B9u)
                ^ ((uint)tileY * 0x85EBCA6Bu)
                ^ ((uint)grassState * 0xC2B2AE35u);

            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return hash;
        }
    }

    public uint GetTerrainLayer(uint objectIdHash, byte grassState, bool canGrowGrass, int tileX, int tileY)
    {
        if (!canGrowGrass || !_grassLayersByObjectIdHash.TryGetValue(objectIdHash, out var grassLayers))
            return GetLayer(objectIdHash);

        var variants = grassState switch {
            0 => grassLayers.Mowed,
            <= 3 => grassLayers.Short,
            <= 6 => grassLayers.Long,
            _ => Array.Empty<uint>(),
        };

        if (variants.Length == 0)
            return GetLayer(objectIdHash);

        var variantHash = HashTerrainVariant(objectIdHash, grassState, tileX, tileY);
        return variants[(int)(variantHash % (uint)variants.Length)];
    }

    internal static TerrainMaterialLibrary FromModelMaterial(ImportedModelMaterial material)
    {
        var texture = material.BaseColorTexture;
        var width = texture?.Width ?? 1;
        var height = texture?.Height ?? 1;
        var pixels = texture?.Pixels.ToArray() ?? new byte[] { 255, 255, 255, 255 };
        var factor = material.BaseColorFactor;

        for (var pixel = 0; pixel < pixels.Length; pixel += 4) {
            pixels[pixel] = ScaleChannel(pixels[pixel], factor.X);
            pixels[pixel + 1] = ScaleChannel(pixels[pixel + 1], factor.Y);
            pixels[pixel + 2] = ScaleChannel(pixels[pixel + 2], factor.Z);
            pixels[pixel + 3] = ScaleChannel(pixels[pixel + 3], factor.W);
        }

        return new TerrainMaterialLibrary(width, height, pixels, new Dictionary<uint, uint>(), new Dictionary<uint, GrassTextureLayers>(), 0);
    }

    private static byte ScaleChannel(byte channel, float factor)
    {
        return (byte)Math.Clamp((int)MathF.Round(channel * Math.Clamp(factor, 0.0f, 1.0f)), 0, 255);
    }

    public static TerrainMaterialLibrary Load(string assetDirectory)
    {
        return Load(assetDirectory, null);
    }

    public static TerrainMaterialLibrary Load(string assetDirectory, string? additionalArrayProperty)
    {
        var manifestPath = Path.Combine(assetDirectory, "materials.json");
        using var manifestStream = File.OpenRead(manifestPath);
        using var manifest = JsonDocument.Parse(manifestStream);
        var fallbackObjectId = manifest.RootElement.GetProperty("fallback").GetString();

        if (string.IsNullOrWhiteSpace(fallbackObjectId))
            throw new InvalidDataException("Terrain material manifest fallback must specify an objectId.");

        var materials = manifest.RootElement.GetProperty("materials").EnumerateArray().ToList();
        if (additionalArrayProperty is not null && manifest.RootElement.TryGetProperty(additionalArrayProperty, out var additionalMaterials))
            materials.AddRange(additionalMaterials.EnumerateArray());
        if (materials.Count == 0) throw new InvalidDataException("Terrain material manifest has no materials.");

        var images = new List<(string ObjectId, byte[] Pixels, int Width, int Height)>(materials.Count);
        var objectIds = new HashSet<string>(StringComparer.Ordinal);
        var layers = new Dictionary<uint, uint>();
        var grassLayers = new Dictionary<uint, GrassTextureLayers>();
        uint? configuredFallbackLayer = null;
        int? width = null;
        int? height = null;

        void AddImage(string objectId, string texture)
        {
            if (string.IsNullOrWhiteSpace(texture))
                throw new InvalidDataException("Terrain material texture values must not be empty.");

            var texturePath = Path.GetFullPath(Path.Combine(assetDirectory, texture));
            var rootPath = Path.GetFullPath(assetDirectory) + Path.DirectorySeparatorChar;
            if (!texturePath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(texturePath))
                throw new InvalidDataException($"Terrain texture path is invalid or missing: {texture}");

            using var stream = File.OpenRead(texturePath);
            var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            if (width.HasValue && (image.Width != width.Value || image.Height != height!.Value))
                throw new InvalidDataException($"Terrain texture '{texture}' has dimensions {image.Width}x{image.Height}, expected {width}x{height}.");

            width ??= image.Width;
            height ??= image.Height;
            images.Add((objectId, image.Data, image.Width, image.Height));
        }

        foreach (var material in materials) {
            var objectId = material.GetProperty("objectId").GetString();
            var texture = material.GetProperty("texture").GetString();

            if (string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(texture) || !objectIds.Add(objectId))
                throw new InvalidDataException("Terrain materials must have unique non-empty objectId and texture values.");

            var layer = (uint)images.Count;
            if (!layers.TryAdd(HashObjectId(objectId), layer))
                throw new InvalidDataException($"Terrain object identifier hash collision: {objectId}");

            AddImage(objectId, texture);

            if (string.Equals(objectId, fallbackObjectId, StringComparison.Ordinal))
                configuredFallbackLayer = layer;
        }

        if (manifest.RootElement.TryGetProperty("grassVariants", out var grassVariantEntries)) {
            foreach (var entry in grassVariantEntries.EnumerateArray()) {
                var objectId = entry.GetProperty("objectId").GetString();

                if (string.IsNullOrWhiteSpace(objectId) || !objectIds.Contains(objectId))
                    throw new InvalidDataException("Grass variants must reference an objectId defined in materials.");

                uint[] ReadVariants(string property) {
                    var variantTextures = entry.GetProperty(property).EnumerateArray().ToArray();

                    if (variantTextures.Length == 0)
                        throw new InvalidDataException($"Grass variant group '{property}' must contain at least one texture.");

                    var variantLayers = new uint[variantTextures.Length];
                    for (var variant = 0; variant < variantTextures.Length; variant++) {
                        var texture = variantTextures[variant].GetString();
                        variantLayers[variant] = (uint)images.Count;
                        AddImage($"{objectId}#grass-{property}-{variant}", texture ?? string.Empty);
                    }

                    return variantLayers;
                }

                var objectIdHash = HashObjectId(objectId);
                var textureLayers = new GrassTextureLayers(
                    ReadVariants("mowed"),
                    ReadVariants("short"),
                    ReadVariants("long"));

                if (!grassLayers.TryAdd(objectIdHash, textureLayers))
                    throw new InvalidDataException($"Terrain grass variant object identifier hash collision: {objectId}");
            }
        }

        var layerSize = checked(width!.Value * height!.Value * 4);
        var pixels = new byte[checked(layerSize * images.Count)];

        for (var layer = 0; layer < images.Count; layer++) {
            images[layer].Pixels.CopyTo(pixels, layer * layerSize);
        }

        if (!configuredFallbackLayer.HasValue)
            throw new InvalidDataException($"Terrain fallback object '{fallbackObjectId}' is not defined in materials.");

        return new TerrainMaterialLibrary(width.Value, height.Value, pixels, layers, grassLayers, configuredFallbackLayer.Value);
    }

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
