namespace RCT3D.Render;

internal sealed class ModelMaterialLibrary
{
    public int Width { get; }
    public int Height { get; }
    public byte[] BaseColorPixels { get; }
    public byte[] NormalPixels { get; }
    public byte[] SpecularPixels { get; }

    private ModelMaterialLibrary(int width, int height, byte[] baseColorPixels, byte[] normalPixels, byte[] specularPixels)
    {
        Width = width;
        Height = height;
        BaseColorPixels = baseColorPixels;
        NormalPixels = normalPixels;
        SpecularPixels = specularPixels;
    }

    public static ModelMaterialLibrary FromImportedMaterial(ImportedModelMaterial material)
    {
        var sourceTextures = new[] {
            material.BaseColorTexture,
            material.NormalTexture,
            material.OcclusionTexture,
            material.MetallicRoughnessTexture,
            material.EmissiveTexture,
            material.DisplacementTexture
        };

        var width = sourceTextures.Where(texture => texture is not null).Select(texture => texture!.Width).DefaultIfEmpty(1).Max();
        var height = sourceTextures.Where(texture => texture is not null).Select(texture => texture!.Height).DefaultIfEmpty(1).Max();
        var baseColor = Resample(material.BaseColorTexture, width, height, [255, 255, 255, 255]);
        var normal = Resample(material.NormalTexture, width, height, [128, 128, 255, 255]);
        var occlusion = Resample(material.OcclusionTexture, width, height, [255, 255, 255, 255]);
        var metallicRoughness = Resample(material.MetallicRoughnessTexture, width, height, [255, 255, 255, 255]);
        var emissive = Resample(material.EmissiveTexture, width, height, [0, 0, 0, 255]);
        var displacement = Resample(material.DisplacementTexture, width, height, [0, 0, 0, 255]);
        var packedNormal = new byte[checked(width * height * 4)];
        var packedSpecular = new byte[packedNormal.Length];

        for (var offset = 0; offset < packedNormal.Length; offset += 4) {
            if (material.HasPackedPbrTextures) {
                packedNormal[offset] = normal[offset];
                packedNormal[offset + 1] = normal[offset + 1];
                packedNormal[offset + 2] = normal[offset + 2];
                packedNormal[offset + 3] = 0;
                packedSpecular[offset] = metallicRoughness[offset];
                packedSpecular[offset + 1] = metallicRoughness[offset + 1];
                packedSpecular[offset + 2] = metallicRoughness[offset + 2];
                packedSpecular[offset + 3] = 0;
                continue;
            }

            baseColor[offset] = ScaleChannel(baseColor[offset], material.BaseColorFactor.X);
            baseColor[offset + 1] = ScaleChannel(baseColor[offset + 1], material.BaseColorFactor.Y);
            baseColor[offset + 2] = ScaleChannel(baseColor[offset + 2], material.BaseColorFactor.Z);
            baseColor[offset + 3] = ScaleChannel(baseColor[offset + 3], material.BaseColorFactor.W);

            packedNormal[offset] = ScaleNormalChannel(normal[offset], material.NormalScale);
            packedNormal[offset + 1] = ScaleNormalChannel(normal[offset + 1], material.NormalScale);
            packedNormal[offset + 2] = ApplyStrength(occlusion[offset], material.OcclusionStrength);
            packedNormal[offset + 3] = displacement[offset];

            packedSpecular[offset] = ScaleChannel(metallicRoughness[offset + 1], material.RoughnessFactor);
            packedSpecular[offset + 1] = ScaleChannel(metallicRoughness[offset + 2], material.MetallicFactor);
            packedSpecular[offset + 2] = 0;
            packedSpecular[offset + 3] = (byte)Math.Clamp((int)MathF.Round(
                (emissive[offset] * material.EmissiveFactor.X * 0.2126f
                    + emissive[offset + 1] * material.EmissiveFactor.Y * 0.7152f
                    + emissive[offset + 2] * material.EmissiveFactor.Z * 0.0722f)), 0, 254);
        }

        return new ModelMaterialLibrary(width, height, baseColor, packedNormal, packedSpecular);
    }

    private static byte ScaleChannel(byte channel, float factor) =>
        (byte)Math.Clamp((int)MathF.Round(channel * Math.Clamp(factor, 0.0f, 1.0f)), 0, 255);

    private static byte ScaleNormalChannel(byte channel, float scale) =>
        (byte)Math.Clamp((int)MathF.Round(128.0f + (channel - 128.0f) * Math.Clamp(scale, 0.0f, 1.0f)), 0, 255);

    private static byte ApplyStrength(byte channel, float strength) =>
        (byte)Math.Clamp((int)MathF.Round(255.0f + (channel - 255.0f) * Math.Clamp(strength, 0.0f, 1.0f)), 0, 255);

    private static byte[] Resample(ImportedModelTexture? texture, int width, int height, byte[] fallback)
    {
        if (texture is null) {
            var fallbackPixels = new byte[checked(width * height * 4)];
            for (var offset = 0; offset < fallbackPixels.Length; offset += 4)
                fallback.CopyTo(fallbackPixels, offset);

            return fallbackPixels;
        }

        var result = new byte[checked(width * height * 4)];
        for (var y = 0; y < height; y++) {
            var sourceY = Math.Min(texture.Height - 1, (int)((long)y * texture.Height / height));

            for (var x = 0; x < width; x++) {
                var sourceX = Math.Min(texture.Width - 1, (int)((long)x * texture.Width / width));
                var sourceOffset = (sourceY * texture.Width + sourceX) * 4;
                var destinationOffset = (y * width + x) * 4;

                texture.Pixels.AsSpan(sourceOffset, 4).CopyTo(result.AsSpan(destinationOffset, 4));
            }
        }

        return result;
    }
}
