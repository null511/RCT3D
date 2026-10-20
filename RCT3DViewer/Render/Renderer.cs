using RCT3DViewer.Render.Buffers;
using RCT3DViewer.Render.Paths;
using RCT3DViewer.Render.Paths.Meshing;
using RCT3DViewer.Render.Terrain;
using RCT3DViewer.Simulation;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using System.Diagnostics;
using System.Numerics;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace RCT3DViewer.Render;

public sealed class Renderer : IDisposable
{
    private IWindow? window;
    private VulkanDeviceContext? deviceContext;

    private Vk? _vk => deviceContext?.Api;
    private PhysicalDevice _physicalDevice => deviceContext?.PhysicalDevice ?? default;
    private Device _logicalDevice => deviceContext?.LogicalDevice ?? default;
    private Queue _graphicsQueue => deviceContext?.GraphicsQueue ?? default;
    private uint _graphicsQueueFamilyIndex => deviceContext?.GraphicsQueueFamilyIndex ?? 0;

    private SwapchainResources? _swapchainResources;
    private DeferredRenderTargets? _deferredTargets;
    private DeferredPassResources? _deferredPassResources;
    private UniformBufferResources? _uniformBufferResources;

    private SwapchainKHR _swapchain => _swapchainResources?.Swapchain ?? default;
    private Image[] _swapchainImages => _swapchainResources?.Images ?? Array.Empty<Image>();
    //private ImageView[] _swapchainImageViews => _swapchainResources?.ImageViews ?? Array.Empty<ImageView>();
    private Framebuffer[] _framebuffers => _swapchainResources?.Framebuffers ?? Array.Empty<Framebuffer>();
    //private Format _swapchainFormat => _swapchainResources?.Format ?? default;
    private Extent2D _swapchainExtent => _swapchainResources?.Extent ?? default;
    private RenderPass _renderPass => _swapchainResources?.RenderPass ?? default;
    private bool _framebufferResized;

    private TerrainGraphicsResources? _terrainGraphicsResources;
    private TerrainGraphicsResources? _pathGraphicsResources;
    private WaterGraphicsResources? _waterGraphicsResources;
    private ModelGraphicsResources[] _vehicleCubeGraphicsResources = Array.Empty<ModelGraphicsResources>();

    private PipelineLayout _pipelineLayout => _terrainGraphicsResources?.PipelineLayout ?? default;
    private Pipeline _graphicsPipeline => _terrainGraphicsResources?.Pipeline ?? default;
    private DescriptorSet _terrainDescriptorSet => _terrainGraphicsResources?.DescriptorSet ?? default;
    private Buffer _vertexBuffer => _terrainGraphicsResources?.VertexBuffer ?? default;
    private Buffer _indexBuffer => _terrainGraphicsResources?.IndexBuffer ?? default;
    private TerrainMaterialLibrary? _terrainMaterials;
    private PathMaterialLibrary? _pathMaterials;
    private PathAccessoryAssetLibrary? _pathAccessoryAssets;
    private PathAccessoryMeshBuilder? _pathAccessoryMeshBuilder;
    private readonly List<(PathAccessoryBatch Batch, ModelGraphicsResources Resources)> _pathAccessoryGraphicsResources = new();
    private CommandPool _commandPool;
    private CommandBuffer[] _commandBuffers = Array.Empty<CommandBuffer>();

    private const int MaxFramesInFlight = 2;
    private Semaphore[] _imageAvailableSemaphores = Array.Empty<Semaphore>();
    private Semaphore[] _renderFinishedSemaphores = Array.Empty<Semaphore>();
    private Fence[] _inFlightFences = Array.Empty<Fence>();
    private Fence[] _imagesInFlight = Array.Empty<Fence>();
    private uint _currentFrame;

    private ViewerInputController? _inputController;
    private readonly ViewerUiController _ui = new();
    private ImGuiVulkanRenderer? _imguiRenderer;
    private bool _exitRequested;
    private readonly FlightCamera _camera = new();
    private ulong _taaFrameIndex;
    private uint _frameCounter;
    private Vector3 _cameraPositionLast;
    private Matrix4x4 _viewProjectionLast = Matrix4x4.Identity;
    private bool _taaHistoryValid;
    private bool _taaEnabledLastFrame = true;
    private bool _sceneOutputInitialized;
    private bool _taaHistoryInitialized;
    private bool _taaResolveInitialized;
    private TerrainMeshBuilder? _terrainMeshBuilder;
    private PathMeshBuilder? _pathMeshBuilder;
    private TerrainMeshData? _terrainMesh;
    private PathMeshData? _pathMesh;
    private WaterMeshData? _waterMesh;
    private int _loadedMapWidth;
    private int _loadedMapHeight;
    private SimulationBridge? _simulation;
    private Task<ParkLoadResult>? _parkLoadTask;
    private ParkLoadResult? _pendingParkLoadResult;
    private string _parkLoadProgress = string.Empty;
    private double _activeFrameTimeMilliseconds;
    public string? LibraryPath { get; set; }
    public string? OpenRCT2Path { get; set; }
    public string? Rct2Path { get; set; }
    public string? StartupParkPath { get; set; }
    public float TerrainSkirtFloor { get; set; } = -24.0f;


