using System.Numerics;
using RCT3D.Interop;

namespace RCT3D.Render.Tracks.Meshing;

internal interface ITrackPieceMeshBuilder
{
    RCTTrackElementType PieceType { get; }
    void Add(in RCTTrackElementData track, TrackMeshGeometry geometry);
    void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry);
}

internal abstract class TrackPieceMeshBuilder(RCTTrackElementType pieceType) : ITrackPieceMeshBuilder
{
    public RCTTrackElementType PieceType { get; } = pieceType;

    public abstract void Add(in RCTTrackElementData track, TrackMeshGeometry geometry);

    public virtual void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry)
    {
        foreach (ref readonly var track in tracks)
            Add(in track, geometry);
    }
}

internal readonly record struct TrackPartProfile(float Width, float Thickness);

internal enum TrackRailSwatch : byte
{
    Main,
    Alternate,
    None
}

internal readonly record struct TrackRailSpecularData(byte Roughness, byte Metalness);

internal sealed record TrackRailProfileShape(
    Vector2[] Vertices,
    bool Closed,
    TrackRailSwatch Swatch,
    TrackRailSpecularData Specular);

internal sealed record TrackRailProfile(TrackRailProfileShape[] Shapes);

internal static class TrackMeshPartGenerator
{
    public static void AddSweptProfile(
        TrackMeshGeometry geometry,
        ReadOnlySpan<Vector3> centerline,
        TrackRailProfile profile,
        byte mainPaletteIndex,
        byte alternatePaletteIndex)
    {
        if (centerline.Length < 2)
            return;

        foreach (var shape in profile.Shapes) {
            if (shape.Vertices.Length < 2)
                continue;

            var paletteIndex = shape.Swatch switch {
                TrackRailSwatch.Main => mainPaletteIndex,
                TrackRailSwatch.Alternate => alternatePaletteIndex,
                TrackRailSwatch.None => (byte)0,
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
                if (tangent.LengthSquared() <= float.Epsilon)
                    return;

                tangent = Vector3.Normalize(tangent);
                var side = Vector3.Cross(Vector3.UnitY, tangent);
                if (side.LengthSquared() <= float.Epsilon)
                    side = Vector3.UnitX;
                else
                    side = Vector3.Normalize(side);
                var up = Vector3.Normalize(Vector3.Cross(tangent, side));
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
                    if (faceNormal.LengthSquared() <= float.Epsilon)
                        continue;

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
                        paletteIndex,
                        shape.Specular,
                        shape.Swatch == TrackRailSwatch.None);
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

    public static void AddSweptPrism(
        TrackMeshGeometry geometry,
        ReadOnlySpan<Vector3> centerline,
        TrackPartProfile profile,
        byte paletteIndex)
    {
        if (centerline.Length < 2 || profile.Width <= 0.0f || profile.Thickness <= 0.0f)
            return;

        for (var i = 0; i < centerline.Length - 1; i++)
            AddPrismSegment(geometry, centerline[i], centerline[i + 1], profile, paletteIndex);
    }

    private static void AddPrismSegment(
        TrackMeshGeometry geometry,
        Vector3 start,
        Vector3 end,
        TrackPartProfile profile,
        byte paletteIndex)
    {
        var forward = end - start;
        if (forward.LengthSquared() <= float.Epsilon)
            return;

        forward = Vector3.Normalize(forward);
        var side = Vector3.Cross(forward, Vector3.UnitY);
        if (side.LengthSquared() <= float.Epsilon)
            side = Vector3.UnitX;
        else
            side = Vector3.Normalize(side);
        var up = Vector3.Normalize(Vector3.Cross(side, forward));
        var halfWidth = side * (profile.Width * 0.5f);
        var halfThickness = up * (profile.Thickness * 0.5f);

        var a = start - halfWidth - halfThickness;
        var b = start + halfWidth - halfThickness;
        var c = start + halfWidth + halfThickness;
        var d = start - halfWidth + halfThickness;
        var e = end - halfWidth - halfThickness;
        var f = end + halfWidth - halfThickness;
        var g = end + halfWidth + halfThickness;
        var h = end - halfWidth + halfThickness;

        geometry.AddQuad(a, b, f, e, paletteIndex, -up);
        geometry.AddQuad(d, h, g, c, paletteIndex, up);
        geometry.AddQuad(a, e, h, d, paletteIndex, -side);
        geometry.AddQuad(b, c, g, f, paletteIndex, side);
        geometry.AddQuad(a, d, c, b, paletteIndex, -forward);
        geometry.AddQuad(e, f, g, h, paletteIndex, forward);
    }
}
