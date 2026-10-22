using System.Numerics;

namespace RCT3D.Render.Tracks.Meshing;

internal sealed class TrackMeshGeometry
{
    private readonly List<TrackVertex> _vertices = new();
    private readonly List<uint> _indices = new();
    private readonly List<RenderRegionIndexRange> _regions = new();
    private RenderRegionCoordinate? _region;
    private int _firstVertex;
    private int _firstIndex;

    public void BeginRegion(RenderRegionCoordinate region)
    {
        if (_region.HasValue)
            throw new InvalidOperationException("A track mesh region is already open.");

        _region = region;
        _firstVertex = _vertices.Count;
        _firstIndex = _indices.Count;
    }

    public void EndRegion()
    {
        if (!_region.HasValue)
            throw new InvalidOperationException("No track mesh region is open.");

        if (_indices.Count > _firstIndex) {
            var range = RenderRegionMeshRanges.Create(_region.Value, _vertices, _indices, _firstVertex, _firstIndex);
            _regions.Add(range);
        }

        _region = null;
    }

    public TrackMeshData Build() => new(_vertices.ToArray(), _indices.ToArray(), _regions.ToArray());

    public uint AddIndexedVertex(Vector3 position, Vector3 normal, byte paletteIndex, TrackRailSpecularData specular, bool untinted = false)
    {
        var index = (uint)_vertices.Count;
        _vertices.Add(new TrackVertex(position, Vector3.Normalize(normal), paletteIndex, specular.Roughness, specular.Metalness, untinted));
        return index;
    }

    public void AddIndexedTriangle(uint a, uint b, uint c)
    {
        var positionA = _vertices[checked((int)a)].Position;
        var positionB = _vertices[checked((int)b)].Position;
        var positionC = _vertices[checked((int)c)].Position;
        var cross = Vector3.Cross(positionB - positionA, positionC - positionA);
        if (cross.LengthSquared() <= float.Epsilon) return;

        var expectedNormal = _vertices[checked((int)a)].Normal
            + _vertices[checked((int)b)].Normal
            + _vertices[checked((int)c)].Normal;

        if (Vector3.Dot(cross, expectedNormal) < 0.0f)
            (b, c) = (c, b);

        _indices.Add(a);
        _indices.Add(b);
        _indices.Add(c);
    }
}
