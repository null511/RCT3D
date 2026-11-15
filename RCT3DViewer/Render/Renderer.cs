using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RCT3D.Configuration;
using RCT3D.Interop;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace RCT3D.Render;

internal sealed class Renderer : IDisposable
{
    private readonly ILogger<Renderer> logger;
    private readonly IServiceProvider provider;
    private readonly GraphicsOptionsStore graphicsOptionsStore;

    private IWindow? window;
    private VulkanRenderResources? renderResources;
    private ViewerUiController ui = null!;
    private ViewerInputController? inputController;
    private FrameCoordinator? frameCoordinator;
    private MenuScreen? menuScreen;
    private IViewerScreen? activeScreen;
    private ParkScreen? pendingPark;
    private Task<ParkStartupData>? startupTask;
    private string? loadingParkPath;
    private string? requestedParkPath;
    private bool menuRequested;
    private bool framebufferResized;
    private bool exitRequested;
    private bool initialFrameSubmitted;
    private bool startupParkHandled;
    private bool isDisposed;
    private double currentDeltaTime;

    public string? ConfigurationError { get; set; }
    public string? LibraryPath { get; set; }
    public string? OpenRCT2Path { get; set; }
    public string? Rct2Path { get; set; }
    public string? Rct1Path { get; set; }
    public string? StartupPark { get; set; }
    public float TerrainSkirtFloor { get; set; } = -24.0f;


    public Renderer(
        ILogger<Renderer> logger,
        IServiceProvider provider,
        IConfiguration configuration,
        GraphicsOptionsStore graphicsOptionsStore)
    {
        this.logger = logger;
        this.provider = provider;
        this.graphicsOptionsStore = graphicsOptionsStore;

        //ConfigurationError = configurationError;
        LibraryPath = configuration["OPENRCT2_DLL"] ?? LibOpenRCT2.DefaultLibraryPath;
        StartupPark = configuration["PARK"];

        OpenRCT2Path = configuration["OPENRCT2_DATA"];
        Rct2Path = configuration["RCT2_PATH"];
        Rct1Path = configuration["RCT1_PATH"];
    }

    public void Run()
    {
        var options = WindowOptions.DefaultVulkan;
        options.Size = new Vector2D<int>(1280, 720);
        options.Title = "RCT3D Viewer";

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
        ui = provider.GetRequiredService<ViewerUiController>();

        logger.LogInformation("Initializing graphics subsystems via Silk.NET Vulkan");

        renderResources = new VulkanRenderResources(window!);
        frameCoordinator = new FrameCoordinator(window!, renderResources);
        inputController = new ViewerInputController(window!);

        //var configuration = Configuration ?? new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        //    ["RCT1_PATH"] = Rct1Path,
        //    ["RCT2_PATH"] = Rct2Path,
        //    ["OPENRCT2_DATA"] = OpenRCT2Path
        //}).Build();

        menuScreen = provider.GetRequiredService<MenuScreen>();
        menuScreen.LoadPark = LoadPark;
        menuScreen.RequestExit = RequestExit;

        activeScreen = menuScreen;

        if (ConfigurationError is not null)
            ui.ShowParkLoadError(ConfigurationError);

        if (framebufferResized) {
            frameCoordinator.MarkFramebufferResized();
            framebufferResized = false;
        }

        logger.LogInformation("Vulkan engine pipeline is operational; Menu is active");
    }

    private void OnUpdateFrame(double deltaTime)
    {
        inputController?.Update();
        activeScreen?.Update(deltaTime);
    }

    private void OnRenderFrame(double deltaTime)
    {
        if (activeScreen is null || isDisposed) return;

        if (exitRequested) {
            window?.Close();
            return;
        }

        ProcessTransitions();
        ProcessLoading();

        try {
            activeScreen.BeforeRender();
        }
        catch (Exception ex) {
            FailParkLoad(ex);
        }

        currentDeltaTime = deltaTime;
        frameCoordinator!.RenderFrame(deltaTime, PrepareFrame, RecordFrame, OnFrameSubmitted, OnSwapchainRecreated, DrawPresentation);
    }

    private void ProcessTransitions()
    {
        if (!startupParkHandled && initialFrameSubmitted) {
            startupParkHandled = true;

            if (requestedParkPath is null && !string.IsNullOrWhiteSpace(StartupPark))
                requestedParkPath = StartupPark;
        }

        if (menuRequested) {
            menuRequested = false;

            try {
                UnloadPark();
            }
            catch (Exception ex) {
                FailParkLoad(ex);
            }
        }

        if (requestedParkPath is not { } parkPath) return;
        requestedParkPath = null;

        try {
            UnloadPark();

            menuScreen!.CloseWindows();
            loadingParkPath = parkPath;
            ui.BeginParkLoad(parkPath);
            ui.SetParkLoadStatus("Initializing OpenRCT2 and park rendering assets...");
            startupTask = ParkScreen.StartAsync(menuScreen.EffectivePaths, LibraryPath ?? LibOpenRCT2.DefaultLibraryPath);
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

                try {
                    pendingPark = new ParkScreen(
                        graphicsOptionsStore, ui,
                        renderResources!, startup, LoadPark, RequestExit, ReturnToMenu);
                }
                catch {
                    startup.Dispose();
                    throw;
                }

                pendingPark.Load(loadingParkPath!, TerrainSkirtFloor);
            }

            if (pendingPark is not { } park) return;

            park.BeforeRender();
            if (park.LoadError is { } error)
                throw new InvalidOperationException(error);

            if (!park.IsReady) return;
            park.Activate(inputController!.Input, renderResources!.ImGuiRenderer!);

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

    private void LoadPark(string parkPath)
    {
        if (ui.IsParkLoading || requestedParkPath is not null) return;
        requestedParkPath = parkPath;
    }

    private void RequestExit() => exitRequested = true;
    private void ReturnToMenu() => menuRequested = true;
    private void OnEngineShutdown() => exitRequested = true;

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        try {
            UnloadPark();
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to shut down the park session");
        }
        finally {
            try {
                inputController?.Dispose();
                inputController = null;
                menuScreen?.Dispose();
                menuScreen = null;
                activeScreen = null;
            }
            finally {
                renderResources?.Dispose();
                renderResources = null;
                window?.Dispose();
                window = null;
            }
        }
    }
}
