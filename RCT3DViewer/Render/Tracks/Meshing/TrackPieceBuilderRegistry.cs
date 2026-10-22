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

        TrackCenterlineGeometry.AddParts(track, geometry, points, partGenerators, paletteEntries,
            TrackCenterlineGeometry.GetRollRadians(track.RollBegin), TrackCenterlineGeometry.GetRollRadians(track.RollEnd));
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
        var horizontalDistance = Vector2.Distance(new Vector2(start.X, start.Z), new Vector2(end.X, end.Z));
        var risePerRun = horizontalDistance > float.Epsilon ? (end.Y - start.Y) / horizontalDistance : 0.0f;
        var pitchedStartDirection = Vector3.Normalize(startDirection + Vector3.UnitY * risePerRun);
        var pitchedEndDirection = Vector3.Normalize(endDirection + Vector3.UnitY * risePerRun);
        var points = TrackCenterlineGeometry.CreateCubicCurvePoints(start, end, pitchedStartDirection, pitchedEndDirection,
            controlLength, tracks.Length * TrackCenterlineGeometry.CurveSubdivisionsPerTile);

        TrackCenterlineGeometry.AddParts(first, geometry, points, partGenerators, paletteEntries,
            GetRollBegin(first), GetRollEnd(first));
    }
}

internal sealed class BankedCurveTrackPieceBuilder(RCTTrackElementType pieceType) : CurveTrackPieceBuilder(pieceType)
{
    protected override float GetRollBegin(in RCTTrackElementData track) => TrackCenterlineGeometry.GetRollRadians(track.RollBegin);
    protected override float GetRollEnd(in RCTTrackElementData track) => TrackCenterlineGeometry.GetRollRadians(track.RollEnd);
}

internal sealed class SlopeTrackPieceBuilder(RCTTrackElementType pieceType, bool beginsFlat, bool endsFlat) : TrackPieceMeshBuilder(pieceType)
{
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
        if (beginsFlat || endsFlat) {
            var risePerRun = MathF.Sign(end.Y - start.Y)
                * (2.0f * TrackCenterlineGeometry.HeightPerBaseUnit / TrackCenterlineGeometry.TileSize);
            var slopeDirection = Vector3.Normalize(direction + Vector3.UnitY * risePerRun);
            var startDirection = beginsFlat ? direction : slopeDirection;
            var endDirection = endsFlat ? direction : slopeDirection;
            points = TrackCenterlineGeometry.CreateCubicCurvePoints(
                start, end, startDirection, endDirection, TrackCenterlineGeometry.TileSize / 3.0f,
                TrackCenterlineGeometry.CurveSubdivisionsPerTile);
        }
        else {
            points = [start, end];
        }

        TrackCenterlineGeometry.AddParts(track, geometry, points, partGenerators, paletteEntries,
            TrackCenterlineGeometry.GetRollRadians(track.RollBegin), TrackCenterlineGeometry.GetRollRadians(track.RollEnd));
    }
}

internal class DiagonalTrackPieceBuilder(RCTTrackElementType pieceType) : CurveTrackPieceBuilder(pieceType)
{
    public override void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries)
    {
        if (tracks.IsEmpty) return;

        var first = tracks[0];
        var last = tracks[^1];

        var startHeading = TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationBegin);
        var endHeading = TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationEnd);
        var startDirection = TrackCenterlineGeometry.GetTrackDirection(startHeading);
        var endDirection = TrackCenterlineGeometry.GetTrackDirection(endHeading);
        var start = TrackCenterlineGeometry.GetTileCenter(first) - startDirection * TrackCenterlineGeometry.TileSize * 0.5f;
        var end = TrackCenterlineGeometry.GetTileCenter(last) + endDirection * TrackCenterlineGeometry.TileSize * 0.5f;
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
            TrackCenterlineGeometry.GetRollRadians(first.RollBegin), TrackCenterlineGeometry.GetRollRadians(first.RollEnd));
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
        var points = TrackCenterlineGeometry.CreateCubicCurvePoints(start, end, startDirection, endDirection,
            Vector3.Distance(start, end) * 0.4f, tracks.Length * TrackCenterlineGeometry.CurveSubdivisionsPerTile);

        TrackCenterlineGeometry.AddParts(first, geometry, points, partGenerators, paletteEntries);
    }
}

