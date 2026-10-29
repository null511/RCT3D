using System.Numerics;

namespace RCT3D.Render.Rides.Tracks;

internal enum TrackSupportPlacementMode
{
    TileBased,
    TrackBased,
}

internal readonly record struct TrackSupportPlacement(
    float TileOffsetX,
    float TileOffsetZ,
    float TrackLateralOffset,
    int HeightExtra,
    float TrackProgress)
{
    public static TrackSupportPlacement AtTile(float tileOffsetX = 0.5f, float tileOffsetZ = 0.5f, int heightExtra = 0) =>
        new(tileOffsetX, tileOffsetZ, 0.0f, heightExtra, float.NaN);

    public static TrackSupportPlacement OnTrack(float lateralOffset = 0.0f, int heightExtra = 0, float trackProgress = float.NaN) =>
        new(0.5f, 0.5f, lateralOffset, heightExtra, trackProgress);
}

internal readonly record struct TrackSupportAnchor(
    Vector3 Position,
    Vector3 Tangent,
    int SurfaceTileX,
    int SurfaceTileY)
{
    public float? SupportBaseHeight { get; init; }
}
