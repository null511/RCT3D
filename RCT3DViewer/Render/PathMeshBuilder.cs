using RCT3DViewer.Interop;
using System.Numerics;

namespace RCT3DViewer.Render;

internal sealed class PathMeshBuilder(PathMaterialLibrary materials)
{
    private const float TileSize = 3.0f;
    private const float BaseHeightScale = 0.75f;
    // OpenRCT2 paths rise 16 game Z units; the adapter exports base height in 8-unit levels.
    private const float PathRise = 2.0f * BaseHeightScale;
    private const float RailHeight = 0.62f;
    private const float RailThickness = 0.045f;
    private const float PostHalfWidth = 0.055f;
    private const float CornerInset = TileSize * 4.0f / 32.0f;
    private const float SurfaceBias = 0.08f;
    private const byte DirectionWest = 0;
    private const byte DirectionNorth = 1;
    private const byte DirectionEast = 2;
    private const byte DirectionSouth = 3;
    private readonly List<TerrainVertex> _vertices = new();
    private readonly List<uint> _indices = new();

    public PathMeshData Build(
        ReadOnlySpan<RCTTilePathData> paths, ReadOnlySpan<RCTTileSurfaceData> surfaces, int mapWidth, int mapHeight)
    {
        _vertices.Clear();
        _indices.Clear();

        foreach (var path in paths) {
            if ((uint)path.TileX >= (uint)mapWidth || (uint)path.TileY >= (uint)mapHeight)
                throw new InvalidDataException($"OpenRCT2 returned a path outside the {mapWidth}x{mapHeight} map.");

            AddPathSurface(path);
            AddPathRailings(path, surfaces[path.TileY * mapWidth + path.TileX]);
        }

        return new PathMeshData(_vertices.ToArray(), _indices.ToArray());
    }

    private void AddPathSurface(RCTTilePathData path)
    {
        var west = path.TileX * TileSize;
        var east = (path.TileX + 1) * TileSize;
        var south = path.TileY * TileSize;
        var north = (path.TileY + 1) * TileSize;
        var baseHeight = path.BaseZ * BaseHeightScale;
        var southWest = new Vector3(west, CornerHeight(path, west: true, north: false, baseHeight) + SurfaceBias, south);
        var southEast = new Vector3(east, CornerHeight(path, west: false, north: false, baseHeight) + SurfaceBias, south);
        var northEast = new Vector3(east, CornerHeight(path, west: false, north: true, baseHeight) + SurfaceBias, north);
        var northWest = new Vector3(west, CornerHeight(path, west: true, north: true, baseHeight) + SurfaceBias, north);
        var material = materials.GetLayer(path.SurfaceIdHash, path.IsQueue);

        AddTriangle(northEast, new Vector2(1, 1), southEast, new Vector2(1, 0), northWest, new Vector2(0, 1), material);
        AddTriangle(southEast, new Vector2(1, 0), southWest, new Vector2(0, 0), northWest, new Vector2(0, 1), material);
    }

