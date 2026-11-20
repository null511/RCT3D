using RCT3D.Interop.Models;
using RCT3D.Render.Paths;
using RCT3D.Render.Rides;
using System.Numerics;

namespace RCT3D.Render;

internal sealed class VehicleRenderer
{
    private readonly Dictionary<RideVehicleCarDefinition, ModelBatch> models = new();
    private readonly Dictionary<RenderRegionCoordinate, AxisAlignedBounds> regions = new();

    public IReadOnlyDictionary<RenderRegionCoordinate, AxisAlignedBounds> Regions => regions;
    public bool HasModel(RideVehicleCarDefinition definition) => models.ContainsKey(definition);

    public ModelBatch AddModel(VehicleModel model)
    {
        var batch = new ModelBatch(model);
        models.Add(model.Definition, batch);
        return batch;
    }

    public void PrepareRegions(Dictionary<ushort, VehiclePose> poses, int mapWidth, int mapHeight)
    {
        regions.Clear();
        foreach (var model in models.Values)
            model.ClearInstances();

        foreach (var pose in poses.Values) {
            if (!pose.IsVisible || !models.TryGetValue(pose.Definition, out var model)) continue;

            var tileX = (int)MathF.Floor(pose.Position.X / WorldUnits.TileSize);
            var tileY = (int)MathF.Floor(pose.Position.Z / WorldUnits.TileSize);
            if ((uint)tileX >= (uint)mapWidth || (uint)tileY >= (uint)mapHeight) continue;

            var coordinate = new RenderRegionCoordinate(
                tileX / RenderRegionLayout.RegionSizeInTiles, tileY / RenderRegionLayout.RegionSizeInTiles);
            var transform = pose.Rotation * Matrix4x4.CreateTranslation(pose.Position);
            var min = model.Model.Data.BoundsMin;
            var max = model.Model.Data.BoundsMax;
            var first = Vector3.Transform(min, transform);
            var bounds = new AxisAlignedBounds(first, first);
            for (var corner = 1; corner < 8; corner++) {
                var point = new Vector3(
                    (corner & 1) == 0 ? min.X : max.X,
                    (corner & 2) == 0 ? min.Y : max.Y,
                    (corner & 4) == 0 ? min.Z : max.Z);
                bounds = bounds.Encapsulate(Vector3.Transform(point, transform));
            }

            model.AddPose(coordinate, pose, bounds);
            regions[coordinate] = regions.TryGetValue(coordinate, out var previous) ? previous.Encapsulate(bounds) : bounds;
        }
    }

    public void ClearInstances()
    {
        regions.Clear();
        foreach (var model in models.Values)
            model.ClearInstances();
    }

    public void Clear()
    {
        ClearInstances();
        models.Clear();
    }

    public sealed class ModelBatch(VehicleModel model)
    {
        private readonly Dictionary<RenderRegionCoordinate, RegionBuilder> regions = new();
        private readonly List<RegionBuilder> activeRegions = new();
        private ModelInstance[] instances = [];
        private ModelRegionRange[] regionBatches = [];
        private int instanceCount;
        private int regionBatchCount;

        public VehicleModel Model { get; } = model;
        public ReadOnlySpan<ModelInstance> Instances => instances.AsSpan(0, instanceCount);
        public ReadOnlySpan<ModelRegionRange> RegionBatches => regionBatches.AsSpan(0, regionBatchCount);

        public void AddPose(RenderRegionCoordinate coordinate, VehiclePose pose, AxisAlignedBounds bounds)
        {
            if (!regions.TryGetValue(coordinate, out var region)) {
                region = new RegionBuilder(coordinate);
                regions.Add(coordinate, region);
            }
            if (region.Poses.Count == 0) activeRegions.Add(region);
            region.Bounds = region.Poses.Count == 0 ? bounds : region.Bounds.Encapsulate(bounds);
            region.Poses.Add(pose);
        }

        public void BuildInstances(Dictionary<RenderRegionCoordinate, ParkRenderRegion> renderRegions, ReadOnlySpan<RCTPaletteEntry> palette)
        {
            instanceCount = 0;
            regionBatchCount = 0;
            foreach (var region in activeRegions) {
                if (!renderRegions[region.Coordinate].VehicleInstancesVisible) continue;
                instanceCount = checked(instanceCount + region.Poses.Count);
                regionBatchCount++;
            }

            if (instances.Length < instanceCount)
                Array.Resize(ref instances, Math.Max(instanceCount, instances.Length * 2));
            if (regionBatches.Length < regionBatchCount)
                Array.Resize(ref regionBatches, Math.Max(regionBatchCount, regionBatches.Length * 2));

            var firstInstance = 0;
            var batchIndex = 0;
            foreach (var region in activeRegions) {
                if (!renderRegions[region.Coordinate].VehicleInstancesVisible) continue;
                regionBatches[batchIndex++] = new ModelRegionRange(
                    region.Coordinate, region.Bounds, (uint)firstInstance, (uint)region.Poses.Count);

                foreach (var pose in region.Poses) {
                    var color = palette[pose.PrimaryPaletteIndex];
                    instances[firstInstance++] = new ModelInstance(pose.Rotation * Matrix4x4.CreateTranslation(pose.Position)) {
                        Color = new Vector4(color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, 1.0f)
                    };
                }
            }
        }

        public void ClearInstances()
        {
            foreach (var region in activeRegions)
                region.Poses.Clear();
            activeRegions.Clear();
            instanceCount = 0;
            regionBatchCount = 0;
        }

        private sealed class RegionBuilder(RenderRegionCoordinate coordinate)
        {
            public RenderRegionCoordinate Coordinate { get; } = coordinate;
            public AxisAlignedBounds Bounds { get; set; }
            public List<VehiclePose> Poses { get; } = new();
        }
    }
}