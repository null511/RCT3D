using System.Numerics;
using RCT3D.Interop;

namespace RCT3D.Render.Terrain;

internal sealed class TerrainMeshBuilder(TerrainMaterialLibrary materials)
{
    internal const float TileSize = 3.0f;
    private const float BaseHeightScale = 0.75f;
    private const float CornerHeightScale = 1.5f;
    private const float NormalInterpolationBoundaryFraction = 1.0f / 8.0f;

    private readonly List<TerrainVertex> _vertices = new();
    private readonly List<uint> _indices = new();
    private readonly List<Vector3> _waterVertices = new();
    private readonly List<uint> _waterIndices = new();
    private readonly List<RenderRegionIndexRange> _regions = new();
    private readonly List<RenderRegionIndexRange> _waterRegions = new();

    private static readonly byte[] TileTriangleIndices = {
        // top-left
        0, 12, 1,
        12, 0, 8,

        // top-right
        2, 10, 3,
        10, 2, 14,

        // bottom-left
        9, 5, 15,
        5, 9, 4,

        // bottom-right
        17, 7, 11,
        7, 17, 6,

        // top edge
        13, 1, 12,
        2, 1, 13,
        2, 13, 14,

        // bottom edge
        5, 16, 15,
        5, 6, 16,
        16, 6, 17,

        // left edge
        8, 18, 12,
        8, 9, 18,
        18, 9, 15,

        // right edge
        10, 19, 14,
        11, 10, 19,
        11, 19, 17,

        // center bottom-left
        16, 18, 15,

        // center top-right
        13, 19, 14,

        // center diagonals
        12, 19, 13,
        19, 12, 17,
        17, 12, 18,
        17, 18, 16,
    };

    private int _skirtSections;
    private int _skirtTriangles;
    private int _flatEdgesSkipped;


    public TerrainMeshBuildData Build(ReadOnlySpan<RCTTileSurfaceData> surfaces, int width, int height, float skirtFloor)
    {
        _vertices.Clear();
        _indices.Clear();
        _waterVertices.Clear();
        _waterIndices.Clear();
        _regions.Clear();
        _waterRegions.Clear();

        _skirtSections = 0;
        _skirtTriangles = 0;
        _flatEdgesSkipped = 0;
        foreach (var region in RenderRegionLayout.Enumerate(width, height)) {
            var tileBounds = RenderRegionLayout.GetTileBounds(region, width, height);

            var firstVertex = _vertices.Count;
            var firstIndex = _indices.Count;
            var firstWaterVertex = _waterVertices.Count;
            var firstWaterIndex = _waterIndices.Count;

            for (var y = tileBounds.TileY; y < tileBounds.TileY + tileBounds.Height; y++)
            for (var x = tileBounds.TileX; x < tileBounds.TileX + tileBounds.Width; x++) {
                var surface = surfaces[y * width + x];

                if (surface.WaterHeight > 0)
                    AddWaterTile(x, y, surface.WaterHeight * (BaseHeightScale / 8.0f), TileSize);

                GetCornerHeights(surface, out var north, out var east, out var south, out var west);

                // OpenRCT2's corner flags map North/East/South/West to the tile's
                // northeast/southeast/southwest/northwest vertices, respectively.
                var cNorth = new Vector3((x + 1) * TileSize, north, (y + 1) * TileSize);
                var cEast = new Vector3((x + 1) * TileSize, east, y * TileSize);
                var cSouth = new Vector3(x * TileSize, south, y * TileSize);
                var cWest = new Vector3(x * TileSize, west, (y + 1) * TileSize);

                var materialId = GetMaterialLayer(surface, x, y);
                AddTerrainTile(x, y, width, height, surfaces, north, east, south, west, materialId);

                var eastEdgeNormal = Vector3.UnitX;

                if (x + 1 < width) {
                    var eastNeighbor = surfaces[y * width + x + 1];
                    GetCornerHeights(eastNeighbor, out _, out _, out var neighborSouth, out var neighborWest);

                    AddSkirtEdge(cEast, east, cNorth, north, neighborSouth, neighborWest,
                        materialId, GetMaterialLayer(eastNeighbor, x + 1, y), eastEdgeNormal);
                }
                else {
                    AddSkirtEdge(cEast, east, cNorth, north, skirtFloor, skirtFloor,
                        materialId, materialId, eastEdgeNormal);
                }

                var northEdgeNormal = Vector3.UnitZ;

                if (y + 1 < height) {
                    var northNeighbor = surfaces[(y + 1) * width + x];
                    GetCornerHeights(northNeighbor, out _, out var neighborEast, out var neighborSouth, out _);

                    AddSkirtEdge(cWest, west, cNorth, north, neighborSouth, neighborEast,
                        materialId, GetMaterialLayer(northNeighbor, x, y + 1), northEdgeNormal);
                }
                else {
                    AddSkirtEdge(cWest, west, cNorth, north, skirtFloor, skirtFloor,
                        materialId, materialId, northEdgeNormal);
                }

                if (x == 0)
                    AddSkirtEdge(cSouth, south, cWest, west, skirtFloor, skirtFloor, materialId, materialId, -Vector3.UnitX);

                if (y == 0)
                    AddSkirtEdge(cSouth, south, cEast, east, skirtFloor, skirtFloor, materialId, materialId, -Vector3.UnitZ);
            }

            if (_indices.Count > firstIndex)
                _regions.Add(RenderRegionMeshRanges.Create(region, _vertices, _indices, firstVertex, firstIndex));

            if (_waterIndices.Count > firstWaterIndex)
                _waterRegions.Add(RenderRegionMeshRanges.Create(region, _waterVertices, _waterIndices, firstWaterVertex, firstWaterIndex));
        }

        return new TerrainMeshBuildData(
            new TerrainMeshData(
                _vertices.ToArray(),
                _indices.ToArray(),
                _skirtSections,
                _skirtTriangles,
                _flatEdgesSkipped,
                _regions.ToArray()),
            new WaterMeshData(
                _waterVertices.ToArray(),
                _waterIndices.ToArray(),
                _waterRegions.ToArray()));
    }

