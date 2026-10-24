using RCT3D.Interop;
using RCT3D.Render.Terrain;

namespace RCT3D.Render.Paths.Meshing;

internal sealed class PathMeshBuilder(PathMaterialLibrary materials)
{
    private readonly Dictionary<uint, PathTypeGeometryBuilder> buildersByRailingIdHash = new();


    public PathMeshData Build(ReadOnlySpan<RCTTilePathData> paths, ReadOnlySpan<RCTTileSurfaceData> surfaces, int mapWidth, int mapHeight)
    {
        if (surfaces.Length < checked(mapWidth * mapHeight))
            throw new InvalidDataException("OpenRCT2 returned fewer terrain surfaces than the map dimensions require.");

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

                builder.AddPath(path, surfaces[path.TileY * mapWidth + path.TileX], geometry, supportStations);
            }

            geometry.EndRegion();
        }

        return geometry.Build();
    }
}

internal sealed class PathSupportStationRegistry
{
    private readonly HashSet<(int X, int Y, bool IsNorthSouth, byte Edge, uint RailingsIdHash)> _posts = new();
    private readonly HashSet<(int X, int Y, bool IsNorthSouth, uint RailingsIdHash)> _crossSections = new();

    public bool TryAddPost(int x, int y, bool isNorthSouth, byte edge, uint railingsIdHash) =>
        _posts.Add((x, y, isNorthSouth, edge, railingsIdHash));

    public bool TryAddCrossSection(int x, int y, bool isNorthSouth, uint railingsIdHash) =>
        _crossSections.Add((x, y, isNorthSouth, railingsIdHash));
}

internal sealed record PathMeshData(TerrainVertex[] Vertices, uint[] Indices, RenderRegionIndexRange[] Regions)
{
    public PathMeshData(TerrainVertex[] vertices, uint[] indices)
        : this(vertices, indices, Array.Empty<RenderRegionIndexRange>()) {}
}
