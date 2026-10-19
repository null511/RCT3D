using RCT3DViewer.Interop;
using System.Numerics;

namespace RCT3DViewer.Render.Paths.Accessories;

internal sealed class PathLampMeshBuilder(PathLampAssetLibrary assets)
{
    private const float TileSize = 3.0f;
    private const float BaseHeightScale = 0.75f;
    private const float SlopedPathHeightOffset = 0.75f;
    private const byte EdgeNorthEast = 0;
    private const byte EdgeSouthEast = 1;
    private const byte EdgeSouthWest = 2;
    private const byte EdgeNorthWest = 3;
    private readonly Dictionary<(PathLampModel Model, int MeshIndex), BatchBuilder> _batches = new();

    public PathLampBatch[] Build(ReadOnlySpan<RCTTilePathData> paths, int mapWidth, int mapHeight)
    {
        _batches.Clear();
        foreach (var path in paths) {
            if (!path.IsLampAddition)
                continue;
            if ((uint)path.TileX >= (uint)mapWidth || (uint)path.TileY >= (uint)mapHeight)
                throw new InvalidDataException($"OpenRCT2 returned a lamp path outside the {mapWidth}x{mapHeight} map.");

            var model = assets.GetModel(path.AdditionIdHash);
            var baseHeight = path.BaseZ * BaseHeightScale + (path.IsSloped ? SlopedPathHeightOffset : 0.0f);
            var region = new RenderRegionCoordinate(
                path.TileX / RenderRegionLayout.RegionSizeInTiles,
                path.TileY / RenderRegionLayout.RegionSizeInTiles);
            for (byte edge = 0; edge < 4; edge++) {
                if ((path.Edges & (1 << edge)) != 0)
                    continue;

                var (localX, localZ, edgeYaw) = GetEdgePlacement(edge);
                var position = new Vector3(path.TileX * TileSize + localX, baseHeight, path.TileY * TileSize + localZ);
                var yaw = (model.YawDegrees + edgeYaw) * (MathF.PI / 180.0f);
                var rotation = Matrix4x4.CreateRotationY(yaw);
                var transform = rotation * Matrix4x4.CreateTranslation(position);

                for (var meshIndex = 0; meshIndex < model.Data.Meshes.Length; meshIndex++) {
                    var mesh = model.Data.Meshes[meshIndex];
                    if (mesh.Vertices.Length == 0 || mesh.Indices.Length == 0)
                        continue;

                    var key = (model, meshIndex);
                    if (!_batches.TryGetValue(key, out var batch)) {
                        batch = new BatchBuilder(mesh);
                        _batches.Add(key, batch);
                    }
                    batch.AddInstance(region, transform);
                }
            }
        }

        return _batches.Select(pair => new PathLampBatch(
            pair.Key.Model,
            pair.Key.MeshIndex,
            pair.Value.Mesh.Material,
            pair.Value.Mesh,
            pair.Value.Regions.Select(region => new ModelRegionBatch(
                region.Key,
                region.Value.Bounds,
                region.Value.Instances.ToArray())).ToArray())).ToArray();
    }

    private static (float X, float Z, float YawDegrees) GetEdgePlacement(byte edge)
    {
        var unit = TileSize / 32.0f;
        return edge switch {
            EdgeNorthEast => (2.0f * unit, 16.0f * unit, 90.0f),
            EdgeSouthEast => (16.0f * unit, 30.0f * unit, 180.0f),
            EdgeSouthWest => (30.0f * unit, 16.0f * unit, -90.0f),
            EdgeNorthWest => (16.0f * unit, 2.0f * unit, 0.0f),
            _ => throw new ArgumentOutOfRangeException(nameof(edge)),
        };
    }

    private static AxisAlignedBounds GetMeshBounds(ImportedModelMesh mesh)
    {
        var first = mesh.Vertices[0].Position;
        var bounds = new AxisAlignedBounds(first, first);
        for (var i = 1; i < mesh.Vertices.Length; i++)
            bounds = bounds.Encapsulate(mesh.Vertices[i].Position);
        return bounds;
    }

    private static AxisAlignedBounds TransformBounds(AxisAlignedBounds bounds, Matrix4x4 transform)
    {
        var first = Vector3.Transform(new Vector3(bounds.Min.X, bounds.Min.Y, bounds.Min.Z), transform);
        var transformed = new AxisAlignedBounds(first, first);
        for (var corner = 1; corner < 8; corner++) {
            var point = new Vector3(
                (corner & 1) == 0 ? bounds.Min.X : bounds.Max.X,
                (corner & 2) == 0 ? bounds.Min.Y : bounds.Max.Y,
                (corner & 4) == 0 ? bounds.Min.Z : bounds.Max.Z);
            transformed = transformed.Encapsulate(Vector3.Transform(point, transform));
        }
        return transformed;
    }

    private sealed class BatchBuilder
    {
        private readonly AxisAlignedBounds _meshBounds;

        public ImportedModelMesh Mesh { get; }
        public Dictionary<RenderRegionCoordinate, RegionBuilder> Regions { get; } = new();

        public BatchBuilder(ImportedModelMesh mesh)
        {
            Mesh = mesh;
            _meshBounds = GetMeshBounds(mesh);
        }

        public void AddInstance(RenderRegionCoordinate region, Matrix4x4 transform)
        {
            if (!Regions.TryGetValue(region, out var regionBuilder)) {
                regionBuilder = new RegionBuilder();
                Regions.Add(region, regionBuilder);
            }

            regionBuilder.Add(new ModelInstance(transform), TransformBounds(_meshBounds, transform));
        }
    }

    private sealed class RegionBuilder
    {
        private bool _hasBounds;

        public AxisAlignedBounds Bounds { get; private set; }
        public List<ModelInstance> Instances { get; } = new();

        public void Add(ModelInstance instance, AxisAlignedBounds bounds)
        {
            Bounds = _hasBounds ? Bounds.Encapsulate(bounds) : bounds;
            _hasBounds = true;
            Instances.Add(instance);
        }
    }
}

internal sealed record PathLampBatch(
    PathLampModel Model,
    int MeshIndex,
    ImportedModelMaterial Material,
    ImportedModelMesh Mesh,
    ModelRegionBatch[] Regions);

internal sealed record ModelRegionBatch(
    RenderRegionCoordinate Region,
    AxisAlignedBounds Bounds,
    ModelInstance[] Instances)
{
    public uint FirstInstance { get; init; }
    public uint InstanceCount => (uint)Instances.Length;
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct ModelInstance
{
    public Matrix4x4 Transform;

    public ModelInstance(Matrix4x4 transform)
    {
        Transform = transform;
    }
}