    private uint GetMaterialLayer(RCTTileSurfaceData surface, int x, int y)
    {
        return materials.GetTerrainLayer(
            surface.TerrainSurfaceIdHash,
            surface.GrassState,
            surface.CanGrowGrass != 0,
            x,
            y);
    }

    private void AddTerrainTile(
        int x, int y, int width, int height, ReadOnlySpan<RCTTileSurfaceData> surfaces,
        float north, float east, float south, float west, uint materialId)
    {
        var boundary = NormalInterpolationBoundaryFraction;

        Span<Vector2> coordinates = stackalloc Vector2[] {
            // outer top edge
            new(0.0f, 1.0f),
            new(boundary, 1.0f),
            new(1.0f - boundary, 1.0f),
            new(1.0f, 1.0f),

            // outer bottom edge
            new(0.0f, 0.0f),
            new(boundary, 0.0f),
            new(1.0f - boundary, 0.0f),
            new(1.0f, 0.0f),

            // outer left edge
            new(0.0f, 1.0f - boundary),
            new(0.0f, boundary),

            // outer right edge
            new(1.0f, 1.0f - boundary),
            new(1.0f, boundary),

            // inner top edge
            new(boundary, 1.0f - boundary),
            new(boundary*2.0f, 1.0f - boundary),
            new(1.0f - boundary, 1.0f - boundary),

            // inner bottom edge
            new(boundary, boundary),
            new(1.0f - 2.0f*boundary, boundary),
            new(1.0f - boundary, boundary),

            // inner left edge
            new(boundary, 1.0f - 2.0f*boundary),

            // inner right edge
            new(1.0f - boundary, 2.0f*boundary),
        };

        var firstTriangleNormal = GetBaseTriangleNormal(north, east, south, west, true);
        var secondTriangleNormal = GetBaseTriangleNormal(north, east, south, west, false);
        var diagonalNormal = Vector3.Normalize(firstTriangleNormal + secondTriangleNormal);

        var firstVertex = checked((uint)_vertices.Count);
        foreach (var coordinate in coordinates) {
            var u = coordinate.X;
            var v = coordinate.Y;

            var point = CreateTerrainPoint(x, y, u, v, north, east, south, west);
            var baseNormal = u + v >= 1.0f ? firstTriangleNormal : secondTriangleNormal;
            var normal = GetInterpolatedNormal(baseNormal, diagonalNormal, x, y, width, height, surfaces, north, east, south, west, u, v);

            _vertices.Add(new TerrainVertex(point, normal, new Vector2(x + u, y + v), materialId));
        }

        foreach (var index in TileTriangleIndices)
            _indices.Add(firstVertex + index);
    }

