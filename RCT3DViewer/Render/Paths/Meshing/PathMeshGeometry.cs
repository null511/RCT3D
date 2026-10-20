using RCT3DViewer.Render.Terrain;
using System.Numerics;

namespace RCT3DViewer.Render.Paths.Meshing;

internal sealed class PathMeshGeometry
{
    private readonly List<TerrainVertex> _vertices = new();
    private readonly List<uint> _indices = new();
    private readonly List<RenderRegionIndexRange> _regions = new();
    private RenderRegionCoordinate _region;
    private int _firstVertex;
    private int _firstIndex;

    public void BeginRegion(RenderRegionCoordinate region)
    {
        _region = region;
        _firstVertex = _vertices.Count;
        _firstIndex = _indices.Count;
    }

    public void EndRegion()
    {
        if (_indices.Count > _firstIndex)
            _regions.Add(RenderRegionMeshRanges.Create(_region, _vertices, _indices, _firstVertex, _firstIndex));
    }

    public PathMeshData Build() => new(_vertices.ToArray(), _indices.ToArray(), _regions.ToArray());

    public void AddTriangle(
        Vector3 a, Vector2 uvA, Vector3 b, Vector2 uvB, Vector3 c, Vector2 uvC, uint material, Vector3? expectedNormal = null)
    {
        var normal = expectedNormal ?? Vector3.Normalize(Vector3.Cross(b - a, c - a));
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0.0f)
            (b, c, uvB, uvC) = (c, b, uvC, uvB);

        var firstIndex = (uint)_vertices.Count;
        _vertices.Add(new TerrainVertex(a, normal, uvA, material));
        _vertices.Add(new TerrainVertex(b, normal, uvB, material));
        _vertices.Add(new TerrainVertex(c, normal, uvC, material));
        _indices.Add(firstIndex);
        _indices.Add(firstIndex + 1);
        _indices.Add(firstIndex + 2);
    }

    public void AddPrismFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 prismCenter, uint material)
    {
        var faceCenter = (a + b + c + d) * 0.25f;
        var normal = Vector3.Normalize(faceCenter - prismCenter);
        AddTriangle(a, Vector2.Zero, b, Vector2.UnitX, c, Vector2.One, material, normal);
        AddTriangle(a, Vector2.Zero, c, Vector2.One, d, Vector2.UnitY, material, normal);
    }
}
