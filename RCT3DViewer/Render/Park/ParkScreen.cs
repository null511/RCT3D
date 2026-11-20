using RCT3D.Configuration;
using Silk.NET.Input;
using Silk.NET.Vulkan;

namespace RCT3D.Render.Park;

internal sealed class ParkScreen : IViewerScreen
{
    private readonly GraphicsOptionsStore graphicsOptionsStore;

    private readonly ParkRenderResources resources;
    private readonly ViewerUiController ui;
    private readonly FlightCamera camera;
    private readonly FrameRenderer frameRenderer;
    private readonly ParkSceneController scene;
    private readonly PeepModeController peepMode;

    private ParkInputController? input;
    private (uint Resolution, float Radius)? pendingShadows;
    private bool isDisposed;

    public bool IsReady => scene.IsReady;
    public string? LoadError => scene.LoadError;


    public ParkScreen(
        GraphicsOptionsStore graphicsOptionsStore,
        VulkanRenderResources shared,
        ViewerUiController ui,
        ParkStartupData startup)
    {
        this.graphicsOptionsStore = graphicsOptionsStore;
        this.ui = ui;

        camera = new FlightCamera { FieldOfViewDegrees = startup.GraphicsOptions.FieldOfView };
        resources = new ParkRenderResources(shared, startup.TerrainMaterials, startup.PathMaterials,
            startup.PathAccessoryAssets, startup.TreeSceneryAssets, startup.GraphicsOptions);

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
            scene.TogglePause, peepMode.IsEnabled, peepMode.StatusMessage,
            peepMode.Toggle, QueueShadowSettings, ApplyFieldOfView, graphicsOptionsStore.Save, resources.DeviceContext!.SupportsWireframe);
    }

    private void QueueShadowSettings(uint resolution, float radius) => pendingShadows = (resolution, radius);

    private void ApplyFieldOfView(float fieldOfView) => camera.FieldOfViewDegrees = fieldOfView;

    public void Record(CommandBuffer commandBuffer, uint imageIndex, uint currentFrame) =>
        frameRenderer.Record(commandBuffer, imageIndex, currentFrame, scene.TerrainMesh, scene.PathMesh,
            scene.WaterMesh, scene.TrackMesh, scene.RideSupportMesh, scene.RideEntranceExitMesh,
            scene.RenderRegions, scene.UpdateVehicleInstances);

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