    private Vector3 CreateTerrainPoint(
        int x, int y, float u, float v, float north, float east, float south, float west)
    {
        var height = u + v >= 1.0f
            ? (north - west) * u + (north - east) * v + east - north + west
            : south + (east - south) * u + (west - south) * v;

        return new Vector3((x + u) * TileSize, height, (y + v) * TileSize);
    }

    private Vector3 GetInterpolatedNormal(
        Vector3 baseNormal, Vector3 diagonalNormal, int x, int y, int width, int height, ReadOnlySpan<RCTTileSurfaceData> surfaces,
        float north, float east, float south, float west, float u, float v)
    {
        var boundary = NormalInterpolationBoundaryFraction;
        if ((u == 0.0f || u == 1.0f) && (v == 0.0f || v == 1.0f))
            return GetConnectedCornerNormal(x, y, u, v, width, height, surfaces);

        var normal = GetDiagonalAdjustedNormal(baseNormal, diagonalNormal, u, v);

        if (u < boundary && x > 0) {
            var neighbor = surfaces[y * width + x - 1];
            GetCornerHeights(neighbor, out var neighborNorth, out var neighborEast, out var neighborSouth, out var neighborWest);
            if (south == neighborEast && west == neighborNorth) {
                normal += GetDiagonalAdjustedFacetNormal(
                    neighborNorth, neighborEast, neighborSouth, neighborWest, true, 1.0f, v)
                    * ((boundary - u) / boundary);
            }
        }

        if (u > 1.0f - boundary && x + 1 < width) {
            var neighbor = surfaces[y * width + x + 1];
            GetCornerHeights(neighbor, out var neighborNorth, out var neighborEast, out var neighborSouth, out var neighborWest);
            if (east == neighborSouth && north == neighborWest) {
                normal += GetDiagonalAdjustedFacetNormal(
                    neighborNorth, neighborEast, neighborSouth, neighborWest, false, 0.0f, v)
                    * ((boundary - (1.0f - u)) / boundary);
            }
        }

        if (v > 1.0f - boundary && y + 1 < height) {
            var neighbor = surfaces[(y + 1) * width + x];
            GetCornerHeights(neighbor, out var neighborNorth, out var neighborEast, out var neighborSouth, out var neighborWest);
            if (west == neighborSouth && north == neighborEast) {
                normal += GetDiagonalAdjustedFacetNormal(
                    neighborNorth, neighborEast, neighborSouth, neighborWest, false, u, 0.0f)
                    * ((boundary - (1.0f - v)) / boundary);
            }
        }

        if (v < boundary && y > 0) {
            var neighbor = surfaces[(y - 1) * width + x];
            GetCornerHeights(neighbor, out var neighborNorth, out var neighborEast, out var neighborSouth, out var neighborWest);
            if (south == neighborWest && east == neighborNorth) {
                normal += GetDiagonalAdjustedFacetNormal(
                    neighborNorth, neighborEast, neighborSouth, neighborWest, true, u, 1.0f)
                    * ((boundary - v) / boundary);
            }
        }

        return Vector3.Normalize(normal);
    }

