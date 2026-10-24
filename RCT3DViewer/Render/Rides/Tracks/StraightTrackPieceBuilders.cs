using RCT3D.Interop;
using System.Numerics;

namespace RCT3D.Render.Rides.Tracks;

internal sealed class StraightTrackPieceBuilder(RCTTrackElementType pieceType, float rise = 0f) : TrackPieceMeshBuilder(pieceType)
{
    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries)
    {
        var direction = TrackCenterlineGeometry.GetDirection(track.Direction);
        var center = TrackCenterlineGeometry.GetTileCenter(track);
        var startHeight = (track.BaseHeight - track.SequenceOffsetZ + track.SplineZBegin) * WorldUnits.HeightPerBaseUnit;
        var endHeight = (track.BaseHeight - track.SequenceOffsetZ + track.SplineZEnd) * WorldUnits.HeightPerBaseUnit;

        var points = new[] {
            center - direction * (WorldUnits.TileSize * 0.5f) + Vector3.UnitY * startHeight,
            center + direction * (WorldUnits.TileSize * 0.5f) + Vector3.UnitY * (endHeight + rise),
        };
        var tangent = Vector3.Normalize(points[1] - points[0]);

        TrackCenterlineGeometry.AddParts(track, geometry, points, partGenerators, paletteEntries,
            TrackCenterlineGeometry.GetRollRadians(track.RollBegin), TrackCenterlineGeometry.GetRollRadians(track.RollEnd),
            startTangent: tangent, endTangent: tangent);
    }
}

internal sealed class BankedSlopeTrackPieceBuilder(RCTTrackElementType pieceType) : TrackPieceMeshBuilder(pieceType)
{
    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries)
    {
        var direction = TrackCenterlineGeometry.GetDirection(track.Direction);
        var center = TrackCenterlineGeometry.GetTileCenter(track);
        var start = center - direction * (WorldUnits.TileSize * 0.5f);
        var end = center + direction * (WorldUnits.TileSize * 0.5f);
        var originHeight = track.BaseHeight - track.SequenceOffsetZ;
        start.Y = (originHeight + track.SplineZBegin) * WorldUnits.HeightPerBaseUnit;
        end.Y = (originHeight + track.SplineZEnd) * WorldUnits.HeightPerBaseUnit;

        var startPitch = TrackCenterlineGeometry.GetPitchSlope(PieceType, atStart: true);
        var endPitch = TrackCenterlineGeometry.GetPitchSlope(PieceType, atStart: false);
        var startDirection = Vector3.Normalize(direction + Vector3.UnitY * startPitch);
        var endDirection = Vector3.Normalize(direction + Vector3.UnitY * endPitch);
        var points = TrackCenterlineGeometry.CreateCubicCurvePoints(start, end, startDirection, endDirection,
            WorldUnits.TileSize / 3.0f, TrackCenterlineGeometry.CurveSubdivisionsPerTile);

        TrackCenterlineGeometry.AddParts(track, geometry, points, partGenerators, paletteEntries,
            TrackCenterlineGeometry.GetRollRadians(track.RollBegin), TrackCenterlineGeometry.GetRollRadians(track.RollEnd),
            startTangent: startDirection, endTangent: endDirection);
    }
}

internal sealed class LongBaseSlopeTrackPieceBuilder(RCTTrackElementType pieceType, float startPitch, float endPitch) : TrackPieceMeshBuilder(pieceType)
{
    public override bool RequiresCompletePiece => true;

    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries) =>
        throw new InvalidOperationException("Long-base slope geometry must be built from a complete piece.");

    public override void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries)
    {
        if (tracks.IsEmpty) return;

        var first = tracks[0];
        var last = tracks[^1];
        var startDirection = TrackCenterlineGeometry.GetTrackDirection(
            TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationBegin));
        var endDirection = TrackCenterlineGeometry.GetTrackDirection(
            TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationEnd));
        var halfTile = WorldUnits.TileSize * 0.5f;
        var start = TrackCenterlineGeometry.GetTileCenter(first) - startDirection * halfTile;
        var end = TrackCenterlineGeometry.GetTileCenter(last) + endDirection * halfTile;
        var originHeight = (first.BaseHeight - first.SequenceOffsetZ) * WorldUnits.HeightPerBaseUnit;
        start.Y = originHeight + first.SplineZBegin * WorldUnits.HeightPerBaseUnit;
        end.Y = originHeight + first.SplineZEnd * WorldUnits.HeightPerBaseUnit;

        var startTangent = Vector3.Normalize(startDirection + Vector3.UnitY * startPitch);
        var endTangent = Vector3.Normalize(endDirection + Vector3.UnitY * endPitch);
        var points = TrackCenterlineGeometry.CreateCubicCurvePoints(
            start, end, startTangent, endTangent, Vector3.Distance(start, end) * 0.4f,
            tracks.Length * TrackCenterlineGeometry.CurveSubdivisionsPerTile);

        TrackCenterlineGeometry.AddParts(first, geometry, points, partGenerators, paletteEntries,
            startTangent: startTangent, endTangent: endTangent);
    }
}

internal sealed class SlopeTrackPieceBuilder(RCTTrackElementType pieceType, float startPitch, float endPitch)
    : TrackPieceMeshBuilder(pieceType)
{
    public SlopeTrackPieceBuilder(RCTTrackElementType pieceType, bool beginsFlat, bool endsFlat)
        : this(pieceType, GetLegacyPitch(pieceType, beginsFlat), GetLegacyPitch(pieceType, endsFlat)) {}

    public override bool RequiresCompletePiece => true;

    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries)
    {
        var direction = TrackCenterlineGeometry.GetDirection(track.Direction);
        var center = TrackCenterlineGeometry.GetTileCenter(track);
        var halfTile = WorldUnits.TileSize * 0.5f;
        var start = center - direction * halfTile;
        var end = center + direction * halfTile;
        var originHeight = track.BaseHeight - track.SequenceOffsetZ;
        start.Y = (originHeight + track.SplineZBegin) * WorldUnits.HeightPerBaseUnit;
        end.Y = (originHeight + track.SplineZEnd) * WorldUnits.HeightPerBaseUnit;

        Vector3[] points;
        Vector3 startTangent;
        Vector3 endTangent;
        if (startPitch != endPitch) {
            var startDirection = Vector3.Normalize(direction + Vector3.UnitY * startPitch);
            var endDirection = Vector3.Normalize(direction + Vector3.UnitY * endPitch);
            startTangent = startDirection;
            endTangent = endDirection;
            points = TrackCenterlineGeometry.CreateCubicCurvePoints(
                start, end, startDirection, endDirection, WorldUnits.TileSize / 3.0f,
                TrackCenterlineGeometry.CurveSubdivisionsPerTile);
        }
        else {
            points = [start, end];
            startTangent = Vector3.Normalize(end - start);
            endTangent = startTangent;
        }

        TrackCenterlineGeometry.AddParts(track, geometry, points, partGenerators, paletteEntries,
            TrackCenterlineGeometry.GetRollRadians(track.RollBegin), TrackCenterlineGeometry.GetRollRadians(track.RollEnd),
            startTangent: startTangent, endTangent: endTangent);
    }

    private static float GetLegacyPitch(RCTTrackElementType pieceType, bool flat) =>
        flat ? Pitch.None : pieceType is RCTTrackElementType.Down25 or RCTTrackElementType.Down25ToFlat or RCTTrackElementType.FlatToDown25
            ? -Pitch.Slope25
            : Pitch.Slope25;
}
