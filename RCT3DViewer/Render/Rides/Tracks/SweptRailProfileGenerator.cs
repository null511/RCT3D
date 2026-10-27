using RCT3D.Interop;
using System.Numerics;

namespace RCT3D.Render.Rides.Tracks;

internal sealed class SweptRailProfileGenerator(TrackRailProfile profile) : ITrackPartGenerator
{
    public void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, ReadOnlySpan<Vector3> centerline, ReadOnlySpan<RCTPaletteEntry> palette, float rollBegin, float rollEnd, Vector3? startTangent, Vector3? endTangent, Vector3? preferredSide, bool smoothRollTransition) =>
        TrackMeshPartGenerator.AddSweptProfile(
            geometry,
            centerline,
            profile,
            track.MainPaletteIndex,
            track.AdditionalPaletteIndex,
            palette,
            rollBegin,
            rollEnd,
            startTangent,
            endTangent,
            preferredSide,
            smoothRollTransition);
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
        float rollEnd,
        Vector3? startTangent = null,
        Vector3? endTangent = null,
        Vector3? preferredSide = null,
        bool smoothRollTransition = false)
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
            var frames = CreateFrames(centerline, rollBegin, rollEnd, startTangent, endTangent, preferredSide, smoothRollTransition);
            if (frames.Length != centerline.Length) return;

            for (var station = 0; station < centerline.Length; station++) {
                var normalSums = new Vector3[shape.Vertices.Length];
                var frame = frames[station];
                var tangent = frame.Tangent;
                var side = frame.Side;
                var up = frame.Up;

                for (var edge = 0; edge < edgeCount; edge++) {
                    var next = (edge + 1) % shape.Vertices.Length;
                    var a = shape.Vertices[edge];
                    var b = shape.Vertices[next];
                    var profileEdge = side * (b.X - a.X) + up * (b.Y - a.Y);
                    var faceNormal = Vector3.Cross(profileEdge, tangent);
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

    public static TrackCenterlineFrame[] CreateFrames(
        ReadOnlySpan<Vector3> centerline,
        float rollBegin,
        float rollEnd,
        Vector3? startTangent,
        Vector3? endTangent,
        Vector3? preferredSide,
        bool smoothRollTransition)
    {
        if (centerline.Length < 2) return [];

        var frames = new TrackCenterlineFrame[centerline.Length];
        for (var station = 0; station < centerline.Length; station++) {
            var previous = centerline[Math.Max(0, station - 1)];
            var next = centerline[Math.Min(centerline.Length - 1, station + 1)];
            var tangent = station == 0 && startTangent.HasValue
                ? startTangent.Value
                : station == centerline.Length - 1 && endTangent.HasValue
                    ? endTangent.Value
                    : next - previous;

            if (tangent.LengthSquared() <= float.Epsilon)
                tangent = station + 1 < centerline.Length ? next - centerline[station] : centerline[station] - previous;
            if (tangent.LengthSquared() <= float.Epsilon) return [];

            tangent = Vector3.Normalize(tangent);
            var side = preferredSide.HasValue
                ? preferredSide.Value - tangent * Vector3.Dot(preferredSide.Value, tangent)
                : Vector3.Cross(Vector3.UnitY, tangent);
            if (side.LengthSquared() <= float.Epsilon)
                side = Vector3.UnitX - tangent * Vector3.Dot(Vector3.UnitX, tangent);
            if (side.LengthSquared() <= float.Epsilon)
                side = Vector3.UnitZ - tangent * Vector3.Dot(Vector3.UnitZ, tangent);
            side = Vector3.Normalize(side);

            var up = Vector3.Normalize(Vector3.Cross(tangent, side));
            var rollProgress = station / (float)(centerline.Length - 1);
            if (smoothRollTransition)
                rollProgress = rollProgress * rollProgress * (3.0f - 2.0f * rollProgress);
            var roll = rollBegin + (rollEnd - rollBegin) * rollProgress;
            if (roll != 0.0f) {
                var rotation = Quaternion.CreateFromAxisAngle(tangent, roll);
                side = Vector3.Transform(side, rotation);
                up = Vector3.Transform(up, rotation);
            }

            frames[station] = new TrackCenterlineFrame(tangent, side, up);
        }

        return frames;
    }

    private static TrackRgba8 GetPaletteColor(byte paletteIndex, ReadOnlySpan<RCTPaletteEntry> palette)
    {
        if (paletteIndex >= palette.Length)
            throw new InvalidDataException($"Track palette index {paletteIndex} is outside the {palette.Length}-entry palette.");

        var entry = palette[paletteIndex];
        return new TrackRgba8(entry.Red, entry.Green, entry.Blue, entry.Alpha);
    }
}
