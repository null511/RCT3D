using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using Serilog;
using System.Numerics;

namespace RCT3D.Render.Rides.Entrances;

internal sealed class RideEntranceExitMeshBuilder(int mapWidth, int mapHeight)
{
    private static readonly TrackRailSpecularData Specular = new(250, 0);
    private static readonly TrackRgba8 EntranceColor = new(0, 255, 0, 255);
    private static readonly TrackRgba8 ExitColor = new(255, 0, 0, 255);
    private static readonly TrackRgba8 ShopColor = new(0, 0, 255, 255);

    private readonly record struct PlaceholderBox(int TileX, int TileY, byte BaseHeight, byte ClearanceHeight, TrackRgba8 Color);

    private readonly Dictionary<RenderRegionCoordinate, List<PlaceholderBox>> boxesByRegion = new();


    public RideEntranceExitMeshData Build(ReadOnlySpan<RCTRideEntranceData> entrances, ReadOnlySpan<RCTTrackElementData> tracks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(mapWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(mapHeight);

        foreach (ref readonly var entrance in entrances) {
            if (RideEntranceModelBuilder.HasModel(entrance)) continue;

            var color = entrance.EntranceType switch {
                RCTRideEntranceType.Entrance => EntranceColor,
                RCTRideEntranceType.Exit => ExitColor,
                _ => throw new InvalidDataException($"OpenRCT2 returned an invalid ride entrance type {(byte)entrance.EntranceType}.")
            };

            AddPlaceholder(new(entrance.TileX, entrance.TileY, entrance.BaseHeight, entrance.ClearanceHeight, color));
        }

        foreach (ref readonly var track in tracks) {
            if (track.IsShopOrFacility == 0) continue;

            AddPlaceholder(new(track.TileX, track.TileY, track.BaseHeight, track.ClearanceHeight, ShopColor));
        }

        var geometry = new RideSupportMeshGeometry();
        foreach (var region in RenderRegionLayout.Enumerate(mapWidth, mapHeight)) {
            if (!boxesByRegion.TryGetValue(region, out var regionBoxes)) continue;

            geometry.BeginRegion(region);
            foreach (var box in regionBoxes) {
                var halfHeight = (box.ClearanceHeight - box.BaseHeight) * WorldUnits.HeightPerBaseUnit * 0.5f;

                var center = new Vector3(
                    (box.TileX + 0.5f) * WorldUnits.TileSize,
                    box.BaseHeight * WorldUnits.HeightPerBaseUnit + halfHeight,
                    (box.TileY + 0.5f) * WorldUnits.TileSize);

                var halfExtents = new Vector3(WorldUnits.TileSize * 0.5f, halfHeight, WorldUnits.TileSize * 0.5f);
                geometry.AddBox(center, halfExtents, box.Color, Specular);
            }
            geometry.EndRegion();
        }

        var mesh = geometry.Build();
        return new RideEntranceExitMeshData(mesh.Vertices, mesh.Indices, mesh.Regions);
    }

    private void AddPlaceholder(PlaceholderBox box)
    {
        if ((uint)box.TileX >= (uint)mapWidth || (uint)box.TileY >= (uint)mapHeight)
            throw new InvalidDataException($"OpenRCT2 returned a building placeholder outside the {mapWidth}x{mapHeight} map.");

        if (box.ClearanceHeight <= box.BaseHeight) {
            Log.Warning(
                "Skipping building placeholder with nonpositive height at tile ({TileX}, {TileY}): base {BaseHeight}, clearance {ClearanceHeight}",
                box.TileX, box.TileY, box.BaseHeight, box.ClearanceHeight);
            return;
        }

        var region = new RenderRegionCoordinate(
            box.TileX / RenderRegionLayout.RegionSizeInTiles,
            box.TileY / RenderRegionLayout.RegionSizeInTiles);

        if (!boxesByRegion.TryGetValue(region, out var regionBoxes)) {
            regionBoxes = new List<PlaceholderBox>();
            boxesByRegion.Add(region, regionBoxes);
        }

        regionBoxes.Add(box);
    }
}
