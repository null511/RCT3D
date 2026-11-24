using RCT3D.Render.Paths;
using RCT3D.Render.Terrain;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace RCT3D.Render;

internal sealed unsafe class ModelGraphicsResources : IDisposable
{
    private readonly VulkanDeviceContext _context;
    private readonly TerrainGraphicsResources _graphicsResources;
    private readonly ModelTextureHeap _textureHeap;
    private readonly uint _baseTextureIndex;
    private ModelRegionRange[] regionBatches = [];
    private int regionBatchCount;

    public Pipeline Pipeline => _graphicsResources.Pipeline;
    public Pipeline WireframePipeline => _graphicsResources.WireframePipeline;
    public PipelineLayout PipelineLayout => _graphicsResources.PipelineLayout;
    public uint BaseTextureIndex => _baseTextureIndex;
    public Buffer VertexBuffer => _graphicsResources.VertexBuffer;
    public Buffer IndexBuffer => _graphicsResources.IndexBuffer;
    public Buffer InstanceBuffer => _graphicsResources.InstanceBuffer;
    public uint IndexCount => _graphicsResources.IndexCount;
    public ReadOnlySpan<ModelRegionRange> RegionBatches => regionBatches.AsSpan(0, regionBatchCount);


    public ModelGraphicsResources(VulkanDeviceContext context, RenderPass renderPass, DescriptorSetLayout uniformDescriptorSetLayout,
        ImportedModelMaterial material, ModelTextureHeap textureHeap, bool isVehicle = false, bool createWireframePipeline = false,
        int instanceBufferCount = 1)
    {
        _context = context;
        _textureHeap = textureHeap;
        var packedMaterial = ModelMaterialLibrary.FromImportedMaterial(material);
        _baseTextureIndex = textureHeap.AddMaterial(packedMaterial);

        _graphicsResources = new TerrainGraphicsResources(
            context,
            renderPass,
            uniformDescriptorSetLayout,
            MaterialTextureArrayData.FromModel(packedMaterial),
            useInstances: true,
            shaderNameOverride: Path.Combine("object", isVehicle ? "vehicle" : "model"),
            createWireframePipeline: createWireframePipeline,
            colorAttachmentCount: 3u,
            sharedModelMaterialDescriptorSetLayout: textureHeap.DescriptorSetLayout,
            sharedModelSamplerDescriptorSetLayout: textureHeap.SamplerDescriptorSetLayout,
            instanceBufferCount: instanceBufferCount);
    }

    public void CreatePipeline() => _graphicsResources.CreatePipeline();

    public void CreateTextureResources(CommandPool commandPool) { }

    public void UploadMesh(ImportedModelMesh mesh)
    {
        var vertices = new ModelVertex[mesh.Vertices.Length];
        for (var index = 0; index < vertices.Length; index++) {
            var vertex = mesh.Vertices[index];
            vertices[index] = new ModelVertex(vertex.Position, vertex.Normal, vertex.TextureCoordinate);
        }

        _graphicsResources.UploadModelMesh(vertices, mesh.Indices);
    }

    public void UploadInstances(ModelRegionBatch[] regions)
    {
        var totalInstances = 0;
        foreach (var region in regions)
            totalInstances = checked(totalInstances + region.Instances.Length);

        var instances = new ModelInstance[totalInstances];
        var uploadedRegions = new ModelRegionRange[regions.Length];
        var firstInstance = 0;
        for (var i = 0; i < regions.Length; i++) {
            var region = regions[i];
            region.Instances.CopyTo(instances, firstInstance);
            uploadedRegions[i] = new ModelRegionRange(
                region.Region, region.Bounds, checked((uint)firstInstance), region.InstanceCount);
            firstInstance += region.Instances.Length;
        }

        UploadInstances(instances, uploadedRegions);
    }

    public void UploadInstances(ReadOnlySpan<ModelInstance> instances, ReadOnlySpan<ModelRegionRange> regions, uint currentFrame = 0)
    {
        regionBatchCount = 0;
        if (instances.IsEmpty) return;

        if (regionBatches.Length < regions.Length)
            Array.Resize(ref regionBatches, Math.Max(regions.Length, regionBatches.Length * 2));

        _graphicsResources.UploadInstances(instances, currentFrame);
        regions.CopyTo(regionBatches);
        regionBatchCount = regions.Length;
    }

    public void ClearInstances() => regionBatchCount = 0;

    public void DrawInstanced(CommandBuffer commandBuffer, DescriptorSet uniformDescriptorSet, ModelRegionRange region, bool wireframe = false)
    {
        if (IndexCount == 0 || region.InstanceCount == 0)
            return;

        var vk = _context.Api;
        var pipeline = wireframe && WireframePipeline.Handle != 0 ? WireframePipeline : Pipeline;
        vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, pipeline);
        var descriptorSets = stackalloc DescriptorSet[3];
        descriptorSets[0] = uniformDescriptorSet;
        descriptorSets[1] = _textureHeap.DescriptorSet;
        descriptorSets[2] = _textureHeap.SamplerDescriptorSet;
        vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, PipelineLayout, 0, 3, descriptorSets, 0, null);
        var baseTextureIndex = _baseTextureIndex;
        vk.CmdPushConstants(commandBuffer, PipelineLayout, ShaderStageFlags.FragmentBit, 0, sizeof(uint), &baseTextureIndex);

        var vertexBuffers = stackalloc Buffer[2];
        vertexBuffers[0] = VertexBuffer;
        vertexBuffers[1] = InstanceBuffer;
        var offsets = stackalloc ulong[2];
        offsets[0] = 0;
        offsets[1] = 0;
        vk.CmdBindVertexBuffers(commandBuffer, 0, 2, vertexBuffers, offsets);
        vk.CmdBindIndexBuffer(commandBuffer, IndexBuffer, 0, IndexType.Uint32);
        vk.CmdDrawIndexed(commandBuffer, IndexCount, region.InstanceCount, 0, 0, region.FirstInstance);
    }

    public void ClearMesh() => _graphicsResources.ClearMesh();

    public void DisposeMeshResources() => _graphicsResources.Dispose();

    public void Dispose() => _graphicsResources.Dispose();
}
