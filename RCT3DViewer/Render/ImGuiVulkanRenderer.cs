using ImGuiNET;
using Silk.NET.Vulkan;
using System.Numerics;
using System.Runtime.InteropServices;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace RCT3DViewer.Render;

internal sealed unsafe class ImGuiVulkanRenderer : IDisposable
{
    private readonly Vk _vk;
    private readonly PhysicalDevice _physicalDevice;
    private readonly Device _device;
    private readonly Queue _queue;
    private readonly RenderPass _renderPass;
    private readonly CommandPool _commandPool;
    private readonly string _shaderDirectory;

    private DescriptorSetLayout _descriptorSetLayout;
    private DescriptorPool _descriptorPool;
    private DescriptorSet _descriptorSet;
    private PipelineLayout _pipelineLayout;
    private Pipeline _pipeline;
    private Image _fontImage;
    private DeviceMemory _fontMemory;
    private ImageView _fontView;
    private Sampler _fontSampler;
    private Buffer[] _vertexBuffers = Array.Empty<Buffer>();
    private DeviceMemory[] _vertexMemories = Array.Empty<DeviceMemory>();
    private ulong[] _vertexCapacities = Array.Empty<ulong>();
    private Buffer[] _indexBuffers = Array.Empty<Buffer>();
    private DeviceMemory[] _indexMemories = Array.Empty<DeviceMemory>();
    private ulong[] _indexCapacities = Array.Empty<ulong>();
    private bool _disposed;

    public bool WantCaptureMouse => ImGui.GetIO().WantCaptureMouse;
    public bool WantCaptureKeyboard => ImGui.GetIO().WantCaptureKeyboard;


    public ImGuiVulkanRenderer(Vk vk, PhysicalDevice physicalDevice, Device device, Queue queue, RenderPass renderPass, CommandPool commandPool, string shaderDirectory, int swapchainImageCount)
    {
        _vk = vk;
        _physicalDevice = physicalDevice;
        _device = device;
        _queue = queue;
        _renderPass = renderPass;
        _commandPool = commandPool;
        _shaderDirectory = shaderDirectory;

        ImGui.CreateContext();
        ImGui.StyleColorsDark();
        ImGui.GetIO().BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

        CreateDescriptorResources();
        CreatePipeline();
        CreateFontTexture();

        ResizeBuffers(swapchainImageCount);
    }

    public void ResizeBuffers(int imageCount)
    {
        DestroyBuffers();

        _vertexBuffers = new Buffer[imageCount];
        _vertexMemories = new DeviceMemory[imageCount];
        _vertexCapacities = new ulong[imageCount];
        _indexBuffers = new Buffer[imageCount];
        _indexMemories = new DeviceMemory[imageCount];
        _indexCapacities = new ulong[imageCount];
    }

