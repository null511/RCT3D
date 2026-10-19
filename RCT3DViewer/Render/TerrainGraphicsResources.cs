using RCT3DViewer.Render.Paths;
using Silk.NET.Vulkan;
using System.Numerics;
using System.Runtime.InteropServices;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace RCT3DViewer.Render;

internal sealed unsafe class TerrainGraphicsResources(VulkanDeviceContext context, RenderPass renderPass, TerrainMaterialLibrary materials, bool useInstances = false) : IDisposable
{
    private CommandPool _commandPool;
    private DescriptorSetLayout _descriptorSetLayout;
    private DescriptorPool _descriptorPool;
    private Image _textureImage;
    private DeviceMemory _textureMemory;
    private ImageView _textureView;
    private Sampler _textureSampler;
    private Buffer _vertexBuffer;
    private DeviceMemory _vertexBufferMemory;
    private Buffer _indexBuffer;
    private DeviceMemory _indexBufferMemory;
    private Buffer _instanceBuffer;
    private DeviceMemory _instanceBufferMemory;

    public PipelineLayout PipelineLayout { get; private set; }
    public Pipeline Pipeline { get; private set; }
    public DescriptorSet DescriptorSet { get; private set; }
    public Buffer VertexBuffer => _vertexBuffer;
    public Buffer IndexBuffer => _indexBuffer;
    public Buffer InstanceBuffer => _instanceBuffer;
    public uint IndexCount { get; private set; }


