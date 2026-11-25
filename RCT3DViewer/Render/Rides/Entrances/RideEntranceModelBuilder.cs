using RCT3D.Interop.Models;
using RCT3D.Render.Paths;
using RCT3D.Render.Terrain;
using Serilog;
using System.Numerics;

namespace RCT3D.Render.Rides.Entrances;

internal sealed class RideEntranceModelBuilder
{
    private static readonly Dictionary<(uint StationId, RCTRideEntranceType Type), string> modelPaths = new();

    public static bool HasModel(in RCTRideEntranceData entrance) =>
        modelPaths.ContainsKey((entrance.StationObjectIdHash, entrance.EntranceType));


    static RideEntranceModelBuilder()
    {
        Map("rct2.station.plain", Path.Combine("Plain", "entrance.glb"), RCTRideEntranceType.Entrance);
        Map("rct2.station.plain", Path.Combine("Plain", "exit.glb"), RCTRideEntranceType.Exit);
    }

    private static void Map(string objectId, string stationFile, RCTRideEntranceType type)
    {
        var key = (TerrainMaterialLibrary.HashObjectId(objectId), type);
        modelPaths[key] = Path.Combine("Station", stationFile);
    }

    public RideEntranceModelBatch[] Build(ReadOnlySpan<RCTRideEntranceData> entrances, int mapWidth, int mapHeight)
    {
        var models = new Dictionary<string, PathAccessoryMeshBuilder.BatchBuilder[]>();
        var missingStyles = 0;

        foreach (ref readonly var entrance in entrances) {
            if (entrance.StationObjectIdHash == 0) missingStyles++;
            if (!modelPaths.TryGetValue((entrance.StationObjectIdHash, entrance.EntranceType), out var relativePath)) continue;
            
            if ((uint)entrance.TileX >= (uint)mapWidth || (uint)entrance.TileY >= (uint)mapHeight)
                throw new InvalidDataException($"OpenRCT2 returned a ride entrance or exit outside the {mapWidth}x{mapHeight} map.");
            
            if (entrance.Direction > 3)
                throw new InvalidDataException($"OpenRCT2 returned an invalid ride entrance or exit direction {entrance.Direction}.");

            if (!models.TryGetValue(relativePath, out var batches)) {
                var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Models", relativePath);
                var model = new SharpGltfModelLoader().Load(path);
                batches = model.Meshes.Select(mesh => new PathAccessoryMeshBuilder.BatchBuilder(mesh)).ToArray();
                models.Add(relativePath, batches);
            }

            var position = new Vector3(
                (entrance.TileX + 0.5f) * WorldUnits.TileSize,
                entrance.BaseHeight * WorldUnits.HeightPerBaseUnit,
                (entrance.TileY + 0.5f) * WorldUnits.TileSize);

            // Native direction points towards the station; model forward is +Z.
            var yaw = (entrance.Direction - 1) * (MathF.PI * 0.5f);
            var transform = Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(position);

            var region = new RenderRegionCoordinate(
                entrance.TileX / RenderRegionLayout.RegionSizeInTiles,
                entrance.TileY / RenderRegionLayout.RegionSizeInTiles);

            foreach (var batch in batches)
                batch.AddInstance(region, transform);
        }

        if (missingStyles > 0)
            Log.Warning("Station style unavailable for {Count} ride entrances or exits; retaining placeholders", missingStyles);

        return models.Values.SelectMany(batches => batches).Select(batch => new RideEntranceModelBatch(
            batch.Mesh,
            batch.Regions.Select(region => new ModelRegionBatch(
                region.Key, region.Value.Bounds, region.Value.Instances.ToArray())).ToArray())).ToArray();
    }
}

internal sealed record RideEntranceModelBatch(ImportedModelMesh Mesh, ModelRegionBatch[] Regions);
