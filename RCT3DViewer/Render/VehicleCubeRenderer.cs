using RCT3D.Render.Paths;
using RCT3D.Interop;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Simulation;
using System.Diagnostics;
using System.Numerics;

namespace RCT3D.Render;

internal static class VehicleCubeRenderer
{
    private const float TileSize = 3.0f;
    private const float UnitsPerTile = 32.0f;
    private const float HeightPerStep = 0.75f;
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
        VehicleSnapshotPair? snapshots,
        int mapWidth,
        int mapHeight,
        IReadOnlyDictionary<ushort, float>? carVerticalOffsets = null,
        Func<ushort, bool>? usesSmoothMovement = null,
        TryGetCenterlinePoseDelegate? tryGetCenterlinePose = null)
    {
        if (snapshots is null || mapWidth <= 0 || mapHeight <= 0)
            return Array.Empty<ModelRegionBatch>();

        var previousVehicles = snapshots.Previous?.Vehicles.ToArray()
            .ToDictionary(vehicle => vehicle.EntityId);

        var currentVehicles = snapshots.Current.Vehicles.Span;
        var targetTimestamp = Stopwatch.GetTimestamp() - Stopwatch.Frequency / 40;
        var previousTimestamp = snapshots.Previous?.Timestamp ?? snapshots.Current.Timestamp;
        var interval = snapshots.Current.Timestamp - previousTimestamp;

        var alpha = snapshots.Previous is null || interval <= 0
            ? 1.0f
            : Math.Clamp((float)(targetTimestamp - previousTimestamp) / interval, 0.0f, 1.0f);

        var regions = new Dictionary<RenderRegionCoordinate, RegionBuilder>();

        foreach (var current in currentVehicles) {
            var x = (float)current.X;
            var y = (float)current.Y;
            var z = (float)current.Z;

            var yaw = GetYawRadians(current.Orientation);
            var pitch = current.PitchRadians;
            var roll = current.RollRadians;
            var rotation = Matrix4x4.CreateRotationZ(roll)
                * Matrix4x4.CreateRotationX(-pitch)
                * Matrix4x4.CreateRotationY(yaw);

            if (previousVehicles is not null && previousVehicles.TryGetValue(current.EntityId, out var previous)) {
                x = previous.X + (current.X - previous.X) * alpha;
                y = previous.Y + (current.Y - previous.Y) * alpha;
                z = previous.Z + (current.Z - previous.Z) * alpha;

                yaw = LerpAngle(GetYawRadians(previous.Orientation), yaw, alpha);
                pitch = LerpAngle(previous.PitchRadians, pitch, alpha);
                roll = LerpAngle(previous.RollRadians, roll, alpha);
            }

            if (usesSmoothMovement?.Invoke(current.RideId) == true
                && tryGetCenterlinePose is not null
                && previousVehicles is not null
                && previousVehicles.TryGetValue(current.EntityId, out var previousVehicle)
                && tryGetCenterlinePose(previousVehicle, current, alpha, out var centerlinePosition, out var centerlineFrame)) {
                x = centerlinePosition.X * UnitsPerTile / TileSize;
                y = centerlinePosition.Z * UnitsPerTile / TileSize;
                z = centerlinePosition.Y * 8.0f / HeightPerStep;
                rotation = centerlineFrame.ToRotation();
            }

            var tileX = (int)MathF.Floor(x / UnitsPerTile);
            var tileY = (int)MathF.Floor(y / UnitsPerTile);
            if ((uint)tileX >= (uint)mapWidth || (uint)tileY >= (uint)mapHeight)
                continue;

            var position = new Vector3(x * TileSize / UnitsPerTile, z * HeightPerStep / 8.0f, y * TileSize / UnitsPerTile);

            if (carVerticalOffsets is not null && carVerticalOffsets.TryGetValue(current.RideId, out var carVerticalOffset))
                position += Vector3.TransformNormal(new Vector3(0.0f, carVerticalOffset, 0.0f), rotation);

            var transform = rotation * Matrix4x4.CreateTranslation(position);
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

    private static float GetYawRadians(byte orientation) => orientation * (MathF.Tau / 32.0f) - (MathF.PI / 2.0f);

    private static float LerpAngle(float start, float end, float amount)
    {
        var delta = MathF.IEEERemainder(end - start, MathF.Tau);
        return start + delta * amount;
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
