using RCT3D.Render.Paths.Meshing;
using Silk.NET.Vulkan;
using System.Runtime.InteropServices;

namespace RCT3D.Render.Paths;

internal sealed unsafe partial class PathSurfaceGraphicsResources
{
    private void CreateLayouts(DescriptorSetLayout uniforms)
    {
        var layouts = stackalloc DescriptorSetLayout[2];
        layouts[0] = uniforms; layouts[1] = textureLayout;
        var info = new PipelineLayoutCreateInfo { SType = StructureType.PipelineLayoutCreateInfo, SetLayoutCount = 2, PSetLayouts = layouts };
        Check(context.Api.CreatePipelineLayout(context.LogicalDevice, in info, null, out var layout), "create path pipeline layout");
        PipelineLayout = layout;
        var push = new PushConstantRange { StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, Size = 8 };
        info.PushConstantRangeCount = 1; info.PPushConstantRanges = &push;
        Check(context.Api.CreatePipelineLayout(context.LogicalDevice, in info, null, out layout), "create path shadow layout");
        ShadowPipelineLayout = layout;
    }

    private Pipeline CreatePipeline(RenderPass pass, PipelineLayout layout, bool shadow, bool wireframe, uint colorCount)
    {
        var vk = context.Api; var device = context.LogicalDevice;
        ShaderModule vert = default, frag = default;
        var name = Marshal.StringToHGlobalAnsi("main");
        try {
            vert = LoadShader(shadow ? "shadow" : "object", "path.vert.spv");
            frag = LoadShader(shadow ? "shadow" : "object", "path.frag.spv");
            var stages = stackalloc PipelineShaderStageCreateInfo[2];
            stages[0] = new PipelineShaderStageCreateInfo { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.VertexBit, Module = vert, PName = (byte*)name };
            stages[1] = new PipelineShaderStageCreateInfo { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.FragmentBit, Module = frag, PName = (byte*)name };
            var binding = new VertexInputBindingDescription { Binding = 0, Stride = (uint)Marshal.SizeOf<PathSurfaceVertex>(), InputRate = VertexInputRate.Vertex };
            var attributes = stackalloc VertexInputAttributeDescription[5];
            attributes[0] = Attribute(0, Format.R32G32B32Sfloat, nameof(PathSurfaceVertex.Position));
            attributes[1] = Attribute(1, Format.R32G32B32Sfloat, nameof(PathSurfaceVertex.Normal));
            attributes[2] = Attribute(2, Format.R32G32Sfloat, nameof(PathSurfaceVertex.TextureCoordinate));
            attributes[3] = Attribute(3, Format.R32Uint, nameof(PathSurfaceVertex.DescriptorIndex));
            attributes[4] = Attribute(4, Format.R32Uint, nameof(PathSurfaceVertex.Layer));
            var vertex = new PipelineVertexInputStateCreateInfo { SType = StructureType.PipelineVertexInputStateCreateInfo, VertexBindingDescriptionCount = 1, PVertexBindingDescriptions = &binding, VertexAttributeDescriptionCount = 5, PVertexAttributeDescriptions = attributes };
            var assembly = new PipelineInputAssemblyStateCreateInfo { SType = StructureType.PipelineInputAssemblyStateCreateInfo, Topology = PrimitiveTopology.TriangleList };
            var viewport = new PipelineViewportStateCreateInfo { SType = StructureType.PipelineViewportStateCreateInfo, ViewportCount = 1, ScissorCount = 1 };
            var raster = new PipelineRasterizationStateCreateInfo { SType = StructureType.PipelineRasterizationStateCreateInfo, PolygonMode = wireframe ? PolygonMode.Line : PolygonMode.Fill, CullMode = CullModeFlags.BackBit, FrontFace = FrontFace.CounterClockwise, LineWidth = 1 };
            var samples = new PipelineMultisampleStateCreateInfo { SType = StructureType.PipelineMultisampleStateCreateInfo, RasterizationSamples = SampleCountFlags.Count1Bit };
            var depth = new PipelineDepthStencilStateCreateInfo { SType = StructureType.PipelineDepthStencilStateCreateInfo, DepthTestEnable = true, DepthWriteEnable = true, DepthCompareOp = shadow ? CompareOp.Less : CompareOp.LessOrEqual, MaxDepthBounds = 1 };
            var colors = new PipelineColorBlendAttachmentState[colorCount];
            for (var i = 0; i < colors.Length; i++) colors[i] = new PipelineColorBlendAttachmentState { ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit };
            var dyns = stackalloc DynamicState[2] { DynamicState.Viewport, DynamicState.Scissor };
            var dynamic = new PipelineDynamicStateCreateInfo { SType = StructureType.PipelineDynamicStateCreateInfo, DynamicStateCount = 2, PDynamicStates = dyns };
            fixed (PipelineColorBlendAttachmentState* colorPtr = colors) {
                var blend = new PipelineColorBlendStateCreateInfo { SType = StructureType.PipelineColorBlendStateCreateInfo, AttachmentCount = colorCount, PAttachments = colorPtr };
                var info = new GraphicsPipelineCreateInfo { SType = StructureType.GraphicsPipelineCreateInfo, StageCount = 2, PStages = stages, Layout = layout, RenderPass = pass,
                    PVertexInputState = &vertex, PInputAssemblyState = &assembly, PViewportState = &viewport, PRasterizationState = &raster, PMultisampleState = &samples,
                    PDepthStencilState = &depth, PColorBlendState = &blend, PDynamicState = &dynamic };
                Check(vk.CreateGraphicsPipelines(device, default, 1, in info, null, out var pipeline), "create path pipeline");
                return pipeline;
            }
        }
        finally {
            if (vert.Handle != 0) vk.DestroyShaderModule(device, vert, null);
            if (frag.Handle != 0) vk.DestroyShaderModule(device, frag, null);
            Marshal.FreeHGlobal(name);
        }
    }

    private static VertexInputAttributeDescription Attribute(uint location, Format format, string field)
        => new() { Location = location, Binding = 0, Format = format, Offset = (uint)Marshal.OffsetOf<PathSurfaceVertex>(field) };

    private ShaderModule LoadShader(string folder, string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Shaders", folder, file));
        fixed (byte* ptr = bytes) {
            var info = new ShaderModuleCreateInfo { SType = StructureType.ShaderModuleCreateInfo, CodeSize = (nuint)bytes.Length, PCode = (uint*)ptr };
            Check(context.Api.CreateShaderModule(context.LogicalDevice, in info, null, out var shader), "create path shader");
            return shader;
        }
    }
}
