using RCT3D.Configuration;
using RCT3D.Render.Buffers;
using RCT3D.Render.Paths;
using RCT3D.Render.Paths.Meshing;
using RCT3D.Render.Scenery;
using RCT3D.Render.Terrain;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace RCT3D.Render.Park;

internal sealed unsafe class ParkRenderResources : IDisposable
{
    public const int MaxFramesInFlight = VulkanRenderResources.MaxFramesInFlight;
    private bool isDisposed;

    public VulkanRenderResources SharedResources { get; }
    public VulkanDeviceContext? DeviceContext => SharedResources.DeviceContext;
    //public SwapchainResources? SwapchainResources => SharedResources.SwapchainResources;
    public CommandPool CommandPool => SharedResources.CommandPool;
    public Vk? Vk => SharedResources.Vk;
    public Device LogicalDevice => SharedResources.LogicalDevice;
    public Queue GraphicsQueue => SharedResources.GraphicsQueue;
    //public uint GraphicsQueueFamilyIndex => SharedResources.GraphicsQueueFamilyIndex;
    //public SwapchainKHR Swapchain => SharedResources.Swapchain;
    //public Image[] SwapchainImages => SharedResources.SwapchainImages;
    //public Framebuffer[] Framebuffers => SharedResources.Framebuffers;
    public Extent2D SwapchainExtent => SharedResources.SwapchainExtent;
    public RenderPass RenderPass => SharedResources.RenderPass;
    public DeferredRenderTargets? DeferredTargets { get; private set; }
    public ShadowGraphicsResources? ShadowGraphicsResources { get; private set; }
    public ModelTextureHeap? ModelTextureHeap { get; private set; }
    public SkyRenderTargets? SkyTargets { get; private set; }
    public DeferredPassResources? DeferredPassResources { get; private set; }
    public UniformBufferResources? UniformBufferResources { get; private set; }
    public TerrainGraphicsResources? TerrainGraphicsResources { get; private set; }
    public TerrainGraphicsResources? PathGraphicsResources { get; private set; }
    public PathSurfaceGraphicsResources? PathSurfaceGraphicsResources { get; private set; }
    public TerrainGraphicsResources? PathRailingGraphicsResources { get; private set; }
    public TerrainGraphicsResources? PathSupportGraphicsResources { get; private set; }
    public TerrainGraphicsResources? TrackGraphicsResources { get; private set; }
    public TerrainGraphicsResources? VehiclePathGraphicsResources { get; private set; }
    public TerrainGraphicsResources? RideSupportGraphicsResources { get; private set; }
    public TerrainGraphicsResources? RideEntranceGraphicsResources { get; private set; }
    public WaterGraphicsResources? WaterGraphicsResources { get; private set; }
    public List<(VehicleRenderer.ModelBatch Batch, ModelGraphicsResources[] Resources)> VehicleGraphicsResources { get; } = new();
    public TerrainMaterialLibrary? TerrainMaterials { get; private set; }
    public PathMaterialLibrary? PathMaterials { get; private set; }
    public PathAccessoryAssetLibrary? PathAccessoryAssets { get; private set; }
    public TreeSceneryAssetLibrary? TreeSceneryAssets { get; private set; }
    public List<(PathAccessoryBatch Batch, ModelGraphicsResources Resources)> PathAccessoryGraphicsResources { get; } = new();
    public List<(TreeSceneryBatch Batch, ModelGraphicsResources Resources)> TreeSceneryGraphicsResources { get; } = new();
    public List<ModelGraphicsResources> RideEntranceModelResources { get; } = new();
    public PipelineLayout PipelineLayout => TerrainGraphicsResources?.PipelineLayout ?? default;
    //public Pipeline GraphicsPipeline => TerrainGraphicsResources?.Pipeline ?? default;
    public DescriptorSet TerrainDescriptorSet => TerrainGraphicsResources?.DescriptorSet ?? default;
    public Buffer VertexBuffer => TerrainGraphicsResources?.VertexBuffer ?? default;
    public Buffer IndexBuffer => TerrainGraphicsResources?.IndexBuffer ?? default;

