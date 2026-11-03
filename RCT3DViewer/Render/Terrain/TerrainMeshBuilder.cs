using RCT3D.Interop.Models;
using System.Numerics;

namespace RCT3D.Render.Terrain;

internal sealed class TerrainMeshBuilder(TerrainMaterialLibrary materials)
{
    private const float NormalInterpolationBoundaryFraction = 1.0f / 8.0f;
    private const float WaterPlaneVerticalOffset = -0.06f;
    private const float MatchingNormalDotThreshold = 0.999999f;

    private readonly List<TerrainVertex> _vertices = new();
    private readonly List<uint> _indices = new();
    private readonly List<WaterVertex> waterVertices = new();
    private readonly List<uint> waterIndices = new();
    private readonly List<RenderRegionIndexRange> _regions = new();
    private readonly List<RenderRegionIndexRange> waterRegions = new();

    private static readonly byte[] tileTriangleIndices = [
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
        19, 10, 14,
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
        17, 18, 16
    ];

    private static readonly byte[] flippedTileTriangleIndices = CreateFlippedTileTriangleIndices();

    private static byte[] CreateFlippedTileTriangleIndices()
    {
        var indices = new byte[tileTriangleIndices.Length];

        for (var index = 0; index < tileTriangleIndices.Length; index += 3) {
            indices[index] = tileTriangleIndices[index];
            indices[index + 1] = tileTriangleIndices[index + 2];
            indices[index + 2] = tileTriangleIndices[index + 1];
        }

        return indices;
    }

    private int _skirtSections;
    private int _skirtTriangles;
    private int _flatEdgesSkipped;