    public void CreatePipeline()
    {
        var vk = context.Api;
        var device = context.LogicalDevice;
        var shaderName = useInstances ? "model" : "terrain";
        var vertPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Shaders", $"{shaderName}.vert.spv");
        var fragPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Shaders", $"{shaderName}.frag.spv");

        byte[] vertShaderCode;
        byte[] fragShaderCode;

        if (File.Exists(vertPath) && File.Exists(fragPath)) {
            Console.WriteLine($"Successfully loaded native SPIR-V shaders from output: {vertPath}");

            vertShaderCode = File.ReadAllBytes(vertPath);
            fragShaderCode = File.ReadAllBytes(fragPath);
        }
        else {
            throw new FileNotFoundException($"CRITICAL: Shader binaries missing even after compilation pass. Check: {vertPath}");
        }

        ShaderModule vertModule;
        ShaderModule fragModule;
        fixed (byte* codePtr = vertShaderCode) {
            ShaderModuleCreateInfo info = new() { SType = StructureType.ShaderModuleCreateInfo, CodeSize = (nuint)vertShaderCode.Length, PCode = (uint*)codePtr };
            vk.CreateShaderModule(device, in info, null, out vertModule);
        }

        fixed (byte* codePtr = fragShaderCode) {
            ShaderModuleCreateInfo info = new() { SType = StructureType.ShaderModuleCreateInfo, CodeSize = (nuint)fragShaderCode.Length, PCode = (uint*)codePtr };
            vk.CreateShaderModule(device, in info, null, out fragModule);
        }

        var vertStageInfo = new PipelineShaderStageCreateInfo {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = vertModule,
            PName = (byte*)Marshal.StringToHGlobalAnsi("main")
        };

        var fragStageInfo = new PipelineShaderStageCreateInfo {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = fragModule,
            PName = (byte*)Marshal.StringToHGlobalAnsi("main")
        };

        var shaderStages = new[] { vertStageInfo, fragStageInfo };

        var bindingDescriptions = new List<VertexInputBindingDescription> {
            new() {
            Binding = 0,
            Stride = (uint)Marshal.SizeOf<TerrainVertex>(),
            InputRate = VertexInputRate.Vertex
            }
        };

        var attributeDescriptions = new List<VertexInputAttributeDescription> {
            new() { Binding = 0, Location = 0, Format = Format.R32G32B32Sfloat, Offset = (uint)Marshal.OffsetOf<TerrainVertex>(nameof(TerrainVertex.Position)) },
            new() { Binding = 0, Location = 1, Format = Format.R32G32B32Sfloat, Offset = (uint)Marshal.OffsetOf<TerrainVertex>(nameof(TerrainVertex.Normal)) },
            new() { Binding = 0, Location = 2, Format = Format.R32G32Sfloat, Offset = (uint)Marshal.OffsetOf<TerrainVertex>(nameof(TerrainVertex.TextureCoordinate)) },
            new() { Binding = 0, Location = 3, Format = Format.R32Sfloat, Offset = (uint)Marshal.OffsetOf<TerrainVertex>(nameof(TerrainVertex.MaterialId)) }
        };

        if (useInstances) {
            var instanceStride = (uint)Marshal.SizeOf<ModelInstance>();
            var matrixOffset = checked((uint)Marshal.OffsetOf<ModelInstance>(nameof(ModelInstance.Transform)).ToInt64());
            bindingDescriptions.Add(new VertexInputBindingDescription {
                Binding = 1,
                Stride = instanceStride,
                InputRate = VertexInputRate.Instance
            });

            for (uint row = 0; row < 4; row++) {
                attributeDescriptions.Add(new VertexInputAttributeDescription {
                    Binding = 1,
                    Location = 4 + row,
                    Format = Format.R32G32B32A32Sfloat,
                    Offset = matrixOffset + row * (uint)Marshal.SizeOf<Vector4>()
                });
            }
        }

        var vertexInputInfo = new PipelineVertexInputStateCreateInfo {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            VertexBindingDescriptionCount = (uint)bindingDescriptions.Count,
            VertexAttributeDescriptionCount = (uint)attributeDescriptions.Count,
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
        
        var dynamicStates = new[] {
            DynamicState.Viewport,
            DynamicState.Scissor,
        };

        var dynamicState = new PipelineDynamicStateCreateInfo {
            SType = StructureType.PipelineDynamicStateCreateInfo,
            DynamicStateCount = (uint)dynamicStates.Length
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
            DepthTestEnable = true,
            DepthWriteEnable = true,
            DepthCompareOp = CompareOp.Less,
            MinDepthBounds = 0.0f,
            MaxDepthBounds = 1.0f
        };

        var colorBlendAttachment = new PipelineColorBlendAttachmentState {
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
            BlendEnable = false,
        };
        
        var colorBlending = new PipelineColorBlendStateCreateInfo {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            AttachmentCount = 1,
            PAttachments = &colorBlendAttachment,
        };

        var descriptorBindings = new DescriptorSetLayoutBinding[] {
            new() { Binding = 0, DescriptorType = DescriptorType.SampledImage, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit },
            new() { Binding = 1, DescriptorType = DescriptorType.Sampler, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit }
        };

        fixed (DescriptorSetLayoutBinding* bindingsPointer = descriptorBindings) {
            var descriptorLayoutInfo = new DescriptorSetLayoutCreateInfo {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)descriptorBindings.Length,
                PBindings = bindingsPointer
            };

            if (vk.CreateDescriptorSetLayout(device, in descriptorLayoutInfo, null, out _descriptorSetLayout) != Result.Success)
                throw new Exception("Failed to create the terrain texture descriptor-set layout.");
        }

        var descriptorLayout = _descriptorSetLayout;
        var pushConstantRange = new PushConstantRange {
            StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
            Size = (uint)Marshal.SizeOf<UniformBufferObject>(),
            Offset = 0,
        };

        var pipelineLayoutInfo = new PipelineLayoutCreateInfo {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = &descriptorLayout,
            PushConstantRangeCount = 1,
            PPushConstantRanges = &pushConstantRange
        };

        vk.CreatePipelineLayout(device, in pipelineLayoutInfo, null, out var pipelineLayout);
        PipelineLayout = pipelineLayout;

        var pipelineInfo = new GraphicsPipelineCreateInfo {
            SType = StructureType.GraphicsPipelineCreateInfo,
            StageCount = 2,
            Layout = PipelineLayout,
            RenderPass = renderPass,
            Subpass = 0,
            PVertexInputState = &vertexInputInfo,
            PInputAssemblyState = &inputAssembly,
            PViewportState = &viewportState,
            PRasterizationState = &rasterizer,
            PMultisampleState = &multisampling,
            PDepthStencilState = &depthStencil,
            PColorBlendState = &colorBlending,
            PDynamicState = &dynamicState
        };

        var bindingDescriptionArray = bindingDescriptions.ToArray();
        var attributeDescriptionArray = attributeDescriptions.ToArray();
        fixed (PipelineShaderStageCreateInfo* stagePointer = shaderStages)
        fixed (VertexInputBindingDescription* bindingPointer = bindingDescriptionArray)
        fixed (VertexInputAttributeDescription* attributePointer = attributeDescriptionArray)
        fixed (DynamicState* dynamicStatesPointer = dynamicStates) {
            vertexInputInfo.PVertexBindingDescriptions = bindingPointer;
            vertexInputInfo.PVertexAttributeDescriptions = attributePointer;
            dynamicState.PDynamicStates = dynamicStatesPointer;
            pipelineInfo.PStages = stagePointer;

            var result = vk.CreateGraphicsPipelines(device, default, 1, in pipelineInfo, null, out var pipeline);

            if (result != Result.Success || pipeline.Handle == 0UL)
                throw new Exception($"Failed to create graphics pipeline. VkResult={result}, pipelineHandle={pipeline.Handle}");

            Pipeline = pipeline;
        }

        vk.DestroyShaderModule(device, vertModule, null);
        vk.DestroyShaderModule(device, fragModule, null);

        Marshal.FreeHGlobal((IntPtr)vertStageInfo.PName);
        Marshal.FreeHGlobal((IntPtr)fragStageInfo.PName);
    }

