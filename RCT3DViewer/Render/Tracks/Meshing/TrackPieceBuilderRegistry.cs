using RCT3D.Interop;
using System.Numerics;

namespace RCT3D.Render.Tracks.Meshing;

internal sealed class TrackPieceBuilderRegistry
{
    private readonly Dictionary<RCTTrackElementType, ITrackPieceMeshBuilder> builders = new();


    public void Register(ITrackPieceMeshBuilder builder) => builders.Add(builder.PieceType, builder);

    //public bool TryResolve(RCTTrackElementType type, out ITrackPieceMeshBuilder builder) => _builders.TryGetValue(type, out builder!);

    public IEnumerable<ITrackPieceMeshBuilder> Builders => builders.Values;
}

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

internal class CurveTrackPieceBuilder(RCTTrackElementType pieceType) : TrackPieceMeshBuilder(pieceType)
{
    public override bool RequiresCompletePiece => true;

    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries) =>
        throw new InvalidOperationException("Curved track geometry must be built from a complete piece.");

    protected virtual float GetRollBegin(in RCTTrackElementData track) => 0.0f;
    protected virtual float GetRollEnd(in RCTTrackElementData track) => 0.0f;

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
        var halfTile = TrackCenterlineGeometry.TileSize * 0.5f;
        var startOffset = (startHeading & 4) != 0 ? Vector3.Zero : startDirection * halfTile;
        var endOffset = (endHeading & 4) != 0 ? Vector3.Zero : endDirection * halfTile;
        var start = startCenter - startOffset;
        var end = endCenter + endOffset;
        var originHeight = (first.BaseHeight - first.SequenceOffsetZ) * TrackCenterlineGeometry.HeightPerBaseUnit;
        start.Y = originHeight + first.SplineZBegin * TrackCenterlineGeometry.HeightPerBaseUnit;
        end.Y = originHeight + first.SplineZEnd * TrackCenterlineGeometry.HeightPerBaseUnit;

        var controlLength = Vector3.Distance(start, end) * 0.4f;
        var startPitch = TrackCenterlineGeometry.GetPitchSlope(PieceType, atStart: true);
        var endPitch = TrackCenterlineGeometry.GetPitchSlope(PieceType, atStart: false);
        var pitchedStartDirection = Vector3.Normalize(startDirection + Vector3.UnitY * startPitch);
        var pitchedEndDirection = Vector3.Normalize(endDirection + Vector3.UnitY * endPitch);
        var points = TrackCenterlineGeometry.CreateCubicCurvePoints(start, end, pitchedStartDirection, pitchedEndDirection,
            controlLength, tracks.Length * TrackCenterlineGeometry.CurveSubdivisionsPerTile);

        TrackCenterlineGeometry.AddParts(first, geometry, points, partGenerators, paletteEntries,
            GetRollBegin(first), GetRollEnd(first),
            startTangent: pitchedStartDirection, endTangent: pitchedEndDirection);
    }
}

internal sealed class BankedCurveTrackPieceBuilder(RCTTrackElementType pieceType) : CurveTrackPieceBuilder(pieceType)
{
    protected override float GetRollBegin(in RCTTrackElementData track) => TrackCenterlineGeometry.GetRollRadians(track.RollBegin);
    protected override float GetRollEnd(in RCTTrackElementData track) => TrackCenterlineGeometry.GetRollRadians(track.RollEnd);
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
            ? -0.4663077f
            : 0.4663077f;
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
        var originHeight = (first.BaseHeight - first.SequenceOffsetZ) * TrackCenterlineGeometry.HeightPerBaseUnit;
        start.Y = originHeight + first.SplineZBegin * TrackCenterlineGeometry.HeightPerBaseUnit;
        end.Y = originHeight + first.SplineZEnd * TrackCenterlineGeometry.HeightPerBaseUnit;
        var horizontalDistance = Vector2.Distance(new Vector2(start.X, start.Z), new Vector2(end.X, end.Z));
        var risePerRun = horizontalDistance > float.Epsilon ? (end.Y - start.Y) / horizontalDistance : 0.0f;
        var pitchedStartDirection = Vector3.Normalize(startDirection + Vector3.UnitY * risePerRun);
        var pitchedEndDirection = Vector3.Normalize(endDirection + Vector3.UnitY * risePerRun);
        var points = TrackCenterlineGeometry.CreateCubicCurvePoints(start, end, pitchedStartDirection, pitchedEndDirection,
            Vector3.Distance(start, end) * 0.4f, tracks.Length * TrackCenterlineGeometry.CurveSubdivisionsPerTile);

