using RCT3D.Render.Paths;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Terrain;
using Silk.NET.Vulkan;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3D.Render;

internal sealed unsafe class ShadowGraphicsResources : IDisposable
{
    private readonly VulkanDeviceContext _context;
    private bool _disposed;

    public PipelineLayout PipelineLayout { get; private set; }
    public Pipeline TerrainPipeline { get; private set; }
    public Pipeline TrackPipeline { get; private set; }
    public Pipeline ModelPipeline { get; private set; }

    public ShadowGraphicsResources(VulkanDeviceContext context, RenderPass renderPass, DescriptorSetLayout uniformDescriptorSetLayout,
        DescriptorSetLayout modelTextureDescriptorSetLayout, DescriptorSetLayout modelSamplerDescriptorSetLayout)
    {
        _context = context;

        try {
            CreatePipelineLayout(uniformDescriptorSetLayout, modelTextureDescriptorSetLayout, modelSamplerDescriptorSetLayout);
            CreatePipelines(renderPass);
        }
        catch {
            Dispose();
            throw;
        }
    }

    private void CreatePipelineLayout(DescriptorSetLayout uniformDescriptorSetLayout, DescriptorSetLayout modelTextureDescriptorSetLayout,
        DescriptorSetLayout modelSamplerDescriptorSetLayout)
    {
        var descriptorLayouts = stackalloc DescriptorSetLayout[3];
        descriptorLayouts[0] = uniformDescriptorSetLayout;
        descriptorLayouts[1] = modelTextureDescriptorSetLayout;
        descriptorLayouts[2] = modelSamplerDescriptorSetLayout;
        var pushConstantRange = new PushConstantRange {
            StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
            Offset = 0,
            Size = sizeof(uint) * 2
        };
        var createInfo = new PipelineLayoutCreateInfo {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 3,
            PSetLayouts = descriptorLayouts,
            PushConstantRangeCount = 1,
            PPushConstantRanges = &pushConstantRange
        };

        Check(_context.Api.CreatePipelineLayout(_context.LogicalDevice, in createInfo, null, out var layout), "shadow pipeline layout");
        PipelineLayout = layout;
    }

    private void CreatePipelines(RenderPass renderPass)
    {
        var meshShader = default(ShaderModule);
        var modelShader = default(ShaderModule);
        var modelFragmentShader = default(ShaderModule);

        try {
            meshShader = CreateVertexShader("mesh");
            modelShader = CreateShader("model.vert.spv");
            modelFragmentShader = CreateShader("model.frag.spv");
            TerrainPipeline = CreatePipeline(meshShader, renderPass, (uint)Marshal.SizeOf<TerrainVertex>(), false);
            TrackPipeline = CreatePipeline(meshShader, renderPass, (uint)Marshal.SizeOf<TrackVertex>(), false);
            ModelPipeline = CreatePipeline(modelShader, renderPass, (uint)Marshal.SizeOf<ModelVertex>(), true, modelFragmentShader);
        }
        finally {
            if (meshShader.Handle != 0) _context.Api.DestroyShaderModule(_context.LogicalDevice, meshShader, null);
            if (modelShader.Handle != 0) _context.Api.DestroyShaderModule(_context.LogicalDevice, modelShader, null);
            if (modelFragmentShader.Handle != 0) _context.Api.DestroyShaderModule(_context.LogicalDevice, modelFragmentShader, null);
        }
    }

    private ShaderModule CreateVertexShader(string shaderName)
        => CreateShader($"{shaderName}.vert.spv");

    private ShaderModule CreateShader(string fileName)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Shaders", "shadow", fileName);

        if (!File.Exists(path))
            throw new FileNotFoundException($"Shadow vertex shader is missing: {path}");

        var shaderCode = File.ReadAllBytes(path);
        fixed (byte* codePointer = shaderCode) {
            var createInfo = new ShaderModuleCreateInfo {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)shaderCode.Length,
                PCode = (uint*)codePointer
            };

