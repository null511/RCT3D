using RCT3D.Interop;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Supports.Builders;

internal sealed class MetalBoxedSupportMeshBuilder : IRideSupportMeshBuilder
{
    private const float FootingHalfWidth = 0.20f;
    private const float FootingHalfHeight = 0.24f;
    private const float ColumnHalfWidth = 0.10f;
    private const float ColumnTopThickness = 0.08f;
    private const float SupportSurfaceOffset = -0.2f;

    private static readonly TrackRailSpecularData supportSpecular = new(150, 190);
    private static readonly TrackRgba8 color_footing = new(160, 160, 160, 255);

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

        var groundHeight = GetSurfaceHeight(surface, anchor.SurfaceTileX, anchor.SurfaceTileY, worldX, worldZ) + SupportSurfaceOffset;

        var topHeight = track.BaseHeight * WorldUnits.HeightPerBaseUnit
            + placement.HeightExtra * WorldUnits.HeightPerBaseUnit / 8.0f
            + trackHeightOffset;

        if (topHeight <= groundHeight + FootingHalfHeight * 2.0f) return;

        AddSupportColumn(geometry, new Vector3(worldX, groundHeight, worldZ), topHeight, color);
    }

    private static void AddSupportColumn(
        RideSupportMeshGeometry geometry,
        Vector3 basePosition,
        float topHeight,
        TrackRgba8 color)
    {
        var footingCenter = basePosition + Vector3.UnitY * FootingHalfHeight;
        geometry.AddBox(footingCenter, new Vector3(FootingHalfWidth, FootingHalfHeight, FootingHalfWidth), color_footing, supportSpecular);

        var columnBottom = basePosition.Y + FootingHalfHeight * 2.0f;
        var columnTop = topHeight - ColumnTopThickness;

        if (columnTop > columnBottom) {
            var columnCenter = new Vector3(basePosition.X, (columnBottom + columnTop) * 0.5f, basePosition.Z);
            var columnHalfHeight = (columnTop - columnBottom) * 0.5f;
            geometry.AddBox(columnCenter, new Vector3(ColumnHalfWidth, columnHalfHeight, ColumnHalfWidth), color, supportSpecular);
        }
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