        TrackCenterlineGeometry.AddParts(first, geometry, points, partGenerators, paletteEntries,
            TrackCenterlineGeometry.GetRollRadians(first.RollBegin), TrackCenterlineGeometry.GetRollRadians(first.RollEnd),
            startTangent: pitchedStartDirection, endTangent: pitchedEndDirection);
    }
}

internal sealed class DiagonalFlatTrackPieceBuilder() : DiagonalTrackPieceBuilder(RCTTrackElementType.DiagFlat);

internal sealed class CurvedLiftHillTrackPieceBuilder(RCTTrackElementType pieceType) : CurveTrackPieceBuilder(pieceType)
{
    public override void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries)
    {
        if (tracks.IsEmpty) return;

        var first = tracks[0];
        var last = tracks[^1];
        var startDirection = TrackCenterlineGeometry.GetDirection(first.Direction);
        var endDirection = TrackCenterlineGeometry.GetDirection((byte)((first.Direction + first.RotationEnd) & 3));
        var start = TrackCenterlineGeometry.GetTileCenter(first) - startDirection * TrackCenterlineGeometry.TileSize * 0.5f;
        var end = TrackCenterlineGeometry.GetTileCenter(last) + endDirection * TrackCenterlineGeometry.TileSize * 0.5f;
        var originHeight = (first.BaseHeight - first.SequenceOffsetZ) * TrackCenterlineGeometry.HeightPerBaseUnit;
        start.Y = originHeight + first.SplineZBegin * TrackCenterlineGeometry.HeightPerBaseUnit;
        end.Y = originHeight + first.SplineZEnd * TrackCenterlineGeometry.HeightPerBaseUnit;
        var pitchedStartDirection = Vector3.Normalize(startDirection + Vector3.UnitY * TrackCenterlineGeometry.Pitch25Slope);
        var pitchedEndDirection = Vector3.Normalize(endDirection + Vector3.UnitY * TrackCenterlineGeometry.Pitch25Slope);
        var points = TrackCenterlineGeometry.CreateCubicCurvePoints(start, end, pitchedStartDirection, pitchedEndDirection,
            Vector3.Distance(start, end) * 0.4f, tracks.Length * TrackCenterlineGeometry.CurveSubdivisionsPerTile);

        TrackCenterlineGeometry.AddParts(first, geometry, points, partGenerators, paletteEntries,
            startTangent: pitchedStartDirection, endTangent: pitchedEndDirection);
    }
}

internal sealed class HelixTrackPieceBuilder : CurveTrackPieceBuilder
{
    private readonly RCTTrackElementType pieceType;

    public HelixTrackPieceBuilder(RCTTrackElementType pieceType) : base(pieceType) => this.pieceType = pieceType;

    public override void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries)
    {
        if (tracks.IsEmpty) return;

        var first = tracks[0];
        var last = tracks[^1];
        var startHeading = TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationBegin);
        var endHeading = TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationEnd);
        var startDirection = TrackCenterlineGeometry.GetTrackDirection(startHeading);
        var endDirection = TrackCenterlineGeometry.GetTrackDirection(endHeading);
        var halfTile = TrackCenterlineGeometry.TileSize * 0.5f;
        var startOffset = (startHeading & 4) != 0 ? Vector3.Zero : startDirection * halfTile;
        var endOffset = (endHeading & 4) != 0 ? Vector3.Zero : endDirection * halfTile;
        var start = TrackCenterlineGeometry.GetTileCenter(first) - startOffset;
        var end = TrackCenterlineGeometry.GetTileCenter(last) + endOffset;
        var originHeight = (first.BaseHeight - first.SequenceOffsetZ) * TrackCenterlineGeometry.HeightPerBaseUnit;
        start.Y = originHeight + first.SplineZBegin * TrackCenterlineGeometry.HeightPerBaseUnit;
        end.Y = originHeight + first.SplineZEnd * TrackCenterlineGeometry.HeightPerBaseUnit;

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

