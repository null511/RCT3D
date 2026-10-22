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
        var startHeight = track.BaseHeight * TrackCenterlineGeometry.HeightPerBaseUnit;

        var points = new[] {
            center - direction * (TrackCenterlineGeometry.TileSize * 0.5f) + Vector3.UnitY * startHeight,
            center + direction * (TrackCenterlineGeometry.TileSize * 0.5f) + Vector3.UnitY * (startHeight + rise),
        };

        TrackCenterlineGeometry.AddParts(track, geometry, points, partGenerators, paletteEntries);
    }
}

internal class CurveTrackPieceBuilder(RCTTrackElementType pieceType) : TrackPieceMeshBuilder(pieceType)
{
    public override bool RequiresCompletePiece => true;

    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries) =>
        throw new InvalidOperationException("Curved track geometry must be built from a complete piece.");

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
        start.Y = first.BaseHeight * TrackCenterlineGeometry.HeightPerBaseUnit;
        end.Y = last.BaseHeight * TrackCenterlineGeometry.HeightPerBaseUnit;

        var controlLength = Vector3.Distance(start, end) * 0.4f;
        var points = TrackCenterlineGeometry.CreateCubicCurvePoints(start, end, startDirection, endDirection,
            controlLength, tracks.Length * TrackCenterlineGeometry.CurveSubdivisionsPerTile);

        TrackCenterlineGeometry.AddParts(first, geometry, points, partGenerators, paletteEntries);
    }
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
        start.Y = (track.BaseHeight + track.SplineZBegin) * TrackCenterlineGeometry.HeightPerBaseUnit;
        end.Y = (track.BaseHeight + track.SplineZEnd) * TrackCenterlineGeometry.HeightPerBaseUnit;

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

        TrackCenterlineGeometry.AddParts(track, geometry, points, partGenerators, paletteEntries);
    }
}

internal sealed class DiagonalFlatTrackPieceBuilder() : CurveTrackPieceBuilder(RCTTrackElementType.DiagFlat)
{
    public override void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries)
    {
        if (tracks.IsEmpty) return;

        var first = tracks[0];
        var last = tracks[^1];

        Span<Vector3> points = stackalloc Vector3[2] {
            TrackCenterlineGeometry.GetTileCenter(first) + Vector3.UnitY * (first.BaseHeight * TrackCenterlineGeometry.HeightPerBaseUnit),
            TrackCenterlineGeometry.GetTileCenter(last) + Vector3.UnitY * (last.BaseHeight * TrackCenterlineGeometry.HeightPerBaseUnit),
        };

        TrackCenterlineGeometry.AddParts(first, geometry, points, partGenerators, paletteEntries);
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

    public static void AddParts(in RCTTrackElementData track, TrackMeshGeometry geometry, ReadOnlySpan<Vector3> centerline, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries)
    {
        foreach (var partGenerator in partGenerators)
            partGenerator.Add(in track, geometry, centerline, paletteEntries);
    }
}
