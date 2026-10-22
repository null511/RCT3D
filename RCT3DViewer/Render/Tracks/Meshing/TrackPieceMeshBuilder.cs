using RCT3D.Interop;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3D.Render.Tracks.Meshing;

internal interface ITrackPieceMeshBuilder
{
    RCTTrackElementType PieceType { get; }
    bool RequiresCompletePiece { get; }

    void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette);
    void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette);
}

internal abstract class TrackPieceMeshBuilder(RCTTrackElementType pieceType) : ITrackPieceMeshBuilder
{
    public RCTTrackElementType PieceType { get; } = pieceType;
    public virtual bool RequiresCompletePiece => false;

    public abstract void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette);

    public virtual void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette)
    {
        foreach (ref readonly var track in tracks)
            Add(in track, geometry, partGenerators, palette);
    }
}

internal interface ITrackPartGenerator
{
    void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, ReadOnlySpan<Vector3> centerline, ReadOnlySpan<RCTPaletteEntry> palette, float rollBegin, float rollEnd);
}

internal enum TrackRailSwatch : byte
{
    Main,
    Alternate,
    None
}

internal readonly record struct TrackRailSpecularData(byte Roughness, byte Metalness);

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct TrackRgba8(byte Red, byte Green, byte Blue, byte Alpha)
{
    public static TrackRgba8 White => new(255, 255, 255, 255);
}

internal sealed record TrackRailProfileShape(Vector2[] Vertices, bool Closed)
{
    public TrackRailSwatch Swatch { get; init; } = TrackRailSwatch.None;
    public TrackRgba8 Color { get; init; } = TrackRgba8.White;
    public TrackRailSpecularData Specular { get; init; } = new(250, 0);
}

internal sealed record TrackRailProfile(TrackRailProfileShape[] Shapes);

internal sealed class SweptRailProfileGenerator(TrackRailProfile profile) : ITrackPartGenerator
{
    public void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, ReadOnlySpan<Vector3> centerline, ReadOnlySpan<RCTPaletteEntry> palette, float rollBegin, float rollEnd) =>
        TrackMeshPartGenerator.AddSweptProfile(
            geometry,
            centerline,
            profile,
            track.MainPaletteIndex,
            track.AdditionalPaletteIndex,
            palette,
            rollBegin,
            rollEnd);
}

internal static class TrackMeshPartGenerator
{
    public static void AddSweptProfile(
        TrackMeshGeometry geometry,
        ReadOnlySpan<Vector3> centerline,
        TrackRailProfile profile,
        byte mainPaletteIndex,
        byte alternatePaletteIndex,
        ReadOnlySpan<RCTPaletteEntry> palette,
        float rollBegin,
        float rollEnd)
    {
        if (centerline.Length < 2) return;

        foreach (var shape in profile.Shapes) {
            if (shape.Vertices.Length < 2) continue;

            var color = shape.Swatch switch {
                TrackRailSwatch.Main => GetPaletteColor(mainPaletteIndex, palette),
                TrackRailSwatch.Alternate => GetPaletteColor(alternatePaletteIndex, palette),
                TrackRailSwatch.None => shape.Color,
                _ => throw new InvalidDataException($"Unknown rail swatch {shape.Swatch}.")
            };

            var edgeCount = shape.Closed ? shape.Vertices.Length : shape.Vertices.Length - 1;
            var stationVertices = new uint[centerline.Length, shape.Vertices.Length];
            var stationTangents = new Vector3[centerline.Length];
            var stationSides = new Vector3[centerline.Length];
            var stationUps = new Vector3[centerline.Length];

            for (var station = 0; station < centerline.Length; station++) {
                var previous = centerline[Math.Max(0, station - 1)];
                var next = centerline[Math.Min(centerline.Length - 1, station + 1)];
                var tangent = next - previous;

                if (tangent.LengthSquared() <= float.Epsilon)
                    tangent = station + 1 < centerline.Length ? next - centerline[station] : centerline[station] - previous;

                if (tangent.LengthSquared() <= float.Epsilon) return;

                tangent = Vector3.Normalize(tangent);
                var side = Vector3.Cross(Vector3.UnitY, tangent);
                side = side.LengthSquared() <= float.Epsilon
                    ? Vector3.UnitX : Vector3.Normalize(side);

                var up = Vector3.Normalize(Vector3.Cross(tangent, side));
                var roll = rollBegin + (rollEnd - rollBegin) * station / (centerline.Length - 1);
                if (roll != 0.0f) {
                    var rotation = Quaternion.CreateFromAxisAngle(tangent, roll);
                    side = Vector3.Transform(side, rotation);
                    up = Vector3.Transform(up, rotation);
                }
                stationTangents[station] = tangent;
                stationSides[station] = side;
                stationUps[station] = up;
            }

            for (var station = 0; station < centerline.Length; station++) {
                var normalSums = new Vector3[shape.Vertices.Length];
                var tangent = stationTangents[station];
                var side = stationSides[station];
                var up = stationUps[station];

                for (var edge = 0; edge < edgeCount; edge++) {
                    var next = (edge + 1) % shape.Vertices.Length;
                    var a = shape.Vertices[edge];
                    var b = shape.Vertices[next];
                    var profileEdge = side * (b.X - a.X) + up * (b.Y - a.Y);
                    var faceNormal = Vector3.Cross(tangent, profileEdge);
                    if (faceNormal.LengthSquared() <= float.Epsilon) continue;

                    faceNormal = Vector3.Normalize(faceNormal);
                    normalSums[edge] += faceNormal;
                    normalSums[next] += faceNormal;
                }

                for (var vertex = 0; vertex < shape.Vertices.Length; vertex++) {
                    var point = shape.Vertices[vertex];
                    var normal = normalSums[vertex];
                    if (normal.LengthSquared() <= float.Epsilon)
                        normal = side;

                    stationVertices[station, vertex] = geometry.AddIndexedVertex(
                        centerline[station] + side * point.X + up * point.Y,
                        normal,
                        color,
                        shape.Specular);
                }
            }

            for (var station = 0; station < centerline.Length - 1; station++) {
                for (var edge = 0; edge < edgeCount; edge++) {
                    var next = (edge + 1) % shape.Vertices.Length;

                    geometry.AddIndexedTriangle(
                        stationVertices[station, edge],
                        stationVertices[station + 1, edge],
                        stationVertices[station + 1, next]);

                    geometry.AddIndexedTriangle(
                        stationVertices[station, edge],
                        stationVertices[station + 1, next],
                        stationVertices[station, next]);
                }
            }
        }
    }

    private static TrackRgba8 GetPaletteColor(byte paletteIndex, ReadOnlySpan<RCTPaletteEntry> palette)
    {
        if (paletteIndex >= palette.Length)
            throw new InvalidDataException($"Track palette index {paletteIndex} is outside the {palette.Length}-entry palette.");

        var entry = palette[paletteIndex];
        return new TrackRgba8(entry.Red, entry.Green, entry.Blue, entry.Alpha);
    }
}
