using RCT3D.Interop.Models;
using System.Collections.Concurrent;
using System.Numerics;

namespace RCT3D.Render.Rides.Tracks;

internal readonly record struct TrackBraceVertex(Vector3 Position, Vector3 Normal);

internal sealed record TrackBracePart(TrackBraceVertex[] Vertices, uint[] Indices)
{
    private static readonly TrackRailSpecularData SpecularDefault = new(255, 0);

    public TrackRailSwatch Swatch { get; set; } = TrackRailSwatch.None;
    public TrackRgba8 Color { get; set; } = TrackRgba8.White;
    public TrackRailSpecularData Specular { get; set; } = SpecularDefault;
}

internal sealed class TrackBraceGenerator(Func<RCTTrackElementData, TrackPart> getShape) : ITrackPartGenerator
{
    private readonly ConcurrentDictionary<(RCTTrackElementType TrackType, byte HasChainLift), TrackPart> partsByTrackType = new();

    public void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, ReadOnlySpan<Vector3> centerline, ReadOnlySpan<RCTPaletteEntry> palette, float rollBegin, float rollEnd, Vector3? startTangent, Vector3? endTangent, Vector3? preferredSide, bool smoothRollTransition)
    {
        if (centerline.Length < 2) return;

        var trackData = track;

        var shape = partsByTrackType.GetOrAdd(
            (track.Part, track.HasChainLift),
            static (_, state) => state.GetShape(state.Track),
            (Track: trackData, GetShape: getShape));

        if (shape.BraceParts.Count == 0 || !shape.BraceInterval.HasValue) return;

        var frames = geometry.GetFrames(
            centerline, rollBegin, rollEnd, startTangent, endTangent, preferredSide, smoothRollTransition);

        if (frames.Length != centerline.Length) return;

        var cumulativeDistances = new float[centerline.Length];
        for (var i = 1; i < centerline.Length; i++)
            cumulativeDistances[i] = cumulativeDistances[i - 1] + Vector3.Distance(centerline[i - 1], centerline[i]);

        var totalDistance = cumulativeDistances[^1];
        var segment = 0;

        for (var braceDistance = shape.BraceInterval.Value * 0.5f; braceDistance < totalDistance; braceDistance += shape.BraceInterval.Value) {
            while (segment < centerline.Length - 2 && braceDistance > cumulativeDistances[segment + 1])
                segment++;

            var segmentLength = cumulativeDistances[segment + 1] - cumulativeDistances[segment];
            if (segmentLength <= float.Epsilon) continue;

            var amount = (braceDistance - cumulativeDistances[segment]) / segmentLength;
            var position = Vector3.Lerp(centerline[segment], centerline[segment + 1], amount);
            var frame = InterpolateFrame(frames[segment], frames[segment + 1], amount);

            AddBraceParts(geometry, shape.BraceParts.ToArray(), position, frame, track.MainPaletteIndex, track.AdditionalPaletteIndex, track.SupportPaletteIndex, palette);
        }
    }

    private static void AddBraceParts(
        TrackMeshGeometry geometry,
        TrackBracePart[] parts,
        Vector3 position,
        TrackCenterlineFrame frame,
        byte mainPaletteIndex,
        byte alternatePaletteIndex,
        byte supportPaletteIndex,
        ReadOnlySpan<RCTPaletteEntry> palette)
    {
        foreach (var part in parts) {
            if (part.Indices.Length % 3 != 0)
                throw new InvalidDataException("Brace mesh indices must contain complete triangles.");

            var color = part.Swatch switch {
                TrackRailSwatch.Main => TrackMeshPartGenerator.GetPaletteColor(mainPaletteIndex, palette),
                TrackRailSwatch.Alternate => TrackMeshPartGenerator.GetPaletteColor(alternatePaletteIndex, palette),
                TrackRailSwatch.Support => TrackMeshPartGenerator.GetPaletteColor(supportPaletteIndex, palette),
                TrackRailSwatch.None => part.Color,
                _ => throw new InvalidDataException($"Unknown brace swatch {part.Swatch}.")
            };

            var vertexIndices = new uint[part.Vertices.Length];
            for (var i = 0; i < part.Vertices.Length; i++) {
                var vertex = part.Vertices[i];
                if (vertex.Normal.LengthSquared() <= float.Epsilon)
                    throw new InvalidDataException("Brace mesh vertex normals must be non-zero.");

                var worldPosition = position
                    + frame.Side * vertex.Position.X
                    + frame.Up * vertex.Position.Y
                    + frame.Tangent * vertex.Position.Z;

                var worldNormal = Vector3.Normalize(
                    frame.Side * vertex.Normal.X
                    + frame.Up * vertex.Normal.Y
                    + frame.Tangent * vertex.Normal.Z);

                vertexIndices[i] = geometry.AddIndexedVertex(worldPosition, worldNormal, color, part.Specular);
            }

            for (var i = 0; i < part.Indices.Length; i += 3) {
                var a = part.Indices[i];
                var b = part.Indices[i + 1];
                var c = part.Indices[i + 2];

                if (a >= vertexIndices.Length || b >= vertexIndices.Length || c >= vertexIndices.Length)
                    throw new InvalidDataException("Brace mesh index is outside the vertex array.");

                geometry.AddIndexedTriangle(vertexIndices[a], vertexIndices[b], vertexIndices[c]);
            }
        }
    }

    private static TrackCenterlineFrame InterpolateFrame(TrackCenterlineFrame a, TrackCenterlineFrame b, float amount)
    {
        var rotationA = Quaternion.CreateFromRotationMatrix(a.ToRotation());
        var rotationB = Quaternion.CreateFromRotationMatrix(b.ToRotation());
        var rotation = Quaternion.Slerp(rotationA, rotationB, amount);

        return new TrackCenterlineFrame(
            Vector3.Transform(Vector3.UnitZ, rotation),
            Vector3.Transform(Vector3.UnitX, rotation),
            Vector3.Transform(Vector3.UnitY, rotation));
    }
}
