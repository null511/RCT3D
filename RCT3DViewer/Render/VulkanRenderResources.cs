using RCT3D.Render.Buffers;
using RCT3D.Render.Paths;
using RCT3D.Render.Paths.Meshing;
using RCT3D.Render.Terrain;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace RCT3D.Render;

internal sealed unsafe class VulkanRenderResources : IDisposable
{
    public const int MaxFramesInFlight = 2;

    private bool isDisposed;

    public VulkanDeviceContext? DeviceContext { get; private set; }
    public SwapchainResources? SwapchainResources { get; private set; }
    public DeferredRenderTargets? DeferredTargets { get; private set; }
    public SkyRenderTargets? SkyTargets { get; private set; }
    public DeferredPassResources? DeferredPassResources { get; private set; }
    public UniformBufferResources? UniformBufferResources { get; private set; }
    public TerrainGraphicsResources? TerrainGraphicsResources { get; private set; }
    public TerrainGraphicsResources? PathGraphicsResources { get; private set; }
    public TerrainGraphicsResources? TrackGraphicsResources { get; private set; }
    public TerrainGraphicsResources? RideSupportGraphicsResources { get; private set; }
    public TerrainGraphicsResources? RideEntranceGraphicsResources { get; private set; }
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

    public void CreateRideSupportGraphicsResources()
    {
        RideSupportGraphicsResources?.Dispose();
        RideSupportGraphicsResources = null;

        var supportResources = new TerrainGraphicsResources(
            DeviceContext!, DeferredTargets!.GBufferRenderPass, UniformBufferResources!.DescriptorSetLayout, TerrainMaterials!,
            shaderNameOverride: Path.Combine("object", "track"), createWireframePipeline: true, colorAttachmentCount: 3,
            useTrackVertexColor: true);

        try {
            supportResources.CreatePipeline();
            supportResources.CreateTextureResources(CommandPool);
            RideSupportGraphicsResources = supportResources;
        }
        catch {
            supportResources.Dispose();
            throw;
        }
    }

