using RCT3D.Render.Rides.Supports;
using RCT3D.Interop.Models;
using RCT3D.Render.Terrain;

namespace RCT3D.Render.Rides.Tracks;

internal sealed record TrackMeshBuildData(
    TrackMeshData Tracks,
    RideSupportMeshData RideSupports,
    WaterMeshData Water,
    TrackCenterlinePiece[] Centerlines,
    RCTTrackElementData[] TrackElements);

internal readonly record struct TrackCenterlinePiece(
    ushort RideId,
    RCTRideType RideType,
    RCTTrackElementType TrackType,
    int OriginTileX,
    int OriginTileY,
    byte Direction,
    int OriginBaseHeight,
    TrackCenterlineSequence[] Sequences);

internal readonly record struct TrackCenterlineSequence(
    int TileX,
    int TileY,
    int BaseHeight,
    TrackCenterlinePath Path);
