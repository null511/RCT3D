using RCT3D.Configuration;
using RCT3D.Render.Paths;
using RCT3D.Render.Paths.Meshing;
using RCT3D.Render.Rides;
using RCT3D.Render.Scenery;
using RCT3D.Render.Terrain;
using RCT3D.Simulation;
using Silk.NET.Input;
using Silk.NET.Vulkan;

namespace RCT3D.Render;

internal sealed class ParkScreen : IViewerScreen
{
    private readonly GraphicsOptionsStore graphicsOptionsStore;

    private readonly ParkRenderResources resources;
    private readonly ViewerUiController ui;
    private readonly FlightCamera camera;
    private readonly FrameRenderer frameRenderer;
    private readonly ParkSceneController scene;
    private readonly PeepModeController peepMode;
    private readonly Action<string> loadPark;
    private readonly Action requestExit;
    private readonly Action returnToMenu;
    //private readonly Action<GraphicsOptionsSettings> saveGraphicsOptions;

    private ParkInputController? input;
    private (uint Resolution, float Radius)? pendingShadows;
    private bool isDisposed;

    public bool IsReady => scene.IsReady;
    public string? LoadError => scene.LoadError;


    public ParkScreen(
        GraphicsOptionsStore graphicsOptionsStore,
        ViewerUiController ui,

        VulkanRenderResources shared,
        ParkStartupData startup,
        Action<string> loadPark,
        Action requestExit,
        Action returnToMenu)
        //Action<GraphicsOptionsSettings> saveGraphicsOptions)
    {
        this.graphicsOptionsStore = graphicsOptionsStore;
        this.ui = ui;

        this.loadPark = loadPark;
        this.requestExit = requestExit;
        this.returnToMenu = returnToMenu;
        //this.saveGraphicsOptions = saveGraphicsOptions;

        camera = new FlightCamera { FieldOfViewDegrees = ui.AppliedGraphicsOptions.FieldOfView };
        resources = new ParkRenderResources(shared, startup.TerrainMaterials, startup.PathMaterials,
            startup.PathAccessoryAssets, startup.TreeSceneryAssets, ui.AppliedGraphicsOptions);

        try {
            frameRenderer = new FrameRenderer(resources, ui, camera);
            scene = new ParkSceneController(startup.Simulation, resources, ui, camera, frameRenderer);
            peepMode = new PeepModeController(camera, scene);
        }
        catch {
            resources.Dispose();
            throw;
        }
    }

    public static Task<ParkStartupData> StartAsync(GamePathSettings paths, string libraryPath) => Task.Run(() => {
        var openRCT2 = ResolveDirectory(paths.OpenRCT2Path, "OPENRCT2_DATA");
        var rct2 = ResolveDirectory(paths.Rct2Path, "RCT2_PATH");
        var rct1 = ResolveDirectory(paths.Rct1Path, "RCT1_PATH");

        if (string.IsNullOrWhiteSpace(libraryPath) || !File.Exists(Path.GetFullPath(libraryPath)))
            throw new InvalidOperationException($"OpenRCT2 adapter DLL does not exist: '{libraryPath}'.");

        RideVehicleDefinitions.Initialize(openRCT2);
        var textureRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Textures");
        var missingTexturePath = Path.Combine(textureRoot, "missing.png");
        var terrainMaterials = TerrainMaterialLibrary.Load(textureRoot, null, missingTexturePath);
        var pathMaterials = PathMaterialLibrary.Load(textureRoot, missingTexturePath);
        var modelRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Models");
        var accessories = PathAccessoryAssetLibrary.Load(modelRoot);
        var trees = TreeSceneryAssetLibrary.Load(modelRoot, Path.Combine(textureRoot, "tree"));
        var simulation = new SimulationBridge(libraryPath, openRCT2, rct2, rct1);
        return new ParkStartupData(simulation, terrainMaterials, pathMaterials, accessories, trees);
    });

