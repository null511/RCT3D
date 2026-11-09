using RCT3D.Interop.Models;
using System.Numerics;

namespace RCT3D.Render.Rides.Tracks;

internal static class TrackCenterlineGeometry
{
    public const int CurveSubdivisionsPerTile = 8;
    public const float VerticalInvertedAnchorOffset = -1.5f;


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

    public static Vector3 GetTrackLeftDirection(Vector3 tangent, byte fallbackDirection)
    {
        var horizontalTangent = new Vector3(tangent.X, 0.0f, tangent.Z);
        if (horizontalTangent.LengthSquared() <= float.Epsilon)
            horizontalTangent = GetTrackDirection(fallbackDirection);

        return Vector3.Normalize(Vector3.Cross(Vector3.Normalize(horizontalTangent), Vector3.UnitY));
    }

    public static int GetHorizontalTurnDirection(ReadOnlySpan<Vector3> points, float progress)
    {
        if (points.Length < 3 || !float.IsFinite(progress) || progress < 0.0f || progress > 1.0f)
            return 0;

        var totalLength = 0.0f;
        for (var i = 0; i < points.Length - 1; i++)
            totalLength += Vector3.Distance(points[i], points[i + 1]);

        if (totalLength <= float.Epsilon)
            return 0;

        var targetDistance = totalLength * progress;
        var traversedDistance = 0.0f;
        var segmentIndex = -1;
        for (var i = 0; i < points.Length - 1; i++) {
            var segmentLength = Vector3.Distance(points[i], points[i + 1]);
            if (segmentLength <= float.Epsilon) continue;

            if (targetDistance <= traversedDistance + segmentLength || i == points.Length - 2) {
                segmentIndex = i;
                break;
            }

            traversedDistance += segmentLength;
        }

        if (segmentIndex < 0)
            return 0;

        var beforeIndex = Math.Max(0, segmentIndex - 1);
        var afterIndex = Math.Min(points.Length - 2, segmentIndex + 1);
        var before = points[beforeIndex + 1] - points[beforeIndex];
        var after = points[afterIndex + 1] - points[afterIndex];
        before.Y = 0.0f;
        after.Y = 0.0f;
        if (before.LengthSquared() <= float.Epsilon || after.LengthSquared() <= float.Epsilon)
            return 0;

        before = Vector3.Normalize(before);
        after = Vector3.Normalize(after);
        var signedTurn = before.X * after.Z - before.Z * after.X;
        return MathF.Abs(signedTurn) <= 1e-5f ? 0 : Math.Sign(signedTurn);
    }

    public static Vector3 GetTileCenter(in RCTTrackElementData track) =>
        new((track.TileX + 0.5f) * WorldUnits.TileSize, 0.0f, (track.TileY + 0.5f) * WorldUnits.TileSize);