    public void Run()
    {
        var options = WindowOptions.DefaultVulkan;
        options.Size = new Vector2D<int>(1280, 720);
        options.Title = "RCT Cross-Platform 3D Real-Time Viewer";

        window = Window.Create(options);
        window.FramebufferResize += _ => _framebufferResized = true;
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
        
        //if (string.IsNullOrEmpty(_parkPath))
        //    throw new InvalidOperationException("ParkPath is undefined!");

        if (string.IsNullOrEmpty(LibraryPath))
            throw new InvalidOperationException("LibraryPath is undefined!");

        try {
            if (!Path.IsPathRooted(OpenRCT2Path))
                OpenRCT2Path = Path.Combine(AppContext.BaseDirectory, OpenRCT2Path);

            _simulation = new SimulationBridge(LibraryPath, OpenRCT2Path, Rct2Path);
            _terrainMaterials = TerrainMaterialLibrary.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Textures", "Terrain"));
            _pathMaterials = PathMaterialLibrary.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Textures", "Paths"));
            _pathAccessoryAssets = PathAccessoryAssetLibrary.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Models"));
            _terrainMeshBuilder = new TerrainMeshBuilder(_terrainMaterials);
            _pathMeshBuilder = new PathMeshBuilder(_pathMaterials);
            _pathAccessoryMeshBuilder = new PathAccessoryMeshBuilder(_pathAccessoryAssets);
        }
        catch (Exception ex) {
            _simulation?.Dispose();
            _simulation = null;
            throw new InvalidOperationException("Failed to initialize OpenRCT2!", ex);
        }

        Console.WriteLine("Initializing Graphics Subsystems via Silk.NET Vulkan...");
        deviceContext = VulkanDeviceContext.Create(window!);
        _swapchainResources = new SwapchainResources(deviceContext, window!);

        _swapchainResources.CreateSwapchain();
        _swapchainResources.CreateImageViews();
        _swapchainResources.CreateDepthResources();
        _swapchainResources.CreateRenderPass();

        _deferredTargets = new DeferredRenderTargets(deviceContext, _swapchainExtent);
        _uniformBufferResources = new UniformBufferResources(deviceContext, MaxFramesInFlight);
        _deferredPassResources = new DeferredPassResources(deviceContext, _renderPass, _deferredTargets, _uniformBufferResources);
        _terrainGraphicsResources = new TerrainGraphicsResources(deviceContext, _deferredTargets.GBufferRenderPass, _uniformBufferResources.DescriptorSetLayout, _terrainMaterials!,
            shaderNameOverride: Path.Combine("defer", "deferredTerrain"), createWireframePipeline: true, colorAttachmentCount: 3);
        _terrainGraphicsResources.CreatePipeline();
        _pathGraphicsResources = new TerrainGraphicsResources(deviceContext, _deferredTargets.GBufferRenderPass, _uniformBufferResources.DescriptorSetLayout, _pathMaterials!.TextureLibrary,
            shaderNameOverride: Path.Combine("defer", "deferredTerrain"), createWireframePipeline: true, colorAttachmentCount: 3);
        _pathGraphicsResources.CreatePipeline();
        _waterGraphicsResources = new WaterGraphicsResources(deviceContext, _deferredTargets.WaterRenderPass, _uniformBufferResources.DescriptorSetLayout);
        _waterGraphicsResources.CreatePipeline();
        _swapchainResources.CreateFramebuffers();

        SetupInputs();

        CreateCommandInfrastructure();
        _terrainGraphicsResources.CreateTextureResources(_commandPool);
        _pathGraphicsResources.CreateTextureResources(_commandPool);
        var vehicleCube = VehicleCubeRenderer.CreateMesh();
        _vehicleCubeGraphicsResources = new ModelGraphicsResources[MaxFramesInFlight];
        for (var i = 0; i < _vehicleCubeGraphicsResources.Length; i++) {
            var resources = new ModelGraphicsResources(deviceContext!, _deferredTargets.GBufferRenderPass, _uniformBufferResources.DescriptorSetLayout, vehicleCube.Material, unlit: true, deferred: true);
            try {
                resources.CreatePipeline();
                resources.CreateTextureResources(_commandPool);
                resources.UploadMesh(vehicleCube);
                _vehicleCubeGraphicsResources[i] = resources;
            }
            catch {
                resources.Dispose();
                throw;
            }
        }

        CreateSyncPrimitives();

        _imguiRenderer = new ImGuiVulkanRenderer(_vk!, _physicalDevice, _logicalDevice, _graphicsQueue, _renderPass, _commandPool,
            Path.Combine(AppContext.BaseDirectory, "Shaders"), _swapchainImages.Length);

        Console.WriteLine("\n=== Vulkan Engine Pipeline Fully Operational ===");
        Console.WriteLine("Hold down RIGHT-MOUSE BUTTON to look around. Use W,A,S,D to fly.");

        var startupParkPath = StartupParkPath;
        if (!string.IsNullOrWhiteSpace(startupParkPath))
            LoadPark(startupParkPath);
    }

    private unsafe void CreateCommandInfrastructure()
    {
        CommandPoolCreateInfo poolInfo = new() { SType = StructureType.CommandPoolCreateInfo, QueueFamilyIndex = _graphicsQueueFamilyIndex, Flags = CommandPoolCreateFlags.ResetCommandBufferBit };
        _vk!.CreateCommandPool(_logicalDevice, in poolInfo, null, out _commandPool);

        CommandBufferAllocateInfo allocInfo = new() {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = (uint)_framebuffers.Length,
        };

        _commandBuffers = new CommandBuffer[_framebuffers.Length];
        fixed (CommandBuffer* cbPtr = _commandBuffers) {
            _vk.AllocateCommandBuffers(_logicalDevice, in allocInfo, cbPtr);
        }
    }

