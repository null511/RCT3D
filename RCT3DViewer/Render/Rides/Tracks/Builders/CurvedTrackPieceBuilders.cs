using System.Numerics;
using RCT3D.Interop.Models;

namespace RCT3D.Render.Rides.Tracks.Builders;

internal class CurveTrackPieceBuilder(RCTTrackElementType pieceType) : TrackPieceMeshBuilder(pieceType)
{
    public override bool RequiresCompletePiece => true;

    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries) =>
        throw new InvalidOperationException("Curved track geometry must be built from a complete piece.");

    protected virtual float GetRollBegin(in RCTTrackElementData track, float bankAngleDegrees) => 0.0f;
    protected virtual float GetRollEnd(in RCTTrackElementData track, float bankAngleDegrees) => 0.0f;
    protected virtual bool SmoothRollTransition => false;
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
            GetRollBegin(first, geometry.TrackBankAngleDegrees), GetRollEnd(first, geometry.TrackBankAngleDegrees),
            startTangent: pitchedStartDirection, endTangent: pitchedEndDirection,
            smoothRollTransition: SmoothRollTransition);
    }
}

internal sealed class SteepQuarterTurnTrackPieceBuilder(RCTTrackElementType pieceType) : CurveTrackPieceBuilder(pieceType)
{
    protected override int GetSubdivisions(int trackCount) =>
        Math.Max(trackCount * TrackCenterlineGeometry.CurveSubdivisionsPerTile, 32);
}

internal sealed class BankedCurveTrackPieceBuilder(RCTTrackElementType pieceType) : CurveTrackPieceBuilder(pieceType)
{
    protected override bool SmoothRollTransition => true;

    protected override float GetRollBegin(in RCTTrackElementData track, float bankAngleDegrees) =>
        TrackCenterlineGeometry.GetRollRadians(track.RollBegin, bankAngleDegrees);
    protected override float GetRollEnd(in RCTTrackElementData track, float bankAngleDegrees) =>
        TrackCenterlineGeometry.GetRollRadians(track.RollEnd, bankAngleDegrees);
}

internal sealed class CorkscrewTrackPieceBuilder(RCTTrackElementType pieceType) : CurveTrackPieceBuilder(pieceType)
{
    private const float HalfTwist = MathF.PI;

    private bool IsUp => PieceType is RCTTrackElementType.LeftCorkscrewUp
        or RCTTrackElementType.RightCorkscrewUp
        or RCTTrackElementType.LeftLargeCorkscrewUp
        or RCTTrackElementType.RightLargeCorkscrewUp;
    private float SignedHalfTwist => PieceType is RCTTrackElementType.LeftCorkscrewUp
        or RCTTrackElementType.LeftCorkscrewDown
        or RCTTrackElementType.LeftLargeCorkscrewUp
        or RCTTrackElementType.LeftLargeCorkscrewDown
        ? HalfTwist
        : -HalfTwist;

    protected override float GetRollBegin(in RCTTrackElementData track, float bankAngleDegrees) => IsUp ? 0.0f : SignedHalfTwist;
    protected override float GetRollEnd(in RCTTrackElementData track, float bankAngleDegrees) => IsUp ? SignedHalfTwist : 0.0f;
    protected override bool SmoothRollTransition => true;
}

internal sealed class ZeroGRollTrackPieceBuilder(RCTTrackElementType pieceType) : CurveTrackPieceBuilder(pieceType)
{
    private bool IsUp => PieceType is RCTTrackElementType.LeftZeroGRollUp
        or RCTTrackElementType.RightZeroGRollUp
        or RCTTrackElementType.LeftLargeZeroGRollUp
        or RCTTrackElementType.RightLargeZeroGRollUp;
    private float SignedHalfTwist => PieceType is RCTTrackElementType.LeftZeroGRollUp
        or RCTTrackElementType.LeftZeroGRollDown
        or RCTTrackElementType.LeftLargeZeroGRollUp
        or RCTTrackElementType.LeftLargeZeroGRollDown
        ? MathF.PI
        : -MathF.PI;

    protected override float GetRollBegin(in RCTTrackElementData track, float bankAngleDegrees) =>
        IsUp ? 0.0f : -SignedHalfTwist;
    protected override float GetRollEnd(in RCTTrackElementData track, float bankAngleDegrees) =>
        IsUp ? SignedHalfTwist : 0.0f;
    protected override bool SmoothRollTransition => true;
    protected override int GetSubdivisions(int trackCount) =>
        Math.Max(base.GetSubdivisions(trackCount), 32);
}

internal class DiagonalTrackPieceBuilder(RCTTrackElementType pieceType) : CurveTrackPieceBuilder(pieceType)
{
    protected override float GetRollBegin(in RCTTrackElementData track, float bankAngleDegrees) =>
        TrackCenterlineGeometry.GetRollRadians(track.RollBegin, bankAngleDegrees);
    protected override float GetRollEnd(in RCTTrackElementData track, float bankAngleDegrees) =>
        TrackCenterlineGeometry.GetRollRadians(track.RollEnd, bankAngleDegrees);

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
        var transitionPitches = TrackCenterlineGeometry.GetDiagonalSlopeTransitionPitches(PieceType);
        var horizontalDistance = Vector2.Distance(new Vector2(start.X, start.Z), new Vector2(end.X, end.Z));
        var risePerRun = horizontalDistance > float.Epsilon ? (end.Y - start.Y) / horizontalDistance : 0.0f;
        var startPitch = transitionPitches?.Start ?? risePerRun;
        var endPitch = transitionPitches?.End ?? risePerRun;
        var pitchedStartDirection = Vector3.Normalize(startDirection + Vector3.UnitY * startPitch);
        var pitchedEndDirection = Vector3.Normalize(endDirection + Vector3.UnitY * endPitch);
        var points = TrackCenterlineGeometry.CreateCubicCurvePoints(start, end, pitchedStartDirection, pitchedEndDirection,
            Vector3.Distance(start, end) * 0.4f, tracks.Length * TrackCenterlineGeometry.CurveSubdivisionsPerTile);

        TrackCenterlineGeometry.AddParts(first, geometry, points, partGenerators, paletteEntries,
            TrackCenterlineGeometry.GetRollRadians(first.RollBegin, geometry.TrackBankAngleDegrees),
            TrackCenterlineGeometry.GetRollRadians(first.RollEnd, geometry.TrackBankAngleDegrees),
            startTangent: pitchedStartDirection, endTangent: pitchedEndDirection,
            smoothRollTransition: first.RollBegin != first.RollEnd);
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
            TrackCenterlineGeometry.GetRollRadians(first.RollBegin, geometry.TrackBankAngleDegrees),
            TrackCenterlineGeometry.GetRollRadians(first.RollEnd, geometry.TrackBankAngleDegrees),
            startTangent: startDirection, endTangent: endDirection);
    }
}
