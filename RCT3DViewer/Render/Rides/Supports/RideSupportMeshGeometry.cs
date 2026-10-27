using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Supports;

internal sealed class RideSupportMeshGeometry
{
    private readonly List<RenderRegionIndexRange> regions = new();
    private readonly List<TrackVertex> vertices = new();
    private readonly List<uint> indices = new();
    private RenderRegionCoordinate? region;
    private int firstVertex;
    private int firstIndex;

    public void BeginRegion(RenderRegionCoordinate value)
    {
        if (region.HasValue)
            throw new InvalidOperationException("A ride support mesh region is already open.");

        region = value;
        firstVertex = vertices.Count;
        firstIndex = indices.Count;
    }

    public void EndRegion()
    {
        if (!region.HasValue)
            throw new InvalidOperationException("No ride support mesh region is open.");

        if (indices.Count > firstIndex)
            regions.Add(RenderRegionMeshRanges.Create(region.Value, vertices, indices, firstVertex, firstIndex));

        region = null;
    }

    public void AddBox(Vector3 center, Vector3 halfExtents, TrackRgba8 color, TrackRailSpecularData specular)
    {
        var westBottom = new Vector3(center.X - halfExtents.X, center.Y - halfExtents.Y, center.Z - halfExtents.Z);
        var eastBottom = new Vector3(center.X + halfExtents.X, center.Y - halfExtents.Y, center.Z - halfExtents.Z);
        var northBottom = new Vector3(center.X + halfExtents.X, center.Y - halfExtents.Y, center.Z + halfExtents.Z);
        var southBottom = new Vector3(center.X - halfExtents.X, center.Y - halfExtents.Y, center.Z + halfExtents.Z);
        var westTop = new Vector3(center.X - halfExtents.X, center.Y + halfExtents.Y, center.Z - halfExtents.Z);
        var eastTop = new Vector3(center.X + halfExtents.X, center.Y + halfExtents.Y, center.Z - halfExtents.Z);
        var northTop = new Vector3(center.X + halfExtents.X, center.Y + halfExtents.Y, center.Z + halfExtents.Z);
        var southTop = new Vector3(center.X - halfExtents.X, center.Y + halfExtents.Y, center.Z + halfExtents.Z);

        AddFace(westBottom, eastBottom, eastTop, westTop, -Vector3.UnitZ, color, specular);
        AddFace(southBottom, northBottom, northTop, southTop, Vector3.UnitZ, color, specular);
        AddFace(westBottom, southBottom, southTop, westTop, -Vector3.UnitX, color, specular);
        AddFace(eastBottom, northBottom, northTop, eastTop, Vector3.UnitX, color, specular);
        AddFace(westTop, eastTop, northTop, southTop, Vector3.UnitY, color, specular);
        AddFace(westBottom, southBottom, northBottom, eastBottom, -Vector3.UnitY, color, specular);
    }

    public void AddBeam(
        Vector3 start,
        Vector3 end,
        Vector3 widthDirection,
        float halfWidth,
        float halfDepth,
        TrackRgba8 color,
        TrackRailSpecularData specular)
    {
        var yAxis = Vector3.Normalize(end - start);
        var xAxis = Vector3.Normalize(widthDirection - yAxis * Vector3.Dot(widthDirection, yAxis));
        var zAxis = Vector3.Normalize(Vector3.Cross(xAxis, yAxis));
        var center = (start + end) * 0.5f;
        var halfLength = Vector3.Distance(start, end) * 0.5f;

        var westBottom = center - xAxis * halfWidth - yAxis * halfLength - zAxis * halfDepth;
        var eastBottom = center + xAxis * halfWidth - yAxis * halfLength - zAxis * halfDepth;
        var northBottom = center + xAxis * halfWidth - yAxis * halfLength + zAxis * halfDepth;
        var southBottom = center - xAxis * halfWidth - yAxis * halfLength + zAxis * halfDepth;
        var westTop = center - xAxis * halfWidth + yAxis * halfLength - zAxis * halfDepth;
        var eastTop = center + xAxis * halfWidth + yAxis * halfLength - zAxis * halfDepth;
        var northTop = center + xAxis * halfWidth + yAxis * halfLength + zAxis * halfDepth;
        var southTop = center - xAxis * halfWidth + yAxis * halfLength + zAxis * halfDepth;

        AddFace(westBottom, eastBottom, eastTop, westTop, -zAxis, color, specular);
        AddFace(southBottom, northBottom, northTop, southTop, zAxis, color, specular);
        AddFace(westBottom, southBottom, southTop, westTop, -xAxis, color, specular);
        AddFace(eastBottom, northBottom, northTop, eastTop, xAxis, color, specular);
        AddFace(westTop, eastTop, northTop, southTop, yAxis, color, specular);
        AddFace(westBottom, southBottom, northBottom, eastBottom, -yAxis, color, specular);
    }

