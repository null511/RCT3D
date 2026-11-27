using System.Numerics;

namespace RCT3D.Render.Paths.Meshing;

internal static class RoundPoleMeshBuilder
{
    public static void Build(Vector3 start, Vector3 end, float radius, int radialSegments, uint material, IPathGeometryWriter geometry)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(radialSegments, 3);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var axis = end - start;
        var length = axis.Length();
        if (length <= 0.0001f) return;

        axis /= length;
        var reference = MathF.Abs(axis.Y) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;
        var side = Vector3.Normalize(Vector3.Cross(reference, axis));
        var up = Vector3.Normalize(Vector3.Cross(axis, side));
        var circumference = MathF.Tau * radius;

        var startCenter = geometry.AddIndexedVertex(start, -axis, new Vector2(0.5f), material);
        var endCenter = geometry.AddIndexedVertex(end, axis, new Vector2(0.5f), material);
        uint previousStart = 0, previousEnd = 0, previousStartCap = 0, previousEndCap = 0;

        for (var index = 0; index <= radialSegments; index++) {
            var ratio = (float)index / radialSegments;
            var angle = index == radialSegments ? 0.0f : MathF.Tau * ratio;
            var (sin, cos) = MathF.SinCos(angle);
            var normal = side * cos + up * sin;
            var offset = normal * radius;
            var capUv = new Vector2(cos, sin) * 0.5f + new Vector2(0.5f);

            var startIndex = geometry.AddIndexedVertex(start + offset, normal, new Vector2(ratio, 0), material);
            var endIndex = geometry.AddIndexedVertex(end + offset, normal, new Vector2(ratio, length / circumference), material);
            var startCap = geometry.AddIndexedVertex(start + offset, -axis, capUv, material);
            var endCap = geometry.AddIndexedVertex(end + offset, axis, capUv, material);

            if (index > 0) {
                // The camera X mirror requires index winding opposite the outward object-space normal.
                geometry.AddIndexedTriangle(previousStart, previousEnd, endIndex);
                geometry.AddIndexedTriangle(previousStart, endIndex, startIndex);
                geometry.AddIndexedTriangle(startCenter, previousStartCap, startCap);
                geometry.AddIndexedTriangle(endCenter, endCap, previousEndCap);
            }

            previousStart = startIndex;
            previousEnd = endIndex;
            previousStartCap = startCap;
            previousEndCap = endCap;
        }
    }
}
