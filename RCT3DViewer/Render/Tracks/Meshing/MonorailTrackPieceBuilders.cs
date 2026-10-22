using RCT3D.Interop;
using System.Numerics;

namespace RCT3D.Render.Tracks.Meshing;

internal sealed class MonorailTrackBuilderResolver
{
    private readonly Dictionary<RCTTrackElementType, ITrackPieceMeshBuilder> _builders = new();

    public MonorailTrackBuilderResolver()
    {
        Register(new MonorailFlatBuilder());
        Register(new MonorailEndStationBuilder());
        Register(new MonorailBeginStationBuilder());
        Register(new MonorailMiddleStationBuilder());
        Register(new MonorailUp25Builder());
        Register(new MonorailFlatToUp25Builder());
        Register(new MonorailUp25ToFlatBuilder());
        Register(new MonorailDown25Builder());
        Register(new MonorailFlatToDown25Builder());
        Register(new MonorailDown25ToFlatBuilder());
        Register(new MonorailLeftQuarterTurn5Builder());
        Register(new MonorailRightQuarterTurn5Builder());
        Register(new MonorailSBendLeftBuilder());
        Register(new MonorailSBendRightBuilder());
        Register(new MonorailLeftQuarterTurn3Builder());
        Register(new MonorailRightQuarterTurn3Builder());
        Register(new MonorailLeftQuarterTurn1Builder());
        Register(new MonorailRightQuarterTurn1Builder());
        Register(new MonorailLeftEighthToDiagBuilder());
        Register(new MonorailRightEighthToDiagBuilder());
        Register(new MonorailLeftEighthToOrthogonalBuilder());
        Register(new MonorailRightEighthToOrthogonalBuilder());
        Register(new MonorailDiagFlatBuilder());
        Register(new MonorailLeftQuarterTurn5CoveredBuilder());
        Register(new MonorailRightQuarterTurn5CoveredBuilder());
        Register(new MonorailSBendLeftCoveredBuilder());
        Register(new MonorailSBendRightCoveredBuilder());
        Register(new MonorailLeftQuarterTurn3CoveredBuilder());
        Register(new MonorailRightQuarterTurn3CoveredBuilder());
    }

    public bool TryResolve(RCTTrackElementType type, out ITrackPieceMeshBuilder builder) => _builders.TryGetValue(type, out builder!);

    private void Register(ITrackPieceMeshBuilder builder) => _builders.Add(builder.PieceType, builder);
}

internal abstract class MonorailStraightTrackPieceBuilder(RCTTrackElementType pieceType, float rise)
    : TrackPieceMeshBuilder(pieceType)
{
    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry)
    {
        var direction = MonorailTrackGeometry.GetDirection(track.Direction);
        var center = MonorailTrackGeometry.GetTileCenter(track);
        var startHeight = track.BaseHeight * MonorailTrackGeometry.HeightPerBaseUnit;
        var points = new[] {
            center - direction * (MonorailTrackGeometry.TileSize * 0.5f) + Vector3.UnitY * startHeight,
            center + direction * (MonorailTrackGeometry.TileSize * 0.5f) + Vector3.UnitY * (startHeight + rise),
        };
        MonorailTrackGeometry.AddTrackParts(track, geometry, points);
    }
}

internal abstract class MonorailCurveTrackPieceBuilder(RCTTrackElementType pieceType)
    : TrackPieceMeshBuilder(pieceType)
{
    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry) =>
        throw new InvalidOperationException("Curved track geometry must be built from a complete piece.");

    public override void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry)
    {
        if (tracks.IsEmpty)
            return;

        var first = tracks[0];
        var last = tracks[^1];
        var pieceDirection = first.Direction;
        var startHeading = MonorailTrackGeometry.CombineDirection(pieceDirection, first.RotationBegin);
        var endHeading = MonorailTrackGeometry.CombineDirection(pieceDirection, first.RotationEnd);
        var startDirection = MonorailTrackGeometry.GetTrackDirection(startHeading);
        var endDirection = MonorailTrackGeometry.GetTrackDirection(endHeading);
        var startCenter = MonorailTrackGeometry.GetTileCenter(first);
        var endCenter = MonorailTrackGeometry.GetTileCenter(last);
        var halfTile = MonorailTrackGeometry.TileSize * 0.5f;
        var startOffset = (startHeading & 4) != 0 ? Vector3.Zero : startDirection * halfTile;
        var endOffset = (endHeading & 4) != 0 ? Vector3.Zero : endDirection * halfTile;
        var start = startCenter - startOffset;
        var end = endCenter + endOffset;
        start.Y = first.BaseHeight * MonorailTrackGeometry.HeightPerBaseUnit;
        end.Y = last.BaseHeight * MonorailTrackGeometry.HeightPerBaseUnit;

        var controlLength = Vector3.Distance(start, end) * 0.4f;
        var points = MonorailTrackGeometry.CreateCubicCurvePoints(start, end, startDirection, endDirection,
            controlLength, tracks.Length * MonorailTrackGeometry.CurveSubdivisionsPerTile);
        MonorailTrackGeometry.AddTrackParts(first, geometry, points);
    }
}

