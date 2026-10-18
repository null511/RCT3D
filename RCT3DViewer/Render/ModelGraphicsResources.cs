using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace RCT3DViewer.Render;

internal sealed unsafe class ModelGraphicsResources : IDisposable
{
    private readonly VulkanDeviceContext _context;
    private readonly TerrainGraphicsResources _graphicsResources;

    public Pipeline Pipeline => _graphicsResources.Pipeline;
    public PipelineLayout PipelineLayout => _graphicsResources.PipelineLayout;
    public DescriptorSet DescriptorSet => _graphicsResources.DescriptorSet;
    public Buffer VertexBuffer => _graphicsResources.VertexBuffer;
    public Buffer IndexBuffer => _graphicsResources.IndexBuffer;
    public uint IndexCount => _graphicsResources.IndexCount;

    public ModelGraphicsResources(VulkanDeviceContext context, RenderPass renderPass, ImportedModelMaterial material)
    {
        _context = context;
        _graphicsResources = new TerrainGraphicsResources(context, renderPass, TerrainMaterialLibrary.FromModelMaterial(material));
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

    public void Draw(CommandBuffer commandBuffer, UniformBufferObject uniforms)
    {
        if (IndexCount == 0)
            return;

        var vk = _context.Api;
        vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, Pipeline);
        var descriptorSet = DescriptorSet;
        vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, PipelineLayout, 0, 1, in descriptorSet, 0, null);
        var constants = &uniforms;
        vk.CmdPushConstants(commandBuffer, PipelineLayout, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
            0, (uint)sizeof(UniformBufferObject), constants);

        ulong offset = 0;
        var vertexBuffer = VertexBuffer;
        vk.CmdBindVertexBuffers(commandBuffer, 0, 1, in vertexBuffer, in offset);
        vk.CmdBindIndexBuffer(commandBuffer, IndexBuffer, 0, IndexType.Uint32);
        vk.CmdDrawIndexed(commandBuffer, IndexCount, 1, 0, 0, 0);
    }

    public void ClearMesh() => _graphicsResources.ClearMesh();

    public void Dispose() => _graphicsResources.Dispose();
}