    public ParkRenderResources(
        VulkanRenderResources sharedResources,
        TerrainMaterialLibrary terrainMaterials,
        PathMaterialLibrary pathMaterials,
        PathAccessoryAssetLibrary pathAccessoryAssets,
        TreeSceneryAssetLibrary treeSceneryAssets,
        GraphicsOptionsSettings graphicsOptions)
    {
        sharedResources.EnsureInitialized();

        SharedResources = sharedResources;
        TerrainMaterials = terrainMaterials;
        PathMaterials = pathMaterials;
        PathAccessoryAssets = pathAccessoryAssets;
        TreeSceneryAssets = treeSceneryAssets;

        try {
            Initialize(graphicsOptions);
        }
        catch {
            Dispose();
            throw;
        }
    }

    private void Initialize(GraphicsOptionsSettings graphicsOptions)
    {
        if (TerrainMaterials is null)
            throw new InvalidOperationException("TerrainMaterials is undefined!");

        if (PathMaterials is null)
            throw new InvalidOperationException("PathMaterials is undefined!");

        DeferredTargets = new DeferredRenderTargets(DeviceContext!, SwapchainExtent,
            graphicsOptions.ShadowResolution, graphicsOptions.ShadowRadius);
        SkyTargets = new SkyRenderTargets(DeviceContext!);
        UniformBufferResources = new UniformBufferResources(DeviceContext!, MaxFramesInFlight);
        DeferredPassResources = new DeferredPassResources(DeviceContext!, RenderPass, DeferredTargets, SkyTargets, UniformBufferResources);
        TerrainGraphicsResources = new TerrainGraphicsResources(DeviceContext!, DeferredTargets.GBufferRenderPass, UniformBufferResources.DescriptorSetLayout, MaterialTextureArrayData.From(TerrainMaterials),
            shaderNameOverride: Path.Combine("object", "terrain"), createWireframePipeline: true, colorAttachmentCount: 3);
        TerrainGraphicsResources.CreatePipeline();
        PathGraphicsResources = new TerrainGraphicsResources(DeviceContext!, DeferredTargets.GBufferRenderPass, UniformBufferResources.DescriptorSetLayout, MaterialTextureArrayData.From(PathMaterials),
            shaderNameOverride: Path.Combine("object", "terrain"), createWireframePipeline: true, colorAttachmentCount: 3);
        PathGraphicsResources.CreatePipeline();
        PathRailingGraphicsResources = new TerrainGraphicsResources(DeviceContext!, DeferredTargets.GBufferRenderPass, UniformBufferResources.DescriptorSetLayout,
            MaterialTextureArrayData.From(PathMaterials), shaderNameOverride: Path.Combine("object", "track"), createWireframePipeline: true,
            colorAttachmentCount: 3, useTrackVertexColor: true);
        PathRailingGraphicsResources.CreatePipeline();
        PathSupportGraphicsResources = new TerrainGraphicsResources(DeviceContext!, DeferredTargets.GBufferRenderPass, UniformBufferResources.DescriptorSetLayout,
            MaterialTextureArrayData.From(PathMaterials), shaderNameOverride: Path.Combine("object", "track"), createWireframePipeline: true,
            colorAttachmentCount: 3, useTrackVertexColor: true);
        PathSupportGraphicsResources.CreatePipeline();
        WaterGraphicsResources = new WaterGraphicsResources(DeviceContext!, DeferredTargets.WaterRenderPass, UniformBufferResources.DescriptorSetLayout, SkyTargets, DeferredTargets.ShadowTargets!);
        WaterGraphicsResources.CreatePipeline();
        ModelTextureHeap = new ModelTextureHeap(DeviceContext!, CommandPool);
        ShadowGraphicsResources = new ShadowGraphicsResources(DeviceContext!, DeferredTargets.ShadowTargets!.RenderPass,
            UniformBufferResources.DescriptorSetLayout, ModelTextureHeap.DescriptorSetLayout, ModelTextureHeap.SamplerDescriptorSetLayout);
        InitializeSkyTextures();
        TerrainGraphicsResources.CreateTextureResources(CommandPool);
        PathGraphicsResources.CreateTextureResources(CommandPool);
        ModelTextureHeap.SetParkTextureStartIndex();
    }