    private unsafe void CreateSyncPrimitives()
    {
        SemaphoreCreateInfo semaphoreInfo = new() {
            SType = StructureType.SemaphoreCreateInfo,
        };

        FenceCreateInfo fenceInfo = new() {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit,
        };

        var imageCount = _swapchainImages.Length;
        _imageAvailableSemaphores = new Semaphore[MaxFramesInFlight];
        _renderFinishedSemaphores = new Semaphore[imageCount];
        _inFlightFences = new Fence[MaxFramesInFlight];

        for (var i = 0; i < MaxFramesInFlight; i++)
            _vk!.CreateSemaphore(_logicalDevice, in semaphoreInfo, null, out _imageAvailableSemaphores[i]);

        for (var i = 0; i < imageCount; i++)
            _vk!.CreateSemaphore(_logicalDevice, in semaphoreInfo, null, out _renderFinishedSemaphores[i]);

        for (uint i = 0; i < MaxFramesInFlight; i++) {
            _vk!.CreateFence(_logicalDevice, in fenceInfo, null, out _inFlightFences[i]);
        }

        _imagesInFlight = new Fence[imageCount];
        for (var i = 0; i < _imagesInFlight.Length; i++)
            _imagesInFlight[i] = new Fence();
    }

    private unsafe void DestroySyncPrimitives()
    {
        if (_vk is null || _logicalDevice.Handle == 0) return;

        foreach (var semaphore in _imageAvailableSemaphores) {
            if (semaphore.Handle != 0)
                _vk.DestroySemaphore(_logicalDevice, semaphore, null);
        }

        foreach (var semaphore in _renderFinishedSemaphores) {
            if (semaphore.Handle != 0)
                _vk.DestroySemaphore(_logicalDevice, semaphore, null);
        }

        foreach (var fence in _inFlightFences) {
            if (fence.Handle != 0)
                _vk.DestroyFence(_logicalDevice, fence, null);
        }

        _imageAvailableSemaphores = Array.Empty<Semaphore>();
        _renderFinishedSemaphores = Array.Empty<Semaphore>();
        _inFlightFences = Array.Empty<Fence>();
        _imagesInFlight = Array.Empty<Fence>();
    }

    private unsafe bool RecreateSwapchain()
    {
        var framebufferSize = window!.FramebufferSize;
        if (framebufferSize.X == 0 || framebufferSize.Y == 0) return false;
        var previousExtent = _swapchainExtent;

        if (_vk!.DeviceWaitIdle(_logicalDevice) != Result.Success)
            throw new Exception("Failed to wait for the Vulkan device before recreating the swapchain.");

        DestroySyncPrimitives();
        _vk.DestroyCommandPool(_logicalDevice, _commandPool, null);
        _commandPool = default;
        _commandBuffers = Array.Empty<CommandBuffer>();
        _swapchainResources!.Recreate();
        _frameCounter = 0;
        _deferredPassResources?.Dispose();
        _deferredTargets?.Resize(_swapchainExtent);
        if (previousExtent.Width != _swapchainExtent.Width || previousExtent.Height != _swapchainExtent.Height) {
            _taaHistoryValid = false;
            _sceneOutputInitialized = false;
            _taaHistoryInitialized = false;
            _taaResolveInitialized = false;
        }
        _deferredPassResources = new DeferredPassResources(deviceContext!, _renderPass, _deferredTargets!, _uniformBufferResources!);
        CreateCommandInfrastructure();
        CreateSyncPrimitives();
        _imguiRenderer?.ResizeBuffers(_swapchainImages.Length);
        _currentFrame = 0;
        _framebufferResized = false;
        return true;
    }

    private void SetupInputs()
    {
        _inputController = new ViewerInputController(window!, _camera, () => _imguiRenderer?.WantCaptureMouse ?? false);
    }

    private void OnUpdateFrame(double deltaTime)
    {
        if (_exitRequested) {
            _exitRequested = false;
            window?.Close();
            return;
        }

        _inputController?.Update(deltaTime, _imguiRenderer);
    }

    private void LoadPark(string parkPath)
    {
        if (_ui.IsParkLoading || _parkLoadTask is not null || _pendingParkLoadResult is not null)
            return;

        var simulation = _simulation ?? throw new InvalidOperationException("OpenRCT2 is not initialized.");
        _ui.BeginParkLoad(parkPath);
        Volatile.Write(ref _parkLoadProgress, "Loading park file...");
        _parkLoadTask = BuildParkMeshesAsync(simulation, parkPath);
    }

    private async Task<ParkLoadResult> BuildParkMeshesAsync(SimulationBridge simulation, string parkPath)
    {
        try {
            var snapshot = await simulation.LoadParkAsync(parkPath, progress => Volatile.Write(ref _parkLoadProgress, progress));

            Volatile.Write(ref _parkLoadProgress, "Generating terrain, water, path, and accessory meshes...");
            var meshData = await Task.Run(() => (
                Terrain: _terrainMeshBuilder!.Build(snapshot.SurfaceData, snapshot.Width, snapshot.Height, TerrainSkirtFloor),
                Paths: _pathMeshBuilder!.Build(snapshot.PathData, snapshot.SurfaceData, snapshot.Width, snapshot.Height),
                PathAccessories: _pathAccessoryMeshBuilder!.Build(snapshot.PathData, snapshot.Width, snapshot.Height)));

            return new ParkLoadResult(
                parkPath, snapshot.Width, snapshot.Height,
                meshData.Terrain.Terrain, meshData.Terrain.Water, meshData.Paths, meshData.PathAccessories);
        }
        catch (Exception ex) {
            throw new InvalidOperationException($"Failed to load '{parkPath}': {ex.Message}", ex);
        }
    }