            Check(_context.Api.CreateShaderModule(_context.LogicalDevice, in createInfo, null, out var shader), $"{fileName} shadow shader module");
            return shader;
        }
    }

    private Pipeline CreatePipeline(ShaderModule vertexShader, RenderPass renderPass, uint vertexStride, bool useInstances, ShaderModule fragmentShader = default)
    {
        var bindings = useInstances
            ? new[] {
                new VertexInputBindingDescription { Binding = 0, Stride = vertexStride, InputRate = VertexInputRate.Vertex },
                new VertexInputBindingDescription { Binding = 1, Stride = (uint)Marshal.SizeOf<ModelInstance>(), InputRate = VertexInputRate.Instance }
            }
            : new[] {
                new VertexInputBindingDescription { Binding = 0, Stride = vertexStride, InputRate = VertexInputRate.Vertex }
            };

        var attributes = useInstances
            ? new[] {
                new VertexInputAttributeDescription { Location = 0, Binding = 0, Format = Format.R32G32B32Sfloat, Offset = 0 },
                new VertexInputAttributeDescription { Location = 2, Binding = 0, Format = Format.R32G32Sfloat, Offset = (uint)Marshal.OffsetOf<ModelVertex>(nameof(ModelVertex.TextureCoordinate)) },
                new VertexInputAttributeDescription { Location = 3, Binding = 1, Format = Format.R32G32B32A32Sfloat, Offset = 0 },
                new VertexInputAttributeDescription { Location = 4, Binding = 1, Format = Format.R32G32B32A32Sfloat, Offset = 16 },
                new VertexInputAttributeDescription { Location = 5, Binding = 1, Format = Format.R32G32B32A32Sfloat, Offset = 32 },
                new VertexInputAttributeDescription { Location = 6, Binding = 1, Format = Format.R32G32B32A32Sfloat, Offset = 48 }
            }
            : new[] {
                new VertexInputAttributeDescription { Location = 0, Binding = 0, Format = Format.R32G32B32Sfloat, Offset = 0 }
            };

        var stageName = Marshal.StringToHGlobalAnsi("main");
        try {
            var stages = fragmentShader.Handle == 0 ? new PipelineShaderStageCreateInfo[1] : new PipelineShaderStageCreateInfo[2];
            stages[0] = new PipelineShaderStageCreateInfo {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = vertexShader,
                PName = (byte*)stageName
            };
            if (fragmentShader.Handle != 0) {
                stages[1] = new PipelineShaderStageCreateInfo {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.FragmentBit,
                    Module = fragmentShader,
                    PName = (byte*)stageName
                };
            }
            var vertexInput = new PipelineVertexInputStateCreateInfo {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = (uint)bindings.Length,
                VertexAttributeDescriptionCount = (uint)attributes.Length
            };
            var inputAssembly = new PipelineInputAssemblyStateCreateInfo {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = PrimitiveTopology.TriangleList
            };
            var viewport = new PipelineViewportStateCreateInfo {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                ScissorCount = 1
            };
            var rasterizer = new PipelineRasterizationStateCreateInfo {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                PolygonMode = PolygonMode.Fill,
                CullMode = CullModeFlags.BackBit,
                FrontFace = FrontFace.CounterClockwise,
                LineWidth = 1.0f
            };
            var multisampling = new PipelineMultisampleStateCreateInfo {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                RasterizationSamples = SampleCountFlags.Count1Bit
            };
            var depthStencil = new PipelineDepthStencilStateCreateInfo {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = true,
                DepthWriteEnable = true,
                DepthCompareOp = CompareOp.Less,
                MinDepthBounds = 0.0f,
                MaxDepthBounds = 1.0f
            };
            var colorBlend = new PipelineColorBlendStateCreateInfo {
                SType = StructureType.PipelineColorBlendStateCreateInfo
            };
            var dynamicStates = new[] { DynamicState.Viewport, DynamicState.Scissor };
            var dynamicState = new PipelineDynamicStateCreateInfo {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = (uint)dynamicStates.Length
            };
            var pipelineInfo = new GraphicsPipelineCreateInfo {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = (uint)stages.Length,
                Layout = PipelineLayout,
                RenderPass = renderPass,
                Subpass = 0,
                PVertexInputState = &vertexInput,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewport,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PDepthStencilState = &depthStencil,
                PColorBlendState = &colorBlend,
                PDynamicState = &dynamicState
            };

            fixed (VertexInputBindingDescription* bindingsPointer = bindings)
            fixed (VertexInputAttributeDescription* attributesPointer = attributes)
            fixed (PipelineShaderStageCreateInfo* stagesPointer = stages)
            fixed (DynamicState* dynamicStatesPointer = dynamicStates) {
                vertexInput.PVertexBindingDescriptions = bindingsPointer;
                vertexInput.PVertexAttributeDescriptions = attributesPointer;
                dynamicState.PDynamicStates = dynamicStatesPointer;
                pipelineInfo.PStages = stagesPointer;
                Check(_context.Api.CreateGraphicsPipelines(_context.LogicalDevice, default, 1, in pipelineInfo, null, out var pipeline), "shadow graphics pipeline");
                return pipeline;
            }
        }
        finally {
            Marshal.FreeHGlobal(stageName);
        }
    }

    private static void Check(Result result, string operation)
    {
        if (result != Result.Success)
            throw new Exception($"Failed to create {operation}: {result}.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var vk = _context.Api;
        var device = _context.LogicalDevice;
        if (TerrainPipeline.Handle != 0) vk.DestroyPipeline(device, TerrainPipeline, null);
        if (TrackPipeline.Handle != 0) vk.DestroyPipeline(device, TrackPipeline, null);
        if (ModelPipeline.Handle != 0) vk.DestroyPipeline(device, ModelPipeline, null);
        if (PipelineLayout.Handle != 0) vk.DestroyPipelineLayout(device, PipelineLayout, null);

        TerrainPipeline = default;
        TrackPipeline = default;
        ModelPipeline = default;
        PipelineLayout = default;
    }
}
