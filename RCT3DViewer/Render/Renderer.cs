using Microsoft.Extensions.Logging;
using RCT3D.Configuration;
using RCT3D.Render.Park;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace RCT3D.Render;

internal sealed class Renderer : IDisposable
{
    private readonly ILogger<Renderer> logger;
    private readonly IWindow window;
    private readonly VulkanRenderResources renderResources;
    private readonly ViewerUiController ui;
    private readonly ViewerInputController inputController;
    private readonly FrameCoordinator frameCoordinator;
    private readonly MenuScreen menuScreen;
    private readonly GamePathConfiguration paths;
    private readonly ParkScreenFactory parkFactory;
    private readonly ViewerCommandQueue commands;
    private readonly ViewerStartupOptions options;

    private IViewerScreen? activeScreen;
    private ParkScreen? pendingPark;
    private Task<ParkStartupData>? startupTask;
    private string? loadingParkPath;
    private bool exitRequested;
    private bool initialFrameSubmitted;
    private bool startupParkHandled;
    private bool isDisposed;
    private bool hasRun;
    private double currentDeltaTime;

    public Renderer(
        ILogger<Renderer> logger,
        IWindow window,
        VulkanRenderResources renderResources,
        ViewerInputController inputController,
        FrameCoordinator frameCoordinator,
        MenuScreen menuScreen,
        ViewerUiController ui,
        GamePathConfiguration paths,
        ParkScreenFactory parkFactory,
        ViewerCommandQueue commands,
        ViewerStartupOptions options)
    {
        this.logger = logger;
        this.window = window;
        this.renderResources = renderResources;
        this.inputController = inputController;
        this.frameCoordinator = frameCoordinator;
        this.menuScreen = menuScreen;
        this.ui = ui;
        this.paths = paths;
        this.parkFactory = parkFactory;
        this.commands = commands;
        this.options = options;
    }

    public void Run()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        if (hasRun)
            throw new InvalidOperationException("The viewer window can only run once.");
        hasRun = true;
        window.FramebufferResize += OnFramebufferResize;
        window.Load += Initialize;
        window.Render += OnRenderFrame;
        window.Update += OnUpdateFrame;
        window.Closing += OnEngineShutdown;

