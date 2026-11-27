using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Paths.Meshing;

internal sealed class PathRailingMeshGeometry : IPathGeometryWriter
{
    private const byte Roughness = 250;
    private const byte Metalness = 0;

    private readonly List<RenderRegionIndexRange> regions = new();
    private readonly List<TrackVertex> vertices = new();
    private readonly List<uint> indices = new();
    private RenderRegionCoordinate region;
    private int firstVertex;
    private int firstIndex;

    public TrackRgba8 Color { get; set; } = TrackRgba8.White;

    public void BeginRegion(RenderRegionCoordinate value)
    {
        region = value;
        firstVertex = vertices.Count;
        firstIndex = indices.Count;
    }

    public void EndRegion()
    {
        if (indices.Count > firstIndex)
            regions.Add(RenderRegionMeshRanges.Create(region, vertices, indices, firstVertex, firstIndex));
    }

    public PathRailingMeshData Build() => new(vertices.ToArray(), indices.ToArray(), regions.ToArray());

    public uint AddIndexedVertex(Vector3 position, Vector3 normal, Vector2 uv, uint material)
    {
        var index = (uint)vertices.Count;
        vertices.Add(new TrackVertex(position, normal, Color, Roughness, Metalness));
        return index;
    }

    public void AddIndexedTriangle(uint a, uint b, uint c)
    {
        indices.Add(a);
        indices.Add(b);
        indices.Add(c);
    }

    public void AddTriangle(
        Vector3 a, Vector2 uvA, Vector3 b, Vector2 uvB, Vector3 c, Vector2 uvC,
        uint material, Vector3? expectedNormal = null)
    {
        var normal = expectedNormal ?? Vector3.Normalize(Vector3.Cross(b - a, c - a));
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0.0f)
            (b, c) = (c, b);

        var firstIndex = (uint)vertices.Count;
        vertices.Add(new TrackVertex(a, normal, Color, Roughness, Metalness));
        vertices.Add(new TrackVertex(b, normal, Color, Roughness, Metalness));
        vertices.Add(new TrackVertex(c, normal, Color, Roughness, Metalness));

        indices.Add(firstIndex);
        indices.Add(firstIndex + 2);
        indices.Add(firstIndex + 1);
    }

    public void AddPrismFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 prismCenter, uint material)
    {
        var faceCenter = (a + b + c + d) * 0.25f;
        var normal = Vector3.Normalize(faceCenter - prismCenter);
        AddTriangle(a, Vector2.Zero, b, Vector2.UnitX, c, Vector2.One, material, normal);
        AddTriangle(a, Vector2.Zero, c, Vector2.One, d, Vector2.UnitY, material, normal);
    }
}
