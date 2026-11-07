using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render;

internal static class VehiclePathDebugRenderer
{
    private static readonly TrackRgba8 PathColor = new(0, 255, 255, 255);
    private static readonly TrackRgba8 UpColor = new(255, 128, 0, 255);
    private const float UpLength = 0.6f;
    private const float MarkerSpacing = 0.75f;

    public static TrackMeshData Build(ReadOnlySpan<RCTVehiclePathSample> samples)
    {
        var builders = new Dictionary<RenderRegionCoordinate, List<(Vector3 A, Vector3 B, TrackRgba8 Color)>>();
        uint pathId = 0;
        Vector3 previous = default;
        RenderRegionCoordinate region = default;
        var distance = MarkerSpacing;
        foreach (var sample in samples) {
            var position = new Vector3(sample.X, sample.Z, sample.Y) * (WorldUnits.TileSize / 32.0f);
            if (sample.PathId != pathId) {
                pathId = sample.PathId;
                previous = position;
                region = new RenderRegionCoordinate(
                    (int)MathF.Floor(position.X / WorldUnits.TileSize / RenderRegionLayout.RegionSizeInTiles),
                    (int)MathF.Floor(position.Z / WorldUnits.TileSize / RenderRegionLayout.RegionSizeInTiles));
                distance = MarkerSpacing;
            }
            var delta = Vector3.Distance(previous, position);
            if (delta > 0.0001f) {
                Add(previous, position, PathColor);
                distance += delta;
            }
            if (distance >= MarkerSpacing) {
                var rotation = VehiclePoseResolver.GetOrientation(sample.Orientation, sample.PitchRadians, sample.RollRadians);
                var up = Vector3.Transform(Vector3.UnitY, rotation);
                Add(position, position + up * UpLength, UpColor);
                distance = 0;
            }
            previous = position;
        }

        var vertices = new List<TrackVertex>();
        var indices = new List<uint>();
        var ranges = new List<RenderRegionIndexRange>();
        foreach (var pair in builders.OrderBy(pair => pair.Key.X).ThenBy(pair => pair.Key.Y)) {
            var firstIndex = indices.Count;
            var bounds = new AxisAlignedBounds(pair.Value[0].A, pair.Value[0].A);
            foreach (var (a, b, color) in pair.Value) {
                indices.Add((uint)vertices.Count);
                vertices.Add(new TrackVertex(a, Vector3.UnitY, color, 255, 0));
                indices.Add((uint)vertices.Count);
                vertices.Add(new TrackVertex(b, Vector3.UnitY, color, 255, 0));
                bounds = bounds.Encapsulate(a).Encapsulate(b);
            }
            ranges.Add(new RenderRegionIndexRange(pair.Key, bounds, (uint)firstIndex, (uint)(indices.Count - firstIndex)));
        }
        return new TrackMeshData(vertices.ToArray(), indices.ToArray(), ranges.ToArray());

        void Add(Vector3 a, Vector3 b, TrackRgba8 color)
        {
            if (!builders.TryGetValue(region, out var lines)) {
                lines = [];
                builders.Add(region, lines);
            }
            lines.Add((a, b, color));
        }
    }
}
