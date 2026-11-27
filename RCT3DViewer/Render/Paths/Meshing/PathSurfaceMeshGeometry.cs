using System.Numerics;

namespace RCT3D.Render.Paths.Meshing;

internal sealed class PathSurfaceMeshGeometry
{
    private readonly List<PathSurfaceVertex> vertices = new();
    private readonly List<uint> indices = new();
    private readonly List<RenderRegionIndexRange> regions = new();
    private RenderRegionCoordinate region;
    private int firstVertex;
    private int firstIndex;

    public void BeginRegion(RenderRegionCoordinate value)
    {
        region = value;
        firstVertex = vertices.Count;
        firstIndex = indices.Count;
    }

    public void EndRegion()
    {
        if (indices.Count == firstIndex) return;

        var bounds = new AxisAlignedBounds(vertices[firstVertex].Position, vertices[firstVertex].Position);
        for (var index = firstVertex + 1; index < vertices.Count; index++)
            bounds = bounds.Encapsulate(vertices[index].Position);
        regions.Add(new RenderRegionIndexRange(region, bounds, checked((uint)firstIndex), checked((uint)(indices.Count - firstIndex))));
    }

    public void AddTriangle(Vector3 a, Vector2 uvA, Vector3 b, Vector2 uvB, Vector3 c, Vector2 uvC, PathSurfaceMaterial material)
    {
        var normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
        var start = checked((uint)vertices.Count);
        vertices.Add(new PathSurfaceVertex(a, normal, uvA, material.DescriptorIndex, material.Layer));
        vertices.Add(new PathSurfaceVertex(b, normal, uvB, material.DescriptorIndex, material.Layer));
        vertices.Add(new PathSurfaceVertex(c, normal, uvC, material.DescriptorIndex, material.Layer));
        indices.Add(start);
        indices.Add(start + 2);
        indices.Add(start + 1);
    }

    public PathSurfaceMeshData Build() => new(vertices.ToArray(), indices.ToArray(), regions.ToArray());
}

internal sealed record PathSurfaceMeshData(PathSurfaceVertex[] Vertices, uint[] Indices, RenderRegionIndexRange[] Regions);