    public void AddVerticalTube(
        Vector3 center,
        float bottomHeight,
        float topHeight,
        float radius,
        int sideCount,
        TrackRgba8 color,
        TrackRailSpecularData specular)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sideCount, 3);
        if (topHeight <= bottomHeight)
            throw new ArgumentOutOfRangeException(nameof(topHeight));
        if (radius <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(radius));

        var bottomRing = new Vector3[sideCount];
        var topRing = new Vector3[sideCount];
        var bottomIndices = new uint[sideCount];
        var topIndices = new uint[sideCount];
        for (var i = 0; i < sideCount; i++) {
            var angle = 2.0f * MathF.PI * i / sideCount;
            var normal = new Vector3(MathF.Cos(angle), 0.0f, MathF.Sin(angle));
            var offset = normal * radius;
            bottomRing[i] = new Vector3(center.X, bottomHeight, center.Z) + offset;
            topRing[i] = new Vector3(center.X, topHeight, center.Z) + offset;
            bottomIndices[i] = (uint)vertices.Count;
            vertices.Add(new TrackVertex(bottomRing[i], normal, color, specular.Roughness, specular.Metalness));
            topIndices[i] = (uint)vertices.Count;
            vertices.Add(new TrackVertex(topRing[i], normal, color, specular.Roughness, specular.Metalness));
        }

        for (var i = 0; i < sideCount; i++) {
            var next = (i + 1) % sideCount;
            var middleAngle = 2.0f * MathF.PI * (i + 0.5f) / sideCount;
            var normal = new Vector3(MathF.Cos(middleAngle), 0.0f, MathF.Sin(middleAngle));
            AddTriangle(bottomIndices[i], bottomIndices[next], topIndices[next], normal);
            AddTriangle(bottomIndices[i], topIndices[next], topIndices[i], normal);
        }

        //AddCap(new Vector3(center.X, bottomHeight, center.Z), bottomRing, -Vector3.UnitY, color, specular);
        AddCap(new Vector3(center.X, topHeight, center.Z), topRing, Vector3.UnitY, color, specular);
    }

    public RideSupportMeshData Build() => new(vertices.ToArray(), indices.ToArray(), regions.ToArray());

    private void AddCap(Vector3 center, Vector3[] ring, Vector3 normal, TrackRgba8 color, TrackRailSpecularData specular)
    {
        var centerIndex = (uint)vertices.Count;
        vertices.Add(new TrackVertex(center, normal, color, specular.Roughness, specular.Metalness));

        for (var i = 0; i < ring.Length; i++) {
            var firstIndex = (uint)vertices.Count;
            vertices.Add(new TrackVertex(ring[i], normal, color, specular.Roughness, specular.Metalness));
            vertices.Add(new TrackVertex(ring[(i + 1) % ring.Length], normal, color, specular.Roughness, specular.Metalness));
            AddTriangle(centerIndex, firstIndex, firstIndex + 1, normal);
        }
    }

    private void AddFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, TrackRgba8 color, TrackRailSpecularData specular)
    {
        var firstIndex = (uint)vertices.Count;
        vertices.Add(new TrackVertex(a, normal, color, specular.Roughness, specular.Metalness));
        vertices.Add(new TrackVertex(b, normal, color, specular.Roughness, specular.Metalness));
        vertices.Add(new TrackVertex(c, normal, color, specular.Roughness, specular.Metalness));
        vertices.Add(new TrackVertex(d, normal, color, specular.Roughness, specular.Metalness));
        AddTriangle(firstIndex, firstIndex + 1, firstIndex + 2, normal);
        AddTriangle(firstIndex, firstIndex + 2, firstIndex + 3, normal);
    }

    private void AddTriangle(uint a, uint b, uint c, Vector3 expectedNormal)
    {
        var positionA = vertices[checked((int)a)].Position;
        var positionB = vertices[checked((int)b)].Position;
        var positionC = vertices[checked((int)c)].Position;
        var cross = Vector3.Cross(positionB - positionA, positionC - positionA);
        if (cross.LengthSquared() <= float.Epsilon)
            return;

        if (Vector3.Dot(cross, expectedNormal) > 0.0f)
            (b, c) = (c, b);

        indices.Add(a);
        indices.Add(b);
        indices.Add(c);
    }
}
