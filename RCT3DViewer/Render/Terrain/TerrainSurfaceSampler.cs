using RCT3D.Interop.Models;

namespace RCT3D.Render.Terrain;

internal static class TerrainSurfaceSampler
{
    private const float CornerHeightScale = WorldUnits.TerrainCornerHeightScale;

    public static float GetHeight(RCTTileSurfaceData surface, float u, float v)
    {
        GetCornerHeights(surface, out var north, out var east, out var south, out var west);
        return GetHeight(north, east, south, west, u, v);
    }

    public static float GetHeight(float north, float east, float south, float west, float u, float v)
    {
        var flipDiagonal = ShouldFlipDiagonal(north, east, south, west);

        return flipDiagonal
            ? u >= v
                ? south + (east - south) * u + (north - east) * v
                : south + (north - west) * u + (west - south) * v
            : u + v >= 1.0f
                ? (north - west) * u + (north - east) * v + east - north + west
                : south + (east - south) * u + (west - south) * v;
    }

    public static void GetCornerHeights(RCTTileSurfaceData surface, out float north, out float east, out float south, out float west)
    {
        var baseHeight = surface.BaseZ * WorldUnits.HeightPerBaseUnit;
        north = baseHeight + surface.NorthCornerHeight * CornerHeightScale;
        east = baseHeight + surface.EastCornerHeight * CornerHeightScale;
        south = baseHeight + surface.SouthCornerHeight * CornerHeightScale;
        west = baseHeight + surface.WestCornerHeight * CornerHeightScale;
    }

    public static bool ShouldFlipDiagonal(float north, float east, float south, float west)
    {
        return (north == east && east == south && west != north)
            || (north == south && south == west && east != north);
    }
}