    private static Vector3 GetDiagonalAdjustedFacetNormal(
        float north, float east, float south, float west, bool firstFacet, float u, float v)
    {
        var firstNormal = GetBaseTriangleNormal(north, east, south, west, true);
        var secondNormal = GetBaseTriangleNormal(north, east, south, west, false);
        var diagonalNormal = Vector3.Normalize(firstNormal + secondNormal);
        var facetNormal = firstFacet ? firstNormal : secondNormal;
        return GetDiagonalAdjustedNormal(facetNormal, diagonalNormal, u, v);
    }

    private static Vector3 GetDiagonalAdjustedNormal(Vector3 facetNormal, Vector3 diagonalNormal, float u, float v)
    {
        var distanceFromDiagonal = MathF.Abs(u + v - 1.0f) * TileSize / MathF.Sqrt(2.0f);
        var boundaryWidth = TileSize * NormalInterpolationBoundaryFraction;
        var blend = Math.Clamp(1.0f - distanceFromDiagonal / boundaryWidth, 0.0f, 1.0f);
        return blend > 0.0f
            ? Vector3.Normalize(Vector3.Lerp(facetNormal, diagonalNormal, blend))
            : facetNormal;
    }

    private Vector3 GetConnectedCornerNormal(
        int x, int y, float u, float v, int width, int height, ReadOnlySpan<RCTTileSurfaceData> surfaces)
    {
        var cornerX = x + (u == 1.0f ? 1 : 0);
        var cornerY = y + (v == 1.0f ? 1 : 0);
        Span<int> tileXs = stackalloc int[] { cornerX - 1, cornerX, cornerX - 1, cornerX };
        Span<int> tileYs = stackalloc int[] { cornerY - 1, cornerY - 1, cornerY, cornerY };
        Span<bool> visited = stackalloc bool[4];
        Span<int> queue = stackalloc int[4];
        visited.Clear();
        var currentIndex = -1;

        for (var index = 0; index < 4; index++)
            if (tileXs[index] == x && tileYs[index] == y)
                currentIndex = index;

        var queueStart = 0;
        var queueEnd = 1;
        queue[0] = currentIndex;
        visited[currentIndex] = true;
        var normal = Vector3.Zero;

        while (queueStart < queueEnd) {
            var index = queue[queueStart++];
            var tileX = tileXs[index];
            var tileY = tileYs[index];
            var surface = surfaces[tileY * width + tileX];
            GetCornerHeights(surface, out var north, out var east, out var south, out var west);
            var localU = cornerX - tileX;
            var localV = cornerY - tileY;
            normal += GetDiagonalAdjustedFacetNormal(
                north, east, south, west, localU + localV >= 1.0f, localU, localV);

            for (var neighborIndex = 0; neighborIndex < 4; neighborIndex++) {
                if (visited[neighborIndex]
                    || tileXs[neighborIndex] < 0 || tileXs[neighborIndex] >= width
                    || tileYs[neighborIndex] < 0 || tileYs[neighborIndex] >= height)
                    continue;

                var distance = Math.Abs(tileXs[neighborIndex] - tileX) + Math.Abs(tileYs[neighborIndex] - tileY);
                if (distance == 1 && AreTilesContinuous(
                    tileX, tileY, tileXs[neighborIndex], tileYs[neighborIndex], width, surfaces)) {
                    visited[neighborIndex] = true;
                    queue[queueEnd++] = neighborIndex;
                }
            }
        }

        return Vector3.Normalize(normal);
    }

    private static bool AreTilesContinuous(
        int x, int y, int neighborX, int neighborY, int width, ReadOnlySpan<RCTTileSurfaceData> surfaces)
    {
        var surface = surfaces[y * width + x];
        var neighbor = surfaces[neighborY * width + neighborX];
        GetCornerHeights(surface, out var north, out var east, out var south, out var west);
        GetCornerHeights(neighbor, out var neighborNorth, out var neighborEast, out var neighborSouth, out var neighborWest);

        if (neighborX == x + 1)
            return east == neighborSouth && north == neighborWest;

        if (neighborX == x - 1)
            return south == neighborEast && west == neighborNorth;

        if (neighborY == y + 1)
            return west == neighborSouth && north == neighborEast;

        return south == neighborWest && east == neighborNorth;
    }

