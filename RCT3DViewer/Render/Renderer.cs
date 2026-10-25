using RCT3D.Render.Paths;
using RCT3D.Render.Paths.Meshing;
using RCT3D.Render.Terrain;
using RCT3D.Simulation;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace RCT3D.Render;

public sealed class Renderer : IDisposable
{
    private IWindow? window;
    private VulkanRenderResources? renderResources;

    private readonly ViewerUiController ui = new();
    private readonly FlightCamera camera = new();

    private VulkanDeviceContext? deviceContext => renderResources?.DeviceContext;
    private ViewerInputController? inputController;
    private ImGuiVulkanRenderer? imguiRenderer => renderResources?.ImGuiRenderer;
    private FrameRenderer? frameRenderer;
    private FrameCoordinator? frameCoordinator;
    private ParkSceneController? parkScene;
    private SimulationBridge? simulation;
    private bool framebufferResized;
    private bool exitRequested;

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

        try {
            if (!Path.IsPathRooted(OpenRCT2Path))
                OpenRCT2Path = Path.Combine(AppContext.BaseDirectory, OpenRCT2Path);

            simulation = new SimulationBridge(LibraryPath, OpenRCT2Path, Rct2Path, Rct1Path);
            var textureRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Textures");
            var missingTexturePath = Path.Combine(textureRoot, "missing.png");
            terrainMaterials = TerrainMaterialLibrary.Load(Path.Combine(textureRoot, "Terrain"), null, missingTexturePath);
            pathMaterials = PathMaterialLibrary.Load(Path.Combine(textureRoot, "Paths"), missingTexturePath);
            pathAccessoryAssets = PathAccessoryAssetLibrary.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Models"));
        }
        catch (Exception ex) {
            simulation?.Dispose();
            simulation = null;

            throw new InvalidOperationException("Failed to initialize OpenRCT2!", ex);
        }

        Console.WriteLine("Initializing Graphics Subsystems via Silk.NET Vulkan...");
        renderResources = new VulkanRenderResources(window!, terrainMaterials, pathMaterials, pathAccessoryAssets);
        frameRenderer = new FrameRenderer(renderResources, ui, camera);
        frameCoordinator = new FrameCoordinator(window!, renderResources);
        parkScene = new ParkSceneController(simulation!, renderResources, ui, camera, frameRenderer);
        simulation = null;

        if (framebufferResized) {
            frameCoordinator.MarkFramebufferResized();
            framebufferResized = false;
        }

        SetupInputs();

        Console.WriteLine("\n=== Vulkan Engine Pipeline Fully Operational ===");
        Console.WriteLine("Hold down RIGHT-MOUSE BUTTON to look around. Use W,A,S,D to fly. Press F9 to toggle simulation pause.");

        var startupParkPath = StartupParkPath;
        if (!string.IsNullOrWhiteSpace(startupParkPath))
            parkScene.LoadPark(startupParkPath, TerrainSkirtFloor);
    }

    private void SetupInputs()
    {
        inputController = new ViewerInputController(window!, camera,
            () => imguiRenderer?.WantCaptureMouse ?? false, () => parkScene?.TogglePause(), ui.ToggleDebug);
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

        frameCoordinator!.RenderFrame(
            deltaTime,
            activeFrameTimeMilliseconds => {
                parkScene!.Update(frameCoordinator!.CurrentFrame);
                var cameraTileX = (int)MathF.Floor(camera.Position.X / WorldUnits.TileSize);
                var cameraTileY = (int)MathF.Floor(camera.Position.Z / WorldUnits.TileSize);
                ui.BuildFrameUi(deltaTime, activeFrameTimeMilliseconds, parkScene.GameTimeOfDayHours, parkScene.IsPaused,
                    cameraTileX, cameraTileY, parkScene.GetTrackElementsAtTile(cameraTileX, cameraTileY),
                    parkPath => parkScene.LoadPark(parkPath, TerrainSkirtFloor), parkScene.TogglePause,
                    () => exitRequested = true, deviceContext!.SupportsWireframe);
            },
            (commandBuffer, imageIndex, currentFrame) => frameRenderer!.Record(commandBuffer, imageIndex, currentFrame,
                parkScene.TerrainMesh, parkScene.PathMesh, parkScene.WaterMesh, parkScene.TrackMesh, parkScene.RideSupportMesh,
                parkScene.RideEntranceExitMesh),
            () => frameRenderer!.FrameSubmitted(),
            extentChanged => frameRenderer!.ResetForSwapchain(extentChanged));
    }

    private void OnEngineShutdown() => Dispose();

    public void Dispose()
    {
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
            Console.Error.WriteLine($"Failed to shut down OpenRCT2: {ex.Message}");
        }
    }
}
