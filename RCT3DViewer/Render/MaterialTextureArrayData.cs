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
    public static MaterialTextureArrayData FromTerrain(TerrainMaterialLibrary materials) => new(
        materials.Width,
        materials.Height,
        materials.LayerCount,
        materials.Pixels,
        materials.NormalPixels,
        materials.SpecularPixels);

    public static MaterialTextureArrayData FromModel(ModelMaterialLibrary material) => new(
        material.Width,
        material.Height,
        1,
        material.BaseColorPixels,
        null,
        null);
}
