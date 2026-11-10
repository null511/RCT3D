using RCT3D.Interop.Models;
using RCT3D.Render.Paths;
using RCT3D.Render.Rides;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3D.Render;

internal sealed class VehicleCubeRenderer
{
    private readonly Dictionary<RenderRegionCoordinate, RegionBuilder> regions = new();
    private readonly List<RegionBuilder> activeRegions = new();
    private ModelInstance[] instances = [];
    private ModelRegionRange[] regionBatches = [];
    private int instanceCount;
    private int regionBatchCount;

    public ReadOnlySpan<RegionBuilder> Regions => CollectionsMarshal.AsSpan(activeRegions);
    public ReadOnlySpan<ModelInstance> Instances => instances.AsSpan(0, instanceCount);
    public ReadOnlySpan<ModelRegionRange> RegionBatches => regionBatches.AsSpan(0, regionBatchCount);

    public static ImportedModelMesh CreateMesh() => CreateCubeMesh(Vector3.Zero, 1.0f);

    private static ImportedModelMesh CreateCubeMesh(Vector3 offset, float size)
    {
        var faces = new (Vector3 Normal, Vector3 A, Vector3 B, Vector3 C, Vector3 D)[] {
            (Vector3.UnitY, new(-0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f)),
            (-Vector3.UnitY, new(-0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, -0.5f), new(-0.5f, -0.5f, -0.5f)),
            (Vector3.UnitX, new(0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, 0.5f), new(0.5f, 0.5f, 0.5f), new(0.5f, 0.5f, -0.5f)),
            (-Vector3.UnitX, new(-0.5f, -0.5f, 0.5f), new(-0.5f, -0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f), new(-0.5f, 0.5f, 0.5f)),
            (Vector3.UnitZ, new(-0.5f, -0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f), new(0.5f, 0.5f, 0.5f), new(0.5f, -0.5f, 0.5f)),
            (-Vector3.UnitZ, new(0.5f, -0.5f, -0.5f), new(0.5f, 0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f), new(-0.5f, -0.5f, -0.5f)),
        };

        var vertices = new ImportedModelVertex[faces.Length * 4];
        var indices = new uint[faces.Length * 6];

        for (var faceIndex = 0; faceIndex < faces.Length; faceIndex++) {
            var face = faces[faceIndex];
            var vertexIndex = faceIndex * 4;

            vertices[vertexIndex] = new ImportedModelVertex(offset + face.A * size, face.Normal, Vector2.Zero);
            vertices[vertexIndex + 1] = new ImportedModelVertex(offset + face.B * size, face.Normal, Vector2.UnitX);
            vertices[vertexIndex + 2] = new ImportedModelVertex(offset + face.C * size, face.Normal, Vector2.One);
            vertices[vertexIndex + 3] = new ImportedModelVertex(offset + face.D * size, face.Normal, Vector2.UnitY);

            var index = faceIndex * 6;
            indices[index] = (uint)vertexIndex;
            indices[index + 1] = (uint)(vertexIndex + 1);
            indices[index + 2] = (uint)(vertexIndex + 2);
            indices[index + 3] = (uint)vertexIndex;
            indices[index + 4] = (uint)(vertexIndex + 2);
            indices[index + 5] = (uint)(vertexIndex + 3);
        }

        var material = new ImportedModelMaterial(
            Vector4.One,
            Vector3.Zero,
            new ImportedModelTexture(1, 1, [255, 255, 255, 255]),
            null,
            false);

        return new ImportedModelMesh("ride-vehicle-cube", vertices, indices, material);
    }

    public void PrepareRegions(
        Dictionary<ushort, VehiclePose> poses,
        IReadOnlyDictionary<ushort, Vector3> sizesByRide,
        int mapWidth,
        int mapHeight)
    {
        foreach (var region in activeRegions)
            region.Poses.Clear();
        activeRegions.Clear();
        instanceCount = 0;
        regionBatchCount = 0;

        if (poses.Count == 0 || mapWidth <= 0 || mapHeight <= 0)
            return;

        foreach (var pose in poses.Values) {
            var position = pose.Position;
            var tileX = (int)MathF.Floor(position.X / WorldUnits.TileSize);
            var tileY = (int)MathF.Floor(position.Z / WorldUnits.TileSize);

            if ((uint)tileX >= (uint)mapWidth || (uint)tileY >= (uint)mapHeight)
                continue;

            var size = sizesByRide.GetValueOrDefault(pose.RideId, RideVehicleDefinition.DefaultSize);
            var boundsHalfSize = new Vector3(size.Length() * 0.5f);
            var bounds = new AxisAlignedBounds(position - boundsHalfSize, position + boundsHalfSize);

            var region = new RenderRegionCoordinate(
                tileX / RenderRegionLayout.RegionSizeInTiles,
                tileY / RenderRegionLayout.RegionSizeInTiles);

            if (!regions.TryGetValue(region, out var regionBuilder)) {
                regionBuilder = new RegionBuilder(region);
                regions.Add(region, regionBuilder);
            }

            if (regionBuilder.Poses.Count == 0)
                activeRegions.Add(regionBuilder);
            regionBuilder.Add(pose, bounds);
        }
    }

    public void BuildInstances(
        Dictionary<RenderRegionCoordinate, ParkRenderRegion> renderRegions,
        IReadOnlyDictionary<ushort, Vector3> sizesByRide,
        ReadOnlySpan<RCTPaletteEntry> palette)
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
                var size = sizesByRide.GetValueOrDefault(pose.RideId, RideVehicleDefinition.DefaultSize);
                var transform = Matrix4x4.CreateScale(size) * pose.Rotation * Matrix4x4.CreateTranslation(pose.Position);
                var color = palette[pose.PrimaryPaletteIndex];

                instances[firstInstance++] = new ModelInstance(transform) {
                    Color = new Vector4(color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, 1.0f)
                };
            }
        }
    }

    public void Clear()
    {
        regions.Clear();
        activeRegions.Clear();
        instanceCount = 0;
        regionBatchCount = 0;
    }

    public sealed class RegionBuilder(RenderRegionCoordinate coordinate)
    {
        public RenderRegionCoordinate Coordinate { get; } = coordinate;
        public AxisAlignedBounds Bounds { get; private set; }
        public List<VehiclePose> Poses { get; } = new();

        public void Add(VehiclePose pose, AxisAlignedBounds bounds)
        {
            Bounds = Poses.Count == 0 ? bounds : Bounds.Encapsulate(bounds);
            Poses.Add(pose);
        }
    }
}
