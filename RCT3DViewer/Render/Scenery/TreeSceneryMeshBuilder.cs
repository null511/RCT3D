using RCT3D.Interop.Models;
using RCT3D.Render.Paths;
using System.Numerics;

namespace RCT3D.Render.Scenery;

internal sealed class TreeSceneryMeshBuilder(TreeSceneryAssetLibrary assets)
{
    private const float OpenRCT2UnitToWorld = WorldUnits.TileSize / 32.0f;

    public TreeSceneryBatch[] Build(ReadOnlySpan<RCTTreeSceneryData> trees, int mapWidth, int mapHeight)
    {
        if (trees.IsEmpty) return [];

        var model = assets.LoadTreeModel();
        var batches = new List<TreeMeshBatchBuilder>(model.Meshes.Length);

        foreach (var mesh in model.Meshes)
            batches.Add(new TreeMeshBatchBuilder(mesh));

        foreach (var tree in trees) {
            var tileX = tree.X / 32;
            var tileY = tree.Y / 32;
            if ((uint)tileX >= (uint)mapWidth || (uint)tileY >= (uint)mapHeight)
                throw new InvalidDataException($"OpenRCT2 returned a tree outside the {mapWidth}x{mapHeight} map.");

            var position = new Vector3(tree.X, tree.Z, tree.Y) * OpenRCT2UnitToWorld;
            var yaw = tree.Direction * (MathF.PI / 2.0f);
            var transform = Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(position);

            var region = new RenderRegionCoordinate(
                tileX / RenderRegionLayout.RegionSizeInTiles,
                tileY / RenderRegionLayout.RegionSizeInTiles);

            foreach (var batch in batches)
                batch.AddInstance(region, transform);
        }

        return batches
            .Where(batch => batch.Mesh.Vertices.Length > 0 && batch.Mesh.Indices.Length > 0)
            .Select(batch => new TreeSceneryBatch(
                batch.Mesh,
                batch.Mesh.Material,
                batch.Regions.Select(region => new ModelRegionBatch(
                    region.Key,
                    region.Value.Bounds,
                    region.Value.Instances.ToArray())).ToArray()))
            .ToArray();
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

    private sealed class TreeMeshBatchBuilder(ImportedModelMesh mesh)
    {
        private readonly AxisAlignedBounds meshBounds = GetMeshBounds(mesh);

        public ImportedModelMesh Mesh { get; } = mesh;
        public Dictionary<RenderRegionCoordinate, TreeRegionBatchBuilder> Regions { get; } = new();

        public void AddInstance(RenderRegionCoordinate region, Matrix4x4 transform)
        {
            if (!Regions.TryGetValue(region, out var regionBuilder)) {
                regionBuilder = new TreeRegionBatchBuilder();
                Regions.Add(region, regionBuilder);
            }

            regionBuilder.Add(new ModelInstance(transform), TransformBounds(meshBounds, transform));
        }
    }

    private sealed class TreeRegionBatchBuilder
    {
        private bool hasBounds;

        public AxisAlignedBounds Bounds { get; private set; }
        public List<ModelInstance> Instances { get; } = new();


        public void Add(ModelInstance instance, AxisAlignedBounds bounds)
        {
            Bounds = hasBounds ? Bounds.Encapsulate(bounds) : bounds;
            hasBounds = true;

            Instances.Add(instance);
        }
    }
}

internal sealed record TreeSceneryBatch(
    ImportedModelMesh Mesh,
    ImportedModelMaterial Material,
    ModelRegionBatch[] Regions);
