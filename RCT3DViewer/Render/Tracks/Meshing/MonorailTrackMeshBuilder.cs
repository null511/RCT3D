using System.Runtime.InteropServices;
using RCT3D.Interop;

namespace RCT3D.Render.Tracks.Meshing;

internal sealed class MonorailTrackMeshBuilder
{
    private readonly MonorailTrackBuilderResolver _resolver = new();

    private readonly record struct TrackPieceKey(
        ushort RideId,
        RCTTrackElementType TrackType,
        int OriginTileX,
        int OriginTileY,
        byte Direction,
        int OriginBaseHeight);

    public TrackMeshData Build(ReadOnlySpan<RCTTrackElementData> trackElements, int mapWidth, int mapHeight)
    {
        if (mapWidth < 0 || mapHeight < 0)
            throw new ArgumentOutOfRangeException(nameof(mapWidth));

        var pieces = new Dictionary<TrackPieceKey, List<RCTTrackElementData>>();
        foreach (var track in trackElements) {
            if ((uint)track.TileX >= (uint)mapWidth || (uint)track.TileY >= (uint)mapHeight)
                throw new InvalidDataException($"OpenRCT2 returned a track element outside the {mapWidth}x{mapHeight} map.");
            if (track.Direction > 3)
                throw new InvalidDataException($"OpenRCT2 returned an invalid track direction at ({track.TileX}, {track.TileY}).");
            if (track.SequenceCount == 0 || track.Sequence >= track.SequenceCount)
                throw new InvalidDataException($"OpenRCT2 returned an invalid track sequence at ({track.TileX}, {track.TileY}).");

            var offset = RotateSequenceOffset(track.SequenceOffsetX, track.SequenceOffsetY, track.Direction);
            var key = new TrackPieceKey(
                track.RideId,
                track.TrackType,
                track.TileX - offset.X,
                track.TileY - offset.Y,
                track.Direction,
                track.BaseHeight - track.SequenceOffsetZ);
            if (!pieces.TryGetValue(key, out var pieceElements)) {
                pieceElements = new List<RCTTrackElementData>(track.SequenceCount);
                pieces.Add(key, pieceElements);
            }
            pieceElements.Add(track);
        }

        var geometry = new TrackMeshGeometry();
        var piecesByRegion = new Dictionary<RenderRegionCoordinate, List<List<RCTTrackElementData>>>();
        foreach (var (key, pieceElements) in pieces) {
            pieceElements.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
            if (!_resolver.TryResolve(key.TrackType, out var builder))
                continue;

            if (builder is MonorailCurveTrackPieceBuilder)
                ValidatePieceSequences(key, pieceElements);

            var region = new RenderRegionCoordinate(
                Math.Clamp(key.OriginTileX / RenderRegionLayout.RegionSizeInTiles, 0, Math.Max(0, RenderRegionLayout.GetRegionCount(mapWidth) - 1)),
                Math.Clamp(key.OriginTileY / RenderRegionLayout.RegionSizeInTiles, 0, Math.Max(0, RenderRegionLayout.GetRegionCount(mapHeight) - 1)));
            if (!piecesByRegion.TryGetValue(region, out var regionPieces)) {
                regionPieces = new List<List<RCTTrackElementData>>();
                piecesByRegion.Add(region, regionPieces);
            }
            regionPieces.Add(pieceElements);
        }

        foreach (var region in RenderRegionLayout.Enumerate(mapWidth, mapHeight)) {
            if (!piecesByRegion.TryGetValue(region, out var regionPieces))
                continue;

            geometry.BeginRegion(region);
            foreach (var piece in regionPieces) {
                var track = piece[0];
                if (!_resolver.TryResolve(track.TrackType, out var builder))
                    continue;
                builder.AddPiece(CollectionsMarshal.AsSpan(piece), geometry);
            }
            geometry.EndRegion();
        }

        return geometry.Build();
    }

    private static (int X, int Y) RotateSequenceOffset(int x, int y, byte direction) => direction switch {
        0 => (x, y),
        1 => (y, -x),
        2 => (-x, -y),
        3 => (-y, x),
        _ => throw new InvalidDataException($"OpenRCT2 returned invalid track direction {direction}.")
    };

    private static void ValidatePieceSequences(TrackPieceKey key, List<RCTTrackElementData> elements)
    {
        var expectedCount = elements[0].SequenceCount;
        if (elements.Count != expectedCount || elements.Any(element => element.SequenceCount != expectedCount))
            throw new InvalidDataException(
                $"Track piece {key.TrackType} for ride {key.RideId} at ({key.OriginTileX}, {key.OriginTileY}) has incomplete sequence data.");

        for (var sequence = 0; sequence < expectedCount; sequence++) {
            if (elements[sequence].Sequence != sequence)
                throw new InvalidDataException(
                    $"Track piece {key.TrackType} for ride {key.RideId} at ({key.OriginTileX}, {key.OriginTileY}) has duplicate or missing sequences.");
        }
    }
}
