using RCT3DViewer.Interop;
using RCT3DViewer.Simulation;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using System.Numerics;
using System.Runtime.InteropServices;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace RCT3DViewer.Render;

public sealed class Renderer : IDisposable
{
    private IWindow? _window;
    private VulkanDeviceContext? _deviceContext;
    private Vk? _vk => _deviceContext?.Api;
    private PhysicalDevice _physicalDevice => _deviceContext?.PhysicalDevice ?? default;
    private Device _logicalDevice => _deviceContext?.LogicalDevice ?? default;
    private Queue _graphicsQueue => _deviceContext?.GraphicsQueue ?? default;
    private uint _graphicsQueueFamilyIndex => _deviceContext?.GraphicsQueueFamilyIndex ?? 0;
    private SwapchainResources? _swapchainResources;
    private SwapchainKHR _swapchain => _swapchainResources?.Swapchain ?? default;
    private Image[] _swapchainImages => _swapchainResources?.Images ?? Array.Empty<Image>();
    //private ImageView[] _swapchainImageViews => _swapchainResources?.ImageViews ?? Array.Empty<ImageView>();
    private Framebuffer[] _framebuffers => _swapchainResources?.Framebuffers ?? Array.Empty<Framebuffer>();
    //private Format _swapchainFormat => _swapchainResources?.Format ?? default;
    private Extent2D _swapchainExtent => _swapchainResources?.Extent ?? default;
    private RenderPass _renderPass => _swapchainResources?.RenderPass ?? default;
    private bool _framebufferResized;

    private TerrainGraphicsResources? _terrainGraphicsResources;
    private PipelineLayout _pipelineLayout => _terrainGraphicsResources?.PipelineLayout ?? default;
    private Pipeline _graphicsPipeline => _terrainGraphicsResources?.Pipeline ?? default;
    private DescriptorSet _terrainDescriptorSet => _terrainGraphicsResources?.DescriptorSet ?? default;
    private Buffer _vertexBuffer => _terrainGraphicsResources?.VertexBuffer ?? default;
    private Buffer _indexBuffer => _terrainGraphicsResources?.IndexBuffer ?? default;
    private TerrainMaterialLibrary? _terrainMaterials;
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
    private TerrainMeshBuilder? _terrainMeshBuilder;
    private TerrainMeshData? _terrainMesh;
    private SimulationBridge? _simulation;
    private RCTTileSurfaceData[]? _surfaceData;
    //private string? _parkPath;
    private int _mapWidth;
    private int _mapHeight;

    public string? LibraryPath { get; set; }
    public string? OpenRCT2Path { get; set; }
    public string? Rct2Path { get; set; }
    public float TerrainSkirtFloor { get; set; } = -24.0f;


    public void Run()
    {
        var options = WindowOptions.DefaultVulkan;
        options.Size = new Vector2D<int>(1280, 720);
        options.Title = "RCT Cross-Platform 3D Real-Time Viewer";

        _window = Window.Create(options);
        _window.FramebufferResize += _ => _framebufferResized = true;
        _window.Load += Initialize;
        _window.Render += OnRenderFrame;
        _window.Update += OnUpdateFrame;
        _window.Closing += OnEngineShutdown;

        try {
            _window.Run();
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
            //_simulation.LoadPark(_parkPath);
            //_loadedParkPath = _parkPath;
            //var (width, height) = _simulation.GetDimensions();
            //if (width <= 0 || height <= 0 || (long)width * height > int.MaxValue)
            //    throw new InvalidOperationException($"OpenRCT2 returned invalid map dimensions {width}x{height}.");

            //_surfaceData = new RCTTileSurfaceData[checked(width * height)];
            //_simulation.FillSurfaceData(_surfaceData);
            _terrainMaterials = TerrainMaterialLibrary.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Textures", "Terrain"));
            _terrainMeshBuilder = new TerrainMeshBuilder(_terrainMaterials);
            //_mapWidth = width;
            //_mapHeight = height;
            //_camera.Position = new Vector3(width * 1.5f, 120.0f, -height * 1.5f);
            //Console.WriteLine($"Loaded park '{_parkPath}' ({width}x{height}) through the OpenRCT2 adapter.");
        }
        catch (Exception ex) {
            _simulation?.Dispose();
            _simulation = null;
            _surfaceData = null;

            throw new InvalidOperationException("Failed to initialize OpenRCT2!", ex);
        }

        Console.WriteLine("Initializing Graphics Subsystems via Silk.NET Vulkan...");
        _deviceContext = VulkanDeviceContext.Create(_window!);
        _swapchainResources = new SwapchainResources(_deviceContext, _window!);

        _swapchainResources.CreateSwapchain();
        _swapchainResources.CreateImageViews();
        _swapchainResources.CreateDepthResources();
        _swapchainResources.CreateRenderPass();
        _terrainGraphicsResources = new TerrainGraphicsResources(_deviceContext, _renderPass, _terrainMaterials!);
        _terrainGraphicsResources.CreatePipeline();
        _swapchainResources.CreateFramebuffers();

        SetupInputs();

        CreateCommandInfrastructure();
        _terrainGraphicsResources.CreateTextureResources(_commandPool);
        CreateSyncPrimitives();

        _imguiRenderer = new ImGuiVulkanRenderer(_vk!, _physicalDevice, _logicalDevice, _graphicsQueue, _renderPass, _commandPool,
            Path.Combine(AppContext.BaseDirectory, "Shaders"), _swapchainImages.Length);

        Console.WriteLine("\n=== Vulkan Engine Pipeline Fully Operational ===");
        Console.WriteLine("Hold down RIGHT-MOUSE BUTTON to look around. Use W,A,S,D to fly.");
    }

