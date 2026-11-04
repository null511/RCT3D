using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Supports.Builders;

internal sealed class MetalStickSupportMeshBuilder : IRideSupportMeshBuilder
{
    private const float ColumnHalfWidth = 0.05f;

    private static readonly TrackRailSpecularData supportSpecular = new(150, 190);

    public TrackSupportPlacementMode PlacementMode => TrackSupportPlacementMode.TrackBased;


    public void AddSupport(
        RCTTrackElementData track,
        TrackSupportPlacement placement,
        TrackSupportAnchor anchor,
        RideSupportMeshGeometry geometry,
        RCTTileSurfaceData surface,
        TrackRgba8 color,
        float trackHeightOffset)
    {
        var worldX = anchor.Position.X;
        var worldZ = anchor.Position.Z;
        var groundHeight = GetSurfaceHeight(surface, anchor.SurfaceTileX, anchor.SurfaceTileY, worldX, worldZ);

        var topHeight = anchor.Position.Y
            + placement.HeightExtra * WorldUnits.HeightPerBaseUnit / 8.0f
            + trackHeightOffset;

        if (topHeight <= groundHeight) return;

        var center = new Vector3(worldX, (groundHeight + topHeight) * 0.5f, worldZ);
        geometry.AddVerticalTube(center, groundHeight, topHeight, ColumnHalfWidth, 5, color, supportSpecular);
    }

    private static float GetSurfaceHeight(RCTTileSurfaceData surface, int tileX, int tileY, float worldX, float worldZ)
    {
        var baseHeight = surface.BaseZ * WorldUnits.HeightPerBaseUnit;
        var northEast = baseHeight + surface.NorthCornerHeight * 1.5f;
        var southEast = baseHeight + surface.EastCornerHeight * 1.5f;
        var southWest = baseHeight + surface.SouthCornerHeight * 1.5f;
        var northWest = baseHeight + surface.WestCornerHeight * 1.5f;
        var x = (worldX - tileX * WorldUnits.TileSize) / WorldUnits.TileSize;
        var z = (worldZ - tileY * WorldUnits.TileSize) / WorldUnits.TileSize;

        if (x + z >= 1.0f)
            return northEast * (x + z - 1.0f) + southEast * (1.0f - z) + northWest * (1.0f - x);

        return southEast * x + southWest * (1.0f - x - z) + northWest * z;
    }
}
