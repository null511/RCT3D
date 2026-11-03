namespace RCT3D.Render.Terrain;

public interface IMaterialLibrary
{
    int Width { get; }
    int Height { get; }
    byte[] ColorPixels { get; }
    byte[] NormalPixels { get; }
    byte[] SpecularPixels { get; }
    int LayerCount { get; }
}
