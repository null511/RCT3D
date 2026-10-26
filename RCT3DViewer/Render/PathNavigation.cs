using RCT3D.Interop;
using RCT3D.Render.Paths.Meshing;
using System.Numerics;

namespace RCT3D.Render;

internal sealed class PathNavigation
{
    private const float EdgeHeightTolerance = 0.16f;
    private const float MaximumMoveStep = 0.12f;
    private readonly PathNode[] nodes;
    private readonly Dictionary<(int X, int Y), List<int>> nodesByTile = new();


    public PathNavigation(ReadOnlySpan<RCTTilePathData> paths)
    {
        nodes = new PathNode[paths.Length];
        for (var index = 0; index < paths.Length; index++) {
            var path = paths[index];
            nodes[index] = new PathNode(path);
            if (!nodesByTile.TryGetValue((path.TileX, path.TileY), out var tileNodes)) {
                tileNodes = new List<int>();
                nodesByTile.Add((path.TileX, path.TileY), tileNodes);
            }

            tileNodes.Add(index);
        }
    }

    public bool TrySnapToNearestPath(Vector3 cameraPosition, float maximumDistance, float eyeHeight, out PathPose pose)
    {
        var footPosition = cameraPosition - Vector3.UnitY * eyeHeight;
        var maximumDistanceSquared = maximumDistance * maximumDistance;
        var nearestDistanceSquared = float.PositiveInfinity;
        var nearestNode = -1;
        var nearestPosition = default(Vector3);

        for (var index = 0; index < nodes.Length; index++) {
            var node = nodes[index];
            var minX = node.Path.TileX * WorldUnits.TileSize;
            var minZ = node.Path.TileY * WorldUnits.TileSize;
            var x = Math.Clamp(footPosition.X, minX, minX + WorldUnits.TileSize);
            var z = Math.Clamp(footPosition.Z, minZ, minZ + WorldUnits.TileSize);
            var y = GetPathHeight(node.Path, x, z);
            var candidate = new Vector3(x, y, z);
            var distanceSquared = Vector3.DistanceSquared(footPosition, candidate);
            if (distanceSquared >= nearestDistanceSquared) continue;

            nearestDistanceSquared = distanceSquared;
            nearestNode = index;
            nearestPosition = candidate;
        }

        if (nearestNode < 0 || nearestDistanceSquared > maximumDistanceSquared) {
            pose = default;
            return false;
        }

        pose = new PathPose(nearestNode, nearestPosition + Vector3.UnitY * eyeHeight);
        return true;
    }

    public bool TryMove(PathPose current, Vector2 displacement, float eyeHeight, out PathPose moved)
    {
        moved = current;
        if ((uint)current.NodeIndex >= (uint)nodes.Length) return false;

        var distance = displacement.Length();
        var steps = Math.Max(1, (int)MathF.Ceiling(distance / MaximumMoveStep));
        var step = displacement / steps;
        for (var index = 0; index < steps; index++) {
            if (TryMoveAxis(moved, step.X, 0.0f, eyeHeight, out var afterX)) moved = afterX;
            if (TryMoveAxis(moved, 0.0f, step.Y, eyeHeight, out var afterZ)) moved = afterZ;
        }

        return true;
    }