    public static float GetPitchSlope(RCTTrackElementType pieceType, bool atStart)
    {
        if (pieceType is RCTTrackElementType.LeftZeroGRollUp
            or RCTTrackElementType.RightZeroGRollUp)
            return atStart ? Pitch.Slope25 : Pitch.None;

        if (pieceType is RCTTrackElementType.LeftZeroGRollDown
            or RCTTrackElementType.RightZeroGRollDown)
            return atStart ? Pitch.None : -Pitch.Slope25;

        if (pieceType is RCTTrackElementType.LeftLargeZeroGRollUp
            or RCTTrackElementType.RightLargeZeroGRollUp)
            return atStart ? Pitch.Slope60 : Pitch.None;

        if (pieceType is RCTTrackElementType.LeftLargeZeroGRollDown
            or RCTTrackElementType.RightLargeZeroGRollDown)
            return atStart ? Pitch.None : -Pitch.Slope60;

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

        if (pieceType is RCTTrackElementType.Up25LeftBanked
            or RCTTrackElementType.Up25RightBanked
            or RCTTrackElementType.Up25ToLeftBankedUp25
            or RCTTrackElementType.Up25ToRightBankedUp25
            or RCTTrackElementType.LeftBankedUp25ToUp25
            or RCTTrackElementType.RightBankedUp25ToUp25
            or RCTTrackElementType.LeftEighthToDiagUp25
            or RCTTrackElementType.RightEighthToDiagUp25
            or RCTTrackElementType.LeftEighthToOrthogonalUp25
            or RCTTrackElementType.RightEighthToOrthogonalUp25
            or RCTTrackElementType.LeftEighthBankToDiagUp25
            or RCTTrackElementType.RightEighthBankToDiagUp25
            or RCTTrackElementType.LeftEighthBankToOrthogonalUp25
            or RCTTrackElementType.RightEighthBankToOrthogonalUp25)
            return Pitch.Slope25;

        if (pieceType is RCTTrackElementType.Down25LeftBanked
            or RCTTrackElementType.Down25RightBanked
            or RCTTrackElementType.Down25ToLeftBankedDown25
            or RCTTrackElementType.Down25ToRightBankedDown25
            or RCTTrackElementType.LeftBankedDown25ToDown25
            or RCTTrackElementType.RightBankedDown25ToDown25
            or RCTTrackElementType.LeftEighthToDiagDown25
            or RCTTrackElementType.RightEighthToDiagDown25
            or RCTTrackElementType.LeftEighthToOrthogonalDown25
            or RCTTrackElementType.RightEighthToOrthogonalDown25
            or RCTTrackElementType.LeftEighthBankToDiagDown25
            or RCTTrackElementType.RightEighthBankToDiagDown25
            or RCTTrackElementType.LeftEighthBankToOrthogonalDown25
            or RCTTrackElementType.RightEighthBankToOrthogonalDown25)
            return -Pitch.Slope25;

        var isUpTransition = pieceType is RCTTrackElementType.LeftBankToUp25
            or RCTTrackElementType.RightBankToUp25
            or RCTTrackElementType.Up25ToLeftBank
            or RCTTrackElementType.Up25ToRightBank
            or RCTTrackElementType.DiagLeftBankToUp25
            or RCTTrackElementType.DiagRightBankToUp25
            or RCTTrackElementType.DiagUp25ToLeftBank
            or RCTTrackElementType.DiagUp25ToRightBank
            or RCTTrackElementType.LeftBankedFlatToLeftBankedUp25
            or RCTTrackElementType.RightBankedFlatToRightBankedUp25
            or RCTTrackElementType.LeftBankedUp25ToLeftBankedFlat
            or RCTTrackElementType.RightBankedUp25ToRightBankedFlat
            or RCTTrackElementType.LeftBankedUp25ToFlat
            or RCTTrackElementType.RightBankedUp25ToFlat
            or RCTTrackElementType.FlatToLeftBankedUp25
            or RCTTrackElementType.FlatToRightBankedUp25;

        var isDownTransition = pieceType is RCTTrackElementType.LeftBankToDown25
            or RCTTrackElementType.RightBankToDown25
            or RCTTrackElementType.Down25ToLeftBank
            or RCTTrackElementType.Down25ToRightBank
            or RCTTrackElementType.DiagLeftBankToDown25
            or RCTTrackElementType.DiagRightBankToDown25
            or RCTTrackElementType.DiagDown25ToLeftBank
            or RCTTrackElementType.DiagDown25ToRightBank
            or RCTTrackElementType.LeftBankedFlatToLeftBankedDown25
            or RCTTrackElementType.RightBankedFlatToRightBankedDown25
            or RCTTrackElementType.LeftBankedDown25ToLeftBankedFlat
            or RCTTrackElementType.RightBankedDown25ToRightBankedFlat
            or RCTTrackElementType.LeftBankedDown25ToFlat
            or RCTTrackElementType.RightBankedDown25ToFlat
            or RCTTrackElementType.FlatToLeftBankedDown25
            or RCTTrackElementType.FlatToRightBankedDown25;

        if (!isUpTransition && !isDownTransition) return Pitch.None;

        var isSlopeAtStart = pieceType is RCTTrackElementType.Up25ToLeftBank
            or RCTTrackElementType.Up25ToRightBank
            or RCTTrackElementType.DiagUp25ToLeftBank
            or RCTTrackElementType.DiagUp25ToRightBank
            or RCTTrackElementType.Down25ToLeftBank
            or RCTTrackElementType.Down25ToRightBank
            or RCTTrackElementType.LeftBankedUp25ToLeftBankedFlat
            or RCTTrackElementType.RightBankedUp25ToRightBankedFlat
            or RCTTrackElementType.LeftBankedUp25ToFlat
            or RCTTrackElementType.RightBankedUp25ToFlat
            or RCTTrackElementType.LeftBankedDown25ToLeftBankedFlat
            or RCTTrackElementType.RightBankedDown25ToRightBankedFlat
            or RCTTrackElementType.LeftBankedDown25ToFlat
            or RCTTrackElementType.RightBankedDown25ToFlat
            or RCTTrackElementType.DiagDown25ToLeftBank
            or RCTTrackElementType.DiagDown25ToRightBank;

        if (atStart != isSlopeAtStart) return Pitch.None;

        return isUpTransition ? Pitch.Slope25 : -Pitch.Slope25;
    }

