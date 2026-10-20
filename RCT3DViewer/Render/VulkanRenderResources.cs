using RCT3DViewer.Render.Buffers;
using RCT3DViewer.Render.Paths;
using RCT3DViewer.Render.Paths.Meshing;
using RCT3DViewer.Render.Terrain;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace RCT3DViewer.Render;

internal sealed unsafe class VulkanRenderResources : IDisposable
{
    public const int MaxFramesInFlight = 2;

    private bool isDisposed;

    public VulkanDeviceContext? DeviceContext { get; private set; }
    public SwapchainResources? SwapchainResources { get; private set; }
    public DeferredRenderTargets? DeferredTargets { get; private set; }
    public DeferredPassResources? DeferredPassResources { get; private set; }
    public UniformBufferResources? UniformBufferResources { get; private set; }
    public TerrainGraphicsResources? TerrainGraphicsResources { get; private set; }
    public TerrainGraphicsResources? PathGraphicsResources { get; private set; }
    public WaterGraphicsResources? WaterGraphicsResources { get; private set; }
    public ModelGraphicsResources[] VehicleCubeGraphicsResources { get; private set; } = Array.Empty<ModelGraphicsResources>();
    public TerrainMaterialLibrary? TerrainMaterials { get; private set; }
    public PathMaterialLibrary? PathMaterials { get; private set; }
    public PathAccessoryAssetLibrary? PathAccessoryAssets { get; private set; }
    public List<(PathAccessoryBatch Batch, ModelGraphicsResources Resources)> PathAccessoryGraphicsResources { get; } = new();
    public CommandPool CommandPool { get; private set; }
    public CommandBuffer[] CommandBuffers { get; private set; } = Array.Empty<CommandBuffer>();
    public Semaphore[] ImageAvailableSemaphores { get; private set; } = Array.Empty<Semaphore>();
    public Semaphore[] RenderFinishedSemaphores { get; private set; } = Array.Empty<Semaphore>();
    public Fence[] InFlightFences { get; private set; } = Array.Empty<Fence>();
    public Fence[] ImagesInFlight { get; private set; } = Array.Empty<Fence>();
    public ImGuiVulkanRenderer? ImGuiRenderer { get; private set; }

    public Vk? Vk => DeviceContext?.Api;
    public Device LogicalDevice => DeviceContext?.LogicalDevice ?? default;
    public Queue GraphicsQueue => DeviceContext?.GraphicsQueue ?? default;
    public uint GraphicsQueueFamilyIndex => DeviceContext?.GraphicsQueueFamilyIndex ?? 0;
    public SwapchainKHR Swapchain => SwapchainResources?.Swapchain ?? default;
    public Image[] SwapchainImages => SwapchainResources?.Images ?? Array.Empty<Image>();
    public Framebuffer[] Framebuffers => SwapchainResources?.Framebuffers ?? Array.Empty<Framebuffer>();
    public Extent2D SwapchainExtent => SwapchainResources?.Extent ?? default;
    public RenderPass RenderPass => SwapchainResources?.RenderPass ?? default;
    public PipelineLayout PipelineLayout => TerrainGraphicsResources?.PipelineLayout ?? default;
    public Pipeline GraphicsPipeline => TerrainGraphicsResources?.Pipeline ?? default;
    public DescriptorSet TerrainDescriptorSet => TerrainGraphicsResources?.DescriptorSet ?? default;
    public Buffer VertexBuffer => TerrainGraphicsResources?.VertexBuffer ?? default;
    public Buffer IndexBuffer => TerrainGraphicsResources?.IndexBuffer ?? default;


    public VulkanRenderResources(
        IWindow window,
        TerrainMaterialLibrary terrainMaterials,
        PathMaterialLibrary pathMaterials,
        PathAccessoryAssetLibrary pathAccessoryAssets)
    {
        TerrainMaterials = terrainMaterials;
        PathMaterials = pathMaterials;
        PathAccessoryAssets = pathAccessoryAssets;

        try {
            Initialize(window);
        }
        catch {
            Dispose();
            throw;
        }
    }

