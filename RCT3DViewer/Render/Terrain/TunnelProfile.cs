namespace RCT3D.Render.Terrain;

internal static class TunnelProfile
{
    public const float WallThickness = 0.15f;
    public static float Inset => WallThickness / WorldUnits.TileSize;
}
