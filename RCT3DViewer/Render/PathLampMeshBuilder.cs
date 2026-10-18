using RCT3DViewer.Interop;
using System.Numerics;

namespace RCT3DViewer.Render;

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
            for (byte edge = 0; edge < 4; edge++) {
                if ((path.Edges & (1 << edge)) != 0)
                    continue;

                var (localX, localZ, edgeYaw) = GetEdgePlacement(edge);
                var position = new Vector3(path.TileX * TileSize + localX, baseHeight, path.TileY * TileSize + localZ);
                var yaw = (model.YawDegrees + edgeYaw) * (MathF.PI / 180.0f);
                var rotation = Matrix4x4.CreateRotationY(yaw);
                var transform = rotation * Matrix4x4.CreateTranslation(position);

                for (var meshIndex = 0; meshIndex < model.Data.Meshes.Length; meshIndex++) {
                    var key = (model, meshIndex);
                    if (!_batches.TryGetValue(key, out var batch)) {
                        batch = new BatchBuilder(model.Data.Meshes[meshIndex].Material);
                        _batches.Add(key, batch);
                    }
                    AddMeshInstance(batch, model.Data.Meshes[meshIndex], transform, rotation);
                }
            }
        }

        return _batches.Select(pair => new PathLampBatch(
            pair.Key.Model,
            pair.Key.MeshIndex,
            pair.Value.Material,
            new PathMeshData(pair.Value.Vertices.ToArray(), pair.Value.Indices.ToArray()))).ToArray();
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

    private static void AddMeshInstance(BatchBuilder batch, ImportedModelMesh mesh, Matrix4x4 transform, Matrix4x4 rotation)
    {
        var firstVertex = checked((uint)batch.Vertices.Count);
        foreach (var source in mesh.Vertices) {
            var position = Vector3.Transform(source.Position, transform);
            var normal = Vector3.Normalize(Vector3.TransformNormal(source.Normal, rotation));
            batch.Vertices.Add(new TerrainVertex(position, normal, source.TextureCoordinate, 0));
        }

        foreach (var index in mesh.Indices)
            batch.Indices.Add(checked(firstVertex + index));
    }

    private sealed class BatchBuilder(ImportedModelMaterial material)
    {
        public ImportedModelMaterial Material { get; } = material;
        public List<TerrainVertex> Vertices { get; } = new();
        public List<uint> Indices { get; } = new();
    }
}

internal sealed record PathLampBatch(
    PathLampModel Model,
    int MeshIndex,
    ImportedModelMaterial Material,
    PathMeshData MeshData);