    public void CreateTextureResources(CommandPool commandPool)
    {
        _commandPool = commandPool;

        var vk = context.Api;
        var device = context.LogicalDevice;
        var layerBytes = checked((ulong)materials.Width * (ulong)materials.Height * 4);
        var textureBytes = checked(layerBytes * (ulong)materials.LayerCount);

        AllocateBuffer(textureBytes, BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out var stagingBuffer, out var stagingMemory);

        try {
            void* mapped;
            if (vk.MapMemory(device, stagingMemory, 0, textureBytes, 0, &mapped) != Result.Success)
                throw new Exception("Failed to map terrain texture staging memory.");

            Marshal.Copy(materials.Pixels, 0, (IntPtr)mapped, materials.Pixels.Length);
            vk.UnmapMemory(device, stagingMemory);

            ImageCreateInfo imageInfo = new() {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.Type2D,
                Format = Format.R8G8B8A8Srgb,
                Extent = new Extent3D((uint)materials.Width, (uint)materials.Height, 1),
                MipLevels = 1,
                ArrayLayers = (uint)materials.LayerCount,
                Samples = SampleCountFlags.Count1Bit,
                Tiling = ImageTiling.Optimal,
                Usage = ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
                SharingMode = SharingMode.Exclusive,
                InitialLayout = ImageLayout.Undefined
            };

            if (vk.CreateImage(device, ref imageInfo, null, out _textureImage) != Result.Success)
                throw new Exception("Failed to create terrain texture array image.");

            vk.GetImageMemoryRequirements(device, _textureImage, out var requirements);
            MemoryAllocateInfo allocationInfo = new() {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = requirements.Size,
                MemoryTypeIndex = context.FindMemoryType(requirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
            };

            if (vk.AllocateMemory(device, ref allocationInfo, null, out _textureMemory) != Result.Success)
                throw new Exception("Failed to allocate terrain texture array memory.");

            if (vk.BindImageMemory(device, _textureImage, _textureMemory, 0) != Result.Success)
                throw new Exception("Failed to bind terrain texture array memory.");

            var regions = new BufferImageCopy[materials.LayerCount];
            for (uint layer = 0; layer < regions.Length; layer++) {
                regions[layer] = new BufferImageCopy {
                    BufferOffset = layerBytes * layer,
                    ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, layer, 1),
                    ImageExtent = new Extent3D((uint)materials.Width, (uint)materials.Height, 1)
                };
            }

            UploadTerrainTexture(stagingBuffer, regions);
        }
        finally {
            vk.DestroyBuffer(device, stagingBuffer, null);
            vk.FreeMemory(device, stagingMemory, null);
        }

        var viewInfo = new ImageViewCreateInfo {
            SType = StructureType.ImageViewCreateInfo,
            Image = _textureImage,
            ViewType = ImageViewType.Type2DArray,
            Format = Format.R8G8B8A8Srgb,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, (uint)materials.LayerCount)
        };

