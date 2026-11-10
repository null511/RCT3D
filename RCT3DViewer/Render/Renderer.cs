using RCT3D.Render.Paths;
using RCT3D.Render.Paths.Meshing;
using RCT3D.Render.Terrain;
using RCT3D.Render.Scenery;
using RCT3D.Simulation;
using RCT3D.Configuration;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Serilog;

namespace RCT3D.Render;

public sealed class Renderer : IDisposable
{
    private IWindow? window;
    private VulkanRenderResources? renderResources;

    private readonly GraphicsOptionsStore graphicsOptionsStore = new();
    private ViewerUiController ui = null!;
    private readonly FlightCamera camera = new();

    private VulkanDeviceContext? deviceContext => renderResources?.DeviceContext;
    private ViewerInputController? inputController;
    private ImGuiVulkanRenderer? imguiRenderer => renderResources?.ImGuiRenderer;
    private FrameRenderer? frameRenderer;
    private FrameCoordinator? frameCoordinator;
    private ParkSceneController? parkScene;
    private PeepModeController? peepMode;
    private SimulationBridge? simulation;
    private bool framebufferResized;
    private bool exitRequested;
    private double currentDeltaTime;
    private readonly Action<double> prepareFrame;
    private readonly Action<Silk.NET.Vulkan.CommandBuffer, uint, uint> recordFrame;
    private readonly Action<uint> updateVehicleInstances;
    private readonly Action frameSubmitted;
    private readonly Action<bool> swapchainRecreated;
    private readonly Action<string> loadPark;
    private readonly Action togglePause;
    private readonly Action requestExit;
    private readonly Action togglePeepMode;
    private readonly Action<uint, float> applyShadowSettings;
    private readonly Action<float> applyFieldOfView;
    private readonly Action<GraphicsOptionsSettings> saveGraphicsOptions;

    public Renderer()
    {
        prepareFrame = PrepareFrame;
        recordFrame = RecordFrame;
        updateVehicleInstances = UpdateVehicleInstances;
        frameSubmitted = OnFrameSubmitted;
        swapchainRecreated = OnSwapchainRecreated;
        loadPark = LoadPark;
        togglePause = TogglePause;
        requestExit = RequestExit;
        togglePeepMode = TogglePeepMode;
        applyShadowSettings = ApplyShadowSettings;
        applyFieldOfView = ApplyFieldOfView;
        saveGraphicsOptions = graphicsOptionsStore.Save;
    }

    public string? LibraryPath { get; set; }
    public string? OpenRCT2Path { get; set; }
    public string? Rct2Path { get; set; }
    public string? Rct1Path { get; set; }
    public string? StartupParkPath { get; set; }
    public float TerrainSkirtFloor { get; set; } = -24.0f;


    public void Run()
    {
        var options = WindowOptions.DefaultVulkan;
        options.Size = new Vector2D<int>(1280, 720);
        options.Title = "RCT Cross-Platform 3D Real-Time Viewer";

        window = Window.Create(options);
        window.FramebufferResize += _ => {
            if (frameCoordinator is { } _frameCoordinator)
                _frameCoordinator.MarkFramebufferResized();
            else
                framebufferResized = true;
        };

        window.Load += Initialize;
        window.Render += OnRenderFrame;
        window.Update += OnUpdateFrame;
        window.Closing += OnEngineShutdown;

        try {
            window.Run();
        }
        finally {
            Dispose();
        }
    }

    public void Initialize()
    {
        var graphicsOptions = graphicsOptionsStore.Load();
        camera.FieldOfViewDegrees = graphicsOptions.FieldOfView;
        ui = new ViewerUiController(graphicsOptions);

        if (string.IsNullOrEmpty(OpenRCT2Path))
            throw new InvalidOperationException("OpenRCT2Path is undefined!");

        if (string.IsNullOrEmpty(Rct2Path))
            throw new InvalidOperationException("Rct2Path is undefined!");

        if (string.IsNullOrEmpty(Rct1Path))
            throw new InvalidOperationException("Rct1Path is undefined! Set RCT3DVIEWER_RCT1_DATA to the RCT1 data directory.");
        
        if (string.IsNullOrEmpty(LibraryPath))
            throw new InvalidOperationException("LibraryPath is undefined!");

        TerrainMaterialLibrary? terrainMaterials;
        PathMaterialLibrary? pathMaterials;
        PathAccessoryAssetLibrary? pathAccessoryAssets;
        TreeSceneryAssetLibrary? treeSceneryAssets;

        try {
            if (!Path.IsPathRooted(OpenRCT2Path))
                OpenRCT2Path = Path.Combine(AppContext.BaseDirectory, OpenRCT2Path);

            simulation = new SimulationBridge(LibraryPath, OpenRCT2Path, Rct2Path, Rct1Path);

            var textureRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Textures");
            var missingTexturePath = Path.Combine(textureRoot, "missing.png");
            terrainMaterials = TerrainMaterialLibrary.Load(textureRoot, null, missingTexturePath);
            pathMaterials = PathMaterialLibrary.Load(textureRoot, missingTexturePath);

            pathAccessoryAssets = PathAccessoryAssetLibrary.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Models"));
            var modelRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Models");
            treeSceneryAssets = TreeSceneryAssetLibrary.Load(modelRoot, Path.Combine(textureRoot, "tree"));
        }
        catch (Exception ex) {
            simulation?.Dispose();
            simulation = null;

            throw new InvalidOperationException("Failed to initialize OpenRCT2!", ex);
        }

        Log.Information("Initializing graphics subsystems via Silk.NET Vulkan");
        renderResources = new VulkanRenderResources(window!, terrainMaterials, pathMaterials, pathAccessoryAssets,
            treeSceneryAssets, graphicsOptions);
        frameRenderer = new FrameRenderer(renderResources, ui, camera);
        frameCoordinator = new FrameCoordinator(window!, renderResources);
        parkScene = new ParkSceneController(simulation!, renderResources, ui, camera, frameRenderer);
        simulation = null;

        if (framebufferResized) {
            frameCoordinator.MarkFramebufferResized();
            framebufferResized = false;
        }

        SetupInputs();

        Log.Information("Vulkan engine pipeline is operational");
        Log.Information("Hold down RIGHT-MOUSE BUTTON to look around. Use W,A,S,D to fly. Press F9 to toggle simulation pause.");

        var startupParkPath = StartupParkPath;
        if (!string.IsNullOrWhiteSpace(startupParkPath))
            parkScene.LoadPark(startupParkPath, TerrainSkirtFloor);
    }