    private static void GetCornerHeights(RCTTileSurfaceData surface, out float north, out float east, out float south, out float west)
    {
        var baseHeight = surface.BaseZ * BaseHeightScale;
        north = baseHeight + surface.NorthCornerHeight * CornerHeightScale;
        east = baseHeight + surface.EastCornerHeight * CornerHeightScale;
        south = baseHeight + surface.SouthCornerHeight * CornerHeightScale;
        west = baseHeight + surface.WestCornerHeight * CornerHeightScale;
    }

    private static Vector3 GetBaseTriangleNormal(float north, float east, float south, float west, bool firstTriangle)
    {
        var cNorth = new Vector3(TileSize, north, TileSize);
        var cEast = new Vector3(TileSize, east, 0.0f);
        var cSouth = new Vector3(0.0f, south, 0.0f);
        var cWest = new Vector3(0.0f, west, TileSize);

        return firstTriangle
            ? Vector3.Normalize(Vector3.Cross(cEast - cNorth, cWest - cNorth))
            : Vector3.Normalize(Vector3.Cross(cSouth - cEast, cWest - cEast));
    }

    private void AddWaterTile(int x, int y, float waterHeight, float tileSize)
    {
        var tileOrigin = _waterVertices.Count;

        _waterVertices.Add(new Vector3((x + 1) * tileSize, waterHeight, (y + 1) * tileSize));
        _waterVertices.Add(new Vector3((x + 1) * tileSize, waterHeight, y * tileSize));
        _waterVertices.Add(new Vector3(x * tileSize, waterHeight, (y + 1) * tileSize));
        _waterVertices.Add(new Vector3(x * tileSize, waterHeight, y * tileSize));

        _waterIndices.Add((uint)tileOrigin);
        _waterIndices.Add((uint)tileOrigin + 1);
        _waterIndices.Add((uint)tileOrigin + 2);
        _waterIndices.Add((uint)tileOrigin + 1);
        _waterIndices.Add((uint)tileOrigin + 3);
        _waterIndices.Add((uint)tileOrigin + 2);
    }

    private void AddSkirtEdge(Vector3 edgeStart, float surfaceAStart, Vector3 edgeEnd, float surfaceAEnd,
        float surfaceBStart, float surfaceBEnd, uint materialA, uint materialB, Vector3 normalA)
    {
        var startDifference = surfaceAStart - surfaceBStart;
        var endDifference = surfaceAEnd - surfaceBEnd;
        if (MathF.Abs(startDifference) < 0.0001f && MathF.Abs(endDifference) < 0.0001f) {
            _flatEdgesSkipped++;
            return;
        }

        if (startDifference * endDifference < 0.0f) {
            var crossing = startDifference / (startDifference - endDifference);
            AddSkirtSection(edgeStart, edgeEnd, surfaceAStart, surfaceAEnd, surfaceBStart, surfaceBEnd,
                0.0f, crossing, materialA, materialB, normalA);

            AddSkirtSection(edgeStart, edgeEnd, surfaceAStart, surfaceAEnd, surfaceBStart, surfaceBEnd,
                crossing, 1.0f, materialA, materialB, normalA);
        }
        else {
            AddSkirtSection(edgeStart, edgeEnd, surfaceAStart, surfaceAEnd, surfaceBStart, surfaceBEnd,
                0.0f, 1.0f, materialA, materialB, normalA);
        }
    }