        if (vk.CreateImageView(device, in viewInfo, null, out _textureView) != Result.Success)
            throw new Exception("Failed to create terrain texture array view.");

        var samplerInfo = new SamplerCreateInfo {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            MipmapMode = SamplerMipmapMode.Linear,
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            MaxAnisotropy = 1,
            MinLod = 0,
            MaxLod = 0,
            BorderColor = BorderColor.IntOpaqueBlack
        };

        if (vk.CreateSampler(device, in samplerInfo, null, out _textureSampler) != Result.Success)
            throw new Exception("Failed to create terrain texture sampler.");

        var poolSizes = new DescriptorPoolSize[] {
            new() { Type = DescriptorType.SampledImage, DescriptorCount = 1 },
            new() { Type = DescriptorType.Sampler, DescriptorCount = 1 }
        };

        fixed (DescriptorPoolSize* poolSizesPointer = poolSizes) {
            var poolInfo = new DescriptorPoolCreateInfo {
                SType = StructureType.DescriptorPoolCreateInfo,
                MaxSets = 1,
                PoolSizeCount = (uint)poolSizes.Length,
                PPoolSizes = poolSizesPointer
            };

            if (vk.CreateDescriptorPool(device, in poolInfo, null, out _descriptorPool) != Result.Success)
                throw new Exception("Failed to create terrain texture descriptor pool.");
        }

        var setLayout = _descriptorSetLayout;
        var setInfo = new DescriptorSetAllocateInfo {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _descriptorPool,
            DescriptorSetCount = 1,
            PSetLayouts = &setLayout
        };

        if (vk.AllocateDescriptorSets(device, in setInfo, out var descriptorSet) != Result.Success)
            throw new Exception("Failed to allocate terrain texture descriptor set.");

        DescriptorSet = descriptorSet;

        var imageDescriptor = new DescriptorImageInfo {
            ImageView = _textureView,
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
        };

        var samplerDescriptor = new DescriptorImageInfo {
            Sampler = _textureSampler,
        };

        var writes = new WriteDescriptorSet[] {
            new() { SType = StructureType.WriteDescriptorSet, DstSet = DescriptorSet, DstBinding = 0, DescriptorCount = 1, DescriptorType = DescriptorType.SampledImage },
            new() { SType = StructureType.WriteDescriptorSet, DstSet = DescriptorSet, DstBinding = 1, DescriptorCount = 1, DescriptorType = DescriptorType.Sampler }
        };

