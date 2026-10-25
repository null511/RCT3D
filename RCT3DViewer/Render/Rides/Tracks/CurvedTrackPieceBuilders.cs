using System.Numerics;
using RCT3D.Interop;

namespace RCT3D.Render.Rides.Tracks;

internal class CurveTrackPieceBuilder(RCTTrackElementType pieceType) : TrackPieceMeshBuilder(pieceType)
{
    public override bool RequiresCompletePiece => true;

    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries) =>
        throw new InvalidOperationException("Curved track geometry must be built from a complete piece.");

    protected virtual float GetRollBegin(in RCTTrackElementData track) => 0.0f;
    protected virtual float GetRollEnd(in RCTTrackElementData track) => 0.0f;
    protected virtual float GetControlLength(Vector3 start, Vector3 end) => Vector3.Distance(start, end) * 0.4f;
    protected virtual int GetSubdivisions(int trackCount) => trackCount * TrackCenterlineGeometry.CurveSubdivisionsPerTile;

    public override void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries)
    {
        if (tracks.IsEmpty) return;

        var first = tracks[0];
        var last = tracks[^1];
        var pieceDirection = first.Direction;
        var startHeading = TrackCenterlineGeometry.CombineDirection(pieceDirection, first.RotationBegin);
        var endHeading = TrackCenterlineGeometry.CombineDirection(pieceDirection, first.RotationEnd);
        var startDirection = TrackCenterlineGeometry.GetTrackDirection(startHeading);
        var endDirection = TrackCenterlineGeometry.GetTrackDirection(endHeading);
        var startCenter = TrackCenterlineGeometry.GetTileCenter(first);
        var endCenter = TrackCenterlineGeometry.GetTileCenter(last);
        var halfTile = WorldUnits.TileSize * 0.5f;
        var startOffset = (startHeading & 4) != 0 ? Vector3.Zero : startDirection * halfTile;
        var endOffset = (endHeading & 4) != 0 ? Vector3.Zero : endDirection * halfTile;
        var start = startCenter - startOffset;
        var end = endCenter + endOffset;
        var originHeight = (first.BaseHeight - first.SequenceOffsetZ) * WorldUnits.HeightPerBaseUnit;
        start.Y = originHeight + first.SplineZBegin * WorldUnits.HeightPerBaseUnit;
        end.Y = originHeight + first.SplineZEnd * WorldUnits.HeightPerBaseUnit;

        var startPitch = TrackCenterlineGeometry.GetPitchSlope(PieceType, atStart: true);
        var endPitch = TrackCenterlineGeometry.GetPitchSlope(PieceType, atStart: false);
        var pitchedStartDirection = Vector3.Normalize(startDirection + Vector3.UnitY * startPitch);
        var pitchedEndDirection = Vector3.Normalize(endDirection + Vector3.UnitY * endPitch);
        var points = TrackCenterlineGeometry.CreateCubicCurvePoints(start, end, pitchedStartDirection, pitchedEndDirection,
            GetControlLength(start, end), GetSubdivisions(tracks.Length));

        TrackCenterlineGeometry.AddParts(first, geometry, points, partGenerators, paletteEntries,
            GetRollBegin(first), GetRollEnd(first),
            startTangent: pitchedStartDirection, endTangent: pitchedEndDirection);
    }
}

internal sealed class SteepQuarterTurnTrackPieceBuilder(RCTTrackElementType pieceType) : CurveTrackPieceBuilder(pieceType)
{
    protected override int GetSubdivisions(int trackCount) =>
        Math.Max(trackCount * TrackCenterlineGeometry.CurveSubdivisionsPerTile, 32);
}

internal sealed class BankedCurveTrackPieceBuilder(RCTTrackElementType pieceType) : CurveTrackPieceBuilder(pieceType)
{
    protected override float GetRollBegin(in RCTTrackElementData track) => TrackCenterlineGeometry.GetRollRadians(track.RollBegin);
    protected override float GetRollEnd(in RCTTrackElementData track) => TrackCenterlineGeometry.GetRollRadians(track.RollEnd);
}

internal class DiagonalTrackPieceBuilder(RCTTrackElementType pieceType) : CurveTrackPieceBuilder(pieceType)
{
    protected override float GetRollBegin(in RCTTrackElementData track) => TrackCenterlineGeometry.GetRollRadians(track.RollBegin);
    protected override float GetRollEnd(in RCTTrackElementData track) => TrackCenterlineGeometry.GetRollRadians(track.RollEnd);