    private void Initialize(IWindow window)
    {
        if (TerrainMaterials is null)
            throw new InvalidOperationException("TerrainMaterials is undefined!");

        if (PathMaterials is null)
            throw new InvalidOperationException("PathMaterials is undefined!");

        DeviceContext = VulkanDeviceContext.Create(window);
        SwapchainResources = new SwapchainResources(DeviceContext, window);
        SwapchainResources.CreateSwapchain();
        SwapchainResources.CreateImageViews();
        SwapchainResources.CreateDepthResources();
        SwapchainResources.CreateRenderPass();

        DeferredTargets = new DeferredRenderTargets(DeviceContext, SwapchainExtent);
        UniformBufferResources = new UniformBufferResources(DeviceContext, MaxFramesInFlight);
        DeferredPassResources = new DeferredPassResources(DeviceContext, RenderPass, DeferredTargets, UniformBufferResources);
        TerrainGraphicsResources = new TerrainGraphicsResources(DeviceContext, DeferredTargets.GBufferRenderPass, UniformBufferResources.DescriptorSetLayout, TerrainMaterials,
            shaderNameOverride: Path.Combine("defer", "deferredTerrain"), createWireframePipeline: true, colorAttachmentCount: 3);
        TerrainGraphicsResources.CreatePipeline();
        PathGraphicsResources = new TerrainGraphicsResources(DeviceContext, DeferredTargets.GBufferRenderPass, UniformBufferResources.DescriptorSetLayout, PathMaterials.TextureLibrary,
            shaderNameOverride: Path.Combine("defer", "deferredTerrain"), createWireframePipeline: true, colorAttachmentCount: 3);
        PathGraphicsResources.CreatePipeline();
        WaterGraphicsResources = new WaterGraphicsResources(DeviceContext, DeferredTargets.WaterRenderPass, UniformBufferResources.DescriptorSetLayout);
        WaterGraphicsResources.CreatePipeline();
        SwapchainResources.CreateFramebuffers();

        CreateCommandInfrastructure();
        TerrainGraphicsResources.CreateTextureResources(CommandPool);
        PathGraphicsResources.CreateTextureResources(CommandPool);
        CreateVehicleCubeResources();
        CreateSyncPrimitives();

        ImGuiRenderer = new ImGuiVulkanRenderer(Vk!, DeviceContext.PhysicalDevice, LogicalDevice, GraphicsQueue, RenderPass, CommandPool,
            Path.Combine(AppContext.BaseDirectory, "Shaders"), SwapchainImages.Length);
    }

    private void CreateVehicleCubeResources()
    {
        var vehicleCube = VehicleCubeRenderer.CreateMesh();
        VehicleCubeGraphicsResources = new ModelGraphicsResources[MaxFramesInFlight];

        for (var i = 0; i < VehicleCubeGraphicsResources.Length; i++) {
            var resources = new ModelGraphicsResources(DeviceContext!, DeferredTargets!.GBufferRenderPass, UniformBufferResources!.DescriptorSetLayout, vehicleCube.Material, unlit: true, deferred: true);
            
            try {
                resources.CreatePipeline();
                resources.CreateTextureResources(CommandPool);
                resources.UploadMesh(vehicleCube);
                VehicleCubeGraphicsResources[i] = resources;
            }
            catch {
                resources.Dispose();
                throw;
            }
        }
    }

    private void CreateCommandInfrastructure()
    {
        CommandPoolCreateInfo poolInfo = new() {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = GraphicsQueueFamilyIndex,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit
        };

        Vk!.CreateCommandPool(LogicalDevice, in poolInfo, null, out var commandPool);
        CommandPool = commandPool;

        CommandBufferAllocateInfo allocInfo = new() {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = CommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = (uint)Framebuffers.Length,
        };

        CommandBuffers = new CommandBuffer[Framebuffers.Length];
        fixed (CommandBuffer* cbPtr = CommandBuffers)
            Vk.AllocateCommandBuffers(LogicalDevice, in allocInfo, cbPtr);
    }

