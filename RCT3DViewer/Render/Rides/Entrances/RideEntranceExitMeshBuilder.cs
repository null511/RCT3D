using RCT3D.Interop;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Entrances;

internal sealed class RideEntranceExitMeshBuilder
{
    private static readonly TrackRailSpecularData Specular = new(250, 0);
    private static readonly TrackRgba8 EntranceColor = new(0, 255, 0, 255);
    private static readonly TrackRgba8 ExitColor = new(255, 0, 0, 255);

    public RideEntranceExitMeshData Build(ReadOnlySpan<RCTRideEntranceData> entrances, int mapWidth, int mapHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(mapWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(mapHeight);

        var entrancesByRegion = new Dictionary<RenderRegionCoordinate, List<RCTRideEntranceData>>();
        foreach (ref readonly var entrance in entrances) {
            if ((uint)entrance.TileX >= (uint)mapWidth || (uint)entrance.TileY >= (uint)mapHeight)
                throw new InvalidDataException($"OpenRCT2 returned a ride entrance outside the {mapWidth}x{mapHeight} map.");

            var region = new RenderRegionCoordinate(
                entrance.TileX / RenderRegionLayout.RegionSizeInTiles,
                entrance.TileY / RenderRegionLayout.RegionSizeInTiles);
            if (!entrancesByRegion.TryGetValue(region, out var regionEntrances)) {
                regionEntrances = new List<RCTRideEntranceData>();
                entrancesByRegion.Add(region, regionEntrances);
            }
            regionEntrances.Add(entrance);
        }

        var geometry = new RideSupportMeshGeometry();
        foreach (var region in RenderRegionLayout.Enumerate(mapWidth, mapHeight)) {
            if (!entrancesByRegion.TryGetValue(region, out var regionEntrances)) continue;

            geometry.BeginRegion(region);
            foreach (var entrance in regionEntrances) {
                var center = new Vector3(
                    (entrance.TileX + 0.5f) * WorldUnits.TileSize,
                    entrance.BaseHeight * WorldUnits.HeightPerBaseUnit + WorldUnits.TileSize * 0.5f,
                    (entrance.TileY + 0.5f) * WorldUnits.TileSize);
                var color = entrance.EntranceType switch {
                    RCTRideEntranceType.Entrance => EntranceColor,
                    RCTRideEntranceType.Exit => ExitColor,
                    _ => throw new InvalidDataException($"OpenRCT2 returned an invalid ride entrance type {(byte)entrance.EntranceType}.")
                };
                geometry.AddBox(center, new Vector3(WorldUnits.TileSize * 0.5f), color, Specular);
            }
            geometry.EndRegion();
        }

        var mesh = geometry.Build();
        return new RideEntranceExitMeshData(mesh.Vertices, mesh.Indices, mesh.Regions);
    }
}
