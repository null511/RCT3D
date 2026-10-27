using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Supports.Builders;

internal sealed class MetalTubesSupportMeshBuilder : IRideSupportMeshBuilder
{
    private const int PillarSideCount = 6;
    private const float FootingHalfWidth = 0.20f;
    private const float FootingHalfHeight = 0.24f;
    private const float PlateHalfWidth = 0.13f;
    private const float PlateHalfHeight = 0.025f;
    private const float PillarRadius = 0.10f;
    private const float PillarTopOffset = 0.08f;
    private const float SupportSurfaceOffset = -0.2f;

    private static readonly TrackRailSpecularData supportSpecular = new(150, 190);
    private static readonly TrackRailSpecularData concreteSpecular = new(220, 0);
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
        var topHeight = anchor.Position.Y
            + placement.HeightExtra * WorldUnits.HeightPerBaseUnit / 8.0f
            + trackHeightOffset;

        var footingHeight = FootingHalfHeight * 2.0f;
        var plateHeight = PlateHalfHeight * 2.0f;
        var pillarBottom = groundHeight + footingHeight + plateHeight;
        var pillarTop = topHeight - PillarTopOffset;
        if (pillarTop <= pillarBottom) return;

        var footingCenter = new Vector3(worldX, groundHeight + FootingHalfHeight, worldZ);
        geometry.AddBox(
            footingCenter,
            new Vector3(FootingHalfWidth, FootingHalfHeight, FootingHalfWidth),
            color_footing,
            concreteSpecular);

        var plateCenter = new Vector3(worldX, groundHeight + footingHeight + PlateHalfHeight, worldZ);
        geometry.AddBox(
            plateCenter,
            new Vector3(PlateHalfWidth, PlateHalfHeight, PlateHalfWidth),
            color,
            supportSpecular);

        geometry.AddVerticalTube(
            new Vector3(worldX, 0.0f, worldZ),
            pillarBottom,
            pillarTop,
            PillarRadius,
            PillarSideCount,
            color,
            supportSpecular);
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