    private void CreateSyncPrimitives()
    {
        SemaphoreCreateInfo semaphoreInfo = new() { SType = StructureType.SemaphoreCreateInfo };
        FenceCreateInfo fenceInfo = new() { SType = StructureType.FenceCreateInfo, Flags = FenceCreateFlags.SignaledBit };

        var imageCount = SwapchainImages.Length;
        ImageAvailableSemaphores = new Semaphore[MaxFramesInFlight];
        RenderFinishedSemaphores = new Semaphore[imageCount];
        InFlightFences = new Fence[MaxFramesInFlight];

        for (var i = 0; i < MaxFramesInFlight; i++)
            Vk!.CreateSemaphore(LogicalDevice, in semaphoreInfo, null, out ImageAvailableSemaphores[i]);

        for (var i = 0; i < imageCount; i++)
            Vk!.CreateSemaphore(LogicalDevice, in semaphoreInfo, null, out RenderFinishedSemaphores[i]);

        for (uint i = 0; i < MaxFramesInFlight; i++)
            Vk!.CreateFence(LogicalDevice, in fenceInfo, null, out InFlightFences[i]);

        ImagesInFlight = new Fence[imageCount];
        for (var i = 0; i < ImagesInFlight.Length; i++)
            ImagesInFlight[i] = new Fence();
    }

    public void RecreateSwapchain()
    {
        if (Vk!.DeviceWaitIdle(LogicalDevice) != Result.Success)
            throw new Exception("Failed to wait for the Vulkan device before recreating the swapchain.");

        DestroySyncPrimitives();
        if (CommandPool.Handle != 0)
            Vk.DestroyCommandPool(LogicalDevice, CommandPool, null);

        CommandPool = default;
        CommandBuffers = Array.Empty<CommandBuffer>();
        SwapchainResources!.Recreate();
        DeferredPassResources?.Dispose();
        DeferredPassResources = null;
        DeferredTargets!.Resize(SwapchainExtent);
        DeferredPassResources = new DeferredPassResources(DeviceContext!, RenderPass, DeferredTargets, UniformBufferResources!);
        CreateCommandInfrastructure();
        CreateSyncPrimitives();
        ImGuiRenderer?.ResizeBuffers(SwapchainImages.Length);
    }

    private void DestroySyncPrimitives()
    {
        if (Vk is null || LogicalDevice.Handle == 0) return;

        foreach (var semaphore in ImageAvailableSemaphores) {
            if (semaphore.Handle != 0)
                Vk.DestroySemaphore(LogicalDevice, semaphore, null);
        }

        foreach (var semaphore in RenderFinishedSemaphores) {
            if (semaphore.Handle != 0)
                Vk.DestroySemaphore(LogicalDevice, semaphore, null);
        }

        foreach (var fence in InFlightFences) {
            if (fence.Handle != 0)
                Vk.DestroyFence(LogicalDevice, fence, null);
        }

        ImageAvailableSemaphores = Array.Empty<Semaphore>();
        RenderFinishedSemaphores = Array.Empty<Semaphore>();
        InFlightFences = Array.Empty<Fence>();
        ImagesInFlight = Array.Empty<Fence>();
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        var vk = Vk;
        var logicalDevice = LogicalDevice;
        if (vk is not null && logicalDevice.Handle != 0) {
            vk.DeviceWaitIdle(logicalDevice);
            ImGuiRenderer?.Dispose();
            ImGuiRenderer = null;
            DestroySyncPrimitives();

            if (CommandPool.Handle != 0)
                vk.DestroyCommandPool(logicalDevice, CommandPool, null);
            CommandPool = default;
            CommandBuffers = Array.Empty<CommandBuffer>();
        }

        foreach (var (_, resources) in PathAccessoryGraphicsResources)
            resources.Dispose();

        PathAccessoryGraphicsResources.Clear();

        foreach (var resources in VehicleCubeGraphicsResources)
            resources.Dispose();

        VehicleCubeGraphicsResources = Array.Empty<ModelGraphicsResources>();

        var waterResources = WaterGraphicsResources;
        WaterGraphicsResources = null;
        waterResources?.Dispose();

        var pathResources = PathGraphicsResources;
        PathGraphicsResources = null;
        pathResources?.Dispose();

        var terrainResources = TerrainGraphicsResources;
        TerrainGraphicsResources = null;
        terrainResources?.Dispose();

        var deferredPassResources = DeferredPassResources;
        DeferredPassResources = null;
        deferredPassResources?.Dispose();

        var deferredTargets = DeferredTargets;
        DeferredTargets = null;
        deferredTargets?.Dispose();

        var swapchainResources = SwapchainResources;
        SwapchainResources = null;
        swapchainResources?.Dispose();

        var uniformBufferResources = UniformBufferResources;
        UniformBufferResources = null;
        uniformBufferResources?.Dispose();

        var deviceContext = DeviceContext;
        DeviceContext = null;
        deviceContext?.Dispose();
    }
}