    public void BeginFrame(int width, int height, float deltaTime)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(width, height);
        io.DisplayFramebufferScale = Vector2.One;
        io.DeltaTime = Math.Max(deltaTime, 1.0f / 1000.0f);
        ImGui.NewFrame();
    }

    public void Render(CommandBuffer commandBuffer, uint imageIndex)
    {
        ImGui.Render();
        var drawData = ImGui.GetDrawData();
        if (drawData.CmdListsCount == 0 || drawData.TotalVtxCount == 0 || drawData.TotalIdxCount == 0) return;

        EnsureBufferCapacity(imageIndex, (ulong)drawData.TotalVtxCount * 20, true);
        EnsureBufferCapacity(imageIndex, (ulong)drawData.TotalIdxCount * sizeof(ushort), false);
        UploadDrawData(imageIndex, drawData);

        var viewport = new Viewport(0, 0, drawData.DisplaySize.X, drawData.DisplaySize.Y, 0, 1);
        _vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);
        _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, _pipeline);
        _vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, _pipelineLayout, 0, 1, in _descriptorSet, 0, null);

        var constants = new ImGuiPushConstants {
            Scale = new Vector2(2.0f / drawData.DisplaySize.X, 2.0f / drawData.DisplaySize.Y),
            Translate = new Vector2(-1.0f - drawData.DisplayPos.X * (2.0f / drawData.DisplaySize.X), -1.0f - drawData.DisplayPos.Y * (2.0f / drawData.DisplaySize.Y))
        };

        _vk.CmdPushConstants(commandBuffer, _pipelineLayout, ShaderStageFlags.VertexBit, 0, (uint)sizeof(ImGuiPushConstants), &constants);

        ulong offset = 0;
        var vertexBuffer = _vertexBuffers[imageIndex];
        _vk.CmdBindVertexBuffers(commandBuffer, 0, 1, in vertexBuffer, in offset);
        _vk.CmdBindIndexBuffer(commandBuffer, _indexBuffers[imageIndex], 0, IndexType.Uint16);

        int globalVertexOffset = 0;
        uint globalIndexOffset = 0;
        for (var listIndex = 0; listIndex < drawData.CmdListsCount; listIndex++) {
            var commandList = drawData.CmdLists[listIndex];
            for (var commandIndex = 0; commandIndex < commandList.CmdBuffer.Size; commandIndex++) {
                var drawCommand = commandList.CmdBuffer[commandIndex];
                if (drawCommand.UserCallback != IntPtr.Zero) continue;

                var clip = drawCommand.ClipRect;
                var left = Math.Clamp((clip.X - drawData.DisplayPos.X) * drawData.FramebufferScale.X, 0, drawData.DisplaySize.X * drawData.FramebufferScale.X);
                var top = Math.Clamp((clip.Y - drawData.DisplayPos.Y) * drawData.FramebufferScale.Y, 0, drawData.DisplaySize.Y * drawData.FramebufferScale.Y);
                var right = Math.Clamp((clip.Z - drawData.DisplayPos.X) * drawData.FramebufferScale.X, 0, drawData.DisplaySize.X * drawData.FramebufferScale.X);
                var bottom = Math.Clamp((clip.W - drawData.DisplayPos.Y) * drawData.FramebufferScale.Y, 0, drawData.DisplaySize.Y * drawData.FramebufferScale.Y);
                if (right <= left || bottom <= top) continue;

                Rect2D scissor = new() {
                    Offset = new Offset2D((int)left, (int)top),
                    Extent = new Extent2D((uint)(right - left), (uint)(bottom - top))
                };

                _vk.CmdSetScissor(commandBuffer, 0, 1, &scissor);
                _vk.CmdDrawIndexed(commandBuffer, drawCommand.ElemCount, 1, globalIndexOffset + drawCommand.IdxOffset, checked((int)(globalVertexOffset + drawCommand.VtxOffset)), 0);
            }

            globalIndexOffset += (uint)commandList.IdxBuffer.Size;
            globalVertexOffset += commandList.VtxBuffer.Size;
        }
    }

    private void CreateDescriptorResources()
    {
        var bindings = new[] {
            new DescriptorSetLayoutBinding {
                Binding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.SampledImage,
                StageFlags = ShaderStageFlags.FragmentBit,
            },
            new DescriptorSetLayoutBinding {
                Binding = 1,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.Sampler,
                StageFlags = ShaderStageFlags.FragmentBit,
            },
        };

        fixed (DescriptorSetLayoutBinding* bindingsPointer = bindings) {
            var layoutInfo = new DescriptorSetLayoutCreateInfo {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)bindings.Length,
                PBindings = bindingsPointer,
            };

            Check(_vk.CreateDescriptorSetLayout(_device, in layoutInfo, null, out _descriptorSetLayout), "create ImGui descriptor layout");
        }

        var pushRange = new PushConstantRange {
            StageFlags = ShaderStageFlags.VertexBit,
            Size = (uint)sizeof(ImGuiPushConstants),
            Offset = 0,
        };

        var setLayout = _descriptorSetLayout;

        var layout = new PipelineLayoutCreateInfo {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = &setLayout,
            PushConstantRangeCount = 1,
            PPushConstantRanges = &pushRange,
        };

        Check(_vk.CreatePipelineLayout(_device, in layout, null, out _pipelineLayout), "create ImGui pipeline layout");

        var poolSizes = new[] {
            new DescriptorPoolSize {
                Type = DescriptorType.SampledImage,
                DescriptorCount = 1,
            },
            new DescriptorPoolSize {
                Type = DescriptorType.Sampler,
                DescriptorCount = 1,
            },
        };

        fixed (DescriptorPoolSize* poolPointer = poolSizes) {
            var poolInfo = new DescriptorPoolCreateInfo {
                SType = StructureType.DescriptorPoolCreateInfo,
                MaxSets = 1,
                PoolSizeCount = (uint)poolSizes.Length,
                PPoolSizes = poolPointer,
            };

            Check(_vk.CreateDescriptorPool(_device, in poolInfo, null, out _descriptorPool), "create ImGui descriptor pool");
        }

        var allocateInfo = new DescriptorSetAllocateInfo {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _descriptorPool,
            DescriptorSetCount = 1,
            PSetLayouts = &setLayout,
        };

        Check(_vk.AllocateDescriptorSets(_device, in allocateInfo, out _descriptorSet), "allocate ImGui descriptor set");
    }

    private void CreatePipeline()
    {
        var vertexBytes = File.ReadAllBytes(Path.Combine(_shaderDirectory, "imgui.vert.spv"));
        var fragmentBytes = File.ReadAllBytes(Path.Combine(_shaderDirectory, "imgui.frag.spv"));
        var vertexModule = CreateShaderModule(vertexBytes);
        var fragmentModule = CreateShaderModule(fragmentBytes);
        var vertexEntry = (byte*)Marshal.StringToHGlobalAnsi("main");
        var fragmentEntry = (byte*)Marshal.StringToHGlobalAnsi("main");

        try {
            var stages = new[] {
                new PipelineShaderStageCreateInfo {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.VertexBit,
                    Module = vertexModule,
                    PName = vertexEntry,
                },
                new PipelineShaderStageCreateInfo {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.FragmentBit,
                    Module = fragmentModule,
                    PName = fragmentEntry,
                },
            };

            var binding = new VertexInputBindingDescription {
                Binding = 0,
                Stride = 20,
                InputRate = VertexInputRate.Vertex,
            };

            var attributes = new[] {
                new VertexInputAttributeDescription {
                    Binding = 0,
                    Location = 0,
                    Format = Format.R32G32Sfloat,
                    Offset = 0,
                },
                new VertexInputAttributeDescription {
                    Binding = 0,
                    Location = 1,
                    Format = Format.R32G32Sfloat,
                    Offset = 8,
                },
                new VertexInputAttributeDescription {
                    Binding = 0,
                    Location = 2,
                    Format = Format.R8G8B8A8Unorm,
                    Offset = 16,
                },
            };

            var vertexInput = new PipelineVertexInputStateCreateInfo {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = &binding,
                VertexAttributeDescriptionCount = (uint)attributes.Length,
            };

            var assembly = new PipelineInputAssemblyStateCreateInfo {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = PrimitiveTopology.TriangleList,
            };
            
            var viewport = new PipelineViewportStateCreateInfo {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                ScissorCount = 1,
            };
            
            var raster = new PipelineRasterizationStateCreateInfo {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                PolygonMode = PolygonMode.Fill,
                CullMode = CullModeFlags.None,
                FrontFace = FrontFace.CounterClockwise,
                LineWidth = 1,
            };
            
            var multisample = new PipelineMultisampleStateCreateInfo {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                RasterizationSamples = SampleCountFlags.Count1Bit,
            };
            
            var depth = new PipelineDepthStencilStateCreateInfo {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = false,
                DepthWriteEnable = false,
                MinDepthBounds = 0,
                MaxDepthBounds = 1,
            };
            
            var blendAttachment = new PipelineColorBlendAttachmentState {
                BlendEnable = true,
                SrcColorBlendFactor = BlendFactor.SrcAlpha,
                DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                ColorBlendOp = BlendOp.Add,
                SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
                AlphaBlendOp = BlendOp.Add,
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
            };

            var blend = new PipelineColorBlendStateCreateInfo {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                AttachmentCount = 1,
                PAttachments = &blendAttachment,
            };
            
            var dynamicStates = new[] {
                DynamicState.Viewport,
                DynamicState.Scissor,
            };

            var dynamic = new PipelineDynamicStateCreateInfo {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = (uint)dynamicStates.Length,
            };
            
            var pipelineInfo = new GraphicsPipelineCreateInfo {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = (uint)stages.Length,
                Layout = _pipelineLayout,
                RenderPass = _renderPass,
                Subpass = 0,
                PVertexInputState = &vertexInput,
                PInputAssemblyState = &assembly,
                PViewportState = &viewport,
                PRasterizationState = &raster,
                PMultisampleState = &multisample,
                PDepthStencilState = &depth,
                PColorBlendState = &blend,
                PDynamicState = &dynamic
            };

            fixed (PipelineShaderStageCreateInfo* stagesPointer = stages)
            fixed (VertexInputAttributeDescription* attributesPointer = attributes)
            fixed (DynamicState* dynamicPointer = dynamicStates) {
                vertexInput.PVertexAttributeDescriptions = attributesPointer;
                dynamic.PDynamicStates = dynamicPointer;
                pipelineInfo.PStages = stagesPointer;
                Check(_vk.CreateGraphicsPipelines(_device, default, 1, in pipelineInfo, null, out _pipeline), "create ImGui graphics pipeline");
            }
        }
        finally {
            _vk.DestroyShaderModule(_device, vertexModule, null);
            _vk.DestroyShaderModule(_device, fragmentModule, null);
            Marshal.FreeHGlobal((IntPtr)vertexEntry);
            Marshal.FreeHGlobal((IntPtr)fragmentEntry);
        }
    }

    private ShaderModule CreateShaderModule(byte[] bytes)
    {
        fixed (byte* code = bytes) {
            var info = new ShaderModuleCreateInfo { SType = StructureType.ShaderModuleCreateInfo, CodeSize = (nuint)bytes.Length, PCode = (uint*)code };
            Check(_vk.CreateShaderModule(_device, in info, null, out var module), "create ImGui shader module");
            return module;
        }
    }

    private void CreateFontTexture()
    {
        var fonts = ImGui.GetIO().Fonts;
        fonts.GetTexDataAsRGBA32(out byte* pixels, out var width, out var height, out _);
        var byteCount = checked((ulong)width * (ulong)height * 4);
        CreateBuffer(byteCount, BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out var staging, out var stagingMemory);
        
        try {
            void* mapped;
            Check(_vk.MapMemory(_device, stagingMemory, 0, byteCount, 0, &mapped), "map ImGui font staging memory");
            System.Buffer.MemoryCopy(pixels, mapped, (long)byteCount, (long)byteCount);
            _vk.UnmapMemory(_device, stagingMemory);

            var imageInfo = new ImageCreateInfo {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.Type2D,
                Format = Format.R8G8B8A8Unorm,
                Extent = new Extent3D((uint)width, (uint)height, 1),
                MipLevels = 1,
                ArrayLayers = 1,
                Samples = SampleCountFlags.Count1Bit,
                Tiling = ImageTiling.Optimal,
                Usage = ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
                SharingMode = SharingMode.Exclusive,
                InitialLayout = ImageLayout.Undefined
            };

            Check(_vk.CreateImage(_device, in imageInfo, null, out _fontImage), "create ImGui font image");
            _vk.GetImageMemoryRequirements(_device, _fontImage, out var requirements);

            var allocation = new MemoryAllocateInfo {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = requirements.Size,
                MemoryTypeIndex = FindMemoryType(requirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit),
            };
            
            Check(_vk.AllocateMemory(_device, in allocation, null, out _fontMemory), "allocate ImGui font image memory");
            Check(_vk.BindImageMemory(_device, _fontImage, _fontMemory, 0), "bind ImGui font image memory");
            UploadFontImage(staging, (uint)width, (uint)height);
        }
        finally {
            _vk.DestroyBuffer(_device, staging, null);
            _vk.FreeMemory(_device, stagingMemory, null);
        }

        var viewInfo = new ImageViewCreateInfo {
            SType = StructureType.ImageViewCreateInfo,
            Image = _fontImage,
            ViewType = ImageViewType.Type2D,
            Format = Format.R8G8B8A8Unorm,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };

        Check(_vk.CreateImageView(_device, in viewInfo, null, out _fontView), "create ImGui font image view");

        var samplerInfo = new SamplerCreateInfo {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            MipmapMode = SamplerMipmapMode.Linear,
            AddressModeU = SamplerAddressMode.ClampToEdge,
            AddressModeV = SamplerAddressMode.ClampToEdge,
            AddressModeW = SamplerAddressMode.ClampToEdge,
            MaxAnisotropy = 1,
            MinLod = 0,
            MaxLod = 0,
            BorderColor = BorderColor.IntOpaqueBlack
        };

        Check(_vk.CreateSampler(_device, in samplerInfo, null, out _fontSampler), "create ImGui font sampler");

        var imageDescriptor = new DescriptorImageInfo {
            ImageView = _fontView,
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal
        };

        var samplerDescriptor = new DescriptorImageInfo {
            Sampler = _fontSampler,
        };
        
        var writes = new[] {
            new WriteDescriptorSet {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _descriptorSet,
                DstBinding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.SampledImage,
            },
            new WriteDescriptorSet {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _descriptorSet,
                DstBinding = 1,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.Sampler,
            },
        };

        fixed (WriteDescriptorSet* writesPointer = writes) {
            writes[0].PImageInfo = &imageDescriptor;
            writes[1].PImageInfo = &samplerDescriptor;
            _vk.UpdateDescriptorSets(_device, (uint)writes.Length, writesPointer, 0, null);
        }

        fonts.SetTexID((IntPtr)1);
        fonts.ClearTexData();
    }

    private void UploadFontImage(Buffer staging, uint width, uint height)
    {
        var allocate = new CommandBufferAllocateInfo {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1,
        };

        Check(_vk.AllocateCommandBuffers(_device, in allocate, out var commandBuffer), "allocate ImGui texture command buffer");
        
        try {
            var begin = new CommandBufferBeginInfo {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
            };

            Check(_vk.BeginCommandBuffer(commandBuffer, in begin), "begin ImGui texture upload");

            var toTransfer = new ImageMemoryBarrier {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.TransferDstOptimal,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = _fontImage,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
                DstAccessMask = AccessFlags.TransferWriteBit,
            };

            _vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit, 0, 0, null, 0, null, 1, &toTransfer);
            
            var region = new BufferImageCopy {
                ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
                ImageExtent = new Extent3D(width, height, 1),
            };
            
            _vk.CmdCopyBufferToImage(commandBuffer, staging, _fontImage, ImageLayout.TransferDstOptimal, 1, &region);

            var toShader = new ImageMemoryBarrier {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.TransferDstOptimal,
                NewLayout = ImageLayout.ShaderReadOnlyOptimal,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = _fontImage,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
                SrcAccessMask = AccessFlags.TransferWriteBit,
                DstAccessMask = AccessFlags.ShaderReadBit
            };

            _vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, 0, 0, null, 0, null, 1, &toShader);

            Check(_vk.EndCommandBuffer(commandBuffer), "end ImGui texture upload");

            var submit = new SubmitInfo {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &commandBuffer,
            };

            Check(_vk.QueueSubmit(_queue, 1, in submit, default), "submit ImGui font upload");
            Check(_vk.QueueWaitIdle(_queue), "wait for ImGui font upload");
        }
        finally {
            _vk.FreeCommandBuffers(_device, _commandPool, 1, in commandBuffer);
        }
    }

    private void UploadDrawData(uint imageIndex, ImDrawDataPtr drawData)
    {
        var vertexSize = (ulong)drawData.TotalVtxCount * 20;
        var indexSize = (ulong)drawData.TotalIdxCount * sizeof(ushort);

        void* vertexData;
        Check(_vk.MapMemory(_device, _vertexMemories[imageIndex], 0, vertexSize, 0, &vertexData), "map ImGui vertex buffer");

        void* indexData;
        Check(_vk.MapMemory(_device, _indexMemories[imageIndex], 0, indexSize, 0, &indexData), "map ImGui index buffer");

        try {
            byte* vertexDestination = (byte*)vertexData;
            byte* indexDestination = (byte*)indexData;

            for (var i = 0; i < drawData.CmdListsCount; i++) {
                var commandList = drawData.CmdLists[i];
                var listVertexBytes = (long)commandList.VtxBuffer.Size * 20;
                var listIndexBytes = (long)commandList.IdxBuffer.Size * sizeof(ushort);
                System.Buffer.MemoryCopy((void*)commandList.VtxBuffer.Data, vertexDestination, listVertexBytes, listVertexBytes);
                System.Buffer.MemoryCopy((void*)commandList.IdxBuffer.Data, indexDestination, listIndexBytes, listIndexBytes);
                vertexDestination += listVertexBytes;
                indexDestination += listIndexBytes;
            }
        }
        finally {
            _vk.UnmapMemory(_device, _vertexMemories[imageIndex]);
            _vk.UnmapMemory(_device, _indexMemories[imageIndex]);
        }
    }

    private void EnsureBufferCapacity(uint imageIndex, ulong required, bool vertex)
    {
        var capacities = vertex ? _vertexCapacities : _indexCapacities;
        if (capacities[imageIndex] >= required) return;

        DestroyBuffer(vertex ? _vertexBuffers[imageIndex] : _indexBuffers[imageIndex], vertex ? _vertexMemories[imageIndex] : _indexMemories[imageIndex]);
        var capacity = Math.Max(required, Math.Max(4096UL, capacities[imageIndex] * 2));
        CreateBuffer(capacity, vertex ? BufferUsageFlags.VertexBufferBit : BufferUsageFlags.IndexBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out var buffer, out var memory);
        
        if (vertex) {
            _vertexBuffers[imageIndex] = buffer;
            _vertexMemories[imageIndex] = memory;
            _vertexCapacities[imageIndex] = capacity;
        }
        else {
            _indexBuffers[imageIndex] = buffer;
            _indexMemories[imageIndex] = memory;
            _indexCapacities[imageIndex] = capacity;
        }
    }

    private void CreateBuffer(ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties, out Buffer buffer, out DeviceMemory memory)
    {
        var bufferInfo = new BufferCreateInfo {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
        };

        Check(_vk.CreateBuffer(_device, in bufferInfo, null, out buffer), "create ImGui buffer");

        _vk.GetBufferMemoryRequirements(_device, buffer, out var requirements);

        var allocation = new MemoryAllocateInfo {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = FindMemoryType(requirements.MemoryTypeBits, properties),
        };
        
        try {
            Check(_vk.AllocateMemory(_device, in allocation, null, out memory), "allocate ImGui buffer memory");
            Check(_vk.BindBufferMemory(_device, buffer, memory, 0), "bind ImGui buffer memory");
        }
        catch {
            _vk.DestroyBuffer(_device, buffer, null);
            buffer = default;
            throw;
        }
    }

    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memoryProperties);

        for (uint i = 0; i < memoryProperties.MemoryTypeCount; i++) {
            if ((typeFilter & (1 << (int)i)) != 0 && (memoryProperties.MemoryTypes[(int)i].PropertyFlags & properties) == properties)
                return i;
        }

        throw new InvalidOperationException($"No Vulkan memory type supports {properties}.");
    }

    private void DestroyBuffers()
    {
        for (var i = 0; i < _vertexBuffers.Length; i++)
            DestroyBuffer(_vertexBuffers[i], _vertexMemories[i]);

        for (var i = 0; i < _indexBuffers.Length; i++)
            DestroyBuffer(_indexBuffers[i], _indexMemories[i]);

        _vertexBuffers = Array.Empty<Buffer>();
        _vertexMemories = Array.Empty<DeviceMemory>();
        _vertexCapacities = Array.Empty<ulong>();
        _indexBuffers = Array.Empty<Buffer>();
        _indexMemories = Array.Empty<DeviceMemory>();
        _indexCapacities = Array.Empty<ulong>();
    }

    private void DestroyBuffer(Buffer buffer, DeviceMemory memory)
    {
        if (buffer.Handle != 0)
            _vk.DestroyBuffer(_device, buffer, null);

        if (memory.Handle != 0)
            _vk.FreeMemory(_device, memory, null);
    }

    private static void Check(Result result, string action)
    {
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to {action}: {result}.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        DestroyBuffers();

        if (_pipeline.Handle != 0) _vk.DestroyPipeline(_device, _pipeline, null);
        if (_pipelineLayout.Handle != 0) _vk.DestroyPipelineLayout(_device, _pipelineLayout, null);
        if (_descriptorPool.Handle != 0) _vk.DestroyDescriptorPool(_device, _descriptorPool, null);
        if (_fontSampler.Handle != 0) _vk.DestroySampler(_device, _fontSampler, null);
        if (_fontView.Handle != 0) _vk.DestroyImageView(_device, _fontView, null);
        if (_fontImage.Handle != 0) _vk.DestroyImage(_device, _fontImage, null);
        if (_fontMemory.Handle != 0) _vk.FreeMemory(_device, _fontMemory, null);
        if (_descriptorSetLayout.Handle != 0) _vk.DestroyDescriptorSetLayout(_device, _descriptorSetLayout, null);

        ImGui.DestroyContext();
    }

    private struct ImGuiPushConstants
    {
        public Vector2 Scale;
        public Vector2 Translate;
    }
}
