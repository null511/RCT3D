using RCT3D.Interop;
using System.Numerics;

namespace RCT3D.Render.Tracks.Meshing;

internal sealed class StraightTrackPieceBuilder(RCTTrackElementType pieceType, float rise) : TrackPieceMeshBuilder(pieceType)
{
    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries)
    {
        var direction = TrackCenterlineGeometry.GetDirection(track.Direction);
        var center = TrackCenterlineGeometry.GetTileCenter(track);
        var startHeight = (track.BaseHeight - track.SequenceOffsetZ + track.SplineZBegin) * TrackCenterlineGeometry.HeightPerBaseUnit;
        var endHeight = (track.BaseHeight - track.SequenceOffsetZ + track.SplineZEnd) * TrackCenterlineGeometry.HeightPerBaseUnit;

        var points = new[] {
            center - direction * (TrackCenterlineGeometry.TileSize * 0.5f) + Vector3.UnitY * startHeight,
            center + direction * (TrackCenterlineGeometry.TileSize * 0.5f) + Vector3.UnitY * (endHeight + rise),
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
        var start = center - direction * (TrackCenterlineGeometry.TileSize * 0.5f);
        var end = center + direction * (TrackCenterlineGeometry.TileSize * 0.5f);
        var originHeight = track.BaseHeight - track.SequenceOffsetZ;
        start.Y = (originHeight + track.SplineZBegin) * TrackCenterlineGeometry.HeightPerBaseUnit;
        end.Y = (originHeight + track.SplineZEnd) * TrackCenterlineGeometry.HeightPerBaseUnit;

        var startPitch = TrackCenterlineGeometry.GetPitchSlope(PieceType, atStart: true);
        var endPitch = TrackCenterlineGeometry.GetPitchSlope(PieceType, atStart: false);
        var startDirection = Vector3.Normalize(direction + Vector3.UnitY * startPitch);
        var endDirection = Vector3.Normalize(direction + Vector3.UnitY * endPitch);
        var points = TrackCenterlineGeometry.CreateCubicCurvePoints(start, end, startDirection, endDirection,
            TrackCenterlineGeometry.TileSize / 3.0f, TrackCenterlineGeometry.CurveSubdivisionsPerTile);

        TrackCenterlineGeometry.AddParts(track, geometry, points, partGenerators, paletteEntries,
            TrackCenterlineGeometry.GetRollRadians(track.RollBegin), TrackCenterlineGeometry.GetRollRadians(track.RollEnd),
            startTangent: startDirection, endTangent: endDirection);
    }
}

internal sealed class SlopeTrackPieceBuilder : TrackPieceMeshBuilder
{
    private readonly float startPitch;
    private readonly float endPitch;

    public SlopeTrackPieceBuilder(RCTTrackElementType pieceType, float startPitch, float endPitch) : base(pieceType)
    {
        this.startPitch = startPitch;
        this.endPitch = endPitch;
    }

    public SlopeTrackPieceBuilder(RCTTrackElementType pieceType, bool beginsFlat, bool endsFlat)
        : this(pieceType, GetLegacyPitch(pieceType, beginsFlat), GetLegacyPitch(pieceType, endsFlat))
    {
    }

    public override bool RequiresCompletePiece => true;

    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries)
    {
        var direction = TrackCenterlineGeometry.GetDirection(track.Direction);
        var center = TrackCenterlineGeometry.GetTileCenter(track);
        var halfTile = TrackCenterlineGeometry.TileSize * 0.5f;
        var start = center - direction * halfTile;
        var end = center + direction * halfTile;
        var originHeight = track.BaseHeight - track.SequenceOffsetZ;
        start.Y = (originHeight + track.SplineZBegin) * TrackCenterlineGeometry.HeightPerBaseUnit;
        end.Y = (originHeight + track.SplineZEnd) * TrackCenterlineGeometry.HeightPerBaseUnit;

        Vector3[] points;
        Vector3 startTangent;
        Vector3 endTangent;
        if (startPitch != endPitch) {
            var startDirection = Vector3.Normalize(direction + Vector3.UnitY * startPitch);
            var endDirection = Vector3.Normalize(direction + Vector3.UnitY * endPitch);
            startTangent = startDirection;
            endTangent = endDirection;
            points = TrackCenterlineGeometry.CreateCubicCurvePoints(
                start, end, startDirection, endDirection, TrackCenterlineGeometry.TileSize / 3.0f,
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
        flat ? 0.0f : pieceType is RCTTrackElementType.Down25 or RCTTrackElementType.Down25ToFlat or RCTTrackElementType.FlatToDown25
            ? -TrackCenterlineGeometry.Pitch25Slope
            : TrackCenterlineGeometry.Pitch25Slope;
}