    private sealed record ParkLoadResult(
        string ParkPath, int Width, int Height, TerrainMeshData Terrain, WaterMeshData Water, PathMeshData Paths,
        PathAccessoryBatch[] PathAccessories);

    private void UpdateParkLoad()
    {
        if (_parkLoadTask is { IsCompleted: true } completedTask) {
            _parkLoadTask = null;

            try {
                _pendingParkLoadResult = completedTask.GetAwaiter().GetResult();
                _ui.SetParkLoadStatus("Uploading terrain, water, path, and accessory meshes...");
                return;
            }
            catch (Exception ex) {
                ClearParkState(ex.Message);
            }
        }

        if (_pendingParkLoadResult is { } result) {
            try {
                if (_vk!.DeviceWaitIdle(_logicalDevice) != Result.Success)
                    throw new InvalidOperationException("Could not wait for the Vulkan device before replacing terrain buffers.");

                _terrainGraphicsResources!.ClearMesh();
                _terrainGraphicsResources.UploadMesh(result.Terrain);
                _pathGraphicsResources!.ClearMesh();
                _pathGraphicsResources.UploadMesh(result.Paths);
                _waterGraphicsResources!.UploadMesh(result.Water);
                DisposePathAccessoryResources();
                foreach (var batch in result.PathAccessories) {
                    var resources = new ModelGraphicsResources(deviceContext!, _deferredTargets!.GBufferRenderPass, _uniformBufferResources!.DescriptorSetLayout, batch.Material, deferred: true);
                    try {
                        resources.CreatePipeline();
                        resources.CreateTextureResources(_commandPool);
                        resources.UploadMesh(batch.Mesh);
                        resources.UploadInstances(batch.Regions);
                        _pathAccessoryGraphicsResources.Add((batch, resources));
                    }
                    catch {
                        resources.Dispose();
                        throw;
                    }
                }
                _terrainMesh = result.Terrain;
                _pathMesh = result.Paths;
                _waterMesh = result.Water;
                _loadedMapWidth = result.Width;
                _loadedMapHeight = result.Height;
                _camera.Position = new Vector3(result.Width * 1.5f, 120.0f, -result.Height * 1.5f);
                _taaHistoryValid = false;
                _pendingParkLoadResult = null;
                _ui.CompleteParkLoad(result.ParkPath);
            }
            catch (Exception ex) {
                ClearParkState($"Failed to upload meshes for '{result.ParkPath}': {ex.Message}");
            }
        }

        if (_parkLoadTask is not null)
            _ui.SetParkLoadStatus(Volatile.Read(ref _parkLoadProgress));
    }

    private void ClearParkState(string failure)
    {
        _terrainMesh = null;
        _pathMesh = null;
        _waterMesh = null;
        _loadedMapWidth = 0;
        _loadedMapHeight = 0;
        _taaHistoryValid = false;
        _pendingParkLoadResult = null;
        _parkLoadTask = null;

        try {
            if (_vk is not null && _logicalDevice.Handle != 0) {
                if (_vk.DeviceWaitIdle(_logicalDevice) != Result.Success)
                    throw new InvalidOperationException("Could not wait for the Vulkan device before clearing park meshes.");

                DisposePathAccessoryResources();
                _terrainGraphicsResources?.ClearMesh();
                _pathGraphicsResources?.ClearMesh();
                _waterGraphicsResources?.ClearMesh();
                ClearVehicleCubeInstances();
            }
        }
        catch (Exception clearException) {
            failure += $" Park graphics could not be fully cleared: {clearException.Message}";
        }

        _ui.ClearParkAfterLoadFailure(failure);
    }

    private void UpdateVehicleCubeInstances()
    {
        if (_vehicleCubeGraphicsResources.Length == 0) return;

        var snapshots = _parkLoadTask is null && _pendingParkLoadResult is null
            ? _simulation?.VehicleSnapshots : null;

        var instances = VehicleCubeRenderer.BuildInstances(snapshots, _loadedMapWidth, _loadedMapHeight);
        _vehicleCubeGraphicsResources[(int)_currentFrame].UploadInstances(instances);
    }

    private void ClearVehicleCubeInstances()
    {
        foreach (var resources in _vehicleCubeGraphicsResources)
            resources.UploadInstances(Array.Empty<ModelRegionBatch>());
    }

