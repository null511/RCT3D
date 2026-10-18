using RCT3DViewer.Interop;
using System.Numerics;

namespace RCT3DViewer.Render;

internal sealed class TerrainMeshBuilder(TerrainMaterialLibrary materials)
{
    private readonly List<TerrainVertex> _vertices = new();
    private readonly List<uint> _indices = new();
    private readonly List<Vector3> _waterVertices = new();
    private readonly List<uint> _waterIndices = new();

    private int _skirtSections;
    private int _skirtTriangles;
    private int _flatEdgesSkipped;


    public TerrainMeshBuildData Build(ReadOnlySpan<RCTTileSurfaceData> surfaces, int width, int height, float skirtFloor)
    {
        _vertices.Clear();
        _indices.Clear();
        _waterVertices.Clear();
        _waterIndices.Clear();

        _skirtSections = 0;
        _skirtTriangles = 0;
        _flatEdgesSkipped = 0;
        const float tileSize = 3.0f;
        const float baseHeightScale = 0.75f;
        const float cornerHeightScale = 1.5f;

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++) {
            var surface = surfaces[y * width + x];

            if (surface.WaterHeight > 0) {
                AddWaterTile(x, y, surface.WaterHeight * (baseHeightScale / 8.0f), tileSize);
            }

            var baseHeight = surface.BaseZ * baseHeightScale;
            var north = baseHeight + surface.NorthCornerHeight * cornerHeightScale;
            var east = baseHeight + surface.EastCornerHeight * cornerHeightScale;
            var south = baseHeight + surface.SouthCornerHeight * cornerHeightScale;
            var west = baseHeight + surface.WestCornerHeight * cornerHeightScale;

            // OpenRCT2's corner flags map North/East/South/West to the tile's
            // northeast/southeast/southwest/northwest vertices, respectively.
            var cNorth = new Vector3((x + 1) * tileSize, north, (y + 1) * tileSize);
            var cEast = new Vector3((x + 1) * tileSize, east, y * tileSize);
            var cSouth = new Vector3(x * tileSize, south, y * tileSize);
            var cWest = new Vector3(x * tileSize, west, (y + 1) * tileSize);

            // Keep both triangles counter-clockwise from above (positive Y normal).
            var normal1 = Vector3.Normalize(Vector3.Cross(cEast - cNorth, cWest - cNorth));
            var normal2 = Vector3.Normalize(Vector3.Cross(cSouth - cEast, cWest - cEast));
            var materialId = GetMaterialLayer(surface, x, y);
            var baseIdx = (uint)_vertices.Count;

            _vertices.Add(new TerrainVertex(cNorth, normal1, new Vector2(x + 1, y + 1), materialId));
            _vertices.Add(new TerrainVertex(cEast, normal1, new Vector2(x + 1, y), materialId));
            _vertices.Add(new TerrainVertex(cWest, normal1, new Vector2(x, y + 1), materialId));
            _vertices.Add(new TerrainVertex(cSouth, normal2, new Vector2(x, y), materialId));

            _indices.Add(baseIdx);
            _indices.Add(baseIdx + 1);
            _indices.Add(baseIdx + 2);
            _indices.Add(baseIdx + 1);
            _indices.Add(baseIdx + 3);
            _indices.Add(baseIdx + 2);

            var eastEdgeNormal = Vector3.UnitX;

            if (x + 1 < width) {
                var eastNeighbor = surfaces[y * width + x + 1];
                var neighborBase = eastNeighbor.BaseZ * baseHeightScale;
                var neighborSouth = neighborBase + eastNeighbor.SouthCornerHeight * cornerHeightScale;
                var neighborWest = neighborBase + eastNeighbor.WestCornerHeight * cornerHeightScale;

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
                var neighborBase = northNeighbor.BaseZ * baseHeightScale;
                var neighborSouth = neighborBase + northNeighbor.SouthCornerHeight * cornerHeightScale;
                var neighborEast = neighborBase + northNeighbor.EastCornerHeight * cornerHeightScale;

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

        return new TerrainMeshBuildData(
            new TerrainMeshData(
                _vertices.ToArray(),
                _indices.ToArray(),
                _skirtSections,
                _skirtTriangles,
                _flatEdgesSkipped),
            new WaterMeshData(
                _waterVertices.ToArray(),
                _waterIndices.ToArray()));
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
    int FlatEdgesSkipped);

internal sealed record TerrainMeshBuildData(TerrainMeshData Terrain, WaterMeshData Water);

internal sealed record WaterMeshData(Vector3[] Vertices, uint[] Indices);
