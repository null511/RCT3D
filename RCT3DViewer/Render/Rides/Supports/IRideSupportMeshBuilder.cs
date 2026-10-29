using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Supports;

internal interface IRideSupportMeshBuilder
{
    TrackSupportPlacementMode PlacementMode { get; }

    bool HasDefaultSupportPlacements => false;

    void GetDefaultSupportPlacements(RCTTrackElementData track, List<TrackSupportPlacement> placements) => placements.Clear();

    float GetTrackLateralOffset(in RCTTrackElementData track, ReadOnlySpan<Vector3> centerlinePoints, float progress) => 0.0f;

    float GetSupportTopHeight(
        in RCTTrackElementData track,
        TrackSupportPlacement placement,
        TrackSupportAnchor anchor,
        float trackHeightOffset) => PlacementMode == TrackSupportPlacementMode.TrackBased
            ? anchor.Position.Y + placement.HeightExtra * WorldUnits.HeightPerBaseUnit / 8.0f + trackHeightOffset
            : track.BaseHeight * WorldUnits.HeightPerBaseUnit + placement.HeightExtra * WorldUnits.HeightPerBaseUnit / 8.0f;

    void AddSupport(
        RCTTrackElementData track,
        TrackSupportPlacement placement,
        TrackSupportAnchor anchor,
        RideSupportMeshGeometry geometry,
        RCTTileSurfaceData surface,
        TrackRgba8 color,
        float trackHeightOffset);
}