    private bool TryMoveAxis(PathPose current, float deltaX, float deltaZ, float eyeHeight, out PathPose moved)
    {
        var footPosition = current.Position - Vector3.UnitY * eyeHeight;
        var x = footPosition.X + deltaX;
        var z = footPosition.Z + deltaZ;
        var tileX = (int)MathF.Floor(x / WorldUnits.TileSize);
        var tileY = (int)MathF.Floor(z / WorldUnits.TileSize);
        var previous = nodes[current.NodeIndex].Path;

        if (tileX == previous.TileX && tileY == previous.TileY) {
            moved = new PathPose(current.NodeIndex, new Vector3(x, GetPathHeight(previous, x, z) + eyeHeight, z));
            return true;
        }

        var direction = (tileX - previous.TileX, tileY - previous.TileY) switch {
            (-1, 0) => PathTypeGeometryBuilder.DirectionWest,
            (0, 1) => PathTypeGeometryBuilder.DirectionNorth,
            (1, 0) => PathTypeGeometryBuilder.DirectionEast,
            (0, -1) => PathTypeGeometryBuilder.DirectionSouth,
            _ => (byte)255,
        };
        if (direction == 255 || !HasEdge(previous, direction)
            || !nodesByTile.TryGetValue((tileX, tileY), out var candidates)) {
            moved = current;
            return false;
        }

        var opposite = (byte)((direction + 2) & 3);
        foreach (var candidateIndex in candidates) {
            var candidate = nodes[candidateIndex].Path;
            if (!HasEdge(candidate, opposite)) continue;
            if (MathF.Abs(GetEdgeHeight(previous, direction) - GetEdgeHeight(candidate, opposite)) > EdgeHeightTolerance) continue;

            moved = new PathPose(candidateIndex, new Vector3(x, GetPathHeight(candidate, x, z) + eyeHeight, z));
            return true;
        }

        moved = current;
        return false;
    }

    private static bool HasEdge(RCTTilePathData path, byte direction) => (path.Edges & (1 << direction)) != 0;

    private static float GetEdgeHeight(RCTTilePathData path, byte direction)
    {
        var baseHeight = path.BaseZ * WorldUnits.HeightPerBaseUnit;
        return direction switch {
            PathTypeGeometryBuilder.DirectionWest => (GetCornerHeight(path, true, false, baseHeight) + GetCornerHeight(path, true, true, baseHeight)) * 0.5f,
            PathTypeGeometryBuilder.DirectionNorth => (GetCornerHeight(path, true, true, baseHeight) + GetCornerHeight(path, false, true, baseHeight)) * 0.5f,
            PathTypeGeometryBuilder.DirectionEast => (GetCornerHeight(path, false, false, baseHeight) + GetCornerHeight(path, false, true, baseHeight)) * 0.5f,
            _ => (GetCornerHeight(path, true, false, baseHeight) + GetCornerHeight(path, false, false, baseHeight)) * 0.5f,
        };
    }

    private static float GetPathHeight(RCTTilePathData path, float worldX, float worldZ)
    {
        var fractionX = (worldX - path.TileX * WorldUnits.TileSize) / WorldUnits.TileSize;
        var fractionZ = (worldZ - path.TileY * WorldUnits.TileSize) / WorldUnits.TileSize;
        var baseHeight = path.BaseZ * WorldUnits.HeightPerBaseUnit;
        var northEast = GetCornerHeight(path, false, true, baseHeight);
        var southEast = GetCornerHeight(path, false, false, baseHeight);
        var southWest = GetCornerHeight(path, true, false, baseHeight);
        var northWest = GetCornerHeight(path, true, true, baseHeight);

        return fractionX + fractionZ >= 1.0f
            ? northEast * (fractionX + fractionZ - 1.0f) + southEast * (1.0f - fractionZ) + northWest * (1.0f - fractionX)
            : southEast * fractionX + southWest * (1.0f - fractionX - fractionZ) + northWest * fractionZ;
    }

    private static float GetCornerHeight(RCTTilePathData path, bool west, bool north, float baseHeight)
    {
        if (!path.IsSloped) return baseHeight;
        var uphill = path.SlopeDirection switch {
            PathTypeGeometryBuilder.DirectionWest => west,
            PathTypeGeometryBuilder.DirectionNorth => north,
            PathTypeGeometryBuilder.DirectionEast => !west,
            PathTypeGeometryBuilder.DirectionSouth => !north,
            _ => false,
        };
        return baseHeight + (uphill ? 2.0f * WorldUnits.HeightPerBaseUnit : 0.0f);
    }

    private readonly record struct PathNode(RCTTilePathData Path);
}

internal readonly record struct PathPose(int NodeIndex, Vector3 Position);