internal sealed class MonorailFlatBuilder() : MonorailStraightTrackPieceBuilder(RCTTrackElementType.Flat, 0.0f) { }
internal sealed class MonorailEndStationBuilder() : MonorailStraightTrackPieceBuilder(RCTTrackElementType.EndStation, 0.0f) { }
internal sealed class MonorailBeginStationBuilder() : MonorailStraightTrackPieceBuilder(RCTTrackElementType.BeginStation, 0.0f) { }
internal sealed class MonorailMiddleStationBuilder() : MonorailStraightTrackPieceBuilder(RCTTrackElementType.MiddleStation, 0.0f) { }
internal sealed class MonorailUp25Builder() : MonorailStraightTrackPieceBuilder(RCTTrackElementType.Up25, 1.5f) { }
internal sealed class MonorailFlatToUp25Builder() : MonorailStraightTrackPieceBuilder(RCTTrackElementType.FlatToUp25, 0.75f) { }
internal sealed class MonorailUp25ToFlatBuilder() : MonorailStraightTrackPieceBuilder(RCTTrackElementType.Up25ToFlat, 0.75f) { }
internal sealed class MonorailDown25Builder() : MonorailStraightTrackPieceBuilder(RCTTrackElementType.Down25, -1.5f) { }
internal sealed class MonorailFlatToDown25Builder() : MonorailStraightTrackPieceBuilder(RCTTrackElementType.FlatToDown25, -0.75f) { }
internal sealed class MonorailDown25ToFlatBuilder() : MonorailStraightTrackPieceBuilder(RCTTrackElementType.Down25ToFlat, -0.75f) { }
internal sealed class MonorailLeftQuarterTurn5Builder() : MonorailCurveTrackPieceBuilder(RCTTrackElementType.LeftQuarterTurn5Tiles) { }
internal sealed class MonorailRightQuarterTurn5Builder() : MonorailCurveTrackPieceBuilder(RCTTrackElementType.RightQuarterTurn5Tiles) { }
internal sealed class MonorailSBendLeftBuilder() : MonorailCurveTrackPieceBuilder(RCTTrackElementType.SBendLeft) { }
internal sealed class MonorailSBendRightBuilder() : MonorailCurveTrackPieceBuilder(RCTTrackElementType.SBendRight) { }
internal sealed class MonorailLeftQuarterTurn3Builder() : MonorailCurveTrackPieceBuilder(RCTTrackElementType.LeftQuarterTurn3Tiles) { }
internal sealed class MonorailRightQuarterTurn3Builder() : MonorailCurveTrackPieceBuilder(RCTTrackElementType.RightQuarterTurn3Tiles) { }
internal sealed class MonorailLeftQuarterTurn1Builder() : MonorailCurveTrackPieceBuilder(RCTTrackElementType.LeftQuarterTurn1Tile) { }
internal sealed class MonorailRightQuarterTurn1Builder() : MonorailCurveTrackPieceBuilder(RCTTrackElementType.RightQuarterTurn1Tile) { }
internal sealed class MonorailLeftEighthToDiagBuilder() : MonorailCurveTrackPieceBuilder(RCTTrackElementType.LeftEighthToDiag) { }
internal sealed class MonorailRightEighthToDiagBuilder() : MonorailCurveTrackPieceBuilder(RCTTrackElementType.RightEighthToDiag) { }
internal sealed class MonorailLeftEighthToOrthogonalBuilder() : MonorailCurveTrackPieceBuilder(RCTTrackElementType.LeftEighthToOrthogonal) { }
internal sealed class MonorailRightEighthToOrthogonalBuilder() : MonorailCurveTrackPieceBuilder(RCTTrackElementType.RightEighthToOrthogonal) { }
internal sealed class MonorailDiagFlatBuilder() : MonorailCurveTrackPieceBuilder(RCTTrackElementType.DiagFlat)
{
    public override void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry)
    {
        if (tracks.IsEmpty)
            return;

        var first = tracks[0];
        var last = tracks[^1];
        Span<Vector3> points = stackalloc Vector3[2] {
            MonorailTrackGeometry.GetTileCenter(first) + Vector3.UnitY * (first.BaseHeight * MonorailTrackGeometry.HeightPerBaseUnit),
            MonorailTrackGeometry.GetTileCenter(last) + Vector3.UnitY * (last.BaseHeight * MonorailTrackGeometry.HeightPerBaseUnit),
        };
        MonorailTrackGeometry.AddTrackParts(first, geometry, points);
    }
}
internal sealed class MonorailLeftQuarterTurn5CoveredBuilder() : MonorailCurveTrackPieceBuilder(RCTTrackElementType.LeftQuarterTurn5TilesCovered) { }
internal sealed class MonorailRightQuarterTurn5CoveredBuilder() : MonorailCurveTrackPieceBuilder(RCTTrackElementType.RightQuarterTurn5TilesCovered) { }
internal sealed class MonorailSBendLeftCoveredBuilder() : MonorailCurveTrackPieceBuilder(RCTTrackElementType.SBendLeftCovered) { }
internal sealed class MonorailSBendRightCoveredBuilder() : MonorailCurveTrackPieceBuilder(RCTTrackElementType.SBendRightCovered) { }
internal sealed class MonorailLeftQuarterTurn3CoveredBuilder() : MonorailCurveTrackPieceBuilder(RCTTrackElementType.LeftQuarterTurn3TilesCovered) { }
internal sealed class MonorailRightQuarterTurn3CoveredBuilder() : MonorailCurveTrackPieceBuilder(RCTTrackElementType.RightQuarterTurn3TilesCovered) { }