        fixed (WriteDescriptorSet* writesPointer = writes) {
            writes[0].PImageInfo = &imageDescriptor;
            writes[1].PImageInfo = &samplerDescriptor;
            vk.UpdateDescriptorSets(device, (uint)writes.Length, writesPointer, 0, null);
        }
    }

    public void UploadMesh(TerrainMeshData mesh)
    {
        UploadMesh(mesh.Vertices, mesh.Indices);

        if (mesh.Vertices.Length > 0) {
            var vertex = mesh.Vertices[0];
            Console.WriteLine($"[Mesh] Uploaded vertices={mesh.Vertices.Length}, indices={mesh.Indices.Length}, firstPos=({vertex.Position.X:F2},{vertex.Position.Y:F2},{vertex.Position.Z:F2})");
        }

        Console.WriteLine($"[Mesh] Terrain skirt sections={mesh.SkirtSections}, triangles={mesh.SkirtTriangles}, flat edges skipped={mesh.FlatEdgesSkipped}");
    }

    public void UploadMesh(PathMeshData mesh)
    {
        UploadMesh(mesh.Vertices, mesh.Indices);
    }

    public void UploadInstances(ModelInstance[] instances)
    {
        if (!useInstances)
            throw new InvalidOperationException("Instance data can only be uploaded to an instanced model pipeline.");

        ClearInstanceBuffer();
        if (instances.Length == 0)
            return;

        var device = context.LogicalDevice;
        var size = checked((ulong)Marshal.SizeOf<ModelInstance>() * (ulong)instances.Length);
        AllocateBuffer(size, BufferUsageFlags.VertexBufferBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out _instanceBuffer, out _instanceBufferMemory);

        try {
            void* instanceData;
            if (context.Api.MapMemory(device, _instanceBufferMemory, 0, size, 0, &instanceData) != Result.Success)
                throw new InvalidOperationException("Failed to map model instance buffer memory.");

            instances.AsSpan().CopyTo(new Span<ModelInstance>(instanceData, instances.Length));
            context.Api.UnmapMemory(device, _instanceBufferMemory);
        }
        catch {
            ClearInstanceBuffer();
            throw;
        }
    }

    private void UploadMesh(TerrainVertex[] vertices, uint[] indices)
    {
        if (vertices.Length == 0 || indices.Length == 0) {
            ClearMesh();
            return;
        }

        var vk = context.Api;
        var device = context.LogicalDevice;
        var vertexBufferSize = (ulong)(Marshal.SizeOf<TerrainVertex>() * vertices.Length);
        AllocateBuffer(vertexBufferSize, BufferUsageFlags.VertexBufferBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out _vertexBuffer, out _vertexBufferMemory);

        void* vertexData;
        vk.MapMemory(device, _vertexBufferMemory, 0, vertexBufferSize, 0, &vertexData);
        var vertexSpan = new Span<TerrainVertex>(vertexData, vertices.Length);
        vertices.AsSpan().CopyTo(vertexSpan);
        vk.UnmapMemory(device, _vertexBufferMemory);

        var indexBufferSize = (ulong)(sizeof(uint) * indices.Length);
        AllocateBuffer(indexBufferSize, BufferUsageFlags.IndexBufferBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out _indexBuffer, out _indexBufferMemory);

        void* indexData;
        vk.MapMemory(device, _indexBufferMemory, 0, indexBufferSize, 0, &indexData);
        var indexSpan = new Span<uint>(indexData, indices.Length);
        indices.AsSpan().CopyTo(indexSpan);
        vk.UnmapMemory(device, _indexBufferMemory);
        IndexCount = (uint)indices.Length;
    }

    public void ClearMesh()
    {
        var vk = context.Api;
        var device = context.LogicalDevice;

        if (_vertexBuffer.Handle != 0)
            vk.DestroyBuffer(device, _vertexBuffer, null);

        if (_vertexBufferMemory.Handle != 0)
            vk.FreeMemory(device, _vertexBufferMemory, null);

        if (_indexBuffer.Handle != 0)
            vk.DestroyBuffer(device, _indexBuffer, null);

        if (_indexBufferMemory.Handle != 0)
            vk.FreeMemory(device, _indexBufferMemory, null);

        _vertexBuffer = default;
        _vertexBufferMemory = default;
        _indexBuffer = default;
        _indexBufferMemory = default;
        IndexCount = 0;
        ClearInstanceBuffer();
    }

    private void ClearInstanceBuffer()
    {
        var vk = context.Api;
        var device = context.LogicalDevice;

        if (_instanceBuffer.Handle != 0)
            vk.DestroyBuffer(device, _instanceBuffer, null);

        if (_instanceBufferMemory.Handle != 0)
            vk.FreeMemory(device, _instanceBufferMemory, null);

        _instanceBuffer = default;
        _instanceBufferMemory = default;
    }

    private void UploadTerrainTexture(Buffer stagingBuffer, BufferImageCopy[] regions)
    {
        var vk = context.Api;
        var device = context.LogicalDevice;
        var allocationInfo = new CommandBufferAllocateInfo {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1,
        };

        if (vk.AllocateCommandBuffers(device, in allocationInfo, out var commandBuffer) != Result.Success)
            throw new Exception("Failed to allocate a one-time command buffer.");

        try {
            var beginInfo = new CommandBufferBeginInfo {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
            };

            vk.BeginCommandBuffer(commandBuffer, in beginInfo);

            var toTransfer = new ImageMemoryBarrier {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.TransferDstOptimal,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = _textureImage,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, (uint)materials.LayerCount),
                SrcAccessMask = AccessFlags.None,
                DstAccessMask = AccessFlags.TransferWriteBit,
            };

            vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit, 0, 0, null, 0, null, 1, &toTransfer);

            fixed (BufferImageCopy* regionsPointer = regions)
                vk.CmdCopyBufferToImage(commandBuffer, stagingBuffer, _textureImage,
                    ImageLayout.TransferDstOptimal, (uint)regions.Length, regionsPointer);

            var toShader = new ImageMemoryBarrier {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.TransferDstOptimal,
                NewLayout = ImageLayout.ShaderReadOnlyOptimal,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = _textureImage,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, (uint)materials.LayerCount),
                SrcAccessMask = AccessFlags.TransferWriteBit,
                DstAccessMask = AccessFlags.ShaderReadBit,
            };

            vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, 0, 0, null, 0, null, 1, &toShader);

            vk.EndCommandBuffer(commandBuffer);
            var submitInfo = new SubmitInfo {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &commandBuffer,
            };

            if (vk.QueueSubmit(context.GraphicsQueue, 1, in submitInfo, default) != Result.Success || vk.QueueWaitIdle(context.GraphicsQueue) != Result.Success)
                throw new Exception("Failed to submit terrain texture upload commands.");
        }
        finally {
            vk.FreeCommandBuffers(device, _commandPool, 1, in commandBuffer);
        }
    }

    private void AllocateBuffer(ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties, out Buffer buffer, out DeviceMemory bufferMemory)
    {
        var vk = context.Api;
        var device = context.LogicalDevice;
        BufferCreateInfo bufferInfo = new() { SType = StructureType.BufferCreateInfo, Size = size, Usage = usage, SharingMode = SharingMode.Exclusive };

        if (vk.CreateBuffer(device, ref bufferInfo, null, out buffer) != Result.Success)
            throw new Exception("Failed to generate structural buffer layout.");

        vk.GetBufferMemoryRequirements(device, buffer, out MemoryRequirements requirements);
        context.TryFindMemoryType(requirements.MemoryTypeBits, properties, out var memoryTypeIndex);

        MemoryAllocateInfo allocationInfo = new() { SType = StructureType.MemoryAllocateInfo, AllocationSize = requirements.Size, MemoryTypeIndex = memoryTypeIndex };
        vk.AllocateMemory(device, ref allocationInfo, null, out bufferMemory);
        vk.BindBufferMemory(device, buffer, bufferMemory, 0);
    }

    private void DestroyTextureResources()
    {
        if (context.LogicalDevice.Handle == 0) return;

        var vk = context.Api;
        var device = context.LogicalDevice;

        if (_descriptorPool.Handle != 0)
            vk.DestroyDescriptorPool(device, _descriptorPool, null);

        if (_textureSampler.Handle != 0)
            vk.DestroySampler(device, _textureSampler, null);

        if (_textureView.Handle != 0)
            vk.DestroyImageView(device, _textureView, null);

        if (_textureImage.Handle != 0)
            vk.DestroyImage(device, _textureImage, null);

        if (_textureMemory.Handle != 0)
            vk.FreeMemory(device, _textureMemory, null);

        _descriptorPool = default;
        DescriptorSet = default;
        _textureSampler = default;
        _textureView = default;
        _textureImage = default;
        _textureMemory = default;
    }

    public void Dispose()
    {
        if (context.LogicalDevice.Handle == 0) return;

        var vk = context.Api;
        var device = context.LogicalDevice;

        ClearMesh();

        if (Pipeline.Handle != 0)
            vk.DestroyPipeline(device, Pipeline, null);

        if (PipelineLayout.Handle != 0)
            vk.DestroyPipelineLayout(device, PipelineLayout, null);

        DestroyTextureResources();

        if (_descriptorSetLayout.Handle != 0)
            vk.DestroyDescriptorSetLayout(device, _descriptorSetLayout, null);

        Pipeline = default;
        PipelineLayout = default;
        _descriptorSetLayout = default;
    }
}
