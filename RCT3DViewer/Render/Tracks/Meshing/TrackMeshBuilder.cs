using RCT3D.Interop;
using System.Runtime.InteropServices;

namespace RCT3D.Render.Tracks.Meshing;

internal sealed class TrackMeshBuilder
{
    private readonly Dictionary<RCTRideType, TrackRideConfiguration> rideConfigurations;

    public TrackMeshBuilder(IEnumerable<TrackRideConfiguration>? rideConfigurations = null)
    {
        this.rideConfigurations = (rideConfigurations ?? TrackRideConfigurations.All)
            .ToDictionary(configuration => configuration.RideType);
    }

    private readonly record struct TrackPieceKey(
        ushort RideId,
        RCTRideType RideType,
        RCTTrackElementType TrackType,
        int OriginTileX,
        int OriginTileY,
        byte Direction,
        int OriginBaseHeight);

    public TrackMeshData Build(ReadOnlySpan<RCTTrackElementData> trackElements, int mapWidth, int mapHeight, ReadOnlySpan<RCTPaletteEntry> palette)
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

            if (!rideConfigurations.TryGetValue(track.RideType, out var configuration)
                || !configuration.TryResolvePiece(track.TrackType, out _)) continue;

            var offset = RotateSequenceOffset(track.SequenceOffsetX, track.SequenceOffsetY, track.Direction);

            var key = new TrackPieceKey(
                track.RideId,
                track.RideType,
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
        var piecesByRegion = new Dictionary<RenderRegionCoordinate, List<(TrackRideConfiguration Configuration, List<RCTTrackElementData> Elements)>>();
        
        foreach (var (key, pieceElements) in pieces) {
            pieceElements.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
            var configuration = rideConfigurations[key.RideType];
            configuration.TryResolvePiece(key.TrackType, out var pieceBuilder);

            if (pieceBuilder.RequiresCompletePiece)
                ValidatePieceSequences(key, pieceElements);

            var region = new RenderRegionCoordinate(
                Math.Clamp(key.OriginTileX / RenderRegionLayout.RegionSizeInTiles, 0, Math.Max(0, RenderRegionLayout.GetRegionCount(mapWidth) - 1)),
                Math.Clamp(key.OriginTileY / RenderRegionLayout.RegionSizeInTiles, 0, Math.Max(0, RenderRegionLayout.GetRegionCount(mapHeight) - 1)));
            
            if (!piecesByRegion.TryGetValue(region, out var regionPieces)) {
                regionPieces = new List<(TrackRideConfiguration, List<RCTTrackElementData>)>();
                piecesByRegion.Add(region, regionPieces);
            }

            regionPieces.Add((configuration, pieceElements));
        }

        foreach (var region in RenderRegionLayout.Enumerate(mapWidth, mapHeight)) {
            if (!piecesByRegion.TryGetValue(region, out var regionPieces)) continue;

            geometry.BeginRegion(region);

            foreach (var (configuration, piece) in regionPieces) {
                var track = piece[0];
                configuration.TryResolvePiece(track.TrackType, out var pieceBuilder);
                pieceBuilder.AddPiece(CollectionsMarshal.AsSpan(piece), geometry, configuration.PartGenerators, palette);
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