    private unsafe void CreateCommandInfrastructure()
    {
        CommandPoolCreateInfo poolInfo = new() { SType = StructureType.CommandPoolCreateInfo, QueueFamilyIndex = _graphicsQueueFamilyIndex, Flags = CommandPoolCreateFlags.ResetCommandBufferBit };
        _vk!.CreateCommandPool(_logicalDevice, in poolInfo, null, out _commandPool);
        CommandBufferAllocateInfo allocInfo = new() { SType = StructureType.CommandBufferAllocateInfo, CommandPool = _commandPool, Level = CommandBufferLevel.Primary, CommandBufferCount = (uint)_framebuffers.Length };
        _commandBuffers = new CommandBuffer[_framebuffers.Length];
        fixed (CommandBuffer* cbPtr = _commandBuffers) {
            _vk.AllocateCommandBuffers(_logicalDevice, in allocInfo, cbPtr);
        }
    }

    private unsafe void CreateSyncPrimitives()
    {
        SemaphoreCreateInfo semaphoreInfo = new() { SType = StructureType.SemaphoreCreateInfo };
        FenceCreateInfo fenceInfo = new() { SType = StructureType.FenceCreateInfo, Flags = FenceCreateFlags.SignaledBit };

        var imageCount = _swapchainImages.Length;
        _imageAvailableSemaphores = new Semaphore[MaxFramesInFlight];
        _renderFinishedSemaphores = new Semaphore[imageCount];
        _inFlightFences = new Fence[MaxFramesInFlight];

        for (var i = 0; i < MaxFramesInFlight; i++)
            _vk!.CreateSemaphore(_logicalDevice, in semaphoreInfo, null, out _imageAvailableSemaphores[i]);

        for (var i = 0; i < imageCount; i++)
            _vk.CreateSemaphore(_logicalDevice, in semaphoreInfo, null, out _renderFinishedSemaphores[i]);

        for (uint i = 0; i < MaxFramesInFlight; i++) {
            _vk!.CreateFence(_logicalDevice, in fenceInfo, null, out _inFlightFences[i]);
        }

        _imagesInFlight = new Fence[imageCount];
        for (var i = 0; i < _imagesInFlight.Length; i++) _imagesInFlight[i] = new Fence();
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
        var framebufferSize = _window!.FramebufferSize;
        if (framebufferSize.X == 0 || framebufferSize.Y == 0) return false;

        if (_vk!.DeviceWaitIdle(_logicalDevice) != Result.Success)
            throw new Exception("Failed to wait for the Vulkan device before recreating the swapchain.");

        DestroySyncPrimitives();
        _vk.DestroyCommandPool(_logicalDevice, _commandPool, null);
        _commandPool = default;
        _commandBuffers = Array.Empty<CommandBuffer>();
        _swapchainResources!.Recreate();
        CreateCommandInfrastructure();
        CreateSyncPrimitives();
        _imguiRenderer?.ResizeBuffers(_swapchainImages.Length);
        _currentFrame = 0;
        _framebufferResized = false;
        return true;
    }

