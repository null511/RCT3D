using RCT3D.Render.Terrain;
using System.Numerics;

namespace RCT3D.Render.Paths.Meshing;

internal sealed class PathMeshGeometry
{
    private readonly List<RenderRegionIndexRange> regions = new();
    private readonly List<RenderRegionIndexRange> supportRegions = new();
    private readonly List<TerrainVertex> vertices = new();
    private readonly List<uint> indices = new();

    private RenderRegionCoordinate _region;
    private int firstVertex;
    private int _firstIndex;


    public void BeginRegion(RenderRegionCoordinate region)
    {
        _region = region;
        firstVertex = vertices.Count;
        _firstIndex = indices.Count;
    }

    public void EndRegion(bool supports = false)
    {
        if (indices.Count > _firstIndex)
            (supports ? supportRegions : regions).Add(RenderRegionMeshRanges.Create(_region, vertices, indices, firstVertex, _firstIndex));
    }

    public PathMeshData Build() => new(vertices.ToArray(), indices.ToArray(), regions.ToArray()) {
        SupportRegions = supportRegions.ToArray()
    };

    public uint AddIndexedVertex(Vector3 position, Vector3 normal, Vector2 uv, uint material)
    {
        var index = (uint)vertices.Count;
        vertices.Add(new TerrainVertex(position, normal, uv, material));
        return index;
    }

    public void AddIndexedTriangle(uint a, uint b, uint c)
    {
        indices.Add(a);
        indices.Add(b);
        indices.Add(c);
    }

    public void AddTriangle(Vector3 a, Vector2 uvA, Vector3 b, Vector2 uvB, Vector3 c, Vector2 uvC, uint material, Vector3? expectedNormal = null)
    {
        var normal = expectedNormal ?? Vector3.Normalize(Vector3.Cross(b - a, c - a));
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0.0f)
            (b, c, uvB, uvC) = (c, b, uvC, uvB);

        var firstIndex = (uint)vertices.Count;

        vertices.Add(new TerrainVertex(a, normal, uvA, material));
        vertices.Add(new TerrainVertex(b, normal, uvB, material));
        vertices.Add(new TerrainVertex(c, normal, uvC, material));

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
