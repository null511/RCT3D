namespace RCT3D.Render;

internal sealed class ModelMaterialLibrary
{
    public int Width { get; }
    public int Height { get; }
    public byte[] BaseColorPixels { get; }

    private ModelMaterialLibrary(int width, int height, byte[] baseColorPixels)
    {
        Width = width;
        Height = height;
        BaseColorPixels = baseColorPixels;
    }

    public static ModelMaterialLibrary FromImportedMaterial(ImportedModelMaterial material)
    {
        var texture = material.BaseColorTexture;
        var width = texture?.Width ?? 1;
        var height = texture?.Height ?? 1;
        var pixels = texture?.Pixels.ToArray() ?? [255, 255, 255, 255];
        var factor = material.BaseColorFactor;

        for (var pixel = 0; pixel < pixels.Length; pixel += 4) {
            pixels[pixel] = ScaleChannel(pixels[pixel], factor.X);
            pixels[pixel + 1] = ScaleChannel(pixels[pixel + 1], factor.Y);
            pixels[pixel + 2] = ScaleChannel(pixels[pixel + 2], factor.Z);
            pixels[pixel + 3] = ScaleChannel(pixels[pixel + 3], factor.W);
        }

        return new ModelMaterialLibrary(width, height, pixels);
    }

    private static byte ScaleChannel(byte channel, float factor) =>
        (byte)Math.Clamp((int)MathF.Round(channel * Math.Clamp(factor, 0.0f, 1.0f)), 0, 255);
}
