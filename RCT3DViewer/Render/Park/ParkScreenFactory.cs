using Microsoft.Extensions.Logging;
using RCT3D.Configuration;
using RCT3D.Render.Paths;
using RCT3D.Render.Paths.Meshing;
using RCT3D.Render.Rides;
using RCT3D.Render.Scenery;
using RCT3D.Render.Terrain;
using RCT3D.Simulation;

namespace RCT3D.Render.Park;

internal sealed class ParkScreenFactory(
    VulkanRenderResources resources,
    ViewerUiController ui,
    GraphicsOptionsStore graphicsOptionsStore,
    ILogger<ParkScreenFactory> logger)
{
    public Task<ParkStartupData> StartAsync(GamePathSettings paths, string libraryPath, GraphicsOptionsSettings graphicsOptions)
    {
        return Task.Run(() => {
            var validationError = GamePathSettingsStore.Validate(paths, requireAll: true);

            if (validationError is not null)
                throw new InvalidOperationException(validationError);

            if (string.IsNullOrWhiteSpace(libraryPath) || !File.Exists(Path.GetFullPath(libraryPath)))
                throw new InvalidOperationException($"OpenRCT2 adapter DLL does not exist: '{libraryPath}'.");

            var openRCT2 = GamePathSettingsStore.ResolveDirectoryPath(paths.OpenRCT2Path!);
            var rct2 = GamePathSettingsStore.ResolveDirectoryPath(paths.Rct2Path!);
            var rct1 = GamePathSettingsStore.ResolveDirectoryPath(paths.Rct1Path!);
            RideVehicleDefinitions.Initialize(openRCT2);

            var textureRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Textures");
            var missingTexturePath = Path.Combine(textureRoot, "missing.png");
            var terrainMaterials = TerrainMaterialLibrary.Load(textureRoot, null, missingTexturePath);
            var pathMaterials = PathMaterialLibrary.Load(textureRoot, missingTexturePath);
            var modelRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Models");
            var accessories = PathAccessoryAssetLibrary.Load(modelRoot);
            var trees = TreeSceneryAssetLibrary.Load(modelRoot, Path.Combine(textureRoot, "tree"));

            logger.LogInformation("Starting the OpenRCT2 park session");
            var simulation = new SimulationBridge(libraryPath, openRCT2, rct2, rct1);
            return new ParkStartupData(simulation, terrainMaterials, pathMaterials, accessories, trees, graphicsOptions);
        });
    }

    public ParkScreen Create(ParkStartupData startup)
    {
        try {
            return new ParkScreen(graphicsOptionsStore, resources, ui, startup);
        }
        catch (Exception failure) {
            try {
                startup.Dispose();
            }
            catch (Exception shutdownFailure) {
                throw new AggregateException("Park creation and shutdown failed.", failure, shutdownFailure);
            }

            throw;
        }
    }
}
