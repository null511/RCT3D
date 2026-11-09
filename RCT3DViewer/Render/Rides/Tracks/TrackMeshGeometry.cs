using System.Numerics;
using RCT3D.Interop.Models;

namespace RCT3D.Render.Rides.Tracks;

internal sealed class TrackMeshGeometry
{
    private readonly List<TrackVertex> _vertices = new();
    private readonly List<uint> _indices = new();
    private readonly List<RenderRegionIndexRange> _regions = new();
    private RenderRegionCoordinate? _region;
    private List<TrackCenterlinePath>? _centerlineCapture;
    private float _verticalOffset;
    private Vector3 _profileOffset;
    private float _trackBankAngleDegrees = 22.5f;
    private int _firstVertex;
    private int _firstIndex;

    public void SetVerticalOffset(float offset) => _verticalOffset = offset;

    public void SetProfileOffset(Vector3 offset) => _profileOffset = offset;

    public void SetTrackBankAngleDegrees(float angle) => _trackBankAngleDegrees = angle;

    public float TrackBankAngleDegrees => _trackBankAngleDegrees;

    public void BeginCenterlineCapture()
    {
        if (_centerlineCapture is not null)
            throw new InvalidOperationException("Track centerline capture is already active.");

        _centerlineCapture = new List<TrackCenterlinePath>();
    }

    public TrackCenterlinePath[] EndCenterlineCapture()
    {
        if (_centerlineCapture is null)
            throw new InvalidOperationException("Track centerline capture is not active.");

        var centerlines = _centerlineCapture.ToArray();
        _centerlineCapture = null;
        return centerlines;
    }

    public void CaptureCenterline(
        in RCTTrackElementData track,
        ReadOnlySpan<Vector3> centerline,
        float rollBegin,
        float rollEnd,
        Vector3? startTangent,
        Vector3? endTangent,
        Vector3? preferredSide,
        bool smoothRollTransition)
    {
        if (_centerlineCapture is null) return;

        var offsetCenterline = centerline.ToArray();
        for (var i = 0; i < offsetCenterline.Length; i++) {
            offsetCenterline[i].Y += _verticalOffset;
            offsetCenterline[i] += _profileOffset;
        }

        var frames = TrackMeshPartGenerator.CreateFrames(
            centerline, rollBegin, rollEnd, startTangent, endTangent, preferredSide, smoothRollTransition);
        _centerlineCapture.Add(new TrackCenterlinePath(track.Sequence, offsetCenterline, frames));
    }

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

    public uint AddIndexedVertex(Vector3 position, Vector3 normal, TrackRgba8 color, TrackRailSpecularData specular)
    {
        var index = (uint)_vertices.Count;
        position.Y += _verticalOffset;
        position += _profileOffset;
        _vertices.Add(new TrackVertex(position, Vector3.Normalize(normal), color, specular.Roughness, specular.Metalness));
        return index;
    }

    public void AddIndexedTriangle(uint a, uint b, uint c)
    {
        var positionA = _vertices[checked((int)a)].Position;
        var positionB = _vertices[checked((int)b)].Position;
        var positionC = _vertices[checked((int)c)].Position;
        var cross = Vector3.Cross(positionB - positionA, positionC - positionA);
        if (cross.LengthSquared() <= float.Epsilon) return;

        _indices.Add(a);
        _indices.Add(b);
        _indices.Add(c);
    }
}
