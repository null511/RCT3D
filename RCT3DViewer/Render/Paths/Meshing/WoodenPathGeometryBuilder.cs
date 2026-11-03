using System.Numerics;
using RCT3D.Interop.Models;
using RCT3D.Render.Terrain;

namespace RCT3D.Render.Paths.Meshing;

internal sealed class WoodenPathGeometryBuilder(PathMaterialLibrary materials) : PathTypeGeometryBuilder(materials)
{
    protected override void AddSupports(
        RCTTilePathData path, RCTTileSurfaceData surface, uint material, PathMeshGeometry geometry,
        PathSupportStationRegistry supportStations)
    {
        var tileOriginX = path.TileX * WorldUnits.TileSize;
        var tileOriginZ = path.TileY * WorldUnits.TileSize;
        var isNorthSouth = path.SupportOrientation == RCTPathSupportOrientation.NorthWestSouthEast;
        var firstEdge = isNorthSouth ? DirectionWest : DirectionSouth;
        var secondEdge = isNorthSouth ? DirectionEast : DirectionNorth;
        var startStation = (X: path.TileX, Y: path.TileY);

        var endStation = isNorthSouth
            ? (X: path.TileX, Y: path.TileY + 1)
            : (X: path.TileX + 1, Y: path.TileY);

        var firstStart = isNorthSouth
            ? new Vector2(tileOriginX + CornerInset, tileOriginZ)
            : new Vector2(tileOriginX, tileOriginZ + CornerInset);

        var secondStart = isNorthSouth
            ? new Vector2(tileOriginX + WorldUnits.TileSize - CornerInset, tileOriginZ)
            : new Vector2(tileOriginX, tileOriginZ + WorldUnits.TileSize - CornerInset);

        var firstEnd = isNorthSouth ? firstStart + new Vector2(0, WorldUnits.TileSize) : firstStart + new Vector2(WorldUnits.TileSize, 0);
        var secondEnd = isNorthSouth ? secondStart + new Vector2(0, WorldUnits.TileSize) : secondStart + new Vector2(WorldUnits.TileSize, 0);

        AddWoodenSupportStation(path, surface, material, geometry, supportStations, startStation, isNorthSouth, firstEdge, firstStart, secondEdge, secondStart);

        AddWoodenSupportStation(path, surface, material, geometry, supportStations, endStation, isNorthSouth, firstEdge, firstEnd, secondEdge, secondEnd);

        if ((path.Edges & (1 << firstEdge)) == 0)
            AddWoodenSupportBay(path, surface, material, geometry, firstStart, firstEnd);

        if ((path.Edges & (1 << secondEdge)) == 0)
            AddWoodenSupportBay(path, surface, material, geometry, secondStart, secondEnd);
    }

    protected override void AddElevatedPlatformSlab(RCTTilePathData path, PathMeshGeometry geometry)
    {
        var material = Materials.GetRailingLayer(TerrainMaterialLibrary.HashObjectId("rct2.footpath_railings.wood"));

        BuildElevatedPlatformSlab(path, geometry, material);
    }

    protected override void AddRailingEdge(RCTTilePathData path, byte direction, Vector3 start, Vector3 end, float baseHeight, uint material, PathMeshGeometry geometry)
    {
        BuildRailingEdge(path, direction, start, end, baseHeight, material, 3, geometry);
    }

    private static void AddWoodenSupportStation(
        RCTTilePathData path, RCTTileSurfaceData surface, uint material, PathMeshGeometry geometry,
        PathSupportStationRegistry supportStations, (int X, int Y) station, bool isNorthSouth,
        byte firstEdge, Vector2 firstPosition, byte secondEdge, Vector2 secondPosition)
    {
        var firstSections = GetSupportColumnSections(path, surface, firstPosition);
        var secondSections = GetSupportColumnSections(path, surface, secondPosition);

        if ((path.Edges & (1 << firstEdge)) == 0 && firstSections.Count > 0 && supportStations.TryAddPost(station.X, station.Y, isNorthSouth, firstEdge, path.RailingsIdHash))
            AddSupportColumn(firstPosition, firstSections, material, geometry);

        if ((path.Edges & (1 << secondEdge)) == 0 && secondSections.Count > 0 && supportStations.TryAddPost(station.X, station.Y, isNorthSouth, secondEdge, path.RailingsIdHash))
            AddSupportColumn(secondPosition, secondSections, material, geometry);

        if ((path.Edges & (1 << firstEdge)) != 0 || (path.Edges & (1 << secondEdge)) != 0
                                                 || firstSections.Count == 0 || secondSections.Count == 0
                                                 || !supportStations.TryAddCrossSection(station.X, station.Y, isNorthSouth, path.RailingsIdHash))
            return;

        for (var sectionIndex = 0; sectionIndex < Math.Min(firstSections.Count, secondSections.Count); sectionIndex += 2)
            AddSectionBeam(firstPosition, firstSections[sectionIndex], secondPosition, secondSections[sectionIndex], material, geometry);
    }

    private static void AddWoodenSupportBay(RCTTilePathData path, RCTTileSurfaceData surface, uint material, PathMeshGeometry geometry, Vector2 start, Vector2 end)
    {
        var startSections = GetSupportColumnSections(path, surface, start);
        var endSections = GetSupportColumnSections(path, surface, end);
        if (startSections.Count == 0 || endSections.Count == 0) return;

        var sectionCount = Math.Max(startSections.Count, endSections.Count);

        for (var sectionIndex = 0; sectionIndex < sectionCount; sectionIndex++) {
            var startSection = sectionIndex < startSections.Count
                ? startSections[sectionIndex]
                : (startSections[^1].Top, startSections[^1].Top);

            var endSection = sectionIndex < endSections.Count
                ? endSections[sectionIndex]
                : (endSections[^1].Top, endSections[^1].Top);

            AddDiagonalBraces(start, startSection, end, endSection, material, geometry);

            if ((sectionIndex & 1) == 0
                && sectionIndex + 1 < startSections.Count
                && sectionIndex + 1 < endSections.Count)
            {
                AddSectionBeam(start, startSections[sectionIndex], end, endSections[sectionIndex], material, geometry);
            }
        }
    }

    private static void AddSectionBeam(
        Vector2 start, (float Bottom, float Top) startSection,
        Vector2 end, (float Bottom, float Top) endSection,
        uint material, PathMeshGeometry geometry)
    {
        AddBeam(
            new Vector3(start.X, startSection.Top, start.Y),
            new Vector3(end.X, endSection.Top, end.Y),
            SupportHalfWidth,
            material,
            geometry);
    }
}
