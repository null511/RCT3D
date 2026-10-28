using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Supports.Builders;

internal sealed class MetalForkSupportMeshBuilder : IRideSupportMeshBuilder
{
    private const float BeamHalfWidth = 0.06f;
    private const float CouplingHalfWidth = 0.08f;
    private const float CouplingHalfDepth = 0.08f;
    private const float ForkHalfSpan = 0.4f;
    private const float ForkBaseRise = 0.4f;
    private const float SupportSurfaceOffset = -0.08f;
    private const float CouplingSpacing = 4.0f;

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
        var trackDirection = new Vector3(anchor.Tangent.X, 0.0f, anchor.Tangent.Z);

        trackDirection = trackDirection.LengthSquared() <= float.Epsilon
            ? TrackCenterlineGeometry.GetTrackDirection(track.Direction)
            : Vector3.Normalize(trackDirection);

        var lateralDirection = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, trackDirection));
        var center = new Vector3(anchor.Position.X, 0f, anchor.Position.Z);

        var groundHeight = GetSurfaceHeight(surface, anchor.SurfaceTileX, anchor.SurfaceTileY, center.X, center.Z) + SupportSurfaceOffset;
        var topHeight = anchor.Position.Y
            + placement.HeightExtra * WorldUnits.HeightPerBaseUnit / 8.0f
            + trackHeightOffset;

        if (topHeight <= groundHeight + ForkBaseRise) return;

        center.Y = groundHeight;
        var beamEnd = new Vector3(center.X, topHeight, center.Z);
        geometry.AddBeam(center, beamEnd, lateralDirection, BeamHalfWidth, BeamHalfWidth, color, supportSpecular);

        var forkTop = center + Vector3.UnitY * ForkBaseRise;
        var leftFoot = center - lateralDirection * ForkHalfSpan;
        var rightFoot = center + lateralDirection * ForkHalfSpan;
        geometry.AddBeam(leftFoot, forkTop, trackDirection, BeamHalfWidth, BeamHalfWidth, color, supportSpecular);
        geometry.AddBeam(rightFoot, forkTop, trackDirection, BeamHalfWidth, BeamHalfWidth, color, supportSpecular);

        var supportHeight = center.Y;
        while (supportHeight + CouplingSpacing < topHeight) {
            supportHeight += CouplingSpacing;
            var couplingCenter = new Vector3(center.X, supportHeight, center.Z);
            geometry.AddBox(couplingCenter, new Vector3(CouplingHalfWidth, BeamHalfWidth, CouplingHalfDepth), color, supportSpecular);
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
