using RCT3D.Render.Terrain;
using System.Numerics;

namespace RCT3D.Render.Rides.Tracks;

internal sealed class WaterPlaneMeshGeometry
{
    private readonly List<Vector3> vertices = new();
    private readonly List<uint> indices = new();
    private readonly List<RenderRegionIndexRange> regions = new();
    private RenderRegionCoordinate? region;
    private int firstVertex;
    private int firstIndex;

    public void BeginRegion(RenderRegionCoordinate region)
    {
        if (this.region.HasValue)
            throw new InvalidOperationException("A water mesh region is already open.");

        this.region = region;
        firstVertex = vertices.Count;
        firstIndex = indices.Count;
    }

    public void AddSweptPolyline(Vector2[] profile, TrackCenterlinePath centerline)
    {
        if (profile.Length < 2 || centerline.Points.Length < 2 || centerline.Frames.Length != centerline.Points.Length)
            return;

        var firstProfileVertex = vertices.Count;
        for (var station = 0; station < centerline.Points.Length; station++) {
            var frame = centerline.Frames[station];
            foreach (var point in profile)
                vertices.Add(centerline.Points[station] + frame.Side * point.X + frame.Up * point.Y);
        }

        for (var station = 0; station < centerline.Points.Length - 1; station++) {
            for (var edge = 0; edge < profile.Length - 1; edge++) {
                var current = checked((uint)(firstProfileVertex + station * profile.Length + edge));
                var nextStation = checked(current + (uint)profile.Length);

                indices.Add(current);
                indices.Add(nextStation);
                indices.Add(nextStation + 1);

                indices.Add(current);
                indices.Add(nextStation + 1);
                indices.Add(current + 1);
            }
        }
    }

    public void EndRegion()
    {
        if (!region.HasValue)
            throw new InvalidOperationException("No water mesh region is open.");

        if (indices.Count > firstIndex)
            regions.Add(RenderRegionMeshRanges.Create(region.Value, vertices, indices, firstVertex, firstIndex));

        region = null;
    }

    public WaterMeshData Build() => new(vertices.ToArray(), indices.ToArray(), regions.ToArray());
}