internal static class TrackCenterlineGeometry
{
    public const float Pitch25Slope = 0.4663077f;

    public const float TileSize = 3.0f;
    public const float HeightPerBaseUnit = 0.75f;
    public const int CurveSubdivisionsPerTile = 8;

    public static Vector3 GetDirection(byte direction) => direction switch {
        0 => -Vector3.UnitX,
        1 => Vector3.UnitZ,
        2 => Vector3.UnitX,
        3 => -Vector3.UnitZ,
        _ => throw new InvalidDataException($"OpenRCT2 returned invalid track direction {direction}.")
    };

    public static byte CombineDirection(byte direction, sbyte rotation) =>
        (byte)(((direction + rotation) & 3) | (rotation & 4));

    public static Vector3 GetTrackDirection(byte direction) => direction switch {
        0 => -Vector3.UnitX,
        1 => Vector3.UnitZ,
        2 => Vector3.UnitX,
        3 => -Vector3.UnitZ,
        4 => Vector3.Normalize(new Vector3(-1.0f, 0.0f, 1.0f)),
        5 => Vector3.Normalize(new Vector3(1.0f, 0.0f, 1.0f)),
        6 => Vector3.Normalize(new Vector3(1.0f, 0.0f, -1.0f)),
        7 => Vector3.Normalize(new Vector3(-1.0f, 0.0f, -1.0f)),
        _ => throw new InvalidDataException($"OpenRCT2 returned invalid track direction {direction}.")
    };

    public static Vector3 GetTileCenter(in RCTTrackElementData track) =>
        new((track.TileX + 0.5f) * TileSize, 0.0f, (track.TileY + 0.5f) * TileSize);

    public static float GetPitchSlope(RCTTrackElementType pieceType, bool atStart)
    {
        if (pieceType is RCTTrackElementType.LeftQuarterTurn3TilesUp25
            or RCTTrackElementType.RightQuarterTurn3TilesUp25)
            return Pitch25Slope;

        if (pieceType is RCTTrackElementType.LeftQuarterTurn3TilesDown25
            or RCTTrackElementType.RightQuarterTurn3TilesDown25)
            return -Pitch25Slope;

        var isUpTransition = pieceType is RCTTrackElementType.LeftBankToUp25
            or RCTTrackElementType.RightBankToUp25
            or RCTTrackElementType.Up25ToLeftBank
            or RCTTrackElementType.Up25ToRightBank
            or RCTTrackElementType.DiagLeftBankToUp25
            or RCTTrackElementType.DiagRightBankToUp25
            or RCTTrackElementType.DiagUp25ToLeftBank
            or RCTTrackElementType.DiagUp25ToRightBank;
        var isDownTransition = pieceType is RCTTrackElementType.LeftBankToDown25
            or RCTTrackElementType.RightBankToDown25
            or RCTTrackElementType.Down25ToLeftBank
            or RCTTrackElementType.Down25ToRightBank
            or RCTTrackElementType.DiagLeftBankToDown25
            or RCTTrackElementType.DiagRightBankToDown25
            or RCTTrackElementType.DiagDown25ToLeftBank
            or RCTTrackElementType.DiagDown25ToRightBank;
        if (!isUpTransition && !isDownTransition) return 0.0f;

        var isSlopeAtStart = pieceType is RCTTrackElementType.Up25ToLeftBank
            or RCTTrackElementType.Up25ToRightBank
            or RCTTrackElementType.DiagUp25ToLeftBank
            or RCTTrackElementType.DiagUp25ToRightBank
            or RCTTrackElementType.Down25ToLeftBank
            or RCTTrackElementType.Down25ToRightBank
            or RCTTrackElementType.DiagDown25ToLeftBank
            or RCTTrackElementType.DiagDown25ToRightBank;
        if (atStart != isSlopeAtStart) return 0.0f;

        return isUpTransition ? Pitch25Slope : -Pitch25Slope;
    }

