using RCT3D.Interop;
using System.Numerics;

namespace RCT3D.Render.Rides.Tracks;

internal static class TrackCenterlineGeometry
{
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
        new((track.TileX + 0.5f) * WorldUnits.TileSize, 0.0f, (track.TileY + 0.5f) * WorldUnits.TileSize);

    public static float GetPitchSlope(RCTTrackElementType pieceType, bool atStart)
    {
        if (pieceType is RCTTrackElementType.LeftQuarterTurn1TileUp60
            or RCTTrackElementType.RightQuarterTurn1TileUp60)
            return Pitch.Slope60;

        if (pieceType is RCTTrackElementType.LeftQuarterTurn1TileDown60
            or RCTTrackElementType.RightQuarterTurn1TileDown60)
            return -Pitch.Slope60;

        if (pieceType is RCTTrackElementType.LeftQuarterTurn3TilesUp25
            or RCTTrackElementType.RightQuarterTurn3TilesUp25
            or RCTTrackElementType.LeftQuarterTurn5TilesUp25
            or RCTTrackElementType.RightQuarterTurn5TilesUp25
            or RCTTrackElementType.LeftBankedQuarterTurn3TileUp25
            or RCTTrackElementType.RightBankedQuarterTurn3TileUp25
            or RCTTrackElementType.LeftBankedQuarterTurn5TileUp25
            or RCTTrackElementType.RightBankedQuarterTurn5TileUp25)
            return Pitch.Slope25;

        if (pieceType is RCTTrackElementType.LeftQuarterTurn3TilesDown25
            or RCTTrackElementType.RightQuarterTurn3TilesDown25
            or RCTTrackElementType.LeftQuarterTurn5TilesDown25
            or RCTTrackElementType.RightQuarterTurn5TilesDown25
            or RCTTrackElementType.LeftBankedQuarterTurn3TileDown25
            or RCTTrackElementType.RightBankedQuarterTurn3TileDown25
            or RCTTrackElementType.LeftBankedQuarterTurn5TileDown25
            or RCTTrackElementType.RightBankedQuarterTurn5TileDown25)
            return -Pitch.Slope25;

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

        if (!isUpTransition && !isDownTransition) return Pitch.None;

        var isSlopeAtStart = pieceType is RCTTrackElementType.Up25ToLeftBank
            or RCTTrackElementType.Up25ToRightBank
            or RCTTrackElementType.DiagUp25ToLeftBank
            or RCTTrackElementType.DiagUp25ToRightBank
            or RCTTrackElementType.Down25ToLeftBank
            or RCTTrackElementType.Down25ToRightBank
            or RCTTrackElementType.DiagDown25ToLeftBank
            or RCTTrackElementType.DiagDown25ToRightBank;

        if (atStart != isSlopeAtStart) return Pitch.None;

        return isUpTransition ? Pitch.Slope25 : -Pitch.Slope25;
    }

    public static (float Start, float End)? GetDiagonalSlopeTransitionPitches(RCTTrackElementType pieceType) => pieceType switch {
        RCTTrackElementType.DiagFlatToUp25 => (Pitch.None, Pitch.Slope25),
        RCTTrackElementType.DiagUp25ToFlat => (Pitch.Slope25, Pitch.None),
        RCTTrackElementType.DiagFlatToDown25 => (Pitch.None, -Pitch.Slope25),
        RCTTrackElementType.DiagDown25ToFlat => (-Pitch.Slope25, Pitch.None),
        RCTTrackElementType.DiagFlatToUp60 => (Pitch.None, Pitch.Slope60),
        RCTTrackElementType.DiagUp60ToFlat => (Pitch.Slope60, Pitch.None),
        RCTTrackElementType.DiagFlatToDown60 => (Pitch.None, -Pitch.Slope60),
        RCTTrackElementType.DiagDown60ToFlat => (-Pitch.Slope60, Pitch.None),
        RCTTrackElementType.DiagUp25ToUp60 => (Pitch.Slope25, Pitch.Slope60),
        RCTTrackElementType.DiagUp60ToUp25 => (Pitch.Slope60, Pitch.Slope25),
        RCTTrackElementType.DiagDown25ToDown60 => (-Pitch.Slope25, -Pitch.Slope60),
        RCTTrackElementType.DiagDown60ToDown25 => (-Pitch.Slope60, -Pitch.Slope25),
        RCTTrackElementType.DiagFlatToUp60LongBase => (Pitch.None, Pitch.Slope60),
        RCTTrackElementType.DiagUp60ToFlatLongBase => (Pitch.Slope60, Pitch.None),
        RCTTrackElementType.DiagFlatToDown60LongBase => (Pitch.None, -Pitch.Slope60),
        RCTTrackElementType.DiagDown60ToFlatLongBase => (-Pitch.Slope60, Pitch.None),
        _ => null,
    };

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

    public static void AddParts(in RCTTrackElementData track, TrackMeshGeometry geometry, ReadOnlySpan<Vector3> centerline, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries, float rollBegin = 0.0f, float rollEnd = 0.0f, Vector3? startTangent = null, Vector3? endTangent = null, Vector3? preferredSide = null, bool smoothRollTransition = false)
    {
        geometry.CaptureCenterline(in track, centerline, rollBegin, rollEnd, smoothRollTransition);

        foreach (var partGenerator in partGenerators)
            partGenerator.Add(in track, geometry, centerline, paletteEntries, rollBegin, rollEnd, startTangent, endTangent, preferredSide, smoothRollTransition);
    }
}

internal readonly record struct TrackCenterlinePath(byte Sequence, Vector3[] Points, float[] RollRadians);