    private void AddSkirtSection(Vector3 edgeStart, Vector3 edgeEnd, float surfaceAStart, float surfaceAEnd,
        float surfaceBStart, float surfaceBEnd, float startT, float endT, uint materialA, uint materialB, Vector3 normalA)
    {
        var startA = float.Lerp(surfaceAStart, surfaceAEnd, startT);
        var endA = float.Lerp(surfaceAStart, surfaceAEnd, endT);
        var startB = float.Lerp(surfaceBStart, surfaceBEnd, startT);
        var endB = float.Lerp(surfaceBStart, surfaceBEnd, endT);
        var aIsHigher = (startA + endA) >= (startB + endB);
        var startHigh = aIsHigher ? startA : startB;
        var endHigh = aIsHigher ? endA : endB;
        var startLow = aIsHigher ? startB : startA;
        var endLow = aIsHigher ? endB : endA;
        if (MathF.Abs(startHigh - startLow) < 0.0001f && MathF.Abs(endHigh - endLow) < 0.0001f)
            return;

        var materialId = aIsHigher ? materialA : materialB;
        var normal = aIsHigher ? normalA : -normalA;
        var startPosition = Vector3.Lerp(edgeStart, edgeEnd, startT);
        var endPosition = Vector3.Lerp(edgeStart, edgeEnd, endT);
        var topStart = new Vector3(startPosition.X, startHigh, startPosition.Z);
        var topEnd = new Vector3(endPosition.X, endHigh, endPosition.Z);
        var bottomStart = new Vector3(startPosition.X, startLow, startPosition.Z);
        var bottomEnd = new Vector3(endPosition.X, endLow, endPosition.Z);
        var edgeUStart = (MathF.Abs(edgeEnd.X - edgeStart.X) > 0.0001f ? startPosition.X : startPosition.Z) / 3.0f;
        var edgeUEnd = (MathF.Abs(edgeEnd.X - edgeStart.X) > 0.0001f ? endPosition.X : endPosition.Z) / 3.0f;
        var baseIndex = (uint)_vertices.Count;

        _skirtSections++;
        _vertices.Add(new TerrainVertex(topStart, normal, new Vector2(edgeUStart, startHigh / 3.0f), materialId));
        _vertices.Add(new TerrainVertex(bottomStart, normal, new Vector2(edgeUStart, startLow / 3.0f), materialId));
        _vertices.Add(new TerrainVertex(topEnd, normal, new Vector2(edgeUEnd, endHigh / 3.0f), materialId));
        _vertices.Add(new TerrainVertex(bottomEnd, normal, new Vector2(edgeUEnd, endLow / 3.0f), materialId));

        if (MathF.Abs(startHigh - startLow) < 0.0001f) {
            AddSkirtTriangle(baseIndex, baseIndex + 2, baseIndex + 3, normal);
        }
        else if (MathF.Abs(endHigh - endLow) < 0.0001f) {
            AddSkirtTriangle(baseIndex, baseIndex + 1, baseIndex + 2, normal);
        }
        else {
            AddSkirtTriangle(baseIndex, baseIndex + 1, baseIndex + 2, normal);
            AddSkirtTriangle(baseIndex + 2, baseIndex + 1, baseIndex + 3, normal);
        }
    }

    private void AddSkirtTriangle(uint indexA, uint indexB, uint indexC, Vector3 expectedNormal)
    {
        var positionA = _vertices[(int)indexA].Position;
        var positionB = _vertices[(int)indexB].Position;
        var positionC = _vertices[(int)indexC].Position;

        if (Vector3.Dot(Vector3.Cross(positionB - positionA, positionC - positionA), expectedNormal) < 0.0f)
            (indexB, indexC) = (indexC, indexB);

        _indices.Add(indexA);
        _indices.Add(indexB);
        _indices.Add(indexC);
        _skirtTriangles++;
    }
}

internal sealed record TerrainMeshData(
    TerrainVertex[] Vertices,
    uint[] Indices,
    int SkirtSections,
    int SkirtTriangles,
    int FlatEdgesSkipped,
    RenderRegionIndexRange[] Regions);

internal sealed record TerrainMeshBuildData(TerrainMeshData Terrain, WaterMeshData Water);

internal sealed record WaterMeshData(Vector3[] Vertices, uint[] Indices, RenderRegionIndexRange[] Regions);