    private void AddPathRailings(RCTTilePathData path, RCTTileSurfaceData surface)
    {
        if (!materials.HasVisibleRailings(path.RailingsIdHash))
            return;

        if (!path.IsQueue && !path.IsSloped && path.BaseZ == surface.BaseZ && surface.SlopeFlags == 0)
            return;

        var west = path.TileX * TileSize;
        var east = (path.TileX + 1) * TileSize;
        var south = path.TileY * TileSize;
        var north = (path.TileY + 1) * TileSize;
        var baseHeight = path.BaseZ * BaseHeightScale;
        var material = materials.GetRailingLayer(path.RailingsIdHash);

        if (path.IsSloped) {
            if (path.SlopeDirection is DirectionWest or DirectionEast) {
                if ((path.Edges & (1 << DirectionNorth)) == 0)
                    AddRailingEdge(path, DirectionNorth, new Vector3(west, 0, north), new Vector3(east, 0, north), baseHeight, material);
                if ((path.Edges & (1 << DirectionSouth)) == 0)
                    AddRailingEdge(path, DirectionSouth, new Vector3(west, 0, south), new Vector3(east, 0, south), baseHeight, material);
            }
            else {
                if ((path.Edges & (1 << DirectionWest)) == 0)
                    AddRailingEdge(path, DirectionWest, new Vector3(west, 0, south), new Vector3(west, 0, north), baseHeight, material);
                if ((path.Edges & (1 << DirectionEast)) == 0)
                    AddRailingEdge(path, DirectionEast, new Vector3(east, 0, south), new Vector3(east, 0, north), baseHeight, material);
            }
            return;
        }

        for (byte direction = 0; direction < 4; direction++) {
            if ((path.Edges & (1 << direction)) != 0)
                continue;

            switch (direction) {
                case DirectionWest:
                    AddRailingEdge(path, direction, new Vector3(west, 0, south), new Vector3(west, 0, north), baseHeight, material);
                    break;
                case DirectionNorth:
                    AddRailingEdge(path, direction, new Vector3(west, 0, north), new Vector3(east, 0, north), baseHeight, material);
                    break;
                case DirectionEast:
                    AddRailingEdge(path, direction, new Vector3(east, 0, south), new Vector3(east, 0, north), baseHeight, material);
                    break;
                case DirectionSouth:
                    AddRailingEdge(path, direction, new Vector3(west, 0, south), new Vector3(east, 0, south), baseHeight, material);
                    break;
            }
        }
    }

    private void AddRailingEdge(
        RCTTilePathData path, byte direction, Vector3 start, Vector3 end, float baseHeight, uint material)
    {
        var startHeight = CornerHeight(path, start.X == path.TileX * TileSize, start.Z == (path.TileY + 1) * TileSize, baseHeight);
        var endHeight = CornerHeight(path, end.X == path.TileX * TileSize, end.Z == (path.TileY + 1) * TileSize, baseHeight);
        start.Y = startHeight;
        end.Y = endHeight;

        var startCorner = CornerBitForEdge(direction, atStart: true);
        var endCorner = CornerBitForEdge(direction, atStart: false);
        var startConnectedDiagonally = (path.Corners & (1 << startCorner)) != 0;
        var endConnectedDiagonally = (path.Corners & (1 << endCorner)) != 0;
        var edge = end - start;
        var edgeLength = edge.Length();
        if (edgeLength <= 2 * CornerInset)
            return;

        var originalStartHeight = startHeight;
        var originalEndHeight = endHeight;
        var edgeDirection = Vector3.Normalize(edge);
        if (startConnectedDiagonally) {
            start += edgeDirection * CornerInset;
            startHeight = originalStartHeight + (originalEndHeight - originalStartHeight) * (CornerInset / edgeLength);
            start.Y = startHeight;
        }
        if (endConnectedDiagonally) {
            end -= edgeDirection * CornerInset;
            endHeight = originalStartHeight + (originalEndHeight - originalStartHeight) * (1.0f - CornerInset / edgeLength);
            end.Y = endHeight;
        }

        var midpoint = (start + end) * 0.5f;
        midpoint.Y = (startHeight + endHeight) * 0.5f;
        AddRailPost(start, material);
        AddRailPost(midpoint, material);
        AddRailPost(end, material);
        AddRailBeam(start, end, RailHeight * 0.5f, material);
        AddRailBeam(start, end, RailHeight, material);
    }

    private static float CornerHeight(RCTTilePathData path, bool west, bool north, float baseHeight)
    {
        if (!path.IsSloped)
            return baseHeight;

        var uphill = path.SlopeDirection switch {
            DirectionWest => west,
            DirectionNorth => north,
            DirectionEast => !west,
            DirectionSouth => !north,
            _ => false,
        };
        return baseHeight + (uphill ? PathRise : 0.0f);
    }