    private void SetupInputs()
    {
        peepMode = new PeepModeController(camera, parkScene!);
        inputController = new ViewerInputController(window!, camera, peepMode,
            () => imguiRenderer?.WantCaptureMouse ?? false, () => parkScene?.TogglePause(), ui.ToggleDebug);
    }

    private void ApplyShadowSettings(uint resolution, float radius)
    {
        var currentResolution = renderResources!.DeferredTargets!.ShadowTargets!.Resolution;
        renderResources.ApplyShadowSettings(resolution, radius);
        if (currentResolution != resolution)
            frameRenderer!.InvalidateShadowMap();
    }

    private void OnUpdateFrame(double deltaTime)
    {
        if (exitRequested) {
            exitRequested = false;
            window?.Close();
            return;
        }

        inputController?.Update(deltaTime, imguiRenderer);
    }

    private void OnRenderFrame(double deltaTime)
    {
        if (parkScene == null) return;

        currentDeltaTime = deltaTime;
        frameCoordinator!.RenderFrame(deltaTime, prepareFrame, recordFrame, frameSubmitted, swapchainRecreated);
    }

    private void PrepareFrame(double activeFrameTimeMilliseconds)
    {
        parkScene!.Update();
        peepMode!.UpdateRideCamera(ui.LookAheadDistance);
        var cameraTileX = (int)MathF.Floor(camera.Position.X / WorldUnits.TileSize);
        var cameraTileY = (int)MathF.Floor(camera.Position.Z / WorldUnits.TileSize);
        var sectionMetrics = frameRenderer!.SectionMetrics;
        ui.BuildFrameUi(currentDeltaTime, activeFrameTimeMilliseconds, parkScene.GameTimeOfDayHours, parkScene.IsPaused,
            cameraTileX, cameraTileY, sectionMetrics.Visible, sectionMetrics.Total,
            frameRenderer.CameraVisibilityMetrics, frameRenderer.ShadowVisibilityMetrics,
            parkScene.GetTrackElementsAtTile(cameraTileX, cameraTileY),
            loadPark, togglePause, requestExit, peepMode!.IsEnabled, peepMode.StatusMessage, togglePeepMode,
            applyShadowSettings, applyFieldOfView, saveGraphicsOptions, deviceContext!.SupportsWireframe);
    }

    private void RecordFrame(Silk.NET.Vulkan.CommandBuffer commandBuffer, uint imageIndex, uint currentFrame) =>
        frameRenderer!.Record(commandBuffer, imageIndex, currentFrame,
            parkScene!.TerrainMesh, parkScene.PathMesh, parkScene.WaterMesh, parkScene.TrackMesh, parkScene.RideSupportMesh,
            parkScene.RideEntranceExitMesh, parkScene.RenderRegions, updateVehicleInstances);

    private void UpdateVehicleInstances(uint currentFrame) => parkScene!.UpdateVehicleCubeInstances(currentFrame);

    private void OnFrameSubmitted() => frameRenderer!.FrameSubmitted();

    private void OnSwapchainRecreated(bool extentChanged) => frameRenderer!.ResetForSwapchain(extentChanged);

    private void LoadPark(string parkPath) => parkScene!.LoadPark(parkPath, TerrainSkirtFloor);

    private void TogglePause() => parkScene!.TogglePause();

    private void RequestExit() => exitRequested = true;

    private void TogglePeepMode() => peepMode!.Toggle();

    private void ApplyFieldOfView(float fieldOfView) => camera.FieldOfViewDegrees = fieldOfView;

    private void OnEngineShutdown() => Dispose();

    public void Dispose()
    {
        peepMode?.Exit();
        peepMode = null;

        parkScene?.Dispose();
        parkScene = null;

        inputController?.Dispose();
        inputController = null;

        var _renderResources = renderResources;
        renderResources = null;
        _renderResources?.Dispose();

        var _simulation = simulation;
        simulation = null;

        try {
            _simulation?.Dispose();
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to shut down OpenRCT2");
        }
    }
}
