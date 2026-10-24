using RCT3D.Interop;

namespace RCT3D.Render.Tracks.Meshing;

internal interface IRideSupportMeshBuilder
{
    void AddSupport(
        RCTTrackElementData track,
        TrackSupportPlacement placement,
        RideSupportMeshGeometry geometry,
        RCTTileSurfaceData surface,
        TrackRgba8 color);
}