internal sealed class HelixTrackPieceBuilder(RCTTrackElementType pieceType) : CurveTrackPieceBuilder(pieceType)
{
    public override void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries)
    {
        if (tracks.IsEmpty) return;

        var first = tracks[0];
        var last = tracks[^1];
        var startDirection = TrackCenterlineGeometry.GetDirection(first.Direction);
        var endDirection = TrackCenterlineGeometry.GetDirection((byte)((first.Direction + first.RotationEnd) & 3));
        var originHeight = (first.BaseHeight - first.SequenceOffsetZ) * TrackCenterlineGeometry.HeightPerBaseUnit;
        var start = TrackCenterlineGeometry.GetTileCenter(first) - startDirection * TrackCenterlineGeometry.TileSize * 0.5f;
        var end = TrackCenterlineGeometry.GetTileCenter(last) + endDirection * TrackCenterlineGeometry.TileSize * 0.5f;
        start.Y = originHeight + first.SplineZBegin * TrackCenterlineGeometry.HeightPerBaseUnit;
        end.Y = originHeight + first.SplineZEnd * TrackCenterlineGeometry.HeightPerBaseUnit;
        var waypoints = new Vector3[tracks.Length + 2];
        waypoints[0] = start;
        for (var i = 0; i < tracks.Length; i++) {
            var track = tracks[i];
            var point = TrackCenterlineGeometry.GetTileCenter(track);
            point.Y = track.BaseHeight * TrackCenterlineGeometry.HeightPerBaseUnit;
            waypoints[i + 1] = point;
        }
        waypoints[^1] = end;
        var centerline = TrackCenterlineGeometry.CreateSmoothPath(waypoints, TrackCenterlineGeometry.CurveSubdivisionsPerTile);

        TrackCenterlineGeometry.AddParts(first, geometry, centerline, partGenerators, paletteEntries,
            TrackCenterlineGeometry.GetRollRadians(first.RollBegin), TrackCenterlineGeometry.GetRollRadians(first.RollEnd));
    }
}

internal static class TrackCenterlineGeometry
{
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

    public static Vector3[] CreateSmoothPath(ReadOnlySpan<Vector3> waypoints, int subdivisionsPerSegment)
    {
        if (waypoints.Length < 2) return waypoints.ToArray();

        var points = new Vector3[(waypoints.Length - 1) * subdivisionsPerSegment + 1];
        for (var segment = 0; segment < waypoints.Length - 1; segment++) {
            var p0 = waypoints[Math.Max(0, segment - 1)];
            var p1 = waypoints[segment];
            var p2 = waypoints[segment + 1];
            var p3 = waypoints[Math.Min(waypoints.Length - 1, segment + 2)];
            for (var sample = 0; sample < subdivisionsPerSegment; sample++) {
                var t = sample / (float)subdivisionsPerSegment;
                var t2 = t * t;
                var t3 = t2 * t;
                points[segment * subdivisionsPerSegment + sample] = 0.5f * (
                    2.0f * p1 + (-p0 + p2) * t + (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t2
                    + (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t3);
            }
        }

        points[^1] = waypoints[^1];
        return points;
    }

    public static float GetRollRadians(sbyte roll) => roll switch {
        0 => 0.0f,
        > 0 => MathF.PI / 8.0f,
        < 0 => -MathF.PI / 8.0f,
    };

    public static void AddParts(in RCTTrackElementData track, TrackMeshGeometry geometry, ReadOnlySpan<Vector3> centerline, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries, float rollBegin = 0.0f, float rollEnd = 0.0f)
    {
        foreach (var partGenerator in partGenerators)
            partGenerator.Add(in track, geometry, centerline, paletteEntries, rollBegin, rollEnd);
    }
}