    public TerrainMeshBuildData Build(
        ReadOnlySpan<RCTTileSurfaceData> surfaces, int width, int height, float skirtFloor, TunnelLayout? tunnels = null)
    {
        _vertices.Clear();
        _indices.Clear();
        waterVertices.Clear();
        waterIndices.Clear();
        _regions.Clear();
        waterRegions.Clear();

        _skirtSections = 0;
        _skirtTriangles = 0;
        _flatEdgesSkipped = 0;

        foreach (var region in RenderRegionLayout.Enumerate(width, height)) {
            var tileBounds = RenderRegionLayout.GetTileBounds(region, width, height);

            var firstVertex = _vertices.Count;
            var firstIndex = _indices.Count;
            var firstWaterVertex = waterVertices.Count;
            var firstWaterIndex = waterIndices.Count;

            for (var y = tileBounds.TileY; y < tileBounds.TileY + tileBounds.Height; y++)
            for (var x = tileBounds.TileX; x < tileBounds.TileX + tileBounds.Width; x++) {
                var surface = surfaces[y * width + x];

                if (surface.WaterHeight > 0)
                    AddWaterTile(x, y, surface.WaterHeight * (WorldUnits.HeightPerBaseUnit / 8.0f) + WaterPlaneVerticalOffset, WorldUnits.TileSize);

                GetCornerHeights(surface, out var north, out var east, out var south, out var west);

                // OpenRCT2's corner flags map North/East/South/West to the tile's
                // northeast/southeast/southwest/northwest vertices, respectively.
                var cNorth = new Vector3((x + 1) * WorldUnits.TileSize, north, (y + 1) * WorldUnits.TileSize);
                var cEast = new Vector3((x + 1) * WorldUnits.TileSize, east, y * WorldUnits.TileSize);
                var cSouth = new Vector3(x * WorldUnits.TileSize, south, y * WorldUnits.TileSize);
                var cWest = new Vector3(x * WorldUnits.TileSize, west, (y + 1) * WorldUnits.TileSize);

                var materialId = GetMaterialLayer(surface);

                var sidingMaterialId = GetMaterialLayer(surface);
                AddTerrainTile(x, y, width, height, surfaces, north, east, south, west, materialId);
                if (tunnels is not null) {
                    foreach (var span in tunnels.GetSpans(x, y))
                        AddTunnelSpan(span, tunnels, sidingMaterialId);
                    AddTunnelStepSkirts(x, y, tunnels, sidingMaterialId);
                }

                var eastEdgeNormal = Vector3.UnitX;

                if (x + 1 < width) {
                    var eastNeighbor = surfaces[y * width + x + 1];
                    GetCornerHeights(eastNeighbor, out _, out _, out var neighborSouth, out var neighborWest);

                    AddSkirtEdge(cEast, east, cNorth, north, neighborSouth, neighborWest,
                        sidingMaterialId, GetMaterialLayer(eastNeighbor), eastEdgeNormal,
                        GetOpening(tunnels, x, y, TerrainSide.East, x + 1, y, TerrainSide.West));
                }
                else {
                    AddSkirtEdge(cEast, east, cNorth, north, skirtFloor, skirtFloor,
                        sidingMaterialId, sidingMaterialId, eastEdgeNormal,
                        GetOpening(tunnels, x, y, TerrainSide.East));
                }

                var northEdgeNormal = Vector3.UnitZ;

                if (y + 1 < height) {
                    var northNeighbor = surfaces[(y + 1) * width + x];
                    GetCornerHeights(northNeighbor, out _, out var neighborEast, out var neighborSouth, out _);

                    AddSkirtEdge(cWest, west, cNorth, north, neighborSouth, neighborEast,
                        sidingMaterialId, GetMaterialLayer(northNeighbor), northEdgeNormal,
                        GetOpening(tunnels, x, y, TerrainSide.North, x, y + 1, TerrainSide.South));
                }
                else {
                    AddSkirtEdge(cWest, west, cNorth, north, skirtFloor, skirtFloor,
                        sidingMaterialId, sidingMaterialId, northEdgeNormal,
                        GetOpening(tunnels, x, y, TerrainSide.North));
                }

                if (x == 0)
                    AddSkirtEdge(cSouth, south, cWest, west, skirtFloor, skirtFloor, sidingMaterialId, sidingMaterialId,
                        -Vector3.UnitX, GetOpening(tunnels, x, y, TerrainSide.West));

                if (y == 0)
                    AddSkirtEdge(cSouth, south, cEast, east, skirtFloor, skirtFloor, sidingMaterialId, sidingMaterialId,
                        -Vector3.UnitZ, GetOpening(tunnels, x, y, TerrainSide.South));
            }

            if (_indices.Count > firstIndex)
                _regions.Add(RenderRegionMeshRanges.Create(region, _vertices, _indices, firstVertex, firstIndex));

            if (waterIndices.Count > firstWaterIndex)
                waterRegions.Add(RenderRegionMeshRanges.Create(region, waterVertices, waterIndices, firstWaterVertex, firstWaterIndex));
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
                waterVertices.ToArray(),
                waterIndices.ToArray(),
                waterRegions.ToArray()));
    }

    private uint GetMaterialLayer(RCTTileSurfaceData surface)
    {
        return materials.GetLayer(surface.TerrainSurfaceIdHash);
    }

    private void AddTerrainTile(
        int x, int y, int width, int height, ReadOnlySpan<RCTTileSurfaceData> surfaces,
        float north, float east, float south, float west, uint materialId)
    {
        var flipDiagonal = ShouldFlipDiagonal(north, east, south, west);
        if (CanUseSimpleQuad(x, y, width, height, surfaces, north, east, south, west, out var faceNormal)) {
            var quadFirstVertex = checked((uint)_vertices.Count);
            Span<Vector2> quadCoordinates = stackalloc Vector2[] {
                new(1.0f, 1.0f),
                new(1.0f, 0.0f),
                new(0.0f, 0.0f),
                new(0.0f, 1.0f),
            };

            foreach (var coordinate in quadCoordinates) {
                var point = CreateTerrainPoint(x, y, coordinate.X, coordinate.Y, north, east, south, west, flipDiagonal);
                _vertices.Add(new TerrainVertex(
                    point, faceNormal, new Vector2(x + coordinate.X, y + coordinate.Y), materialId));
            }

            if (flipDiagonal) {
                _indices.Add(quadFirstVertex);
                _indices.Add(quadFirstVertex + 2);
                _indices.Add(quadFirstVertex + 1);
                _indices.Add(quadFirstVertex);
                _indices.Add(quadFirstVertex + 3);
                _indices.Add(quadFirstVertex + 2);
            }
            else {
                _indices.Add(quadFirstVertex);
                _indices.Add(quadFirstVertex + 3);
                _indices.Add(quadFirstVertex + 1);
                _indices.Add(quadFirstVertex + 1);
                _indices.Add(quadFirstVertex + 3);
                _indices.Add(quadFirstVertex + 2);
            }

            return;
        }

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

        var firstTriangleNormal = GetBaseTriangleNormal(north, east, south, west, true, flipDiagonal);
        var secondTriangleNormal = GetBaseTriangleNormal(north, east, south, west, false, flipDiagonal);
        var diagonalNormal = Vector3.Normalize(firstTriangleNormal + secondTriangleNormal);

        var subdividedFirstVertex = checked((uint)_vertices.Count);
        foreach (var coordinate in coordinates) {
            var u = flipDiagonal ? 1.0f - coordinate.X : coordinate.X;
            var v = coordinate.Y;

            var point = CreateTerrainPoint(x, y, u, v, north, east, south, west, flipDiagonal);
            var baseNormal = IsFirstTriangle(u, v, flipDiagonal) ? firstTriangleNormal : secondTriangleNormal;
            var normal = GetInterpolatedNormal(
                baseNormal, diagonalNormal, x, y, width, height, surfaces, north, east, south, west, u, v, flipDiagonal);

            _vertices.Add(new TerrainVertex(point, normal, new Vector2(x + u, y + v), materialId));
        }

        var tileIndices = flipDiagonal ? flippedTileTriangleIndices : tileTriangleIndices;
        foreach (var index in tileIndices)
            _indices.Add(subdividedFirstVertex + index);
    }

    private static TunnelOpening? GetOpening(
        TunnelLayout? tunnels, int tileX, int tileY, TerrainSide side,
        int neighborX = -1, int neighborY = -1, TerrainSide neighborSide = TerrainSide.West)
    {
        if (tunnels is null) return null;

        if (tunnels.TryGetOpening(tileX, tileY, side, out var opening))
            return opening;

        return neighborX >= 0 && tunnels.TryGetOpening(neighborX, neighborY, neighborSide, out opening)
            ? opening : null;
    }

    private void AddTunnelStepSkirts(int tileX, int tileY, TunnelLayout tunnels, uint materialId)
    {
        var x0 = tileX * WorldUnits.TileSize;
        var x1 = (tileX + 1) * WorldUnits.TileSize;
        var z0 = tileY * WorldUnits.TileSize;
        var z1 = (tileY + 1) * WorldUnits.TileSize;
        foreach (var skirt in tunnels.GetStepSkirts(tileX, tileY, TerrainSide.East)) {
            var start = skirt.FacesCurrentTile ? z1 : z0;
            var end = skirt.FacesCurrentTile ? z0 : z1;
            AddTunnelWallQuad(new Vector3(x1, skirt.BottomHeight, start),
                new Vector3(x1, skirt.BottomHeight, end),
                new Vector3(x1, skirt.TopHeight, end),
                new Vector3(x1, skirt.TopHeight, start),
                skirt.FacesCurrentTile ? -Vector3.UnitX : Vector3.UnitX, materialId);
        }
        foreach (var skirt in tunnels.GetStepSkirts(tileX, tileY, TerrainSide.North)) {
            var start = skirt.FacesCurrentTile ? x0 : x1;
            var end = skirt.FacesCurrentTile ? x1 : x0;
            AddTunnelWallQuad(new Vector3(start, skirt.BottomHeight, z1),
                new Vector3(end, skirt.BottomHeight, z1),
                new Vector3(end, skirt.TopHeight, z1),
                new Vector3(start, skirt.TopHeight, z1),
                skirt.FacesCurrentTile ? -Vector3.UnitZ : Vector3.UnitZ, materialId);
        }
    }

    private void AddTunnelSpan(TunnelSpan span, TunnelLayout tunnels, uint materialId)
    {
        var tileSize = WorldUnits.TileSize;
        var thickness = TunnelProfile.WallThickness;
        var tileX0 = span.TileX * tileSize;
        var tileX1 = (span.TileX + 1) * tileSize;
        var tileZ0 = span.TileY * tileSize;
        var tileZ1 = (span.TileY + 1) * tileSize;
        var x0 = tileX0 + thickness;
        var x1 = tileX1 - thickness;
        var z0 = tileZ0 + thickness;
        var z1 = tileZ1 - thickness;
        var floor = span.FloorHeight;
        var roof = span.RoofHeight - thickness;
        if (roof <= floor)
            return;

        AddTunnelQuad(
            new Vector3(tileX0, floor, tileZ0), new Vector3(tileX1, floor, tileZ0),
            new Vector3(tileX1, floor, tileZ1), new Vector3(tileX0, floor, tileZ1),
            Vector3.UnitY, materialId);

        AddTunnelQuad(
            new Vector3(tileX0, roof, tileZ0), new Vector3(tileX0, roof, tileZ1),
            new Vector3(tileX1, roof, tileZ1), new Vector3(tileX1, roof, tileZ0),
            -Vector3.UnitY, materialId);

        var connectsWest = tunnels.HasTunnelConnection(span.TileX, span.TileY, TerrainSide.West, floor, span.RoofHeight);
        var connectsNorth = tunnels.HasTunnelConnection(span.TileX, span.TileY, TerrainSide.North, floor, span.RoofHeight);
        var connectsEast = tunnels.HasTunnelConnection(span.TileX, span.TileY, TerrainSide.East, floor, span.RoofHeight);
        var connectsSouth = tunnels.HasTunnelConnection(span.TileX, span.TileY, TerrainSide.South, floor, span.RoofHeight);
        var westWallExists = !connectsWest && !tunnels.HasOpening(span.TileX, span.TileY, TerrainSide.West);
        var northWallExists = !connectsNorth && !tunnels.HasOpening(span.TileX, span.TileY, TerrainSide.North);
        var eastWallExists = !connectsEast && !tunnels.HasOpening(span.TileX, span.TileY, TerrainSide.East);
        var southWallExists = !connectsSouth && !tunnels.HasOpening(span.TileX, span.TileY, TerrainSide.South);
        var westWallZStart = southWallExists ? z0 : tileZ0;
        var westWallZEnd = northWallExists ? z1 : tileZ1;
        var eastWallZStart = westWallZStart;
        var eastWallZEnd = westWallZEnd;
        var northWallXStart = westWallExists ? x0 : tileX0;
        var northWallXEnd = eastWallExists ? x1 : tileX1;
        var southWallXStart = northWallXStart;
        var southWallXEnd = northWallXEnd;
        if (westWallExists) {
            if (tunnels.HasOutsetCorner(span, TerrainSide.West, TerrainSide.South)) westWallZStart -= thickness;
            if (tunnels.HasOutsetCorner(span, TerrainSide.West, TerrainSide.North)) westWallZEnd += thickness;
        }
        if (eastWallExists) {
            if (tunnels.HasOutsetCorner(span, TerrainSide.East, TerrainSide.South)) eastWallZStart -= thickness;
            if (tunnels.HasOutsetCorner(span, TerrainSide.East, TerrainSide.North)) eastWallZEnd += thickness;
        }
        if (northWallExists) {
            if (tunnels.HasOutsetCorner(span, TerrainSide.North, TerrainSide.West)) northWallXStart -= thickness;
            if (tunnels.HasOutsetCorner(span, TerrainSide.North, TerrainSide.East)) northWallXEnd += thickness;
        }
        if (southWallExists) {
            if (tunnels.HasOutsetCorner(span, TerrainSide.South, TerrainSide.West)) southWallXStart -= thickness;
            if (tunnels.HasOutsetCorner(span, TerrainSide.South, TerrainSide.East)) southWallXEnd += thickness;
        }
        if (westWallExists)
            AddTunnelWallQuad(new Vector3(x0, floor, westWallZStart),
                new Vector3(x0, floor, westWallZEnd),
                new Vector3(x0, roof, westWallZEnd),
                new Vector3(x0, roof, westWallZStart), Vector3.UnitX, materialId);
        if (eastWallExists)
            AddTunnelWallQuad(new Vector3(x1, floor, eastWallZEnd),
                new Vector3(x1, floor, eastWallZStart),
                new Vector3(x1, roof, eastWallZStart),
                new Vector3(x1, roof, eastWallZEnd), -Vector3.UnitX, materialId);
        if (northWallExists)
            AddTunnelWallQuad(new Vector3(northWallXStart, floor, z1),
                new Vector3(northWallXEnd, floor, z1),
                new Vector3(northWallXEnd, roof, z1),
                new Vector3(northWallXStart, roof, z1), -Vector3.UnitZ, materialId);
        if (southWallExists)
            AddTunnelWallQuad(new Vector3(southWallXEnd, floor, z0),
                new Vector3(southWallXStart, floor, z0),
                new Vector3(southWallXStart, roof, z0),
                new Vector3(southWallXEnd, roof, z0), Vector3.UnitZ, materialId);

        foreach (var side in Enum.GetValues<TerrainSide>())
            if (tunnels.TryGetOpening(span.TileX, span.TileY, side, out var opening))
                AddTunnelPortalLintel(span, opening, materialId);
    }

    private void AddTunnelPortalLintel(TunnelSpan span, TunnelOpening opening, uint materialId)
    {
        var tileSize = WorldUnits.TileSize;
        var thickness = TunnelProfile.WallThickness;

        var tileX0 = span.TileX * tileSize;
        var tileX1 = (span.TileX + 1) * tileSize;
        var tileZ0 = span.TileY * tileSize;
        var tileZ1 = (span.TileY + 1) * tileSize;

        var lower = opening.RoofHeight - thickness;
        var upper = opening.RoofHeight;
        if (upper <= lower) return;

        switch (opening.Side) {
            case TerrainSide.West:
                AddTunnelPortalQuad(new Vector3(tileX0, lower, tileZ0 + thickness),
                    new Vector3(tileX0, lower, tileZ1 - thickness),
                    new Vector3(tileX0, upper, tileZ1 - thickness),
                    new Vector3(tileX0, upper, tileZ0 + thickness),
                    Vector3.UnitX, materialId);
                break;
            case TerrainSide.North:
                AddTunnelPortalQuad(new Vector3(tileX0 + thickness, lower, tileZ1),
                    new Vector3(tileX1 - thickness, lower, tileZ1),
                    new Vector3(tileX1 - thickness, upper, tileZ1),
                    new Vector3(tileX0 + thickness, upper, tileZ1),
                    -Vector3.UnitZ, materialId);
                break;
            case TerrainSide.East:
                AddTunnelPortalQuad(new Vector3(tileX1, lower, tileZ1 - thickness),
                    new Vector3(tileX1, lower, tileZ0 + thickness),
                    new Vector3(tileX1, upper, tileZ0 + thickness),
                    new Vector3(tileX1, upper, tileZ1 - thickness),
                    -Vector3.UnitX, materialId);
                break;
            case TerrainSide.South:
                AddTunnelPortalQuad(new Vector3(tileX1 - thickness, lower, tileZ0),
                    new Vector3(tileX0 + thickness, lower, tileZ0),
                    new Vector3(tileX0 + thickness, upper, tileZ0),
                    new Vector3(tileX1 - thickness, upper, tileZ0),
                    Vector3.UnitZ, materialId);
                break;
        }
    }

    private void AddTunnelPortalQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal, uint materialId)
    {
        AddTunnelWallQuad(p0, p1, p2, p3, normal, materialId);
        AddTunnelWallQuad(p1, p0, p3, p2, -normal, materialId);
    }

    private void AddTunnelQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal, uint materialId)
    {
        var firstVertex = checked((uint)_vertices.Count);
        AddTunnelVertex(p0, normal, materialId);
        AddTunnelVertex(p1, normal, materialId);
        AddTunnelVertex(p2, normal, materialId);
        AddTunnelVertex(p3, normal, materialId);

        _indices.Add(firstVertex);
        _indices.Add(firstVertex + 1);
        _indices.Add(firstVertex + 2);
        _indices.Add(firstVertex);
        _indices.Add(firstVertex + 2);
        _indices.Add(firstVertex + 3);
    }

    private void AddTunnelWallQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal, uint materialId)
    {
        var tangent = Vector3.Normalize(Vector3.Cross(normal, Vector3.UnitY));
        var firstVertex = checked((uint)_vertices.Count);

        AddTunnelWallVertex(p0, normal, tangent, materialId);
        AddTunnelWallVertex(p1, normal, tangent, materialId);
        AddTunnelWallVertex(p2, normal, tangent, materialId);
        AddTunnelWallVertex(p3, normal, tangent, materialId);

        _indices.Add(firstVertex);
        _indices.Add(firstVertex + 1);
        _indices.Add(firstVertex + 2);
        _indices.Add(firstVertex);
        _indices.Add(firstVertex + 2);
        _indices.Add(firstVertex + 3);
    }

    private void AddTunnelVertex(Vector3 position, Vector3 normal, uint materialId) =>
        _vertices.Add(new TerrainVertex(
            position, normal, new Vector2(position.X / WorldUnits.TileSize, position.Z / WorldUnits.TileSize), materialId));

    private void AddTunnelWallVertex(Vector3 position, Vector3 normal, Vector3 tangent, uint materialId) =>
        _vertices.Add(new TerrainVertex(position, normal,
            new Vector2(Vector3.Dot(position, tangent) / WorldUnits.TileSize,
                position.Y / WorldUnits.TileSize), materialId));

    private static bool CanUseSimpleQuad(
        int x, int y, int width, int height, ReadOnlySpan<RCTTileSurfaceData> surfaces,
        float north, float east, float south, float west, out Vector3 faceNormal)
    {
        faceNormal = Vector3.Zero;
        if (north + south != east + west) return false;

        var flipDiagonal = ShouldFlipDiagonal(north, east, south, west);
        faceNormal = GetBaseTriangleNormal(north, east, south, west, true, flipDiagonal);
        var secondFaceNormal = GetBaseTriangleNormal(north, east, south, west, false, flipDiagonal);
        if (!AreNormalsMatching(faceNormal, secondFaceNormal)) return false;

        if (x > 0 && AreTilesContinuous(x, y, x - 1, y, width, surfaces)
            && !AreSharedEdgeNormalsMatching(x - 1, y, width, surfaces, faceNormal,
                new Vector2(1.0f, 0.0f), new Vector2(1.0f, 1.0f)))
            return false;

        if (x + 1 < width && AreTilesContinuous(x, y, x + 1, y, width, surfaces)
            && !AreSharedEdgeNormalsMatching(x + 1, y, width, surfaces, faceNormal,
                new Vector2(0.0f, 0.0f), new Vector2(0.0f, 1.0f)))
            return false;

        if (y > 0 && AreTilesContinuous(x, y, x, y - 1, width, surfaces)
            && !AreSharedEdgeNormalsMatching(x, y - 1, width, surfaces, faceNormal,
                new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f)))
            return false;

        if (y + 1 < height && AreTilesContinuous(x, y, x, y + 1, width, surfaces)
            && !AreSharedEdgeNormalsMatching(x, y + 1, width, surfaces, faceNormal,
                new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f)))
            return false;

        return AreConnectedCornerNormalsMatching(x, y, width, height, surfaces, faceNormal);
    }

    private static bool AreSharedEdgeNormalsMatching(
        int neighborX, int neighborY, int width, ReadOnlySpan<RCTTileSurfaceData> surfaces,
        Vector3 faceNormal, Vector2 endpointA, Vector2 endpointB)
    {
        GetCornerHeights(surfaces[neighborY * width + neighborX], out var north, out var east, out var south, out var west);
        var flipDiagonal = ShouldFlipDiagonal(north, east, south, west);
        return IsEndpointNormalMatching(north, east, south, west, flipDiagonal, endpointA, faceNormal)
            && IsEndpointNormalMatching(north, east, south, west, flipDiagonal, endpointB, faceNormal);
    }

    private static bool IsEndpointNormalMatching(
        float north, float east, float south, float west, bool flipDiagonal, Vector2 endpoint, Vector3 faceNormal)
    {
        var normal = GetDiagonalAdjustedFacetNormal(
            north, east, south, west,
            IsFirstTriangle(endpoint.X, endpoint.Y, flipDiagonal), endpoint.X, endpoint.Y, flipDiagonal);

        return AreNormalsMatching(faceNormal, normal);
    }

    private static bool AreConnectedCornerNormalsMatching(
        int x, int y, int width, int height, ReadOnlySpan<RCTTileSurfaceData> surfaces, Vector3 faceNormal)
    {
        Span<int> cornerXs = stackalloc int[] { x, x + 1, x, x + 1 };
        Span<int> cornerYs = stackalloc int[] { y, y, y + 1, y + 1 };
        Span<int> adjacentXs = stackalloc int[4];
        Span<int> adjacentYs = stackalloc int[4];
        Span<bool> visited = stackalloc bool[4];
        Span<int> queue = stackalloc int[4];

        for (var corner = 0; corner < 4; corner++) {
            adjacentXs[0] = cornerXs[corner] - 1;
            adjacentXs[1] = cornerXs[corner];
            adjacentXs[2] = cornerXs[corner] - 1;
            adjacentXs[3] = cornerXs[corner];
            adjacentYs[0] = cornerYs[corner] - 1;
            adjacentYs[1] = cornerYs[corner] - 1;
            adjacentYs[2] = cornerYs[corner];
            adjacentYs[3] = cornerYs[corner];
            visited.Clear();
            var start = -1;

            for (var index = 0; index < 4; index++) {
                if (adjacentXs[index] == x && adjacentYs[index] == y)
                    start = index;
            }

            var queueStart = 0;
            var queueEnd = 1;
            queue[0] = start;
            visited[start] = true;

            while (queueStart < queueEnd) {
                var index = queue[queueStart++];
                var tileX = adjacentXs[index];
                var tileY = adjacentYs[index];
                if (tileX >= 0 && tileX < width && tileY >= 0 && tileY < height) {
                    GetCornerHeights(surfaces[tileY * width + tileX], out var north, out var east, out var south, out var west);
                    var flipDiagonal = ShouldFlipDiagonal(north, east, south, west);
                    var localU = cornerXs[corner] - tileX;
                    var localV = cornerYs[corner] - tileY;
                    var normal = GetDiagonalAdjustedFacetNormal(
                        north, east, south, west,
                        IsFirstTriangle(localU, localV, flipDiagonal), localU, localV, flipDiagonal);
                    if (!AreNormalsMatching(faceNormal, normal))
                        return false;
                }

                for (var neighbor = 0; neighbor < 4; neighbor++) {
                    if (visited[neighbor]
                        || adjacentXs[neighbor] < 0 || adjacentXs[neighbor] >= width
                        || adjacentYs[neighbor] < 0 || adjacentYs[neighbor] >= height)
                        continue;

                    var distance = Math.Abs(adjacentXs[neighbor] - tileX) + Math.Abs(adjacentYs[neighbor] - tileY);
                    if (distance == 1 && AreTilesContinuous(
                        tileX, tileY, adjacentXs[neighbor], adjacentYs[neighbor], width, surfaces)) {
                        visited[neighbor] = true;
                        queue[queueEnd++] = neighbor;
                    }
                }
            }
        }

        return true;
    }

    private static bool AreNormalsMatching(Vector3 first, Vector3 second)
    {
        return Vector3.Dot(first, second) >= MatchingNormalDotThreshold;
    }

    private static Vector3 CreateTerrainPoint(int x, int y, float u, float v, float north, float east, float south, float west, bool flipDiagonal)
    {
        var height = TerrainSurfaceSampler.GetHeight(north, east, south, west, u, v);

        return new Vector3((x + u) * WorldUnits.TileSize, height, (y + v) * WorldUnits.TileSize);
    }

    private static bool IsFirstTriangle(float u, float v, bool flipDiagonal)
    {
        return flipDiagonal ? u >= v : u + v >= 1.0f;
    }

    private Vector3 GetInterpolatedNormal(
        Vector3 baseNormal, Vector3 diagonalNormal, int x, int y, int width, int height, ReadOnlySpan<RCTTileSurfaceData> surfaces,
        float north, float east, float south, float west, float u, float v, bool flipDiagonal)
    {
        var boundary = NormalInterpolationBoundaryFraction;
        if ((u == 0.0f || u == 1.0f) && (v == 0.0f || v == 1.0f))
            return GetConnectedCornerNormal(x, y, u, v, width, height, surfaces);

        var normal = GetDiagonalAdjustedNormal(baseNormal, diagonalNormal, u, v, flipDiagonal);

        if (u < boundary && x > 0) {
            var neighbor = surfaces[y * width + x - 1];
            GetCornerHeights(neighbor, out var neighborNorth, out var neighborEast, out var neighborSouth, out var neighborWest);
            var neighborFlipDiagonal = ShouldFlipDiagonal(neighborNorth, neighborEast, neighborSouth, neighborWest);
            if (south == neighborEast && west == neighborNorth) {
                normal += GetDiagonalAdjustedFacetNormal(
                    neighborNorth, neighborEast, neighborSouth, neighborWest,
                    IsFirstTriangle(1.0f, v, neighborFlipDiagonal), 1.0f, v, neighborFlipDiagonal)
                    * ((boundary - u) / boundary);
            }
        }

        if (u > 1.0f - boundary && x + 1 < width) {
            var neighbor = surfaces[y * width + x + 1];
            GetCornerHeights(neighbor, out var neighborNorth, out var neighborEast, out var neighborSouth, out var neighborWest);
            var neighborFlipDiagonal = ShouldFlipDiagonal(neighborNorth, neighborEast, neighborSouth, neighborWest);
            if (east == neighborSouth && north == neighborWest) {
                normal += GetDiagonalAdjustedFacetNormal(
                    neighborNorth, neighborEast, neighborSouth, neighborWest,
                    IsFirstTriangle(0.0f, v, neighborFlipDiagonal), 0.0f, v, neighborFlipDiagonal)
                    * ((boundary - (1.0f - u)) / boundary);
            }
        }

        if (v > 1.0f - boundary && y + 1 < height) {
            var neighbor = surfaces[(y + 1) * width + x];
            GetCornerHeights(neighbor, out var neighborNorth, out var neighborEast, out var neighborSouth, out var neighborWest);
            var neighborFlipDiagonal = ShouldFlipDiagonal(neighborNorth, neighborEast, neighborSouth, neighborWest);
            if (west == neighborSouth && north == neighborEast) {
                normal += GetDiagonalAdjustedFacetNormal(
                    neighborNorth, neighborEast, neighborSouth, neighborWest,
                    IsFirstTriangle(u, 0.0f, neighborFlipDiagonal), u, 0.0f, neighborFlipDiagonal)
                    * ((boundary - (1.0f - v)) / boundary);
            }
        }

        if (v < boundary && y > 0) {
            var neighbor = surfaces[(y - 1) * width + x];
            GetCornerHeights(neighbor, out var neighborNorth, out var neighborEast, out var neighborSouth, out var neighborWest);
            var neighborFlipDiagonal = ShouldFlipDiagonal(neighborNorth, neighborEast, neighborSouth, neighborWest);
            if (south == neighborWest && east == neighborNorth) {
                normal += GetDiagonalAdjustedFacetNormal(
                    neighborNorth, neighborEast, neighborSouth, neighborWest,
                    IsFirstTriangle(u, 1.0f, neighborFlipDiagonal), u, 1.0f, neighborFlipDiagonal)
                    * ((boundary - v) / boundary);
            }
        }

        return Vector3.Normalize(normal);
    }

    private static Vector3 GetDiagonalAdjustedFacetNormal(
        float north, float east, float south, float west, bool firstFacet, float u, float v, bool flipDiagonal)
    {
        var firstNormal = GetBaseTriangleNormal(north, east, south, west, true, flipDiagonal);
        var secondNormal = GetBaseTriangleNormal(north, east, south, west, false, flipDiagonal);
        var diagonalNormal = Vector3.Normalize(firstNormal + secondNormal);
        var facetNormal = firstFacet ? firstNormal : secondNormal;
        return GetDiagonalAdjustedNormal(facetNormal, diagonalNormal, u, v, flipDiagonal);
    }

    private static Vector3 GetDiagonalAdjustedNormal(
        Vector3 facetNormal, Vector3 diagonalNormal, float u, float v, bool flipDiagonal)
    {
        var distanceFromDiagonal = (flipDiagonal ? MathF.Abs(u - v) : MathF.Abs(u + v - 1.0f))
            * WorldUnits.TileSize / MathF.Sqrt(2.0f);
        var boundaryWidth = WorldUnits.TileSize * NormalInterpolationBoundaryFraction;
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
            var flipDiagonal = ShouldFlipDiagonal(north, east, south, west);
            normal += GetDiagonalAdjustedFacetNormal(
                north, east, south, west, IsFirstTriangle(localU, localV, flipDiagonal), localU, localV, flipDiagonal);

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
        TerrainSurfaceSampler.GetCornerHeights(surface, out north, out east, out south, out west);
    }

    private static bool ShouldFlipDiagonal(float north, float east, float south, float west)
    {
        return TerrainSurfaceSampler.ShouldFlipDiagonal(north, east, south, west);
    }

    private static Vector3 GetBaseTriangleNormal(
        float north, float east, float south, float west, bool firstTriangle, bool flipDiagonal = false)
    {
        var cNorth = new Vector3(WorldUnits.TileSize, north, WorldUnits.TileSize);
        var cEast = new Vector3(WorldUnits.TileSize, east, 0.0f);
        var cSouth = new Vector3(0.0f, south, 0.0f);
        var cWest = new Vector3(0.0f, west, WorldUnits.TileSize);

        if (flipDiagonal) {
            return firstTriangle
                ? Vector3.Normalize(Vector3.Cross(cEast - cNorth, cSouth - cNorth))
                : Vector3.Normalize(Vector3.Cross(cSouth - cNorth, cWest - cNorth));
        }

        return firstTriangle
            ? Vector3.Normalize(Vector3.Cross(cEast - cNorth, cWest - cNorth))
            : Vector3.Normalize(Vector3.Cross(cSouth - cEast, cWest - cEast));
    }

    private void AddWaterTile(int x, int y, float waterHeight, float tileSize)
    {
        var tileOrigin = waterVertices.Count;

        waterVertices.Add(new WaterVertex(new Vector3((x + 1) * tileSize, waterHeight, (y + 1) * tileSize), Vector3.UnitY));
        waterVertices.Add(new WaterVertex(new Vector3((x + 1) * tileSize, waterHeight, y * tileSize), Vector3.UnitY));
        waterVertices.Add(new WaterVertex(new Vector3(x * tileSize, waterHeight, (y + 1) * tileSize), Vector3.UnitY));
        waterVertices.Add(new WaterVertex(new Vector3(x * tileSize, waterHeight, y * tileSize), Vector3.UnitY));

        waterIndices.Add((uint)tileOrigin);
        waterIndices.Add((uint)tileOrigin + 1);
        waterIndices.Add((uint)tileOrigin + 2);
        waterIndices.Add((uint)tileOrigin + 1);
        waterIndices.Add((uint)tileOrigin + 3);
        waterIndices.Add((uint)tileOrigin + 2);
    }

    private void AddSkirtEdge(Vector3 edgeStart, float surfaceAStart, Vector3 edgeEnd, float surfaceAEnd,
        float surfaceBStart, float surfaceBEnd, uint materialA, uint materialB, Vector3 normalA,
        TunnelOpening? opening = null)
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
                0.0f, crossing, materialA, materialB, normalA, opening);

            AddSkirtSection(edgeStart, edgeEnd, surfaceAStart, surfaceAEnd, surfaceBStart, surfaceBEnd,
                crossing, 1.0f, materialA, materialB, normalA, opening);
        }
        else {
            AddSkirtSection(edgeStart, edgeEnd, surfaceAStart, surfaceAEnd, surfaceBStart, surfaceBEnd,
                0.0f, 1.0f, materialA, materialB, normalA, opening);
        }
    }

    private void AddSkirtSection(Vector3 edgeStart, Vector3 edgeEnd, float surfaceAStart, float surfaceAEnd,
        float surfaceBStart, float surfaceBEnd, float startT, float endT, uint materialA, uint materialB,
        Vector3 normalA, TunnelOpening? opening)
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
        if (opening is not { } hole) {
            AddSkirtBand(edgeStart, edgeEnd, startT, endT, startLow, endLow, startHigh, endHigh, materialId, normal);
            return;
        }

        var openingStart = TunnelProfile.Inset;
        var openingEnd = 1.0f - TunnelProfile.Inset;
        var leftEnd = MathF.Min(endT, openingStart);

        if (leftEnd > startT) {
            var leftAmount = (leftEnd - startT) / (endT - startT);
            AddSkirtBand(edgeStart, edgeEnd, startT, leftEnd,
                startLow, float.Lerp(startLow, endLow, leftAmount),
                startHigh, float.Lerp(startHigh, endHigh, leftAmount), materialId, normal);
        }

        var cutStart = MathF.Max(startT, openingStart);
        var cutEnd = MathF.Min(endT, openingEnd);

        if (cutEnd > cutStart) {
            var startAmount = (cutStart - startT) / (endT - startT);
            var endAmount = (cutEnd - startT) / (endT - startT);
            var lowStart = float.Lerp(startLow, endLow, startAmount);
            var lowEnd = float.Lerp(startLow, endLow, endAmount);
            var highStart = float.Lerp(startHigh, endHigh, startAmount);
            var highEnd = float.Lerp(startHigh, endHigh, endAmount);
            var lowerTopStart = Math.Clamp(hole.FloorHeight, lowStart, highStart);
            var lowerTopEnd = Math.Clamp(hole.FloorHeight, lowEnd, highEnd);
            var upperBottomStart = Math.Clamp(hole.RoofHeight, lowStart, highStart);
            var upperBottomEnd = Math.Clamp(hole.RoofHeight, lowEnd, highEnd);

            if (lowerTopStart > lowStart || lowerTopEnd > lowEnd)
                AddSkirtBand(edgeStart, edgeEnd, cutStart, cutEnd, lowStart, lowEnd, lowerTopStart, lowerTopEnd, materialId, normal);

            if (highStart > upperBottomStart || highEnd > upperBottomEnd)
                AddSkirtBand(edgeStart, edgeEnd, cutStart, cutEnd, upperBottomStart, upperBottomEnd, highStart, highEnd, materialId, normal);
        }

        var rightStart = MathF.Max(startT, openingEnd);
        if (endT > rightStart) {
            var startAmount = (rightStart - startT) / (endT - startT);

            AddSkirtBand(edgeStart, edgeEnd, rightStart, endT,
                float.Lerp(startLow, endLow, startAmount), endLow,
                float.Lerp(startHigh, endHigh, startAmount), endHigh, materialId, normal);
        }
    }

    private void AddSkirtBand(Vector3 edgeStart, Vector3 edgeEnd, float startT, float endT,
        float startLow, float endLow, float startHigh, float endHigh, uint materialId, Vector3 normal)
    {
        if (MathF.Abs(startHigh - startLow) < 0.0001f && MathF.Abs(endHigh - endLow) < 0.0001f)
            return;

        var tangent = Vector3.Normalize(Vector3.Cross(normal, Vector3.UnitY));
        var startPosition = Vector3.Lerp(edgeStart, edgeEnd, startT);
        var endPosition = Vector3.Lerp(edgeStart, edgeEnd, endT);
        var topStart = new Vector3(startPosition.X, startHigh, startPosition.Z);
        var topEnd = new Vector3(endPosition.X, endHigh, endPosition.Z);
        var bottomStart = new Vector3(startPosition.X, startLow, startPosition.Z);
        var bottomEnd = new Vector3(endPosition.X, endLow, endPosition.Z);
        var edgeUStart = Vector3.Dot(startPosition, tangent) / WorldUnits.TileSize;
        var edgeUEnd = Vector3.Dot(endPosition, tangent) / WorldUnits.TileSize;
        var baseIndex = (uint)_vertices.Count;

        _skirtSections++;
        _vertices.Add(new TerrainVertex(topStart, normal, new Vector2(edgeUStart, startHigh / WorldUnits.TileSize), materialId));
        _vertices.Add(new TerrainVertex(bottomStart, normal, new Vector2(edgeUStart, startLow / WorldUnits.TileSize), materialId));
        _vertices.Add(new TerrainVertex(topEnd, normal, new Vector2(edgeUEnd, endHigh / WorldUnits.TileSize), materialId));
        _vertices.Add(new TerrainVertex(bottomEnd, normal, new Vector2(edgeUEnd, endLow / WorldUnits.TileSize), materialId));

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

        if (Vector3.Dot(Vector3.Cross(positionB - positionA, positionC - positionA), expectedNormal) > 0.0f)
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

internal sealed record WaterMeshData(WaterVertex[] Vertices, uint[] Indices, RenderRegionIndexRange[] Regions)
{
    public static WaterMeshData Combine(WaterMeshData first, WaterMeshData second)
    {
        if (first.Indices.Length == 0) return second;
        if (second.Indices.Length == 0) return first;

        var firstRegions = first.Regions.ToDictionary(range => range.Region);
        var secondRegions = second.Regions.ToDictionary(range => range.Region);
        var vertices = new List<WaterVertex>();
        var indices = new List<uint>();
        var regions = new List<RenderRegionIndexRange>();

        foreach (var region in firstRegions.Keys.Concat(secondRegions.Keys).Distinct()
                     .OrderBy(region => region.Y).ThenBy(region => region.X)) {
            var firstVertex = vertices.Count;
            var firstIndex = indices.Count;

            AddRegion(first, firstRegions, region, vertices, indices);
            AddRegion(second, secondRegions, region, vertices, indices);

            if (indices.Count > firstIndex)
                regions.Add(RenderRegionMeshRanges.Create(region, vertices, indices, firstVertex, firstIndex));
        }

        return new WaterMeshData(vertices.ToArray(), indices.ToArray(), regions.ToArray());
    }

    private static void AddRegion(
        WaterMeshData mesh,
        IReadOnlyDictionary<RenderRegionCoordinate, RenderRegionIndexRange> ranges,
        RenderRegionCoordinate region,
        List<WaterVertex> vertices,
        List<uint> indices)
    {
        if (!ranges.TryGetValue(region, out var range)) return;

        var endIndex = checked((int)(range.FirstIndex + range.IndexCount));
        for (var index = checked((int)range.FirstIndex); index < endIndex; index++) {
            var sourceVertex = checked((int)mesh.Indices[index]);
            indices.Add(checked((uint)vertices.Count));
            vertices.Add(mesh.Vertices[sourceVertex]);
        }
    }
}