    public static (float Start, float End)? GetDiagonalSlopeTransitionPitches(RCTTrackElementType pieceType) => pieceType switch {
        RCTTrackElementType.DiagLeftBankToUp25 or RCTTrackElementType.DiagRightBankToUp25
            => (Pitch.None, Pitch.Slope25),
        RCTTrackElementType.DiagUp25ToLeftBank or RCTTrackElementType.DiagUp25ToRightBank
            => (Pitch.Slope25, Pitch.None),
        RCTTrackElementType.DiagLeftBankToDown25 or RCTTrackElementType.DiagRightBankToDown25
            => (Pitch.None, -Pitch.Slope25),
        RCTTrackElementType.DiagDown25ToLeftBank or RCTTrackElementType.DiagDown25ToRightBank
            => (-Pitch.Slope25, Pitch.None),
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
        RCTTrackElementType.DiagUp25ToLeftBankedUp25 or RCTTrackElementType.DiagUp25ToRightBankedUp25
            or RCTTrackElementType.DiagLeftBankedUp25ToUp25 or RCTTrackElementType.DiagRightBankedUp25ToUp25
            or RCTTrackElementType.DiagUp25LeftBanked or RCTTrackElementType.DiagUp25RightBanked
            => (Pitch.Slope25, Pitch.Slope25),
        RCTTrackElementType.DiagDown25ToLeftBankedDown25 or RCTTrackElementType.DiagDown25ToRightBankedDown25
            or RCTTrackElementType.DiagLeftBankedDown25ToDown25 or RCTTrackElementType.DiagRightBankedDown25ToDown25
            or RCTTrackElementType.DiagDown25LeftBanked or RCTTrackElementType.DiagDown25RightBanked
            => (-Pitch.Slope25, -Pitch.Slope25),
        RCTTrackElementType.DiagLeftBankedFlatToLeftBankedUp25 or RCTTrackElementType.DiagRightBankedFlatToRightBankedUp25
            or RCTTrackElementType.DiagFlatToLeftBankedUp25 or RCTTrackElementType.DiagFlatToRightBankedUp25
            => (Pitch.None, Pitch.Slope25),
        RCTTrackElementType.DiagLeftBankedUp25ToLeftBankedFlat or RCTTrackElementType.DiagRightBankedUp25ToRightBankedFlat
            or RCTTrackElementType.DiagLeftBankedUp25ToFlat or RCTTrackElementType.DiagRightBankedUp25ToFlat
            => (Pitch.Slope25, Pitch.None),
        RCTTrackElementType.DiagLeftBankedFlatToLeftBankedDown25 or RCTTrackElementType.DiagRightBankedFlatToRightBankedDown25
            or RCTTrackElementType.DiagFlatToLeftBankedDown25 or RCTTrackElementType.DiagFlatToRightBankedDown25
            => (Pitch.None, -Pitch.Slope25),
        RCTTrackElementType.DiagLeftBankedDown25ToLeftBankedFlat or RCTTrackElementType.DiagRightBankedDown25ToRightBankedFlat
            or RCTTrackElementType.DiagLeftBankedDown25ToFlat or RCTTrackElementType.DiagRightBankedDown25ToFlat
            => (-Pitch.Slope25, Pitch.None),
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

    public static float GetRollRadians(sbyte roll, float bankAngleDegrees) => roll switch {
        0 => 0.0f,
        > 0 => bankAngleDegrees * (MathF.PI / 180.0f),
        < 0 => -bankAngleDegrees * (MathF.PI / 180.0f),
    };

    public static void AddVerticallyAlignedParts(
        in RCTTrackElementData track,
        TrackMeshGeometry geometry,
        ReadOnlySpan<Vector3> centerline,
        IReadOnlyList<ITrackPartGenerator> partGenerators,
        ReadOnlySpan<RCTPaletteEntry> paletteEntries,
        bool verticalAnchorAtStart,
        float rollBegin = 0.0f,
        float rollEnd = 0.0f,
        Vector3? startTangent = null,
        Vector3? endTangent = null,
        Vector3? preferredSide = null,
        bool smoothRollTransition = false)
    {
        var frames = TrackMeshPartGenerator.CreateFrames(
            centerline, rollBegin, rollEnd, startTangent, endTangent, preferredSide, smoothRollTransition);
        if (frames.Length != centerline.Length) return;

        geometry.SetProfileOffset(GetVerticalProfileOffset(
            track, partGenerators, frames[verticalAnchorAtStart ? 0 : frames.Length - 1]));

        try {
            AddParts(track, geometry, centerline, partGenerators, paletteEntries,
                rollBegin, rollEnd, startTangent, endTangent, preferredSide, smoothRollTransition);
        }
        finally {
            geometry.SetProfileOffset(Vector3.Zero);
        }
    }

    public static Vector3 GetVerticalProfileOffset(
        in RCTTrackElementData track,
        IReadOnlyList<ITrackPartGenerator> partGenerators,
        TrackCenterlineFrame frame)
    {
        var wallNormal = -frame.Up;
        var maximumProfileProjection = float.NegativeInfinity;
        foreach (var partGenerator in partGenerators)
            maximumProfileProjection = MathF.Max(
                maximumProfileProjection,
                partGenerator.GetMaximumProfileProjection(track, wallNormal, frame));

        return float.IsFinite(maximumProfileProjection)
            ? wallNormal * (WorldUnits.TileSize * 0.5f - maximumProfileProjection)
            : Vector3.Zero;
    }

    public static void AddParts(in RCTTrackElementData track, TrackMeshGeometry geometry, ReadOnlySpan<Vector3> centerline, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries, float rollBegin = 0.0f, float rollEnd = 0.0f, Vector3? startTangent = null, Vector3? endTangent = null, Vector3? preferredSide = null, bool smoothRollTransition = false)
    {
        geometry.CaptureCenterline(
            in track, centerline, rollBegin, rollEnd, startTangent, endTangent, preferredSide, smoothRollTransition);

        foreach (var partGenerator in partGenerators)
            partGenerator.Add(in track, geometry, centerline, paletteEntries, rollBegin, rollEnd, startTangent, endTangent, preferredSide, smoothRollTransition);
    }
}

internal readonly record struct TrackCenterlinePath(byte Sequence, Vector3[] Points, TrackCenterlineFrame[] Frames);

internal readonly record struct TrackCenterlineFrame(Vector3 Tangent, Vector3 Side, Vector3 Up)
{
    public Matrix4x4 ToRotation() => new(
        Side.X, Side.Y, Side.Z, 0.0f,
        Up.X, Up.Y, Up.Z, 0.0f,
        Tangent.X, Tangent.Y, Tangent.Z, 0.0f,
        0.0f, 0.0f, 0.0f, 1.0f);
}
