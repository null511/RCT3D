using Silk.NET.Vulkan;
using System.Runtime.InteropServices;

namespace RCT3DViewer.Render;

internal sealed unsafe class FullscreenPassResources : IDisposable
{
    private readonly VulkanDeviceContext _context;
    private readonly DescriptorSetLayout _descriptorSetLayout;
    private DescriptorPool _descriptorPool;
    private Sampler _sampler;
    private DescriptorSet[] _descriptorSets = Array.Empty<DescriptorSet>();
    private bool _disposed;

    public PipelineLayout PipelineLayout { get; private set; }
    public Pipeline Pipeline { get; private set; }

    public FullscreenPassResources(VulkanDeviceContext context, RenderPass renderPass, string shaderName, ImageView[][] sampledImageSets)
    {
        _context = context;

        if (sampledImageSets.Length == 0 || sampledImageSets.Any(set => set.Length != sampledImageSets[0].Length))
            throw new ArgumentException("Each fullscreen descriptor set must contain the same nonzero number of image views.", nameof(sampledImageSets));

        try {
            _descriptorSetLayout = CreateDescriptorSetLayout(sampledImageSets[0].Length);
            CreateDescriptors(sampledImageSets);
            CreatePipeline(renderPass, shaderName);
        }
        catch {
            Dispose();
            throw;
        }
    }