    public void CreateRideEntranceGraphicsResources()
    {
        RideEntranceGraphicsResources?.Dispose();
        RideEntranceGraphicsResources = null;

        var entranceResources = new TerrainGraphicsResources(
            DeviceContext!, DeferredTargets!.GBufferRenderPass, UniformBufferResources!.DescriptorSetLayout, TerrainMaterials!,
            shaderNameOverride: Path.Combine("object", "track"), createWireframePipeline: true, colorAttachmentCount: 3,
            useTrackVertexColor: true);

        try {
            entranceResources.CreatePipeline();
            entranceResources.CreateTextureResources(CommandPool);
            RideEntranceGraphicsResources = entranceResources;
        }
        catch {
            entranceResources.Dispose();
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
        SkyTargets = new SkyRenderTargets(DeviceContext);
        UniformBufferResources = new UniformBufferResources(DeviceContext, MaxFramesInFlight);
        DeferredPassResources = new DeferredPassResources(DeviceContext, RenderPass, DeferredTargets, SkyTargets, UniformBufferResources);
        TerrainGraphicsResources = new TerrainGraphicsResources(DeviceContext, DeferredTargets.GBufferRenderPass, UniformBufferResources.DescriptorSetLayout, TerrainMaterials,
            shaderNameOverride: Path.Combine("object", "terrain"), createWireframePipeline: true, colorAttachmentCount: 3);
        TerrainGraphicsResources.CreatePipeline();
        PathGraphicsResources = new TerrainGraphicsResources(DeviceContext, DeferredTargets.GBufferRenderPass, UniformBufferResources.DescriptorSetLayout, PathMaterials.TextureLibrary,
            shaderNameOverride: Path.Combine("object", "terrain"), createWireframePipeline: true, colorAttachmentCount: 3);
        PathGraphicsResources.CreatePipeline();
        WaterGraphicsResources = new WaterGraphicsResources(DeviceContext, DeferredTargets.WaterRenderPass, UniformBufferResources.DescriptorSetLayout, SkyTargets);
        WaterGraphicsResources.CreatePipeline();
        SwapchainResources.CreateFramebuffers();

        CreateCommandInfrastructure();
        InitializeSkyTextures();
        TerrainGraphicsResources.CreateTextureResources(CommandPool);
        PathGraphicsResources.CreateTextureResources(CommandPool);
        CreateVehicleCubeResources();
        CreateSyncPrimitives();

        ImGuiRenderer = new ImGuiVulkanRenderer(Vk!, DeviceContext.PhysicalDevice, LogicalDevice, GraphicsQueue, RenderPass, CommandPool,
            Path.Combine(AppContext.BaseDirectory, "Shaders"), SwapchainImages.Length);
    }

    public void CreateTrackGraphicsResources()
    {
        TrackGraphicsResources?.Dispose();
        TrackGraphicsResources = null;

        var trackResources = new TerrainGraphicsResources(
            DeviceContext!, DeferredTargets!.GBufferRenderPass, UniformBufferResources!.DescriptorSetLayout, TerrainMaterials!,
            shaderNameOverride: Path.Combine("object", "track"), createWireframePipeline: true, colorAttachmentCount: 3,
            useTrackVertexColor: true);

        try {
            trackResources.CreatePipeline();
            trackResources.CreateTextureResources(CommandPool);
            TrackGraphicsResources = trackResources;
        }
        catch {
            trackResources.Dispose();
            throw;
        }
    }

    private void InitializeSkyTextures()
    {
        var commandBuffer = default(CommandBuffer);
        var allocateInfo = new CommandBufferAllocateInfo {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = CommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };

        if (Vk!.AllocateCommandBuffers(LogicalDevice, in allocateInfo, &commandBuffer) != Result.Success)
            throw new InvalidOperationException("Failed to allocate the sky initialization command buffer.");

        try {
            var beginInfo = new CommandBufferBeginInfo {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
            };

            if (Vk.BeginCommandBuffer(commandBuffer, in beginInfo) != Result.Success)
                throw new InvalidOperationException("Failed to begin the sky initialization command buffer.");

            var skyTargets = SkyTargets!;
            var skyPasses = DeferredPassResources!;
            var uniformDescriptorSet = UniformBufferResources!.GetDescriptorSet(0);

            RecordSkyImageBarrier(commandBuffer, skyTargets.SkyTransmitImage,
                ImageLayout.Undefined, ImageLayout.General,
                PipelineStageFlags.TopOfPipeBit, AccessFlags.None,
                PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit);

            skyPasses.SkyTransmit.Dispatch(commandBuffer, 0, uniformDescriptorSet, new Vector2D<uint>(256, 64));

            RecordSkyImageBarrier(commandBuffer, skyTargets.SkyTransmitImage,
                ImageLayout.General, ImageLayout.ShaderReadOnlyOptimal,
                PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit,
                PipelineStageFlags.ComputeShaderBit | PipelineStageFlags.FragmentShaderBit, AccessFlags.ShaderReadBit);

            RecordSkyImageBarrier(commandBuffer, skyTargets.SkyMultiscatterImage,
                ImageLayout.Undefined, ImageLayout.General,
                PipelineStageFlags.TopOfPipeBit, AccessFlags.None,
                PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit);

            skyPasses.SkyMultiscatter.Dispatch(commandBuffer, 0, uniformDescriptorSet, new Vector2D<uint>(32, 32));

            RecordSkyImageBarrier(commandBuffer, skyTargets.SkyMultiscatterImage,
                ImageLayout.General, ImageLayout.ShaderReadOnlyOptimal,
                PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit,
                PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderReadBit);

            RecordSkyImageBarrier(commandBuffer, skyTargets.SkyIrradianceImage,
                ImageLayout.Undefined, ImageLayout.TransferDstOptimal,
                PipelineStageFlags.TopOfPipeBit, AccessFlags.None,
                PipelineStageFlags.TransferBit, AccessFlags.TransferWriteBit);

            var irradianceClearColor = new ClearColorValue(0.0f, 0.0f, 0.0f, 0.0f);
            var irradianceSubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1);
            Vk.CmdClearColorImage(commandBuffer, skyTargets.SkyIrradianceImage, ImageLayout.TransferDstOptimal,
                &irradianceClearColor, 1, &irradianceSubresourceRange);

            RecordSkyImageBarrier(commandBuffer, skyTargets.SkyIrradianceImage,
                ImageLayout.TransferDstOptimal, ImageLayout.General,
                PipelineStageFlags.TransferBit, AccessFlags.TransferWriteBit,
                PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit);

            if (Vk.EndCommandBuffer(commandBuffer) != Result.Success)
                throw new InvalidOperationException("Failed to end the sky initialization command buffer.");

            var submitInfo = new SubmitInfo {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &commandBuffer,
            };

            if (Vk.QueueSubmit(GraphicsQueue, 1, in submitInfo, default) != Result.Success)
                throw new InvalidOperationException("Failed to submit the sky initialization commands.");

            if (Vk.QueueWaitIdle(GraphicsQueue) != Result.Success)
                throw new InvalidOperationException("Failed to wait for sky initialization commands.");
        }
        finally {
            Vk.FreeCommandBuffers(LogicalDevice, CommandPool, 1, &commandBuffer);
        }
    }

    private void RecordSkyImageBarrier(CommandBuffer commandBuffer, Image image, ImageLayout oldLayout, ImageLayout newLayout,
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

        Vk!.CmdPipelineBarrier(commandBuffer, sourceStage, destinationStage, 0, 0, null, 0, null, 1, &barrier);
    }

    private void CreateVehicleCubeResources()
    {
        var vehicleCube = VehicleCubeRenderer.CreateMesh();
        VehicleCubeGraphicsResources = new ModelGraphicsResources[MaxFramesInFlight];

        for (var i = 0; i < VehicleCubeGraphicsResources.Length; i++) {
            var resources = new ModelGraphicsResources(DeviceContext!, DeferredTargets!.GBufferRenderPass, UniformBufferResources!.DescriptorSetLayout, vehicleCube.Material, unlit: true);
            
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
        DeferredPassResources = new DeferredPassResources(DeviceContext!, RenderPass, DeferredTargets, SkyTargets!, UniformBufferResources!);
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

        var trackResources = TrackGraphicsResources;
        TrackGraphicsResources = null;
        trackResources?.Dispose();

        var rideSupportResources = RideSupportGraphicsResources;
        RideSupportGraphicsResources = null;
        rideSupportResources?.Dispose();

        var rideEntranceResources = RideEntranceGraphicsResources;
        RideEntranceGraphicsResources = null;
        rideEntranceResources?.Dispose();

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

        var skyTargets = SkyTargets;
        SkyTargets = null;
        skyTargets?.Dispose();

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