    private void SetupInputs()
    {
        _inputController = new ViewerInputController(_window!, _camera, () => _imguiRenderer?.WantCaptureMouse ?? false);
    }

    private void OnUpdateFrame(double deltaTime)
    {
        if (_exitRequested) {
            _exitRequested = false;
            _window?.Close();
            return;
        }

        _inputController?.Update(deltaTime, _imguiRenderer);
    }

    private void LoadPark(string parkPath)
    {
        var simulation = _simulation ?? throw new InvalidOperationException("OpenRCT2 is not initialized.");
        _ui.BeginParkLoad();
        var previousMesh = _terrainMesh;

        try {
            simulation.LoadPark(parkPath);
            var (width, height) = simulation.GetDimensions();
            if (width <= 0 || height <= 0 || (long)width * height > int.MaxValue)
                throw new InvalidOperationException($"OpenRCT2 returned invalid map dimensions {width}x{height}.");

            var surfaceData = new RCTTileSurfaceData[checked(width * height)];
            simulation.FillSurfaceData(surfaceData);
            var terrainMesh = _terrainMeshBuilder!.Build(surfaceData, width, height, TerrainSkirtFloor);
            _terrainMesh = terrainMesh;
            if (_vk!.DeviceWaitIdle(_logicalDevice) != Result.Success)
                throw new InvalidOperationException("Could not wait for the Vulkan device before replacing terrain buffers.");
            _terrainGraphicsResources!.UploadMesh(terrainMesh);

            _surfaceData = surfaceData;
            _mapWidth = width;
            _mapHeight = height;
            _ui.SetLoadedParkPath(parkPath);
            //_parkPath = parkPath;
            _camera.Position = new Vector3(width * 1.5f, 120.0f, -height * 1.5f);
            _ui.ClearParkLoadError();
        }
        catch (Exception ex) {
            if (_ui.LoadedParkPath is not null) {
                try {
                    simulation.LoadPark(_ui.LoadedParkPath);
                }
                catch (Exception restoreException) {
                    _ui.ShowParkLoadError($"Failed to load '{parkPath}': {ex.Message} The previous park could not be restored: {restoreException.Message}");
                }
            }

            _terrainMesh = previousMesh;
            if (!_ui.HasParkLoadError)
                _ui.ShowParkLoadError($"Failed to load '{parkPath}': {ex.Message}");
        }
    }

    private unsafe void OnRenderFrame(double deltaTime)
    {
        if (_window!.FramebufferSize.X == 0 || _window.FramebufferSize.Y == 0) return;
        if (_framebufferResized && !RecreateSwapchain()) return;

        uint imageIndex = 0;
        _vk!.WaitForFences(_logicalDevice, 1, in _inFlightFences[_currentFrame], true, ulong.MaxValue);
        var acquireResult = _deviceContext!.SwapchainExtension!.AcquireNextImage(_logicalDevice, _swapchain, ulong.MaxValue, _imageAvailableSemaphores[_currentFrame], default, ref imageIndex);
        if (acquireResult == Result.ErrorOutOfDateKhr) {
            RecreateSwapchain();
            return;
        }

        if (acquireResult != Result.Success && acquireResult != Result.SuboptimalKhr)
            throw new Exception($"Failed to acquire a swapchain image: {acquireResult}.");

        var recreateAfterPresent = acquireResult == Result.SuboptimalKhr;

        if (_imagesInFlight[imageIndex].Handle != 0) {
            _vk.WaitForFences(_logicalDevice, 1, in _imagesInFlight[imageIndex], true, ulong.MaxValue);
        }

        _imagesInFlight[imageIndex] = _inFlightFences[_currentFrame];
        var cmd = _commandBuffers[imageIndex];
        _imguiRenderer?.BeginFrame((int)_swapchainExtent.Width, (int)_swapchainExtent.Height, (float)deltaTime);
        _ui.BuildFrameUi(deltaTime, LoadPark, () => _exitRequested = true);
        _vk.ResetCommandBuffer(cmd, 0);
        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };
        _vk.BeginCommandBuffer(cmd, in beginInfo);