    private DescriptorSetLayout CreateDescriptorSetLayout(int imageCount)
    {
        var bindings = new DescriptorSetLayoutBinding[imageCount + 1];

        for (uint binding = 0; binding < imageCount; binding++) {
            bindings[binding] = new DescriptorSetLayoutBinding {
                Binding = binding,
                DescriptorType = DescriptorType.SampledImage,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.FragmentBit
            };
        }

        bindings[imageCount] = new DescriptorSetLayoutBinding {
            Binding = (uint)imageCount,
            DescriptorType = DescriptorType.Sampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit
        };

        fixed (DescriptorSetLayoutBinding* bindingPointer = bindings) {
            var createInfo = new DescriptorSetLayoutCreateInfo {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)bindings.Length,
                PBindings = bindingPointer
            };

            if (_context.Api.CreateDescriptorSetLayout(_context.LogicalDevice, in createInfo, null, out var layout) != Result.Success)
                throw new Exception($"Failed to create {imageCount}-input fullscreen descriptor-set layout.");

            return layout;
        }
    }

    private void CreateDescriptors(ImageView[][] sampledImageSets)
    {
        var vk = _context.Api;
        var device = _context.LogicalDevice;
        var imageCount = sampledImageSets[0].Length;

        var samplerInfo = new SamplerCreateInfo {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            MipmapMode = SamplerMipmapMode.Nearest,
            AddressModeU = SamplerAddressMode.ClampToEdge,
            AddressModeV = SamplerAddressMode.ClampToEdge,
            AddressModeW = SamplerAddressMode.ClampToEdge,
            MaxAnisotropy = 1,
            MinLod = 0,
            MaxLod = 0,
            BorderColor = BorderColor.IntOpaqueBlack
        };

        if (vk.CreateSampler(device, in samplerInfo, null, out _sampler) != Result.Success)
            throw new Exception("Failed to create fullscreen-pass sampler.");

        var poolSizes = new[] {
            new DescriptorPoolSize {
                Type = DescriptorType.SampledImage,
                DescriptorCount = checked((uint)(imageCount * sampledImageSets.Length)),
            },
            new DescriptorPoolSize {
                Type = DescriptorType.Sampler,
                DescriptorCount = (uint)sampledImageSets.Length,
            },
        };

        fixed (DescriptorPoolSize* poolSizePointer = poolSizes) {
            var poolInfo = new DescriptorPoolCreateInfo {
                SType = StructureType.DescriptorPoolCreateInfo,
                MaxSets = (uint)sampledImageSets.Length,
                PoolSizeCount = (uint)poolSizes.Length,
                PPoolSizes = poolSizePointer
            };

            if (vk.CreateDescriptorPool(device, in poolInfo, null, out _descriptorPool) != Result.Success)
                throw new Exception("Failed to create fullscreen-pass descriptor pool.");
        }

        var layouts = Enumerable.Repeat(_descriptorSetLayout, sampledImageSets.Length).ToArray();
        _descriptorSets = new DescriptorSet[layouts.Length];

        fixed (DescriptorSetLayout* layoutPointer = layouts)
        fixed (DescriptorSet* descriptorPointer = _descriptorSets) {
            var allocateInfo = new DescriptorSetAllocateInfo {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _descriptorPool,
                DescriptorSetCount = (uint)layouts.Length,
                PSetLayouts = layoutPointer
            };

            if (vk.AllocateDescriptorSets(device, in allocateInfo, descriptorPointer) != Result.Success)
                throw new Exception("Failed to allocate fullscreen-pass descriptor sets.");
        }

        for (var setIndex = 0; setIndex < sampledImageSets.Length; setIndex++) {
            for (var imageIndex = 0; imageIndex < imageCount; imageIndex++) {
                var imageInfo = new DescriptorImageInfo {
                    ImageView = sampledImageSets[setIndex][imageIndex],
                    ImageLayout = ImageLayout.ShaderReadOnlyOptimal
                };

                var write = new WriteDescriptorSet {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = _descriptorSets[setIndex],
                    DstBinding = (uint)imageIndex,
                    DescriptorCount = 1,
                    DescriptorType = DescriptorType.SampledImage,
                    PImageInfo = &imageInfo
                };

                vk.UpdateDescriptorSets(device, 1, &write, 0, null);
            }

            var samplerDescriptor = new DescriptorImageInfo { Sampler = _sampler };

            var samplerWrite = new WriteDescriptorSet {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _descriptorSets[setIndex],
                DstBinding = (uint)imageCount,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.Sampler,
                PImageInfo = &samplerDescriptor
            };

            vk.UpdateDescriptorSets(device, 1, &samplerWrite, 0, null);
        }
    }

    private void CreatePipeline(RenderPass renderPass, string shaderName)
    {
        var vk = _context.Api;
        var device = _context.LogicalDevice;
        var shaderDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Shaders", "post");
        var vertexPath = Path.Combine(shaderDirectory, $"{shaderName}.vert.spv");
        var fragmentPath = Path.Combine(shaderDirectory, $"{shaderName}.frag.spv");

        if (!File.Exists(vertexPath) || !File.Exists(fragmentPath))
            throw new FileNotFoundException($"Fullscreen shader binaries are missing for '{shaderName}'.");

        ShaderModule vertexModule = default;
        ShaderModule fragmentModule = default;
        var vertexName = Marshal.StringToHGlobalAnsi("main");
        var fragmentName = Marshal.StringToHGlobalAnsi("main");

        try {
            vertexModule = CreateShaderModule(File.ReadAllBytes(vertexPath));
            fragmentModule = CreateShaderModule(File.ReadAllBytes(fragmentPath));

            var stages = new[] {
                new PipelineShaderStageCreateInfo {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.VertexBit,
                    Module = vertexModule,
                    PName = (byte*)vertexName,
                },
                new PipelineShaderStageCreateInfo {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.FragmentBit,
                    Module = fragmentModule,
                    PName = (byte*)fragmentName,
                },
            };

            var descriptorLayout = _descriptorSetLayout;

            var layoutInfo = new PipelineLayoutCreateInfo {
                SType = StructureType.PipelineLayoutCreateInfo,
                PSetLayouts = &descriptorLayout,
                SetLayoutCount = 1,
            };

            if (vk.CreatePipelineLayout(device, in layoutInfo, null, out var pipelineLayout) != Result.Success)
                throw new Exception($"Failed to create {shaderName} pipeline layout.");

            PipelineLayout = pipelineLayout;

            var vertexInput = new PipelineVertexInputStateCreateInfo {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
            };
            
            var inputAssembly = new PipelineInputAssemblyStateCreateInfo {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = PrimitiveTopology.TriangleList,
            };
            
            var viewportState = new PipelineViewportStateCreateInfo {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                ScissorCount = 1,
            };
            
            var rasterizer = new PipelineRasterizationStateCreateInfo {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                PolygonMode = PolygonMode.Fill,
                CullMode = CullModeFlags.None,
                FrontFace = FrontFace.CounterClockwise,
                LineWidth = 1.0f,
            };

            var multisampling = new PipelineMultisampleStateCreateInfo {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                RasterizationSamples = SampleCountFlags.Count1Bit,
            };
            
            var depthStencil = new PipelineDepthStencilStateCreateInfo {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthCompareOp = CompareOp.Always,
                MinDepthBounds = 0.0f,
                MaxDepthBounds = 1.0f,
                DepthTestEnable = false,
                DepthWriteEnable = false,
            };

            var blendAttachment = new PipelineColorBlendAttachmentState {
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
                BlendEnable = false,
            };

            var colorBlend = new PipelineColorBlendStateCreateInfo {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                PAttachments = &blendAttachment,
                AttachmentCount = 1,
            };

            var dynamicStates = new[] {
                DynamicState.Viewport,
                DynamicState.Scissor,
            };

            var dynamicState = new PipelineDynamicStateCreateInfo {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = (uint)dynamicStates.Length,
            };
            
            var pipelineInfo = new GraphicsPipelineCreateInfo {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = (uint)stages.Length,
                Layout = PipelineLayout,
                RenderPass = renderPass,
                PVertexInputState = &vertexInput,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PDepthStencilState = &depthStencil,
                PColorBlendState = &colorBlend,
                PDynamicState = &dynamicState,
                Subpass = 0,
            };

            fixed (PipelineShaderStageCreateInfo* stagePointer = stages)
            fixed (DynamicState* dynamicPointer = dynamicStates) {
                pipelineInfo.PStages = stagePointer;
                dynamicState.PDynamicStates = dynamicPointer;

                if (vk.CreateGraphicsPipelines(device, default, 1, in pipelineInfo, null, out var pipeline) != Result.Success)
                    throw new Exception($"Failed to create {shaderName} graphics pipeline.");

                Pipeline = pipeline;
            }
        }
        finally {
            if (vertexModule.Handle != 0) vk.DestroyShaderModule(device, vertexModule, null);
            if (fragmentModule.Handle != 0) vk.DestroyShaderModule(device, fragmentModule, null);

            Marshal.FreeHGlobal(vertexName);
            Marshal.FreeHGlobal(fragmentName);
        }
    }

    private ShaderModule CreateShaderModule(byte[] code)
    {
        fixed (byte* codePointer = code) {
            var createInfo = new ShaderModuleCreateInfo {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)code.Length,
                PCode = (uint*)codePointer
            };

            if (_context.Api.CreateShaderModule(_context.LogicalDevice, in createInfo, null, out var module) != Result.Success)
                throw new Exception("Failed to create fullscreen shader module.");

            return module;
        }
    }

    public void Draw(CommandBuffer commandBuffer, int descriptorSetIndex)
    {
        if ((uint)descriptorSetIndex >= _descriptorSets.Length)
            throw new ArgumentOutOfRangeException(nameof(descriptorSetIndex));

        var vk = _context.Api;
        vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, Pipeline);
        var descriptorSet = _descriptorSets[descriptorSetIndex];
        vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, PipelineLayout, 0, 1, in descriptorSet, 0, null);
        vk.CmdDraw(commandBuffer, 3, 1, 0, 0);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var vk = _context.Api;
        var device = _context.LogicalDevice;

        if (Pipeline.Handle != 0) vk.DestroyPipeline(device, Pipeline, null);
        if (PipelineLayout.Handle != 0) vk.DestroyPipelineLayout(device, PipelineLayout, null);
        if (_descriptorPool.Handle != 0) vk.DestroyDescriptorPool(device, _descriptorPool, null);
        if (_sampler.Handle != 0) vk.DestroySampler(device, _sampler, null);
        if (_descriptorSetLayout.Handle != 0) vk.DestroyDescriptorSetLayout(device, _descriptorSetLayout, null);

        Pipeline = default;
        PipelineLayout = default;
        _descriptorPool = default;
        _sampler = default;
        _descriptorSets = Array.Empty<DescriptorSet>();
    }
}
