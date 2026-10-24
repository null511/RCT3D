using RCT3D.Interop;
using System.Numerics;

namespace RCT3D.Render.Tracks.Meshing;

internal sealed class MetalBoxedSupportMeshBuilder : IRideSupportMeshBuilder
{
    private const float FootingHalfWidth = 0.19f;
    private const float FootingHalfHeight = 0.07f;
    private const float ColumnHalfWidth = 0.10f;
    private const float ColumnTopThickness = 0.08f;
    private const float SupportSurfaceOffset = 0.002f;

    private static readonly TrackRailSpecularData supportSpecular = new(150, 190);


    public void AddSupport(
        RCTTrackElementData track,
        TrackSupportPlacement placement,
        RideSupportMeshGeometry geometry,
        RCTTileSurfaceData surface,
        TrackRgba8 color)
    {
        var trackDirection = TrackCenterlineGeometry.GetTrackDirection(track.Direction);
        var lateralDirection = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, trackDirection));
        var worldX = (track.TileX + placement.TileOffsetX) * WorldUnits.TileSize;
        var worldZ = (track.TileY + placement.TileOffsetZ) * WorldUnits.TileSize;
        var groundHeight = GetSurfaceHeight(surface, track.TileX, track.TileY, worldX, worldZ) + SupportSurfaceOffset;
        var topHeight = track.BaseHeight * WorldUnits.HeightPerBaseUnit + placement.HeightExtra * WorldUnits.HeightPerBaseUnit;
        if (topHeight <= groundHeight + FootingHalfHeight * 2.0f) return;

        AddSupportColumn(geometry, new Vector3(worldX, groundHeight, worldZ), topHeight, trackDirection, lateralDirection, color);
    }

    private static void AddSupportColumn(
        RideSupportMeshGeometry geometry,
        Vector3 basePosition,
        float topHeight,
        Vector3 trackDirection,
        Vector3 lateralDirection,
        TrackRgba8 color)
    {
        var footingCenter = basePosition + Vector3.UnitY * FootingHalfHeight;
        geometry.AddBox(footingCenter, new Vector3(FootingHalfWidth, FootingHalfHeight, FootingHalfWidth), color, supportSpecular);

        var columnBottom = basePosition.Y + FootingHalfHeight * 2.0f;
        var columnTop = topHeight - ColumnTopThickness;

        if (columnTop > columnBottom) {
            var columnCenter = new Vector3(basePosition.X, (columnBottom + columnTop) * 0.5f, basePosition.Z);
            var columnHalfHeight = (columnTop - columnBottom) * 0.5f;
            geometry.AddBox(columnCenter, new Vector3(ColumnHalfWidth, columnHalfHeight, ColumnHalfWidth), color, supportSpecular);
        }

        //var topCenter = new Vector3(basePosition.X, topHeight - ColumnTopThickness * 0.5f, basePosition.Z);
        //var transverseHalfWidth = 0.36f;
        //var halfExtents = new Vector3(
        //    MathF.Abs(lateralDirection.X) * transverseHalfWidth + MathF.Abs(trackDirection.X) * 0.14f,
        //    ColumnTopThickness * 0.5f,
        //    MathF.Abs(lateralDirection.Z) * transverseHalfWidth + MathF.Abs(trackDirection.Z) * 0.14f);

        //geometry.AddBox(topCenter, halfExtents, color, supportSpecular);
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