    public override void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries)
    {
        if (tracks.IsEmpty) return;

        var first = tracks[0];
        var last = tracks[^1];

        var startHeading = TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationBegin);
        var endHeading = TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationEnd);
        var startDirection = TrackCenterlineGeometry.GetTrackDirection(startHeading);
        var endDirection = TrackCenterlineGeometry.GetTrackDirection(endHeading);
        var start = TrackCenterlineGeometry.GetTileCenter(first);
        var end = TrackCenterlineGeometry.GetTileCenter(last);
        var originHeight = (first.BaseHeight - first.SequenceOffsetZ) * WorldUnits.HeightPerBaseUnit;
        start.Y = originHeight + first.SplineZBegin * WorldUnits.HeightPerBaseUnit;
        end.Y = originHeight + first.SplineZEnd * WorldUnits.HeightPerBaseUnit;
        var smoothTransition = PieceType is RCTTrackElementType.DiagRightBankToDown25
            or RCTTrackElementType.DiagDown25ToLeftBank;
        var transitionPitches = TrackCenterlineGeometry.GetDiagonalSlopeTransitionPitches(PieceType);
        var horizontalDistance = Vector2.Distance(new Vector2(start.X, start.Z), new Vector2(end.X, end.Z));
        var risePerRun = horizontalDistance > float.Epsilon ? (end.Y - start.Y) / horizontalDistance : 0.0f;
        var startPitch = transitionPitches?.Start ?? (smoothTransition ? TrackCenterlineGeometry.GetPitchSlope(PieceType, atStart: true) : risePerRun);
        var endPitch = transitionPitches?.End ?? (smoothTransition ? TrackCenterlineGeometry.GetPitchSlope(PieceType, atStart: false) : risePerRun);
        var pitchedStartDirection = Vector3.Normalize(startDirection + Vector3.UnitY * startPitch);
        var pitchedEndDirection = Vector3.Normalize(endDirection + Vector3.UnitY * endPitch);
        var points = TrackCenterlineGeometry.CreateCubicCurvePoints(start, end, pitchedStartDirection, pitchedEndDirection,
            Vector3.Distance(start, end) * 0.4f, tracks.Length * TrackCenterlineGeometry.CurveSubdivisionsPerTile);

        TrackCenterlineGeometry.AddParts(first, geometry, points, partGenerators, paletteEntries,
            TrackCenterlineGeometry.GetRollRadians(first.RollBegin), TrackCenterlineGeometry.GetRollRadians(first.RollEnd),
            startTangent: pitchedStartDirection, endTangent: pitchedEndDirection,
            smoothRollTransition: smoothTransition);
    }
}

internal sealed class DiagonalFlatTrackPieceBuilder() : DiagonalTrackPieceBuilder(RCTTrackElementType.DiagFlat);

internal sealed class HelixTrackPieceBuilder(RCTTrackElementType pieceType) : CurveTrackPieceBuilder(pieceType)
{
    private readonly RCTTrackElementType pieceType = pieceType;

    public override void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries)
    {
        if (tracks.IsEmpty) return;

        var first = tracks[0];
        var last = tracks[^1];
        var startHeading = TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationBegin);
        var endHeading = TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationEnd);
        var startDirection = TrackCenterlineGeometry.GetTrackDirection(startHeading);
        var endDirection = TrackCenterlineGeometry.GetTrackDirection(endHeading);
        var halfTile = WorldUnits.TileSize * 0.5f;
        var startOffset = (startHeading & 4) != 0 ? Vector3.Zero : startDirection * halfTile;
        var endOffset = (endHeading & 4) != 0 ? Vector3.Zero : endDirection * halfTile;
        var start = TrackCenterlineGeometry.GetTileCenter(first) - startOffset;
        var end = TrackCenterlineGeometry.GetTileCenter(last) + endOffset;
        var originHeight = (first.BaseHeight - first.SequenceOffsetZ) * WorldUnits.HeightPerBaseUnit;
        start.Y = originHeight + first.SplineZBegin * WorldUnits.HeightPerBaseUnit;
        end.Y = originHeight + first.SplineZEnd * WorldUnits.HeightPerBaseUnit;

        var turnLeft = pieceType is RCTTrackElementType.LeftHalfBankedHelixUpSmall
            or RCTTrackElementType.LeftHalfBankedHelixDownSmall
            or RCTTrackElementType.LeftHalfBankedHelixUpLarge
            or RCTTrackElementType.LeftHalfBankedHelixDownLarge;

        var centerline = TrackCenterlineGeometry.CreateSemicirclePoints(
            start, end, startDirection, endDirection, turnLeft, tracks.Length * TrackCenterlineGeometry.CurveSubdivisionsPerTile);

        TrackCenterlineGeometry.AddParts(first, geometry, centerline, partGenerators, paletteEntries,
            TrackCenterlineGeometry.GetRollRadians(first.RollBegin), TrackCenterlineGeometry.GetRollRadians(first.RollEnd),
            startTangent: startDirection, endTangent: endDirection);
    }
}
