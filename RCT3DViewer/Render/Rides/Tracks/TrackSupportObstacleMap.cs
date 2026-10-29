using RCT3D.Interop.Models;

namespace RCT3D.Render.Rides.Tracks;

internal sealed class TrackSupportObstacleMap
{
    private readonly Dictionary<(int TileX, int TileY), List<OccupiedObject>> occupiedByTile = new();

    public void AddPath(in RCTTilePathData path)
    {
        var baseHeight = path.BaseZ * WorldUnits.HeightPerBaseUnit;
        var topHeight = path.ClearanceHeight * WorldUnits.HeightPerBaseUnit;
        Add(path.TileX, path.TileY, baseHeight, topHeight, null);
    }

    public void AddTrack(in RCTTrackElementData track)
    {
        var bottomHeight = track.BaseHeight * WorldUnits.HeightPerBaseUnit;
        var topHeight = track.ClearanceHeight * WorldUnits.HeightPerBaseUnit;
        Add(track.TileX, track.TileY, bottomHeight, topHeight, track);
    }

    public bool TryGetHighestIntersection(
        int tileX,
        int tileY,
        float supportBottom,
        float supportTop,
        RCTTrackElementData? hostTrack,
        out float highestObjectTop)
    {
        highestObjectTop = float.NegativeInfinity;
        if (!occupiedByTile.TryGetValue((tileX, tileY), out var occupiedObjects))
            return false;

        foreach (var occupiedObject in occupiedObjects) {
            if (hostTrack.HasValue && occupiedObject.TrackElement is { } track && track.Equals(hostTrack.Value))
                continue;

            if (occupiedObject.TopHeight < supportBottom || occupiedObject.BottomHeight > supportTop)
                continue;

            highestObjectTop = MathF.Max(highestObjectTop, occupiedObject.TopHeight);
        }

        return !float.IsNegativeInfinity(highestObjectTop);
    }

    private void Add(
        int tileX,
        int tileY,
        float bottomHeight,
        float topHeight,
        RCTTrackElementData? trackElement)
    {
        if (!float.IsFinite(bottomHeight) || !float.IsFinite(topHeight) || topHeight < bottomHeight)
            throw new ArgumentOutOfRangeException(nameof(topHeight));

        if (!occupiedByTile.TryGetValue((tileX, tileY), out var occupiedObjects)) {
            occupiedObjects = new List<OccupiedObject>();
            occupiedByTile.Add((tileX, tileY), occupiedObjects);
        }

        occupiedObjects.Add(new OccupiedObject(bottomHeight, topHeight, trackElement));
    }

    private readonly record struct OccupiedObject(
        float BottomHeight,
        float TopHeight,
        RCTTrackElementData? TrackElement);
}
