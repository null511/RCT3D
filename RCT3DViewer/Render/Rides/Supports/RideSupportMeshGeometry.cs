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

    public RideSupportMeshData Build() => new(vertices.ToArray(), indices.ToArray(), regions.ToArray());

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
