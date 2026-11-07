using RCT3D.Render.Paths;
using System.Numerics;

namespace RCT3D.Render;

internal static class VehicleCubeRenderer
{
    //private const float TileSize = 3.0f;
    private const float CubeSize = 0.7f;
    private const float CubeHalfSize = CubeSize * 0.5f;

    private const float CubeBoundsHalfSize = CubeHalfSize * 1.7320508f;


    public static ImportedModelMesh CreateMesh() => CreateCubeMesh(Vector3.Zero, CubeSize);

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
            new ImportedModelTexture(1, 1, [255, 0, 0, 255]),
            null,
            false);

        return new ImportedModelMesh("ride-vehicle-cube", vertices, indices, material);
    }

    public static ModelRegionBatch[] BuildInstances(
        IReadOnlyDictionary<ushort, VehiclePose> poses,
        int mapWidth,
        int mapHeight)
    {
        if (poses.Count == 0 || mapWidth <= 0 || mapHeight <= 0)
            return Array.Empty<ModelRegionBatch>();

        var regions = new Dictionary<RenderRegionCoordinate, RegionBuilder>();

        foreach (var pose in poses.Values) {
            var position = pose.Position;
            var tileX = (int)MathF.Floor(position.X / WorldUnits.TileSize);
            var tileY = (int)MathF.Floor(position.Z / WorldUnits.TileSize);

            if ((uint)tileX >= (uint)mapWidth || (uint)tileY >= (uint)mapHeight)
                continue;

            var transform = pose.Rotation * Matrix4x4.CreateTranslation(position);
            var boundsPosition = position;

            var bounds = new AxisAlignedBounds(
                boundsPosition - new Vector3(CubeBoundsHalfSize),
                boundsPosition + new Vector3(CubeBoundsHalfSize));

            var region = new RenderRegionCoordinate(
                tileX / RenderRegionLayout.RegionSizeInTiles,
                tileY / RenderRegionLayout.RegionSizeInTiles);

            if (!regions.TryGetValue(region, out var regionBuilder)) {
                regionBuilder = new RegionBuilder();
                regions.Add(region, regionBuilder);
            }

            regionBuilder.Add(new ModelInstance(transform), bounds);
        }

        return regions.OrderBy(pair => pair.Key.X).ThenBy(pair => pair.Key.Y)
            .Select(pair => new ModelRegionBatch(pair.Key, pair.Value.Bounds, pair.Value.Instances.ToArray()))
            .ToArray();
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
