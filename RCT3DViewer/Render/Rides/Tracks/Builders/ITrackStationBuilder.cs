using RCT3D.Interop.Models;

namespace RCT3D.Render.Rides.Tracks.Builders;

internal interface ITrackStationBuilder
{
    void AddStationTile(
        RCTTrackElementType trackType,
        int tileX,
        int tileY,
        byte direction,
        byte baseHeight,
        TrackMeshGeometry geometry,
        TrackRgba8 supportColor,
        StationSideOpenings sideOpenings);
}

[Flags]
internal enum StationSideOpenings : byte
{
    None = 0,
    NegativeSide = 1,
    PositiveSide = 2,
}