    private static int CornerBitForEdge(byte direction, bool atStart)
    {
        // OpenRCT2's corner-bit order is NW, NE, SE, SW.
        return (direction, atStart) switch {
            (DirectionWest, true) => 3,
            (DirectionWest, false) => 0,
            (DirectionNorth, true) => 0,
            (DirectionNorth, false) => 1,
            (DirectionEast, true) => 2,
            (DirectionEast, false) => 1,
            (DirectionSouth, true) => 3,
            (DirectionSouth, false) => 2,
            _ => 0,
        };
    }

    private void AddRailPost(Vector3 foot, uint material)
    {
        AddBeam(foot + Vector3.UnitY * (RailHeight * 0.5f), foot + Vector3.UnitY * (RailHeight * 0.5f), PostHalfWidth, material);
    }

    private void AddRailBeam(Vector3 start, Vector3 end, float height, uint material)
    {
        start.Y += height;
        end.Y += height;
        AddBeam(start, end, RailThickness, material);
    }

    private void AddBeam(Vector3 start, Vector3 end, float halfWidth, uint material)
    {
        var axis = end - start;
        if (axis.LengthSquared() < 0.0001f) {
            var centerPoint = start;
            start = centerPoint - Vector3.UnitY * (RailHeight * 0.5f);
            end = centerPoint + Vector3.UnitY * (RailHeight * 0.5f);
            axis = end - start;
        }
        axis = Vector3.Normalize(axis);
        var side = Vector3.Cross(Vector3.UnitY, axis);
        if (side.LengthSquared() < 0.0001f)
            side = Vector3.UnitX;
        else
            side = Vector3.Normalize(side);
        var up = Vector3.Normalize(Vector3.Cross(axis, side));
        Span<Vector3> crossSection = stackalloc Vector3[4];
        crossSection[0] = -side * halfWidth - up * halfWidth;
        crossSection[1] = side * halfWidth - up * halfWidth;
        crossSection[2] = side * halfWidth + up * halfWidth;
        crossSection[3] = -side * halfWidth + up * halfWidth;
        Span<Vector3> points = stackalloc Vector3[8];
        for (var i = 0; i < 4; i++) {
            points[i] = start + crossSection[i];
            points[i + 4] = end + crossSection[i];
        }

        var prismCenter = (start + end) * 0.5f;
        AddPrismFace(points[0], points[1], points[5], points[4], prismCenter, material);
        AddPrismFace(points[1], points[2], points[6], points[5], prismCenter, material);
        AddPrismFace(points[2], points[3], points[7], points[6], prismCenter, material);
        AddPrismFace(points[3], points[0], points[4], points[7], prismCenter, material);
        AddPrismFace(points[0], points[3], points[2], points[1], prismCenter, material);
        AddPrismFace(points[4], points[5], points[6], points[7], prismCenter, material);
    }

    private void AddPrismFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 prismCenter, uint material)
    {
        var faceCenter = (a + b + c + d) * 0.25f;
        var normal = Vector3.Normalize(faceCenter - prismCenter);
        AddTriangle(a, Vector2.Zero, b, Vector2.UnitX, c, Vector2.One, material, normal);
        AddTriangle(a, Vector2.Zero, c, Vector2.One, d, Vector2.UnitY, material, normal);
    }

    private void AddTriangle(
        Vector3 a, Vector2 uvA, Vector3 b, Vector2 uvB, Vector3 c, Vector2 uvC, uint material, Vector3? expectedNormal = null)
    {
        var normal = expectedNormal ?? Vector3.Normalize(Vector3.Cross(b - a, c - a));
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0.0f)
            (b, c, uvB, uvC) = (c, b, uvC, uvB);

        var firstIndex = (uint)_vertices.Count;
        _vertices.Add(new TerrainVertex(a, normal, uvA, material));
        _vertices.Add(new TerrainVertex(b, normal, uvB, material));
        _vertices.Add(new TerrainVertex(c, normal, uvC, material));
        _indices.Add(firstIndex);
        _indices.Add(firstIndex + 1);
        _indices.Add(firstIndex + 2);
    }
}

internal sealed record PathMeshData(TerrainVertex[] Vertices, uint[] Indices);