    private unsafe void OnRenderFrame(double deltaTime)
    {
        if (window!.FramebufferSize.X == 0 || window.FramebufferSize.Y == 0) return;
        if (_framebufferResized && !RecreateSwapchain()) return;

        var frameStart = Stopwatch.GetTimestamp();
        long synchronizationTicks = 0;
        uint imageIndex = 0;
        var synchronizationStart = Stopwatch.GetTimestamp();
        _vk!.WaitForFences(_logicalDevice, 1, in _inFlightFences[_currentFrame], true, ulong.MaxValue);
        synchronizationTicks += Stopwatch.GetTimestamp() - synchronizationStart;
        synchronizationStart = Stopwatch.GetTimestamp();
        var acquireResult = deviceContext!.SwapchainExtension!.AcquireNextImage(_logicalDevice, _swapchain, ulong.MaxValue, _imageAvailableSemaphores[_currentFrame], default, ref imageIndex);
        synchronizationTicks += Stopwatch.GetTimestamp() - synchronizationStart;

        if (acquireResult == Result.ErrorOutOfDateKhr) {
            RecreateSwapchain();
            return;
        }

        if (acquireResult != Result.Success && acquireResult != Result.SuboptimalKhr)
            throw new Exception($"Failed to acquire a swapchain image: {acquireResult}.");

        var recreateAfterPresent = acquireResult == Result.SuboptimalKhr;

        if (_imagesInFlight[imageIndex].Handle != 0) {
            synchronizationStart = Stopwatch.GetTimestamp();
            _vk.WaitForFences(_logicalDevice, 1, in _imagesInFlight[imageIndex], true, ulong.MaxValue);
            synchronizationTicks += Stopwatch.GetTimestamp() - synchronizationStart;
        }

        _imagesInFlight[imageIndex] = _inFlightFences[_currentFrame];
        var cmd = _commandBuffers[imageIndex];
        _imguiRenderer?.BeginFrame((int)_swapchainExtent.Width, (int)_swapchainExtent.Height, (float)deltaTime);

        UpdateParkLoad();
        UpdateVehicleCubeInstances();

        _ui.BuildFrameUi(deltaTime, _activeFrameTimeMilliseconds, _simulation?.GameTimeOfDayHours, LoadPark, () => _exitRequested = true, deviceContext!.SupportsWireframe);
        if (_taaEnabledLastFrame != _ui.TaaEnabled) {
            _taaHistoryValid = false;
            _taaEnabledLastFrame = _ui.TaaEnabled;
        }

        _vk.ResetCommandBuffer(cmd, 0);

        CommandBufferBeginInfo beginInfo = new() {
            SType = StructureType.CommandBufferBeginInfo,
        };

        _vk.BeginCommandBuffer(cmd, in beginInfo);

        var clearValues = stackalloc ClearValue[4];
        clearValues[0] = new ClearValue { Color = new ClearColorValue(0.4f, 0.6f, 0.9f, 1.0f) };
        clearValues[1] = new ClearValue { Color = new ClearColorValue(0.0f, 0.0f, 0.0f, 0.0f) };
        clearValues[2] = new ClearValue { Color = new ClearColorValue(0.0f, 0.0f, 0.0f, 0.0f) };
        clearValues[3] = new ClearValue {
            DepthStencil = new ClearDepthStencilValue {
                Depth = 1.0f,
                Stencil = 0,
            },
        };

        RenderPassBeginInfo renderPassInfo = new() {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _deferredTargets!.GBufferRenderPass,
            Framebuffer = _deferredTargets.GBufferFramebuffer,
            RenderArea = new Rect2D(new Offset2D(0, 0), _swapchainExtent),
            ClearValueCount = 4,
            PClearValues = clearValues
        };

        _vk.CmdBeginRenderPass(cmd, in renderPassInfo, SubpassContents.Inline);
        var terrainPipeline = _ui.WireframeEnabled ? _terrainGraphicsResources!.WireframePipeline : _graphicsPipeline;
        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, terrainPipeline);

        Viewport viewport = new() {
            X = 0.0f,
            Y = 0.0f,
            Width = _swapchainExtent.Width,
            Height = _swapchainExtent.Height,
            MinDepth = 0.0f,
            MaxDepth = 1.0f
        };

        Rect2D scissor = new() {
            Offset = new Offset2D(0, 0),
            Extent = _swapchainExtent,
        };

