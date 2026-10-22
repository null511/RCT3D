using RCT3D.Render.Paths;
using RCT3D.Render.Paths.Meshing;
using RCT3D.Render.Terrain;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace RCT3D.Render;

internal sealed unsafe class ModelGraphicsResources : IDisposable
{
    private readonly VulkanDeviceContext _context;
    private readonly TerrainGraphicsResources _graphicsResources;

    public Pipeline Pipeline => _graphicsResources.Pipeline;
    public PipelineLayout PipelineLayout => _graphicsResources.PipelineLayout;
    public DescriptorSet DescriptorSet => _graphicsResources.DescriptorSet;
    public Buffer VertexBuffer => _graphicsResources.VertexBuffer;
    public Buffer IndexBuffer => _graphicsResources.IndexBuffer;
    public Buffer InstanceBuffer => _graphicsResources.InstanceBuffer;
    public uint IndexCount => _graphicsResources.IndexCount;
    public ModelRegionBatch[] RegionBatches { get; private set; } = Array.Empty<ModelRegionBatch>();


    public ModelGraphicsResources(VulkanDeviceContext context, RenderPass renderPass, DescriptorSetLayout uniformDescriptorSetLayout, ImportedModelMaterial material, bool unlit = false, bool deferred = false)
    {
        _context = context;

        _graphicsResources = new TerrainGraphicsResources(
            context,
            renderPass,
            uniformDescriptorSetLayout,
            TerrainMaterialLibrary.FromModelMaterial(material),
            useInstances: true,
            shaderNameOverride: deferred
                ? Path.Combine("defer", unlit ? "deferredVehicleCube" : "deferredModel")
                : unlit ? "vehicleCube" : "model",
            colorAttachmentCount: deferred ? 3u : 1u);
    }

    public void CreatePipeline() => _graphicsResources.CreatePipeline();

    public void CreateTextureResources(CommandPool commandPool) => _graphicsResources.CreateTextureResources(commandPool);

    public void UploadMesh(ImportedModelMesh mesh)
    {
        var vertices = new TerrainVertex[mesh.Vertices.Length];
        for (var index = 0; index < vertices.Length; index++) {
            var vertex = mesh.Vertices[index];
            vertices[index] = new TerrainVertex(vertex.Position, vertex.Normal, vertex.TextureCoordinate, 0);
        }

        _graphicsResources.UploadMesh(new PathMeshData(vertices, mesh.Indices));
    }

    public void UploadMesh(PathMeshData mesh) => _graphicsResources.UploadMesh(mesh);

    public void UploadInstances(ModelRegionBatch[] regions)
    {
        var totalInstances = 0;
        foreach (var region in regions)
            totalInstances = checked(totalInstances + region.Instances.Length);

        var instances = new ModelInstance[totalInstances];
        var uploadedRegions = new ModelRegionBatch[regions.Length];
        var firstInstance = 0;
        for (var i = 0; i < regions.Length; i++) {
            var region = regions[i];
            region.Instances.CopyTo(instances, firstInstance);
            uploadedRegions[i] = region with { FirstInstance = checked((uint)firstInstance) };
            firstInstance += region.Instances.Length;
        }

        _graphicsResources.UploadInstances(instances);
        RegionBatches = uploadedRegions;
    }

    public void DrawInstanced(CommandBuffer commandBuffer, DescriptorSet uniformDescriptorSet, ModelRegionBatch region)
    {
        if (IndexCount == 0 || region.InstanceCount == 0)
            return;

        var vk = _context.Api;
        vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, Pipeline);
        var descriptorSets = stackalloc DescriptorSet[2];
        descriptorSets[0] = uniformDescriptorSet;
        descriptorSets[1] = DescriptorSet;
        vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, PipelineLayout, 0, 2, descriptorSets, 0, null);

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

    public void Dispose() => _graphicsResources.Dispose();
}
