using RCT3D.Render.Terrain;

namespace RCT3D.Render;

internal sealed record MaterialTextureArrayData(
    int Width,
    int Height,
    int LayerCount,
    byte[] BaseColorPixels,
    byte[]? NormalPixels,
    byte[]? SpecularPixels)
{
    public static MaterialTextureArrayData From(IMaterialLibrary library) => new(
        library.Width,
        library.Height,
        library.LayerCount,
        library.ColorPixels,
        library.NormalPixels,
        library.SpecularPixels);

    public static MaterialTextureArrayData FromModel(ModelMaterialLibrary material) => new(
        material.Width,
        material.Height,
        1,
        material.BaseColorPixels,
        material.NormalPixels,
        material.SpecularPixels);
}
