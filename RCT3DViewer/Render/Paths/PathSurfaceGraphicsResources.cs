using RCT3D.Render.Paths.Meshing;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace RCT3D.Render.Paths;

// Create, upload, replace and dispose only after prior GPU uses have completed. The command
// pool must belong to the graphics queue family and remain valid throughout construction.
internal sealed unsafe partial class PathSurfaceGraphicsResources : IDisposable
{
    // Must match both path shaders' descriptor array lengths.
    public const uint DescriptorCapacity = 64;
    private readonly VulkanDeviceContext context;
    private readonly CommandPool commandPool;
    private readonly uint[] layerCounts;
    private readonly List<(Image Image, DeviceMemory Memory, ImageView View)> images = [];
    private DescriptorSetLayout textureLayout;
    private DescriptorPool pool;
    private Sampler sampler;
    private Buffer vertexBuffer, indexBuffer;
    private DeviceMemory vertexMemory, indexMemory;
    private bool disposed;

    public PipelineLayout PipelineLayout { get; private set; }
    public PipelineLayout ShadowPipelineLayout { get; private set; }
    public Pipeline Pipeline { get; private set; }
    public Pipeline WireframePipeline { get; private set; }
    public Pipeline ShadowPipeline { get; private set; }
    public DescriptorSet DescriptorSet { get; private set; }
    public Buffer VertexBuffer => vertexBuffer;
    public Buffer IndexBuffer => indexBuffer;
    public uint IndexCount { get; private set; }