        window.Run();
    }

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        logger.LogInformation("Initializing graphics subsystems via Silk.NET Vulkan");

        renderResources.Initialize();
        inputController.Initialize();
        activeScreen = menuScreen;

        if (paths.LoadError is { } error)
            ui.ShowParkLoadError(error);

        logger.LogInformation("Vulkan engine pipeline is operational; Menu is active");
    }

    private void OnUpdateFrame(double deltaTime)
    {
        if (isDisposed || !inputController.IsInitialized) return;

        inputController.Update();
        activeScreen?.Update(deltaTime);
    }

    private void OnRenderFrame(double deltaTime)
    {
        if (activeScreen is null || isDisposed) return;

        ProcessTransitions();

        if (exitRequested) {
            window.Close();
            return;
        }

        ProcessLoading();

        try {
            activeScreen.BeforeRender();
        }
        catch (Exception ex) {
            FailParkLoad(ex);
        }

        currentDeltaTime = deltaTime;
        frameCoordinator.RenderFrame(deltaTime, PrepareFrame, RecordFrame, OnFrameSubmitted, OnSwapchainRecreated, DrawPresentation);
    }

    private void ProcessTransitions()
    {
        if (!startupParkHandled && initialFrameSubmitted) {
            startupParkHandled = true;

            if (!string.IsNullOrWhiteSpace(options.StartupPark))
                commands.LoadPark(options.StartupPark);
        }

        string? requestedParkPath = null;
        var menuRequested = false;

        while (commands.TryDequeue(out var command)) {
            switch (command) {
                case ViewerCommand.Exit:
                    exitRequested = true;
                    break;
                case ViewerCommand.ReturnToMenu:
                    menuRequested = true;
                    break;
                case ViewerCommand.LoadPark load:
                    if (ui.IsParkLoading || requestedParkPath is not null)
                        logger.LogWarning("Ignoring duplicate park load request for {ParkPath}", load.Path);
                    else
                        requestedParkPath = load.Path;
                    break;
            }
        }

        if (exitRequested) return;

        if (menuRequested) {
            try {
                UnloadPark();
            }
            catch (Exception ex) {
                FailParkLoad(ex);
            }

            return;
        }

        if (requestedParkPath is not { } parkPath) return;

        try {
            UnloadPark();

            menuScreen.CloseWindows();
            loadingParkPath = parkPath;
            ui.BeginParkLoad(parkPath);
            ui.SetParkLoadStatus("Initializing OpenRCT2 and park rendering assets...");
            startupTask = parkFactory.StartAsync(paths.Effective, options.LibraryPath, ui.AppliedGraphicsOptions);
        }
        catch (Exception ex) {
            FailParkLoad(ex);
        }
    }

    private void ProcessLoading()
    {
        try {
            if (startupTask is { IsCompleted: true } completed) {
                startupTask = null;
                var startup = completed.GetAwaiter().GetResult();

                pendingPark = parkFactory.Create(startup);
                pendingPark.Load(loadingParkPath!, options.TerrainSkirtFloor);
            }

            if (pendingPark is not { } park) return;

            park.BeforeRender();
            if (park.LoadError is { } error)
                throw new InvalidOperationException(error);

            if (!park.IsReady) return;
            park.Activate(inputController.Input, renderResources.ImGuiRenderer!);

            activeScreen = park;
            pendingPark = null;
            loadingParkPath = null;
            logger.LogInformation("Park screen is active");
        }
        catch (Exception ex) {
            FailParkLoad(ex);
        }
    }

    private void FailParkLoad(Exception failure)
    {
        logger.LogError(failure, "Park session failed");

        try {
            UnloadPark();
        }
        catch (Exception cleanupError) {
            logger.LogError(cleanupError, "Failed to unload the park after an error");
            failure = new AggregateException(failure, cleanupError);
        }

        ui.ShowParkLoadError(failure.Message);
    }

    private void UnloadPark()
    {
        var previous = activeScreen;
        activeScreen = menuScreen;

        try {
            if (previous is not null && previous != menuScreen)
                previous.Dispose();
        }
        finally {
            var pending = pendingPark;
            pendingPark = null;

            try {
                pending?.Dispose();
            }
            finally {
                try {
                    if (startupTask is { } task) {
                        startupTask = null;
                        task.GetAwaiter().GetResult().Dispose();
                    }
                }
                finally {
                    loadingParkPath = null;
                    ui.UnloadPark();
                }
            }
        }
    }

    private void PrepareFrame(double activeFrameTimeMilliseconds) => activeScreen!.PrepareFrame(currentDeltaTime, activeFrameTimeMilliseconds);
    private void RecordFrame(CommandBuffer commandBuffer, uint imageIndex, uint currentFrame) => activeScreen!.Record(commandBuffer, imageIndex, currentFrame);
    private void DrawPresentation(CommandBuffer commandBuffer, uint currentFrame) => activeScreen!.DrawPresentation(commandBuffer, currentFrame);
    
    private void OnFrameSubmitted()
    {
        initialFrameSubmitted = true;
        activeScreen!.FrameSubmitted();
    }

    private void OnSwapchainRecreated(bool extentChanged)
    {
        activeScreen!.Resize(extentChanged);
        pendingPark?.Resize(extentChanged);
    }

    private void OnFramebufferResize(Vector2D<int> size) => frameCoordinator.MarkFramebufferResized();
    private void OnEngineShutdown() => exitRequested = true;

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        window.FramebufferResize -= OnFramebufferResize;
        window.Load -= Initialize;
        window.Render -= OnRenderFrame;
        window.Update -= OnUpdateFrame;
        window.Closing -= OnEngineShutdown;

        try {
            UnloadPark();
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to shut down the park session");
        }
        finally {
            activeScreen = null;
        }
    }
}
