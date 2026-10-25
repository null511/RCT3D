using RCT3D.Interop;
using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Rides.Supports;

internal interface IRideSupportMeshBuilder
{
    TrackSupportPlacementMode PlacementMode { get; }

    bool HasDefaultSupportPlacements => false;

    void GetDefaultSupportPlacements(RCTTrackElementData track, List<TrackSupportPlacement> placements) => placements.Clear();

    void AddSupport(
        RCTTrackElementData track,
        TrackSupportPlacement placement,
        TrackSupportAnchor anchor,
        RideSupportMeshGeometry geometry,
        RCTTileSurfaceData surface,
        TrackRgba8 color,
        float trackHeightOffset);
}
