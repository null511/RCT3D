using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides;

internal class BracePartBuilder(List<TrackBracePart> braces)
{
    public TrackBracePart Box(Vector3[] points, Action<TrackBracePart>? partAction = null)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Length != 8)
            throw new ArgumentException("A box requires exactly eight points.", nameof(points));

        var a = points[0];
        var b = points[1];
        var c = points[2];
        var d = points[3];
        var e = points[4];
        var f = points[5];
        var g = points[6];
        var h = points[7];

        var vertices = new TrackBraceVertex[24];
        var indices = new uint[36];
        var nextVertex = 0;

        AddFace(a, b, c, d);
        AddFace(e, h, g, f);
        AddFace(a, e, f, b);
        AddFace(d, c, g, h);
        AddFace(a, d, h, e);
        AddFace(b, f, g, c);

        var part = new TrackBracePart(vertices, indices);
        partAction?.Invoke(part);
        braces.Add(part);
        return part;

        void AddFace(Vector3 first, Vector3 second, Vector3 third, Vector3 fourth) {
            var normal = Vector3.Normalize(Vector3.Cross(second - first, third - first));
            var vertexOffset = nextVertex;
            var indexOffset = vertexOffset / 4 * 6;

            vertices[vertexOffset] = new TrackBraceVertex(first, normal);
            vertices[vertexOffset + 1] = new TrackBraceVertex(second, normal);
            vertices[vertexOffset + 2] = new TrackBraceVertex(third, normal);
            vertices[vertexOffset + 3] = new TrackBraceVertex(fourth, normal);

            indices[indexOffset] = (uint)vertexOffset;
            indices[indexOffset + 1] = (uint)(vertexOffset + 2);
            indices[indexOffset + 2] = (uint)(vertexOffset + 1);
            indices[indexOffset + 3] = (uint)vertexOffset;
            indices[indexOffset + 4] = (uint)(vertexOffset + 3);
            indices[indexOffset + 5] = (uint)(vertexOffset + 2);

            nextVertex += 4;
        }
    }

    public TrackBracePart Box(Vector3 center, Vector3 halfSize, Action<TrackBracePart>? partAction = null)
    {
        var minX = center.X - halfSize.X;
        var maxX = center.X + halfSize.X;
        var minY = center.Y - halfSize.Y;
        var maxY = center.Y + halfSize.Y;
        var minZ = center.Z - halfSize.Z;
        var maxZ = center.Z + halfSize.Z;

        return Box([
            new Vector3(minX, minY, maxZ),
            new Vector3(minX, minY, minZ),
            new Vector3(maxX, minY, minZ),
            new Vector3(maxX, minY, maxZ),
            new Vector3(minX, maxY, maxZ),
            new Vector3(minX, maxY, minZ),
            new Vector3(maxX, maxY, minZ),
            new Vector3(maxX, maxY, maxZ),
        ], partAction);
    }

    public TrackBracePart Cylinder(Vector3 start, Vector3 end, float radius, int segmentCount, Action<TrackBracePart>? partAction = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(segmentCount, 3);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var direction = end - start;
        if (direction.LengthSquared() <= float.Epsilon)
            throw new ArgumentException("Cylinder endpoints must be distinct.", nameof(end));

        var axis = Vector3.Normalize(direction);
        var reference = MathF.Abs(Vector3.Dot(axis, Vector3.UnitY)) < 0.99f ? Vector3.UnitY : Vector3.UnitZ;
        var radialY = Vector3.Normalize(reference - Vector3.Dot(reference, axis) * axis);
        var radialZ = Vector3.Cross(axis, radialY);

        var vertices = new TrackBraceVertex[segmentCount * 2];
        var indices = new uint[segmentCount * 6];

        for (var segment = 0; segment < segmentCount; segment++) {
            var angle = 2.0f * MathF.PI * segment / segmentCount;
            var normal = radialY * MathF.Cos(angle) + radialZ * MathF.Sin(angle);

            vertices[segment] = new TrackBraceVertex(start + radius * normal, normal);
            vertices[segment + segmentCount] = new TrackBraceVertex(end + radius * normal, normal);

            var next = (segment + 1) % segmentCount;
            var index = segment * 6;

            indices[index] = (uint)segment;
            indices[index + 1] = (uint)(segment + segmentCount);
            indices[index + 2] = (uint)(next + segmentCount);
            indices[index + 3] = (uint)segment;
            indices[index + 4] = (uint)(next + segmentCount);
            indices[index + 5] = (uint)next;
        }

        var part = new TrackBracePart(vertices, indices);
        partAction?.Invoke(part);
        braces.Add(part);
        return part;
    }
}