    public static Vector3[] CreateCubicCurvePoints(
        Vector3 start,
        Vector3 end,
        Vector3 startDirection,
        Vector3 endDirection,
        float controlLength,
        int subdivisions)
    {
        var control1 = start + startDirection * controlLength;
        var control2 = end - endDirection * controlLength;
        var points = new Vector3[subdivisions + 1];

        for (var i = 0; i <= subdivisions; i++) {
            var t = i / (float)subdivisions;
            var inverseT = 1.0f - t;
            points[i] = inverseT * inverseT * inverseT * start
                + 3.0f * inverseT * inverseT * t * control1
                + 3.0f * inverseT * t * t * control2
                + t * t * t * end;
        }

        return points;
    }

    public static Vector3[] CreateSemicirclePoints(
        Vector3 start,
        Vector3 end,
        Vector3 startDirection,
        Vector3 endDirection,
        bool turnLeft,
        int subdivisions)
    {
        var chord = new Vector2(end.X - start.X, end.Z - start.Z);
        var radius = chord.Length() * 0.5f;
        if (radius <= float.Epsilon) return CreateCubicCurvePoints(start, end, startDirection, endDirection, 0.0f, subdivisions);

        var center = new Vector2((start.X + end.X) * 0.5f, (start.Z + end.Z) * 0.5f);
        var startOffset = new Vector2(start.X - center.X, start.Z - center.Y);
        var startAngle = MathF.Atan2(startOffset.Y, startOffset.X);
        var leftAngleDelta = turnLeft ? MathF.PI : -MathF.PI;
        var rightAngleDelta = -leftAngleDelta;
        var leftStartTangent = new Vector3(-MathF.Sin(startAngle) * leftAngleDelta, 0.0f, MathF.Cos(startAngle) * leftAngleDelta);
        var rightStartTangent = new Vector3(-MathF.Sin(startAngle) * rightAngleDelta, 0.0f, MathF.Cos(startAngle) * rightAngleDelta);
        var endAngle = startAngle + leftAngleDelta;
        var leftEndTangent = new Vector3(-MathF.Sin(endAngle) * leftAngleDelta, 0.0f, MathF.Cos(endAngle) * leftAngleDelta);
        endAngle = startAngle + rightAngleDelta;
        var rightEndTangent = new Vector3(-MathF.Sin(endAngle) * rightAngleDelta, 0.0f, MathF.Cos(endAngle) * rightAngleDelta);
        var leftScore = Vector3.Dot(leftStartTangent, startDirection) + Vector3.Dot(leftEndTangent, endDirection);
        var rightScore = Vector3.Dot(rightStartTangent, startDirection) + Vector3.Dot(rightEndTangent, endDirection);
        var angleDelta = leftScore >= rightScore ? leftAngleDelta : rightAngleDelta;
        var points = new Vector3[subdivisions + 1];

        for (var i = 0; i <= subdivisions; i++) {
            var t = i / (float)subdivisions;
            var angle = startAngle + angleDelta * t;
            points[i] = new Vector3(
                center.X + MathF.Cos(angle) * radius,
                start.Y + (end.Y - start.Y) * t,
                center.Y + MathF.Sin(angle) * radius);
        }

        points[0] = start;
        points[^1] = end;

        return points;
    }

    public static float GetRollRadians(sbyte roll) => roll switch {
        0 => 0.0f,
        > 0 => MathF.PI / 8.0f,
        < 0 => -MathF.PI / 8.0f,
    };

    public static void AddParts(in RCTTrackElementData track, TrackMeshGeometry geometry, ReadOnlySpan<Vector3> centerline, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries, float rollBegin = 0.0f, float rollEnd = 0.0f, Vector3? startTangent = null, Vector3? endTangent = null)
    {
        foreach (var partGenerator in partGenerators)
            partGenerator.Add(in track, geometry, centerline, paletteEntries, rollBegin, rollEnd, startTangent, endTangent);
    }
}