    public PathSurfaceGraphicsResources(PathSurfaceMaterials materials, VulkanDeviceContext context, RenderPass renderPass,
        RenderPass shadowRenderPass, DescriptorSetLayout uniformDescriptorSetLayout, CommandPool commandPool, uint colorAttachmentCount = 3)
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(context);
        this.context = context;
        this.commandPool = commandPool;
        layerCounts = materials.Arrays.Select(a => checked((uint)a.LayerCount)).ToArray();
        try {
            Validate(materials);
            CreateTextures(materials);
            CreateLayouts(uniformDescriptorSetLayout);
            Pipeline = CreatePipeline(renderPass, PipelineLayout, false, false, colorAttachmentCount);
            if (context.SupportsWireframe)
                WireframePipeline = CreatePipeline(renderPass, PipelineLayout, false, true, colorAttachmentCount);
            ShadowPipeline = CreatePipeline(shadowRenderPass, ShadowPipelineLayout, true, false, 0);
        }
        catch { Dispose(); throw; }
    }

    private void Validate(PathSurfaceMaterials materials)
    {
        if (!context.SupportsPathTextureNonUniformIndexing)
            throw new NotSupportedException("Path rendering requires Vulkan 1.2 sampled-image non-uniform indexing.");
        if (commandPool.Handle == 0) throw new ArgumentException("A valid command pool is required.", nameof(commandPool));
        if (materials.Arrays.Count == 0 || materials.Arrays.Count > DescriptorCapacity)
            throw new InvalidOperationException($"Park needs {materials.Arrays.Count} path surface arrays; capacity is {DescriptorCapacity}, including fallback.");
        context.Api.GetPhysicalDeviceProperties(context.PhysicalDevice, out var properties);
        var limits = properties.Limits;
        // Uniform layout includes four fragment-visible UBOs, even when shaders do not use all of them.
        if (limits.MaxPerStageDescriptorSampledImages < DescriptorCapacity * 3 || limits.MaxDescriptorSetSampledImages < DescriptorCapacity * 3 ||
            limits.MaxPerStageDescriptorSamplers < 1 || limits.MaxDescriptorSetSamplers < 1 || limits.MaxPerStageResources < DescriptorCapacity * 3 + 5 ||
            limits.MaxPerStageDescriptorUniformBuffers < 4 || limits.MaxDescriptorSetUniformBuffers < 4 || limits.MaxBoundDescriptorSets < 2)
            throw new NotSupportedException("Vulkan descriptor limits are insufficient for path rendering: 192 sampled images, one sampler, and four uniform buffers.");
        foreach (var array in materials.Arrays) {
            if (array.Width != 32 || array.Height != 32 || array.LayerCount < 1 || array.LayerCount > limits.MaxImageArrayLayers ||
                array.ColorPixels.Length != array.NormalPixels.Length || array.ColorPixels.Length != array.SpecularPixels.Length ||
                array.ColorPixels.Length != checked(array.Width * array.Height * 4 * array.LayerCount))
                throw new InvalidDataException("Invalid or unsupported path texture-array dimensions or layer count.");
        }
        foreach (var format in new[] { Format.R8G8B8A8Srgb, Format.R8G8B8A8Unorm }) {
            context.Api.GetPhysicalDeviceFormatProperties(context.PhysicalDevice, format, out var props);
            if ((props.OptimalTilingFeatures & FormatFeatureFlags.SampledImageBit) == 0)
                throw new NotSupportedException($"Path texture format {format} cannot be sampled.");
            var result = context.Api.GetPhysicalDeviceImageFormatProperties(context.PhysicalDevice, format, ImageType.Type2D, ImageTiling.Optimal,
                ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit, 0, out var imageProps);
            if (result != Result.Success || imageProps.MaxExtent.Width < 32 || imageProps.MaxExtent.Height < 32 || imageProps.MaxMipLevels < 6 ||
                (imageProps.SampleCounts & SampleCountFlags.Count1Bit) == 0 ||
                materials.Arrays.Any(a => a.LayerCount > imageProps.MaxArrayLayers || (ulong)a.LayerCount * 5460 > imageProps.MaxResourceSize))
                throw new NotSupportedException($"Vulkan image limits do not support the path arrays in format {format}.");
        }
    }

    public void UploadMesh(PathSurfaceVertex[] vertices, uint[] indices)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(indices);
        foreach (var vertex in vertices) {
            if (vertex.DescriptorIndex >= layerCounts.Length || vertex.Layer >= layerCounts[vertex.DescriptorIndex])
                throw new ArgumentException("A path vertex references an invalid descriptor or layer.", nameof(vertices));
        }
        if (indices.Any(i => i >= vertices.Length)) throw new ArgumentOutOfRangeException(nameof(indices));
        ClearMesh();
        if (vertices.Length == 0 || indices.Length == 0) return;
        try {
            Upload(vertices, BufferUsageFlags.VertexBufferBit, out vertexBuffer, out vertexMemory);
            Upload(indices, BufferUsageFlags.IndexBufferBit, out indexBuffer, out indexMemory);
            IndexCount = (uint)indices.Length;
        }
        catch { ClearMesh(); throw; }
    }

    public void BindVisible(CommandBuffer commandBuffer, DescriptorSet uniforms, bool wireframe = false)
    {
        var pipeline = wireframe && WireframePipeline.Handle != 0 ? WireframePipeline : Pipeline;
        Bind(commandBuffer, uniforms, pipeline, PipelineLayout);
    }

    public void BindShadow(CommandBuffer commandBuffer, DescriptorSet uniforms, uint cascadeIndex)
    {
        Bind(commandBuffer, uniforms, ShadowPipeline, ShadowPipelineLayout);
        var push = stackalloc uint[2] { 0, cascadeIndex };
        context.Api.CmdPushConstants(commandBuffer, ShadowPipelineLayout, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0, 2 * sizeof(uint), push);
    }

    public void DrawRange(CommandBuffer commandBuffer, uint firstIndex, uint indexCount)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (firstIndex > IndexCount || indexCount > IndexCount - firstIndex)
            throw new ArgumentOutOfRangeException(nameof(indexCount));
        if (indexCount != 0) context.Api.CmdDrawIndexed(commandBuffer, indexCount, 1, firstIndex, 0, 0);
    }

    private void Bind(CommandBuffer commandBuffer, DescriptorSet uniforms, Pipeline pipeline, PipelineLayout layout)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IndexCount == 0) throw new InvalidOperationException("No path mesh has been uploaded.");
        var sets = stackalloc DescriptorSet[2] { uniforms, DescriptorSet };
        var buffer = vertexBuffer;
        ulong offset = 0;
        context.Api.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, pipeline);
        context.Api.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, layout, 0, 2, sets, 0, null);
        context.Api.CmdBindVertexBuffers(commandBuffer, 0, 1, &buffer, &offset);
        context.Api.CmdBindIndexBuffer(commandBuffer, indexBuffer, 0, IndexType.Uint32);
    }

    private void Upload<T>(T[] data, BufferUsageFlags usage, out Buffer buffer, out DeviceMemory memory) where T : unmanaged
    {
        var size = checked((ulong)data.Length * (ulong)sizeof(T));
        AllocateBuffer(size, usage, out buffer, out memory);
        void* ptr;
        Check(context.Api.MapMemory(context.LogicalDevice, memory, 0, size, 0, &ptr), "map path mesh");
        data.AsSpan().CopyTo(new Span<T>(ptr, data.Length));
        context.Api.UnmapMemory(context.LogicalDevice, memory);
    }

    private void AllocateBuffer(ulong size, BufferUsageFlags usage, out Buffer buffer, out DeviceMemory memory)
    {
        var vk = context.Api; var device = context.LogicalDevice; buffer = default; memory = default;
        try {
            var info = new BufferCreateInfo { SType = StructureType.BufferCreateInfo, Size = size, Usage = usage, SharingMode = SharingMode.Exclusive };
            Check(vk.CreateBuffer(device, in info, null, out buffer), "create path buffer");
            vk.GetBufferMemoryRequirements(device, buffer, out var req);
            var alloc = new MemoryAllocateInfo { SType = StructureType.MemoryAllocateInfo, AllocationSize = req.Size,
                MemoryTypeIndex = context.FindMemoryType(req.MemoryTypeBits, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit) };
            Check(vk.AllocateMemory(device, in alloc, null, out memory), "allocate path buffer");
            Check(vk.BindBufferMemory(device, buffer, memory, 0), "bind path buffer");
        }
        catch {
            if (buffer.Handle != 0) vk.DestroyBuffer(device, buffer, null);
            if (memory.Handle != 0) vk.FreeMemory(device, memory, null);
            buffer = default; memory = default; throw;
        }
    }

    private void ClearMesh()
    {
        var vk = context.Api; var device = context.LogicalDevice;
        if (vertexBuffer.Handle != 0) vk.DestroyBuffer(device, vertexBuffer, null);
        if (indexBuffer.Handle != 0) vk.DestroyBuffer(device, indexBuffer, null);
        if (vertexMemory.Handle != 0) vk.FreeMemory(device, vertexMemory, null);
        if (indexMemory.Handle != 0) vk.FreeMemory(device, indexMemory, null);
        vertexBuffer = default; indexBuffer = default; vertexMemory = default; indexMemory = default; IndexCount = 0;
    }

    private static void Check(Result result, string operation)
    {
        if (result != Result.Success) throw new InvalidOperationException($"Failed to {operation}: {result}.");
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        var vk = context.Api; var device = context.LogicalDevice;
        ClearMesh();
        if (Pipeline.Handle != 0) vk.DestroyPipeline(device, Pipeline, null);
        if (WireframePipeline.Handle != 0) vk.DestroyPipeline(device, WireframePipeline, null);
        if (ShadowPipeline.Handle != 0) vk.DestroyPipeline(device, ShadowPipeline, null);
        if (PipelineLayout.Handle != 0) vk.DestroyPipelineLayout(device, PipelineLayout, null);
        if (ShadowPipelineLayout.Handle != 0) vk.DestroyPipelineLayout(device, ShadowPipelineLayout, null);
        if (pool.Handle != 0) vk.DestroyDescriptorPool(device, pool, null);
        if (sampler.Handle != 0) vk.DestroySampler(device, sampler, null);
        foreach (var image in images) {
            vk.DestroyImageView(device, image.View, null);
            vk.DestroyImage(device, image.Image, null);
            vk.FreeMemory(device, image.Memory, null);
        }
        if (textureLayout.Handle != 0) vk.DestroyDescriptorSetLayout(device, textureLayout, null);
    }
}