    public void CreatePathSurfaceGraphicsResources(PathSurfaceMaterials materials, PathSurfaceMeshData mesh)
    {
        ClearPathSurfaceGraphicsResources();
        var surfaceResources = new PathSurfaceGraphicsResources(materials, DeviceContext!, DeferredTargets!.GBufferRenderPass,
            DeferredTargets.ShadowTargets!.RenderPass, UniformBufferResources!.DescriptorSetLayout, CommandPool);
        try {
            surfaceResources.UploadMesh(mesh.Vertices, mesh.Indices);
            PathSurfaceGraphicsResources = surfaceResources;
        }
        catch {
            surfaceResources.Dispose();
            throw;
        }
    }

    public void ClearPathSurfaceGraphicsResources()
    {
        PathSurfaceGraphicsResources?.Dispose();
        PathSurfaceGraphicsResources = null;
    }

    public void CreateRideSupportGraphicsResources()
    {
        RideSupportGraphicsResources?.Dispose();
        RideSupportGraphicsResources = null;

        var supportResources = new TerrainGraphicsResources(
            DeviceContext!, DeferredTargets!.GBufferRenderPass, UniformBufferResources!.DescriptorSetLayout, MaterialTextureArrayData.From(TerrainMaterials!),
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
            DeviceContext!, DeferredTargets!.GBufferRenderPass, UniformBufferResources!.DescriptorSetLayout, MaterialTextureArrayData.From(TerrainMaterials!),
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

    public void CreateTrackGraphicsResources()
    {
        TrackGraphicsResources?.Dispose();
        TrackGraphicsResources = null;

        var trackResources = new TerrainGraphicsResources(
            DeviceContext!, DeferredTargets!.GBufferRenderPass, UniformBufferResources!.DescriptorSetLayout, MaterialTextureArrayData.From(TerrainMaterials!),
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

    public void CreateVehiclePathGraphicsResources()
    {
        VehiclePathGraphicsResources?.Dispose();
        VehiclePathGraphicsResources = null;

        var lineResources = new TerrainGraphicsResources(
            DeviceContext!, DeferredTargets!.GBufferRenderPass, UniformBufferResources!.DescriptorSetLayout,
            MaterialTextureArrayData.From(TerrainMaterials!),
            shaderNameOverride: Path.Combine("object", "vehiclePathDebug"), colorAttachmentCount: 3,
            useTrackVertexColor: true, topology: PrimitiveTopology.LineList);

        try {
            lineResources.CreatePipeline();
            VehiclePathGraphicsResources = lineResources;
        }
        catch {
            lineResources.Dispose();
            throw;
        }
    }

    public void ApplyShadowSettings(uint resolution, float radius)
    {
        var deferredTargets = DeferredTargets ?? throw new InvalidOperationException("Deferred render targets are unavailable.");
        var currentTargets = deferredTargets.ShadowTargets ?? throw new InvalidOperationException("Shadow render targets are unavailable.");
        if (currentTargets.Resolution == resolution) {
            currentTargets.UpdateRadius(radius);
            return;
        }

        if (Vk!.DeviceWaitIdle(LogicalDevice) != Result.Success)
            throw new InvalidOperationException("Failed to wait for the Vulkan device before applying shadow resolution.");

        var replacementTargets = new ShadowRenderTargets(DeviceContext!, currentTargets.DepthFormat, resolution, radius);
        ShadowGraphicsResources? replacementGraphics = null;
        DeferredPassResources? replacementPasses = null;

        try {
            replacementGraphics = new ShadowGraphicsResources(DeviceContext!, replacementTargets.RenderPass,
                UniformBufferResources!.DescriptorSetLayout, ModelTextureHeap!.DescriptorSetLayout, ModelTextureHeap.SamplerDescriptorSetLayout);
            deferredTargets.SetShadowTargets(replacementTargets);
            replacementPasses = new DeferredPassResources(DeviceContext!, RenderPass, deferredTargets, SkyTargets!, UniformBufferResources!);
        }
        catch {
            deferredTargets.SetShadowTargets(currentTargets);
            replacementPasses?.Dispose();
            replacementGraphics?.Dispose();
            replacementTargets.Dispose();
            throw;
        }

        var previousGraphics = ShadowGraphicsResources;
        var previousPasses = DeferredPassResources;
        ShadowGraphicsResources = replacementGraphics;
        DeferredPassResources = replacementPasses;
        previousPasses?.Dispose();
        previousGraphics?.Dispose();
        currentTargets.Dispose();
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

    public void CreateVehicleResources(VehicleRenderer.ModelBatch batch)
    {
        var meshes = batch.Model.Data.Meshes;
        var modelResources = new ModelGraphicsResources[meshes.Length];
        try {
            for (var i = 0; i < meshes.Length; i++) {
                var mesh = meshes[i];
                var resources = new ModelGraphicsResources(DeviceContext!, DeferredTargets!.GBufferRenderPass, UniformBufferResources!.DescriptorSetLayout,
                    mesh.Material, ModelTextureHeap!, isVehicle: true, instanceBufferCount: MaxFramesInFlight);
                modelResources[i] = resources;
                resources.CreatePipeline();
                resources.CreateTextureResources(CommandPool);
                resources.UploadMesh(mesh);
            }
            VehicleGraphicsResources.Add((batch, modelResources));
        }
        catch {
            foreach (var resources in modelResources)
                resources?.Dispose();
            throw;
        }
    }

    public void DisposeVehicleResources()
    {
        foreach (var (_, meshes) in VehicleGraphicsResources)
        foreach (var resources in meshes)
            resources.Dispose();
        VehicleGraphicsResources.Clear();
    }

    public void RecreateSwapchain()
    {
        if (Vk!.DeviceWaitIdle(LogicalDevice) != Result.Success)
            throw new InvalidOperationException("Failed to wait for the Vulkan device before resizing park resources.");

        ModelTextureHeap?.SetCommandPool(CommandPool);
        DeferredPassResources?.Dispose();
        DeferredPassResources = null;
        DeferredTargets!.Resize(SwapchainExtent);
        DeferredPassResources = new DeferredPassResources(DeviceContext!, RenderPass, DeferredTargets, SkyTargets!, UniformBufferResources!);
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        var vk = Vk;
        var logicalDevice = LogicalDevice;
        if (vk is not null && logicalDevice.Handle != 0)
            vk.DeviceWaitIdle(logicalDevice);

        foreach (var (_, resources) in PathAccessoryGraphicsResources)
            resources.Dispose();

        PathAccessoryGraphicsResources.Clear();
        foreach (var (_, resources) in TreeSceneryGraphicsResources)
            resources.Dispose();

        TreeSceneryGraphicsResources.Clear();
        foreach (var resources in RideEntranceModelResources)
            resources.Dispose();
        RideEntranceModelResources.Clear();
        DisposeVehicleResources();
        WaterGraphicsResources?.Dispose();
        WaterGraphicsResources = null;
        TrackGraphicsResources?.Dispose();
        TrackGraphicsResources = null;
        VehiclePathGraphicsResources?.Dispose();
        VehiclePathGraphicsResources = null;
        RideSupportGraphicsResources?.Dispose();
        RideSupportGraphicsResources = null;
        RideEntranceGraphicsResources?.Dispose();
        RideEntranceGraphicsResources = null;
        PathSupportGraphicsResources?.Dispose();
        PathSupportGraphicsResources = null;
        PathRailingGraphicsResources?.Dispose();
        PathRailingGraphicsResources = null;
        ClearPathSurfaceGraphicsResources();
        PathGraphicsResources?.Dispose();
        PathGraphicsResources = null;
        TerrainGraphicsResources?.Dispose();
        TerrainGraphicsResources = null;
        ShadowGraphicsResources?.Dispose();
        ShadowGraphicsResources = null;
        ModelTextureHeap?.Dispose();
        ModelTextureHeap = null;
        DeferredPassResources?.Dispose();
        DeferredPassResources = null;
        DeferredTargets?.Dispose();
        DeferredTargets = null;
        SkyTargets?.Dispose();
        SkyTargets = null;
        UniformBufferResources?.Dispose();
        UniformBufferResources = null;
        TerrainMaterials = null;
        PathMaterials = null;
        PathAccessoryAssets = null;
        TreeSceneryAssets = null;
    }
}
