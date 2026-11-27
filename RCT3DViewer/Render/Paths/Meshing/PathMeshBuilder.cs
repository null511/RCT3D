using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Terrain;
using System.Numerics;

namespace RCT3D.Render.Paths.Meshing;

internal sealed class PathMeshBuilder(PathMaterialLibrary materials, PathSurfaceMaterials surfaceMaterials)
{
    private readonly Dictionary<uint, PathTypeGeometryBuilder> buildersByRailingIdHash = new();

    public static RCTTilePathData[] GetVisibleSurfaces(PathMaterialLibrary materials, ReadOnlySpan<RCTTilePathData> paths, ReadOnlySpan<RCTTileSurfaceData> surfaces, int mapWidth, int mapHeight)
    {
        if (surfaces.Length < checked(mapWidth * mapHeight))
            throw new InvalidDataException("OpenRCT2 returned fewer terrain surfaces than the map dimensions require.");

        var builders = new Dictionary<uint, PathTypeGeometryBuilder>();
        var visible = new List<RCTTilePathData>();
        foreach (var path in paths) {
            if ((uint)path.TileX >= (uint)mapWidth || (uint)path.TileY >= (uint)mapHeight)
                throw new InvalidDataException($"OpenRCT2 returned a path outside the {mapWidth}x{mapHeight} map.");

            if (!builders.TryGetValue(path.RailingsIdHash, out var builder)) {
                builder = PathGeometryBuilderResolver.Resolve(materials, path.RailingsIdHash);
                builders.Add(path.RailingsIdHash, builder);
            }

            if (builder.ShouldDrawSurface(path, surfaces[path.TileY * mapWidth + path.TileX]))
                visible.Add(path);
        }

        return visible.ToArray();
    }

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
        var surfaceGeometry = new PathSurfaceMeshGeometry();
        var railingGeometry = new PathRailingMeshGeometry();
        var supportGeometry = new PathRailingMeshGeometry();
        var supportStations = new PathSupportStationRegistry();

        foreach (var region in RenderRegionLayout.Enumerate(mapWidth, mapHeight)) {
            if (!pathsByRegion.TryGetValue(region, out var regionPaths)) continue;

            geometry.BeginRegion(region);
            surfaceGeometry.BeginRegion(region);
            railingGeometry.BeginRegion(region);

            foreach (var path in regionPaths) {
                if (!buildersByRailingIdHash.TryGetValue(path.RailingsIdHash, out var builder)) {
                    builder = PathGeometryBuilderResolver.Resolve(materials, path.RailingsIdHash);
                    buildersByRailingIdHash.Add(path.RailingsIdHash, builder);
                }

                var surface = surfaces[path.TileY * mapWidth + path.TileX];
                builder.AddPath(path, surface, geometry, surfaceGeometry, surfaceMaterials);
                builder.AddPathRailings(path, surface, railingGeometry);
            }

            geometry.EndRegion();
            surfaceGeometry.EndRegion();
            railingGeometry.EndRegion();

            supportGeometry.BeginRegion(region);
            foreach (var path in regionPaths)
                buildersByRailingIdHash[path.RailingsIdHash].AddPathSupports(
                    path, surfaces[path.TileY * mapWidth + path.TileX], occupancy, supportGeometry, supportStations);
            supportGeometry.EndRegion();
        }

        return geometry.Build() with { Surfaces = surfaceGeometry.Build(), Railings = railingGeometry.Build(), Supports = supportGeometry.Build() };
    }
}

internal sealed class PathSupportStationRegistry
{
    private readonly HashSet<(Vector3 Start, Vector3 End, TrackRgba8 Color)> _poles = new();

    public bool TryAddPole(Vector3 start, Vector3 end, TrackRgba8 color) => _poles.Add((start, end, color));
}

internal sealed record PathMeshData(TerrainVertex[] Vertices, uint[] Indices, RenderRegionIndexRange[] Regions)
{
    public PathSurfaceMeshData Surfaces { get; init; } = new(Array.Empty<PathSurfaceVertex>(), Array.Empty<uint>(), Array.Empty<RenderRegionIndexRange>());
    public PathRailingMeshData Supports { get; init; } = new(Array.Empty<TrackVertex>(), Array.Empty<uint>(), Array.Empty<RenderRegionIndexRange>());
    public PathRailingMeshData Railings { get; init; } = new(Array.Empty<TrackVertex>(), Array.Empty<uint>(), Array.Empty<RenderRegionIndexRange>());

    public PathMeshData(TerrainVertex[] vertices, uint[] indices)
        : this(vertices, indices, Array.Empty<RenderRegionIndexRange>()) {}
}

internal sealed record PathRailingMeshData(TrackVertex[] Vertices, uint[] Indices, RenderRegionIndexRange[] Regions);
