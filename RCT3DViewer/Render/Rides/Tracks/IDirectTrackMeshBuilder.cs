using RCT3D.Interop.Models;

namespace RCT3D.Render.Rides.Tracks;

internal readonly record struct TrackPieceLocation(
    ushort RideId,
    RCTRideType RideType,
    RCTTrackElementType Part,
    int TileX,
    int TileY,
    int BaseHeight);

internal interface IDirectTrackMeshBuilder
{
    bool Supports(RCTTrackElementType part);

    void AddPiece(
        ReadOnlySpan<RCTTrackElementData> tracks,
        IReadOnlySet<TrackPieceLocation> pieceLocations,
        TrackMeshGeometry geometry,
        ReadOnlySpan<RCTPaletteEntry> palette);
}
