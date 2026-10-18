using StbImageSharp;
using System.Text.Json;

namespace RCT3DViewer.Render;

public sealed class TerrainMaterialLibrary
{
    private readonly Dictionary<uint, uint> _layersByObjectIdHash;

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }
    public uint FallbackLayer {get;}

    public int LayerCount => Pixels.Length / (Width * Height * 4);


    private TerrainMaterialLibrary(int width, int height, byte[] pixels, Dictionary<uint, uint> layersByObjectIdHash, uint fallbackLayer)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
        FallbackLayer = fallbackLayer;

        _layersByObjectIdHash = layersByObjectIdHash;
    }

    public uint GetLayer(uint objectIdHash)
    {
        return _layersByObjectIdHash.TryGetValue(objectIdHash, out var layer) ? layer : FallbackLayer;
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

        return new TerrainMaterialLibrary(width, height, pixels, new Dictionary<uint, uint>(), 0);
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
        int? width = null;
        int? height = null;

        foreach (var material in materials) {
            var objectId = material.GetProperty("objectId").GetString();
            var texture = material.GetProperty("texture").GetString();

            if (string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(texture) || !objectIds.Add(objectId))
                throw new InvalidDataException("Terrain materials must have unique non-empty objectId and texture values.");

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

        var layerSize = checked(width!.Value * height!.Value * 4);
        var pixels = new byte[checked(layerSize * images.Count)];
        var layers = new Dictionary<uint, uint>();
        var fallbackLayer = uint.MaxValue;

        for (var layer = 0; layer < images.Count; layer++) {
            images[layer].Pixels.CopyTo(pixels, layer * layerSize);
            var idHash = HashObjectId(images[layer].ObjectId);

            if (!layers.TryAdd(idHash, (uint)layer))
                throw new InvalidDataException($"Terrain object identifier hash collision: {images[layer].ObjectId}");

            if (string.Equals(images[layer].ObjectId, fallbackObjectId, StringComparison.Ordinal))
                fallbackLayer = (uint)layer;
        }

        if (fallbackLayer == uint.MaxValue)
            throw new InvalidDataException($"Terrain fallback object '{fallbackObjectId}' is not defined in materials.");

        return new TerrainMaterialLibrary(width.Value, height.Value, pixels, layers, fallbackLayer);
    }

    public static uint HashObjectId(string objectId)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;

        foreach (var value in System.Text.Encoding.UTF8.GetBytes(objectId)) {
            hash ^= value;
            hash *= prime;
        }

        return hash;
    }
}
