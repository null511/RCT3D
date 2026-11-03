using StbImageSharp;
using System.Text;

namespace RCT3D.Render;

internal class MaterialLibraryBuilder
{
    private readonly List<ImageData> images = new();
    private readonly Dictionary<uint, uint> _layers = new();
    private readonly HashSet<string> objectIds = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<uint, uint> Layers => _layers;
    public int? Width {get; private set;}
    public int? Height {get; private set;}


    public void AddImage(string objectId, string texture, string textureDirectory) {

        if (!objectIds.Add(objectId))
            throw new InvalidDataException("Terrain materials must have unique objectId and texture values.");

        var layer = (uint)images.Count;
        if (!_layers.TryAdd(HashObjectId(objectId), layer))
            throw new InvalidDataException($"Terrain object identifier hash collision: {objectId}");

        var texturePath = Path.GetFullPath(Path.Combine(textureDirectory, texture));
        var rootPath = Path.GetFullPath(textureDirectory) + Path.DirectorySeparatorChar;
        if (!texturePath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(texturePath))
            throw new InvalidDataException($"Terrain texture path is invalid or missing: {texture}");

        using var stream = File.OpenRead(texturePath);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        if (Width.HasValue && (image.Width != Width.Value || image.Height != Height!.Value))
            throw new InvalidDataException($"Terrain texture '{texture}' has dimensions {image.Width}x{image.Height}, expected {Width}x{Height}.");

        Width ??= image.Width;
        Height ??= image.Height;

        images.Add(new ImageData {
            //ObjectId = objectId,
            Pixels = image.Data,
            NormalPixels = LoadCompanion("n", 128, 128, 255, 255),
            SpecularPixels = LoadCompanion("s", 255, 0, 0, 0),
            //Width = image.Width,
            //Height = image.Height,
        });
        return;

        byte[] LoadCompanion(string suffix, byte red, byte green, byte blue, byte alpha) {
            var companionPath = Path.Combine(
                Path.GetDirectoryName(texturePath)!,
                $"{Path.GetFileNameWithoutExtension(texturePath)}_{suffix}{Path.GetExtension(texturePath)}");

            if (!File.Exists(companionPath))
                return CreateSolidPixels(image.Width, image.Height, red, green, blue, alpha);

            using var companionStream = File.OpenRead(companionPath);
            var companion = ImageResult.FromStream(companionStream, ColorComponents.RedGreenBlueAlpha);
            if (companion.Width != image.Width || companion.Height != image.Height)
                throw new InvalidDataException($"Terrain {suffix} texture '{Path.GetFileName(companionPath)}' has dimensions {companion.Width}x{companion.Height}, expected {image.Width}x{image.Height}.");

            return companion.Data;
        }
    }

    public (byte[] color, byte[] normal, byte[] specular) Build()
    {
        if (images.Count == 0)
            throw new ApplicationException("No images have been added!");

        ArgumentNullException.ThrowIfNull(Width);
        ArgumentNullException.ThrowIfNull(Height);

        var layerSize = checked(Width.Value * Height.Value * 4);

        var pixels = new byte[checked(layerSize * images.Count)];
        var normalPixels = new byte[pixels.Length];
        var specularPixels = new byte[pixels.Length];

        for (var layer = 0; layer < images.Count; layer++) {
            images[layer].Pixels.CopyTo(pixels, layer * layerSize);
            images[layer].NormalPixels.CopyTo(normalPixels, layer * layerSize);
            images[layer].SpecularPixels.CopyTo(specularPixels, layer * layerSize);
        }

        return (pixels, normalPixels, specularPixels);
    }

    private static byte[] CreateSolidPixels(int width, int height, byte red, byte green, byte blue, byte alpha)
    {
        var pixels = new byte[checked(width * height * 4)];

        for (var offset = 0; offset < pixels.Length; offset += 4) {
            pixels[offset] = red;
            pixels[offset + 1] = green;
            pixels[offset + 2] = blue;
            pixels[offset + 3] = alpha;
        }

        return pixels;
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

    private class ImageData
    {
        //public string ObjectId;
        public required byte[] Pixels;
        public required byte[] NormalPixels;
        public required byte[] SpecularPixels;
        //public int Width;
        //public int Height;
    }
}
