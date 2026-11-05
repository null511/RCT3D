using RCT3D.Interop.Models;
using RCT3D.Render.Terrain;
using System.Numerics;

namespace RCT3D.Render.Paths.Meshing;

internal sealed class PathMeshBuilder(PathMaterialLibrary materials)
{
    private readonly Dictionary<uint, PathTypeGeometryBuilder> buildersByRailingIdHash = new();


    public PathMeshData Build(ReadOnlySpan<RCTTilePathData> paths, ReadOnlySpan<RCTTileSurfaceData> surfaces, MapOccupancyMap occupancy, int mapWidth, int mapHeight)
    {
        if (surfaces.Length < checked(mapWidth * mapHeight))
            throw new InvalidDataException("OpenRCT2 returned fewer terrain surfaces than the map dimensions require.");

        if (occupancy.Width != mapWidth || occupancy.Height != mapHeight)
            throw new InvalidDataException("The occupancy map dimensions do not match the terrain map.");

        var pathsByRegion = new Dictionary<RenderRegionCoordinate, List<RCTTilePathData>>();

        foreach (var path in paths) {
            if ((uint)path.TileX >= (uint)mapWidth || (uint)path.TileY >= (uint)mapHeight)
                throw new InvalidDataException($"OpenRCT2 returned a path outside the {mapWidth}x{mapHeight} map.");

            if (path is { IsSloped: true, SlopeDirection: > 3 })
                throw new InvalidDataException($"OpenRCT2 returned an invalid slope direction for the path at ({path.TileX}, {path.TileY}).");

            var region = new RenderRegionCoordinate(
                path.TileX / RenderRegionLayout.RegionSizeInTiles,
                path.TileY / RenderRegionLayout.RegionSizeInTiles);

            if (!pathsByRegion.TryGetValue(region, out var regionPaths)) {
                regionPaths = new List<RCTTilePathData>();
                pathsByRegion.Add(region, regionPaths);
            }

            regionPaths.Add(path);
        }

        var geometry = new PathMeshGeometry();
        var supportStations = new PathSupportStationRegistry();

        foreach (var region in RenderRegionLayout.Enumerate(mapWidth, mapHeight)) {
            if (!pathsByRegion.TryGetValue(region, out var regionPaths)) continue;

            geometry.BeginRegion(region);

            foreach (var path in regionPaths) {
                if (!buildersByRailingIdHash.TryGetValue(path.RailingsIdHash, out var builder)) {
                    builder = PathGeometryBuilderResolver.Resolve(materials, path.RailingsIdHash);
                    buildersByRailingIdHash.Add(path.RailingsIdHash, builder);
                }

                builder.AddPath(path, surfaces[path.TileY * mapWidth + path.TileX], geometry);
            }

            geometry.EndRegion();

            geometry.BeginRegion(region);
            foreach (var path in regionPaths)
                buildersByRailingIdHash[path.RailingsIdHash].AddPathSupports(
                    path, surfaces[path.TileY * mapWidth + path.TileX], occupancy, geometry, supportStations);
            geometry.EndRegion(supports: true);
        }

        return geometry.Build();
    }
}

internal sealed class PathSupportStationRegistry
{
    private readonly HashSet<(Vector3 Start, Vector3 End, uint Material)> _poles = new();

    public bool TryAddPole(Vector3 start, Vector3 end, uint material) => _poles.Add((start, end, material));
}

internal sealed record PathMeshData(TerrainVertex[] Vertices, uint[] Indices, RenderRegionIndexRange[] Regions)
{
    public RenderRegionIndexRange[] SupportRegions { get; init; } = Array.Empty<RenderRegionIndexRange>();

    public PathMeshData(TerrainVertex[] vertices, uint[] indices)
        : this(vertices, indices, Array.Empty<RenderRegionIndexRange>()) {}
}
