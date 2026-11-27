using Silk.NET.Vulkan;

namespace RCT3D.Render.Paths;

// The alpha threshold must agree with lib.common.AlphaThreshold.
internal static class PathSurfaceMipmaps
{
    private const int Size = 32;
    private const int Levels = 6;
    private const int AlphaThresholdByte = 51;

    public static (byte[] Pixels, BufferImageCopy[] Regions) Build(byte[] source, int layers, bool color)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (layers <= 0 || source.Length != checked(Size * Size * 4 * layers))
            throw new ArgumentException("Expected 32x32 RGBA pixels for every path texture layer.", nameof(source));

        var layerStride = Size * Size * 4;
        var totalSize = 0;
        for (var level = 0; level < Levels; level++) {
            var dimension = Size >> level;
            totalSize = checked(totalSize + dimension * dimension * 4 * layers);
        }
        var pixels = new byte[totalSize];
        var regions = new BufferImageCopy[Levels];
        var offset = 0;
        var previous = source;
        for (var level = 0; level < Levels; level++) {
            var dimension = Size >> level;
            byte[] current;
            if (level == 0) {
                current = source;
            }
            else {
                current = new byte[dimension * dimension * 4 * layers];
                var previousDimension = dimension * 2;
                for (var layer = 0; layer < layers; layer++) {
                    var originalCoverage = 0;
                    if (color) {
                        for (var p = 0; p < Size * Size; p++)
                            if (source[layer * layerStride + p * 4 + 3] >= AlphaThresholdByte) originalCoverage++;
                    }
                    for (var y = 0; y < dimension; y++) {
                        for (var x = 0; x < dimension; x++) {
                            var destination = ((layer * dimension + y) * dimension + x) * 4;
                            var alpha = 0f;
                            var r = 0f; var g = 0f; var b = 0f;
                            for (var sy = 0; sy < 2; sy++) {
                                for (var sx = 0; sx < 2; sx++) {
                                    var index = ((layer * previousDimension + y * 2 + sy) * previousDimension + x * 2 + sx) * 4;
                                    var weight = color ? previous[index + 3] / 255f : 1f;
                                    alpha += previous[index + 3];
                                    r += (color ? SrgbToLinear(previous[index]) : previous[index] / 255f) * weight;
                                    g += (color ? SrgbToLinear(previous[index + 1]) : previous[index + 1] / 255f) * weight;
                                    b += (color ? SrgbToLinear(previous[index + 2]) : previous[index + 2] / 255f) * weight;
                                }
                            }
                            var divisor = color ? alpha / 255f : 4f;
                            if (divisor > 0) {
                                current[destination] = ToByte(color ? LinearToSrgb(r / divisor) : r / divisor);
                                current[destination + 1] = ToByte(color ? LinearToSrgb(g / divisor) : g / divisor);
                                current[destination + 2] = ToByte(color ? LinearToSrgb(b / divisor) : b / divisor);
                            }
                            current[destination + 3] = (byte)Math.Clamp((int)MathF.Round(alpha / 4f), 0, 255);
                        }
                    }
                    if (color) PreserveCoverage(current, layer * dimension * dimension * 4, dimension * dimension, originalCoverage / (float)(Size * Size));
                }
            }
            Array.Copy(current, 0, pixels, offset, current.Length);
            regions[level] = new BufferImageCopy {
                BufferOffset = (ulong)offset,
                ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, (uint)level, 0, (uint)layers),
                ImageExtent = new Extent3D((uint)dimension, (uint)dimension, 1)
            };
            offset += current.Length;
            previous = current;
        }
        return (pixels, regions);
    }

    private static void PreserveCoverage(byte[] pixels, int offset, int count, float target)
    {
        var bestScale = 1f;
        var bestError = float.MaxValue;
        // Every possible coverage transition occurs when a nonzero alpha crosses the cutoff.
        for (var i = 0; i <= count; i++) {
            var alpha = i == count ? 255 : pixels[offset + i * 4 + 3];
            var scale = i == count ? 1f : alpha == 0 ? 0f : AlphaThresholdByte / (float)alpha;
            var covered = 0;
            for (var j = 0; j < count; j++)
                if (pixels[offset + j * 4 + 3] * scale >= AlphaThresholdByte) covered++;
            var error = MathF.Abs(covered / (float)count - target);
            if (error < bestError || (error == bestError && MathF.Abs(scale - 1f) < MathF.Abs(bestScale - 1f))) {
                bestError = error;
                bestScale = scale;
            }
        }
        for (var i = 0; i < count; i++) {
            var index = offset + i * 4 + 3;
            pixels[index] = (byte)Math.Clamp((int)MathF.Round(pixels[index] * bestScale), 0, 255);
        }
    }

    private static float SrgbToLinear(byte value)
    {
        var s = value / 255f;
        return s <= 0.04045f ? s / 12.92f : MathF.Pow((s + 0.055f) / 1.055f, 2.4f);
    }

    private static float LinearToSrgb(float value) => value <= 0.0031308f ? value * 12.92f : 1.055f * MathF.Pow(value, 1f / 2.4f) - 0.055f;
    private static byte ToByte(float value) => (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
}