        ClearValue* clearValues = stackalloc ClearValue[2];
        clearValues[0] = new ClearValue { Color = new ClearColorValue(0.4f, 0.6f, 0.9f, 1.0f) };
        clearValues[1] = new ClearValue { DepthStencil = new ClearDepthStencilValue { Depth = 1.0f, Stencil = 0 } };

        RenderPassBeginInfo renderPassInfo = new() {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _renderPass,
            Framebuffer = _framebuffers[imageIndex],
            RenderArea = new Rect2D(new Offset2D(0, 0), _swapchainExtent),
            ClearValueCount = 2,
            PClearValues = clearValues
        };

        _vk.CmdBeginRenderPass(cmd, in renderPassInfo, SubpassContents.Inline);
        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _graphicsPipeline);

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
        _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _pipelineLayout, 0, 1, in terrainDescriptorSet, 0, null);

        var aspect = (float)_swapchainExtent.Width / _swapchainExtent.Height;
        // Row-vector convention in shader: clip = position * viewProjection
        var viewProjection = _camera.GetViewMatrix() * _camera.GetProjectionMatrix(aspect);
        var timeAngle = (_ui.TimeOfDayHours - 6.0f) * (MathF.PI / 12.0f);
        var sunElevation = MathF.Sin(timeAngle);
        var horizontalLength = MathF.Cos(timeAngle);
        var sunAzimuth = _ui.SunAngleDegrees * (MathF.PI / 180.0f);
        var sunDirection = Vector3.Normalize(new Vector3(MathF.Cos(sunAzimuth) * horizontalLength, sunElevation, MathF.Sin(sunAzimuth) * horizontalLength));
        var ambientLight = 0.15f + 0.15f * MathF.Max(sunElevation, 0.0f);

        UniformBufferObject ubo = new() {
            ViewProjection = viewProjection,
            SunDirection = new Vector4(sunDirection, 0.0f),
            Lighting = new Vector4(ambientLight, 0.85f, 0.0f, 0.0f)
        };

        _vk.CmdPushConstants(cmd, _pipelineLayout, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0, (uint)Marshal.SizeOf<UniformBufferObject>(), &ubo);

        ulong offset = 0;
        var vBuf = _vertexBuffer;
        _vk.CmdBindVertexBuffers(cmd, 0, 1, in vBuf, in offset);
        _vk.CmdBindIndexBuffer(cmd, _indexBuffer, 0, IndexType.Uint32);
        _vk.CmdDrawIndexed(cmd, (uint)(_terrainMesh?.Indices.Length ?? 0), 1, 0, 0, 0);
        _imguiRenderer?.Render(cmd, imageIndex);
        _vk.CmdEndRenderPass(cmd);
        _vk.EndCommandBuffer(cmd);

        var waitStages = PipelineStageFlags.ColorAttachmentOutputBit;
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
        _vk.QueueSubmit(_graphicsQueue, 1, in submitInfo, _inFlightFences[_currentFrame]);

        var swap = _swapchain;
        PresentInfoKHR presentInfo = new() {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &rendFinSema,
            SwapchainCount = 1,
            PSwapchains = &swap,
            PImageIndices = &imageIndex
        };

        var presentResult = _deviceContext!.SwapchainExtension!.QueuePresent(_graphicsQueue, in presentInfo);
        if (presentResult != Result.Success && presentResult != Result.SuboptimalKhr && presentResult != Result.ErrorOutOfDateKhr)
            throw new Exception($"Failed to present a swapchain image: {presentResult}.");

        _currentFrame = (uint)((_currentFrame + 1) % MaxFramesInFlight);
        if (recreateAfterPresent || _framebufferResized || presentResult == Result.SuboptimalKhr || presentResult == Result.ErrorOutOfDateKhr)
            RecreateSwapchain();
    }

    private void OnEngineShutdown() => Dispose();

    public unsafe void Dispose()
    {
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

        var swapchainResources = _swapchainResources;
        _swapchainResources = null;
        swapchainResources?.Dispose();

        var deviceContext = _deviceContext;
        _deviceContext = null;
        deviceContext?.Dispose();

        var simulation = _simulation;
        _simulation = null;
        simulation?.Dispose();
    }
}