internal static class MonorailTrackGeometry
{
    public const float TileSize = 3.0f;
    public const float HeightPerBaseUnit = 0.75f;
    public const int CurveSubdivisionsPerTile = 8;

    private static readonly TrackPartProfile DeckProfile = new(0.9f, 0.22f);
    private static readonly TrackPartProfile RailProfile = new(0.08f, 0.18f);
    private static readonly TrackPartProfile TieProfile = new(0.08f, 0.12f);

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

    public static void AddTrackParts(in RCTTrackElementData track, TrackMeshGeometry geometry, ReadOnlySpan<Vector3> centerline)
    {
        TrackMeshPartGenerator.AddSweptPrism(geometry, centerline, DeckProfile, track.MainPaletteIndex);

        if (centerline.Length < 2)
            return;

        var tieCount = centerline.Length <= 2
            ? 1
            : Math.Max(1, (int)MathF.Ceiling(GetLength(centerline) / 0.75f));
        for (var tie = 0; tie <= tieCount; tie++) {
            var t = tie / (float)tieCount;
            var distance = GetLength(centerline) * t;
            var position = SampleByDistance(centerline, distance);
            var tangent = Vector3.Normalize(
                SampleByDistance(centerline, MathF.Min(distance + 0.05f, GetLength(centerline)))
                - SampleByDistance(centerline, MathF.Max(distance - 0.05f, 0.0f)));
            var side = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, tangent));
            Span<Vector3> tieLine = stackalloc Vector3[2] {
                position - side * 0.58f - Vector3.UnitY * 0.16f,
                position + side * 0.58f - Vector3.UnitY * 0.16f,
            };
            TrackMeshPartGenerator.AddSweptPrism(geometry, tieLine, TieProfile, track.AdditionalPaletteIndex);
        }

        var leftRail = Offset(centerline, 0.37f);
        var rightRail = Offset(centerline, -0.37f);
        TrackMeshPartGenerator.AddSweptPrism(geometry, leftRail, RailProfile, track.AdditionalPaletteIndex);
        TrackMeshPartGenerator.AddSweptPrism(geometry, rightRail, RailProfile, track.AdditionalPaletteIndex);
    }

    private static Vector3[] Offset(ReadOnlySpan<Vector3> centerline, float amount)
    {
        var points = new Vector3[centerline.Length];
        for (var i = 0; i < centerline.Length; i++) {
            var before = centerline[Math.Max(0, i - 1)];
            var after = centerline[Math.Min(centerline.Length - 1, i + 1)];
            var tangent = Vector3.Normalize(after - before);
            var side = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, tangent));
            points[i] = centerline[i] + side * amount;
        }
        return points;
    }

    private static float GetLength(ReadOnlySpan<Vector3> points)
    {
        var length = 0.0f;
        for (var i = 0; i < points.Length - 1; i++)
            length += Vector3.Distance(points[i], points[i + 1]);
        return length;
    }

    private static Vector3 SampleByDistance(ReadOnlySpan<Vector3> points, float distance)
    {
        for (var i = 0; i < points.Length - 1; i++) {
            var segmentLength = Vector3.Distance(points[i], points[i + 1]);
            if (distance <= segmentLength || i == points.Length - 2)
                return Vector3.Lerp(points[i], points[i + 1], segmentLength <= float.Epsilon ? 0.0f : distance / segmentLength);
            distance -= segmentLength;
        }

        return points[^1];
    }
}