    private static string ResolveDirectory(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{name} is required. Set it in Menu > Configuration.");

        var path = GamePathSettingsStore.ResolveDirectoryPath(value);

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"{name} directory does not exist: '{path}'.");

        return path;
    }

    public void Load(string parkPath, float terrainSkirtFloor) => scene.LoadPark(parkPath, terrainSkirtFloor);

    public void Activate(IInputContext input, ImGuiVulkanRenderer imgui) =>
        this.input = new ParkInputController(input, camera, peepMode, imgui, scene.TogglePause, ui.ToggleDebug);

    public void Update(double deltaTime) => input?.Update(deltaTime);

    public void BeforeRender()
    {
        if (pendingShadows is { } settings) {
            pendingShadows = null;
            var resolution = resources.DeferredTargets!.ShadowTargets!.Resolution;
            resources.ApplyShadowSettings(settings.Resolution, settings.Radius);

            if (resolution != settings.Resolution)
                frameRenderer.InvalidateShadowMap();
        }

        scene.Update();

        if (scene.Simulation.SimulationFailure is { } failure)
            throw new InvalidOperationException("The OpenRCT2 simulation failed.", failure);

        if (IsReady)
            peepMode.UpdateRideCamera(ui.LookAheadDistance);
    }

    public void PrepareFrame(double deltaTime, double activeFrameTimeMilliseconds)
    {
        var tileX = (int)MathF.Floor(camera.Position.X / WorldUnits.TileSize);
        var tileY = (int)MathF.Floor(camera.Position.Z / WorldUnits.TileSize);
        var sections = frameRenderer.SectionMetrics;

        ui.BuildFrameUi(deltaTime, activeFrameTimeMilliseconds, scene.GameTimeOfDayHours, scene.IsPaused,
            tileX, tileY, sections.Visible, sections.Total, frameRenderer.CameraVisibilityMetrics,
            frameRenderer.ShadowVisibilityMetrics, scene.GetTrackElementsAtTile(tileX, tileY),
            loadPark, scene.TogglePause, requestExit, returnToMenu, peepMode.IsEnabled, peepMode.StatusMessage,
            peepMode.Toggle, QueueShadowSettings, ApplyFieldOfView, graphicsOptionsStore.Save, resources.DeviceContext!.SupportsWireframe);
    }

    private void QueueShadowSettings(uint resolution, float radius) => pendingShadows = (resolution, radius);

    private void ApplyFieldOfView(float fieldOfView) => camera.FieldOfViewDegrees = fieldOfView;

    public void Record(CommandBuffer commandBuffer, uint imageIndex, uint currentFrame) =>
        frameRenderer.Record(commandBuffer, imageIndex, currentFrame, scene.TerrainMesh, scene.PathMesh,
            scene.WaterMesh, scene.TrackMesh, scene.RideSupportMesh, scene.RideEntranceExitMesh,
            scene.RenderRegions, scene.UpdateVehicleCubeInstances);

    public void DrawPresentation(CommandBuffer commandBuffer, uint currentFrame) =>
        frameRenderer.DrawPresentation(commandBuffer, currentFrame);

    public void FrameSubmitted() => frameRenderer.FrameSubmitted();

    public void Resize(bool extentChanged)
    {
        resources.RecreateSwapchain();
        frameRenderer.ResetForSwapchain(extentChanged);
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        ui.CloseScreenWindows(ApplyFieldOfView);

        try {
            peepMode.Exit();
        }
        finally {
            input?.Dispose();
            input = null;

            try {
                scene.Dispose();
            }
            finally {
                resources.Dispose();
            }
        }
    }
}

internal sealed record ParkStartupData(
    SimulationBridge Simulation,
    TerrainMaterialLibrary TerrainMaterials,
    PathMaterialLibrary PathMaterials,
    PathAccessoryAssetLibrary PathAccessoryAssets,
    TreeSceneryAssetLibrary TreeSceneryAssets) : IDisposable
{
    public void Dispose() => Simulation.Dispose();
}