        _vk.CmdSetViewport(cmd, 0, 1, &viewport);
        _vk.CmdSetScissor(cmd, 0, 1, &scissor);
        var terrainDescriptorSet = _terrainDescriptorSet;
        var uniformDescriptorSet = _uniformBufferResources!.GetDescriptorSet(_currentFrame);
        var terrainDescriptorSets = stackalloc DescriptorSet[2];
        terrainDescriptorSets[0] = uniformDescriptorSet;
        terrainDescriptorSets[1] = terrainDescriptorSet;
        _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _pipelineLayout, 0, 2, terrainDescriptorSets, 0, null);

        var aspect = (float)_swapchainExtent.Width / _swapchainExtent.Height;
        // Row-vector convention in shader: clip = position * viewProjection
        var viewProjection = _camera.GetViewMatrix() * _camera.GetProjectionMatrix(aspect);
        if (!Matrix4x4.Invert(viewProjection, out var inverseViewProjection))
            throw new InvalidOperationException("The camera view-projection matrix is not invertible.");

        CalculateSunDirection(out var sunElevation, out var sunDirection);

        var viewFrustum = ViewFrustum.FromViewProjection(viewProjection);
        var ambientLight = 0.15f + 0.15f * MathF.Max(sunElevation, 0.0f);
        var cameraPosition = _camera.Position;

        var renderDimensions = new Vector2D<uint>(_swapchainExtent.Width, _swapchainExtent.Height);
        CameraUniforms cameraUniforms = new() {
            CameraPosition = new Vector4(cameraPosition, 1.0f),
            ViewProjection = viewProjection,
            ViewProjectionInverse = inverseViewProjection,
        };

        var taaJitter = _ui.TaaEnabled ? GetR2Jitter(_taaFrameIndex + 1) : Vector2.Zero;
        TemporalUniforms temporalUniforms = new() {
            CameraPositionLast = new Vector4(_frameCounter == 0 ? cameraPosition : _cameraPositionLast, 1.0f),
            ViewProjectionLast = _frameCounter == 0 ? viewProjection : _viewProjectionLast,
        };

        SceneUniforms sceneUniforms = new() {
            RenderDimensions = renderDimensions,
            TaaJitter = taaJitter,
            SunDirection = new Vector4(sunDirection, 0.0f),
            Lighting = new Vector4(ambientLight, 1.25f, 0.0f, 0.0f),
            FrameCounter = _frameCounter,
            TaaSharpen = _ui.Sharpening,
            MipBias = _ui.MipBias,
            Padding = 0.0f,
        };

        _uniformBufferResources!.Update(_currentFrame, cameraUniforms, temporalUniforms, sceneUniforms);

        ulong offset = 0;
        if (_terrainMesh is { Vertices.Length: > 0, Indices.Length: > 0 } terrainMesh && _vertexBuffer.Handle != 0 && _indexBuffer.Handle != 0) {
            var vBuf = _vertexBuffer;
            _vk.CmdBindVertexBuffers(cmd, 0, 1, in vBuf, in offset);
            _vk.CmdBindIndexBuffer(cmd, _indexBuffer, 0, IndexType.Uint32);
            foreach (var region in terrainMesh.Regions) {
                if (viewFrustum.Intersects(region.Bounds))
                    _vk.CmdDrawIndexed(cmd, region.IndexCount, 1, region.FirstIndex, 0, 0);
            }
        }

        if (_vehicleCubeGraphicsResources.Length > 0) {
            var vehicleResources = _vehicleCubeGraphicsResources[(int)_currentFrame];
            foreach (var region in vehicleResources.RegionBatches) {
                if (viewFrustum.Intersects(region.Bounds))
                    vehicleResources.DrawInstanced(cmd, uniformDescriptorSet, region);
            }
        }

        if (_pathMesh is { Vertices.Length: > 0, Indices.Length: > 0 } pathMesh
            && _pathGraphicsResources is { IndexCount: > 0 } pathResources) {
            var pathPipeline = _ui.WireframeEnabled ? pathResources.WireframePipeline : pathResources.Pipeline;
            _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, pathPipeline);
            var pathDescriptorSet = pathResources.DescriptorSet;
            var pathDescriptorSets = stackalloc DescriptorSet[2];
            pathDescriptorSets[0] = uniformDescriptorSet;
            pathDescriptorSets[1] = pathDescriptorSet;
            _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, pathResources.PipelineLayout, 0, 2, pathDescriptorSets, 0, null);
            var pathVertexBuffer = pathResources.VertexBuffer;
            _vk.CmdBindVertexBuffers(cmd, 0, 1, in pathVertexBuffer, in offset);
            _vk.CmdBindIndexBuffer(cmd, pathResources.IndexBuffer, 0, IndexType.Uint32);
            foreach (var region in pathMesh.Regions) {
                if (viewFrustum.Intersects(region.Bounds))
                    _vk.CmdDrawIndexed(cmd, region.IndexCount, 1, region.FirstIndex, 0, 0);
            }
        }

        if (_terrainMesh is not null) {
            foreach (var (_, resources) in _pathAccessoryGraphicsResources)
            foreach (var region in resources.RegionBatches) {
                if (viewFrustum.Intersects(region.Bounds))
                    resources.DrawInstanced(cmd, uniformDescriptorSet, region);
            }
        }

        _vk.CmdEndRenderPass(cmd);

        RecordImageBarrier(cmd, _deferredTargets.SceneOutputImage,
            _sceneOutputInitialized ? ImageLayout.ShaderReadOnlyOptimal : ImageLayout.Undefined,
            ImageLayout.General,
            _sceneOutputInitialized ? PipelineStageFlags.ComputeShaderBit : PipelineStageFlags.TopOfPipeBit,
            _sceneOutputInitialized ? AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit : AccessFlags.None,
            PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit);

        _deferredPassResources!.Composite.Dispatch(cmd, 0, uniformDescriptorSet, renderDimensions);
        _sceneOutputInitialized = true;

        RenderPassBeginInfo waterRenderPassInfo = new() {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _deferredTargets.WaterRenderPass,
            Framebuffer = _deferredTargets.WaterFramebuffer,
            RenderArea = new Rect2D(new Offset2D(0, 0), _swapchainExtent)
        };

        _vk.CmdBeginRenderPass(cmd, in waterRenderPassInfo, SubpassContents.Inline);

        if (_waterMesh is { Indices.Length: > 0 } waterMesh && _waterGraphicsResources is { IndexCount: > 0 } waterResources) {
            var visibleWaterRegions = new List<RenderRegionIndexRange>();
            foreach (var region in waterMesh.Regions) {
                if (viewFrustum.Intersects(region.Bounds))
                    visibleWaterRegions.Add(region);
            }

            visibleWaterRegions.Sort((left, right) =>
                Vector3.DistanceSquared(_camera.Position, right.Bounds.Center)
                    .CompareTo(Vector3.DistanceSquared(_camera.Position, left.Bounds.Center)));

            _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, waterResources.Pipeline);
            _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, waterResources.PipelineLayout, 0, 1, in uniformDescriptorSet, 0, null);
            var waterVertexBuffer = waterResources.VertexBuffer;
            _vk.CmdBindVertexBuffers(cmd, 0, 1, in waterVertexBuffer, in offset);
            _vk.CmdBindIndexBuffer(cmd, waterResources.IndexBuffer, 0, IndexType.Uint32);

            foreach (var region in visibleWaterRegions)
                _vk.CmdDrawIndexed(cmd, region.IndexCount, 1, region.FirstIndex, 0, 0);
        }

        _vk.CmdEndRenderPass(cmd);

        if (_ui.TaaEnabled) {
            var taaDescriptorIndex = _taaHistoryValid ? 0 : 1;

            var resolveOldLayout = _taaResolveInitialized
                ? ImageLayout.ShaderReadOnlyOptimal
                : ImageLayout.Undefined;

            RecordImageBarrier(cmd, _deferredTargets.TaaResolveImage, resolveOldLayout,
                ImageLayout.General,
                _taaResolveInitialized
                    ? PipelineStageFlags.ComputeShaderBit
                    : PipelineStageFlags.TopOfPipeBit,
                _taaResolveInitialized ? AccessFlags.ShaderReadBit : AccessFlags.None,
                PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit);

            _deferredPassResources.TaaResolve.Dispatch(cmd, taaDescriptorIndex, uniformDescriptorSet, renderDimensions);

            RecordImageBarrier(cmd, _deferredTargets.TaaResolveImage, ImageLayout.General,
                ImageLayout.ShaderReadOnlyOptimal, PipelineStageFlags.ComputeShaderBit,
                AccessFlags.ShaderWriteBit,
                PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderReadBit);

            _taaResolveInitialized = true;

            var historyOldLayout = _taaHistoryInitialized
                ? ImageLayout.ShaderReadOnlyOptimal
                : ImageLayout.Undefined;

            RecordImageBarrier(cmd, _deferredTargets.TaaHistoryImage, historyOldLayout,
                ImageLayout.General,
                _taaHistoryInitialized
                    ? PipelineStageFlags.ComputeShaderBit | PipelineStageFlags.FragmentShaderBit
                    : PipelineStageFlags.TopOfPipeBit,
                _taaHistoryInitialized ? AccessFlags.ShaderReadBit : AccessFlags.None,
                PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit);

            RecordImageBarrier(cmd, _deferredTargets.SceneOutputImage, ImageLayout.ShaderReadOnlyOptimal,
                ImageLayout.General,
                PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.ComputeShaderBit,
                AccessFlags.ColorAttachmentWriteBit | AccessFlags.ShaderReadBit,
                PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit);

            _deferredPassResources.TaaSharpen.Dispatch(cmd, 0, uniformDescriptorSet, renderDimensions);

            RecordImageBarrier(cmd, _deferredTargets.SceneOutputImage, ImageLayout.General,
                ImageLayout.ShaderReadOnlyOptimal, PipelineStageFlags.ComputeShaderBit,
                AccessFlags.ShaderWriteBit, PipelineStageFlags.ComputeShaderBit,
                AccessFlags.ShaderReadBit);

            RecordImageBarrier(cmd, _deferredTargets.TaaHistoryImage, ImageLayout.General,
                ImageLayout.ShaderReadOnlyOptimal, PipelineStageFlags.ComputeShaderBit,
                AccessFlags.ShaderWriteBit,
                PipelineStageFlags.ComputeShaderBit | PipelineStageFlags.FragmentShaderBit, AccessFlags.ShaderReadBit);

            _taaHistoryInitialized = true;
        }

        clearValues[0] = new ClearValue {
            Color = new ClearColorValue(0.4f, 0.6f, 0.9f, 1.0f),
        };

        clearValues[1] = new ClearValue {
            DepthStencil = new ClearDepthStencilValue {
                Depth = 1.0f,
                Stencil = 0,
            },
        };

        RenderPassBeginInfo presentationRenderPassInfo = new() {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _renderPass,
            Framebuffer = _framebuffers[imageIndex],
            RenderArea = new Rect2D(new Offset2D(0, 0), _swapchainExtent),
            ClearValueCount = 2,
            PClearValues = clearValues
        };

        _vk.CmdBeginRenderPass(cmd, in presentationRenderPassInfo, SubpassContents.Inline);
        _vk.CmdSetViewport(cmd, 0, 1, &viewport);
        _vk.CmdSetScissor(cmd, 0, 1, &scissor);
        _deferredPassResources.Tonemap.Draw(cmd, 0);
        _imguiRenderer?.Render(cmd, imageIndex);
        _vk.CmdEndRenderPass(cmd);
        _vk.EndCommandBuffer(cmd);

        var waitStages = PipelineStageFlags.AllCommandsBit;
        var imgAnimSema = _imageAvailableSemaphores[_currentFrame];
        var rendFinSema = _renderFinishedSemaphores[imageIndex];

        SubmitInfo submitInfo = new() {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &imgAnimSema,
            PWaitDstStageMask = &waitStages,
            CommandBufferCount = 1,
            PCommandBuffers = &cmd,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = &rendFinSema
        };

        _vk.ResetFences(_logicalDevice, 1, in _inFlightFences[_currentFrame]);
        var submitResult = _vk.QueueSubmit(_graphicsQueue, 1, in submitInfo, _inFlightFences[_currentFrame]);
        if (submitResult != Result.Success)
            throw new Exception($"Failed to submit the frame to the graphics queue: {submitResult}.");

        _cameraPositionLast = cameraPosition;
        _viewProjectionLast = viewProjection;
        _frameCounter = _frameCounter == uint.MaxValue ? 1u : _frameCounter + 1u;
        _taaFrameIndex++;
        _taaHistoryValid = _ui.TaaEnabled;

        var swap = _swapchain;
        PresentInfoKHR presentInfo = new() {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &rendFinSema,
            SwapchainCount = 1,
            PSwapchains = &swap,
            PImageIndices = &imageIndex
        };

        synchronizationStart = Stopwatch.GetTimestamp();
        var presentResult = deviceContext!.SwapchainExtension!.QueuePresent(_graphicsQueue, in presentInfo);
        synchronizationTicks += Stopwatch.GetTimestamp() - synchronizationStart;
        if (presentResult != Result.Success && presentResult != Result.SuboptimalKhr && presentResult != Result.ErrorOutOfDateKhr)
            throw new Exception($"Failed to present a swapchain image: {presentResult}.");

        _activeFrameTimeMilliseconds = Math.Max(
            0.0,
            Stopwatch.GetElapsedTime(frameStart).TotalMilliseconds - synchronizationTicks * 1000.0 / Stopwatch.Frequency);

        _currentFrame = (_currentFrame + 1) % MaxFramesInFlight;
        if (recreateAfterPresent || _framebufferResized || presentResult == Result.SuboptimalKhr || presentResult == Result.ErrorOutOfDateKhr)
            RecreateSwapchain();
    }

    private void OnEngineShutdown() => Dispose();

    private static Vector2 GetR2Jitter(ulong sampleIndex)
    {
        const double generator = 1.324717957244746;
        const double alphaX = 1.0 / generator;
        const double alphaY = 1.0 / (generator * generator);
        var sample = (double)sampleIndex;
        return new Vector2(
            (float)(0.5 + sample * alphaX - Math.Floor(0.5 + sample * alphaX)) - 0.5f,
            (float)(0.5 + sample * alphaY - Math.Floor(0.5 + sample * alphaY)) - 0.5f);
    }

    private unsafe void RecordImageBarrier(CommandBuffer commandBuffer, Image image, ImageLayout oldLayout, ImageLayout newLayout,
        PipelineStageFlags sourceStage, AccessFlags sourceAccess, PipelineStageFlags destinationStage, AccessFlags destinationAccess)
    {
        var barrier = new ImageMemoryBarrier {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
            SrcAccessMask = sourceAccess,
            DstAccessMask = destinationAccess
        };

        _vk!.CmdPipelineBarrier(commandBuffer, sourceStage, destinationStage, 0, 0, null, 0, null, 1, &barrier);
    }

    public unsafe void Dispose()
    {
        try {
            _parkLoadTask?.GetAwaiter().GetResult();
        }
        catch {
            // ignore
        }

        _parkLoadTask = null;
        _pendingParkLoadResult = null;

        _inputController?.Dispose();
        _inputController = null;

        var vk = _vk;
        var logicalDevice = _logicalDevice;
        if (vk is not null && logicalDevice.Handle != 0) {
            vk.DeviceWaitIdle(logicalDevice);
            _imguiRenderer?.Dispose();
            _imguiRenderer = null;
            DestroySyncPrimitives();

            if (_commandPool.Handle != 0)
                vk.DestroyCommandPool(logicalDevice, _commandPool, null);
            _commandPool = default;
            _commandBuffers = Array.Empty<CommandBuffer>();
        }

        var terrainResources = _terrainGraphicsResources;
        _terrainGraphicsResources = null;
        terrainResources?.Dispose();

        var pathResources = _pathGraphicsResources;
        _pathGraphicsResources = null;
        pathResources?.Dispose();

        DisposePathAccessoryResources();

        foreach (var resources in _vehicleCubeGraphicsResources) resources.Dispose();
        _vehicleCubeGraphicsResources = Array.Empty<ModelGraphicsResources>();

        var waterResources = _waterGraphicsResources;
        _waterGraphicsResources = null;
        waterResources?.Dispose();

        var deferredPassResources = _deferredPassResources;
        _deferredPassResources = null;
        deferredPassResources?.Dispose();

        var deferredTargets = _deferredTargets;
        _deferredTargets = null;
        deferredTargets?.Dispose();

        var swapchainResources = _swapchainResources;
        _swapchainResources = null;
        swapchainResources?.Dispose();

        var uniformBufferResources = _uniformBufferResources;
        _uniformBufferResources = null;
        uniformBufferResources?.Dispose();

        var deviceContext = this.deviceContext;
        this.deviceContext = null;
        deviceContext?.Dispose();

        var simulation = _simulation;
        _simulation = null;

        try {
            simulation?.Dispose();
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"Failed to shut down OpenRCT2: {ex.Message}");
        }
    }

    private void DisposePathAccessoryResources()
    {
        foreach (var (_, resources) in _pathAccessoryGraphicsResources)
            resources.Dispose();
        _pathAccessoryGraphicsResources.Clear();
    }

    private void CalculateSunDirection(out float sunElevation, out Vector3 sunDirection)
    {
        var timeAngle = (_ui.EffectiveTimeOfDayHours - 6.0f) * (MathF.PI / 12.0f);
        var zenithAngle = _ui.ZenithDegrees * (MathF.PI / 180.0f);
        sunElevation = MathF.Sin(timeAngle) * MathF.Cos(zenithAngle);

        var horizontalLength = MathF.Sqrt(MathF.Max(0.0f, 1.0f - sunElevation * sunElevation));
        var sunAzimuth = _ui.AzimuthDegrees * (MathF.PI / 180.0f);
        sunDirection = Vector3.Normalize(new Vector3(MathF.Cos(sunAzimuth) * horizontalLength, sunElevation, MathF.Sin(sunAzimuth) * horizontalLength));
    }
}
