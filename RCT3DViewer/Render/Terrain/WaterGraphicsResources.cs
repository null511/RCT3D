using Silk.NET.Vulkan;
using System.Numerics;
using System.Runtime.InteropServices;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace RCT3DViewer.Render.Terrain;

internal sealed unsafe class WaterGraphicsResources(VulkanDeviceContext context, RenderPass renderPass, DescriptorSetLayout uniformDescriptorSetLayout) : IDisposable
{
    private Buffer _vertexBuffer;
    private DeviceMemory _vertexBufferMemory;
    private Buffer _indexBuffer;
    private DeviceMemory _indexBufferMemory;

    public PipelineLayout PipelineLayout { get; private set; }
    public Pipeline Pipeline { get; private set; }
    public Buffer VertexBuffer => _vertexBuffer;
    public Buffer IndexBuffer => _indexBuffer;
    public uint IndexCount { get; private set; }


    public void CreatePipeline()
    {
        var vk = context.Api;
        var device = context.LogicalDevice;
        var shaderDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Shaders");
        var vertPath = Path.Combine(shaderDirectory, "water.vert.spv");
        var fragPath = Path.Combine(shaderDirectory, "water.frag.spv");
        if (!File.Exists(vertPath) || !File.Exists(fragPath))
            throw new FileNotFoundException($"Water shader binaries are missing: {vertPath}");

        var vertCode = File.ReadAllBytes(vertPath);
        var fragCode = File.ReadAllBytes(fragPath);
        ShaderModule vertModule = default;
        ShaderModule fragModule = default;
        var entryPoint = IntPtr.Zero;

        try {
            vertModule = CreateShaderModule(vertCode);
            fragModule = CreateShaderModule(fragCode);
            entryPoint = Marshal.StringToHGlobalAnsi("main");

            var shaderStages = new[] {
                new PipelineShaderStageCreateInfo {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.VertexBit,
                    Module = vertModule,
                    PName = (byte*)entryPoint,
                },
                new PipelineShaderStageCreateInfo {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.FragmentBit,
                    Module = fragModule,
                    PName = (byte*)entryPoint,
                },
            };

            VertexInputBindingDescription binding = new() {
                Binding = 0,
                Stride = (uint)Marshal.SizeOf<Vector3>(),
                InputRate = VertexInputRate.Vertex
            };

            VertexInputAttributeDescription attribute = new() {
                Binding = 0,
                Location = 0,
                Format = Format.R32G32B32Sfloat,
                Offset = 0
            };

            PipelineVertexInputStateCreateInfo vertexInput = new() {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = &binding,
                VertexAttributeDescriptionCount = 1,
                PVertexAttributeDescriptions = &attribute
            };

            PipelineInputAssemblyStateCreateInfo inputAssembly = new() {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = PrimitiveTopology.TriangleList
            };

            PipelineViewportStateCreateInfo viewport = new() {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                ScissorCount = 1
            };

            var dynamicStates = new[] {
                DynamicState.Viewport,
                DynamicState.Scissor,
            };

            PipelineDynamicStateCreateInfo dynamicState = new() {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = (uint)dynamicStates.Length
            };

            PipelineRasterizationStateCreateInfo rasterizer = new() {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                PolygonMode = PolygonMode.Fill,
                CullMode = CullModeFlags.None,
                FrontFace = FrontFace.CounterClockwise,
                LineWidth = 1.0f
            };

            PipelineMultisampleStateCreateInfo multisampling = new() {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                RasterizationSamples = SampleCountFlags.Count1Bit
            };

            PipelineDepthStencilStateCreateInfo depthStencil = new() {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = true,
                DepthWriteEnable = false,
                DepthCompareOp = CompareOp.Less,
                MinDepthBounds = 0.0f,
                MaxDepthBounds = 1.0f
            };

            PipelineColorBlendAttachmentState colorBlendAttachment = new() {
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
                BlendEnable = true,
                SrcColorBlendFactor = BlendFactor.SrcAlpha,
                DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                ColorBlendOp = BlendOp.Add,
                SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
                AlphaBlendOp = BlendOp.Add
            };

            PipelineColorBlendStateCreateInfo colorBlending = new() {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                AttachmentCount = 1,
                PAttachments = &colorBlendAttachment
            };

            var uniformLayout = uniformDescriptorSetLayout;
            PipelineLayoutCreateInfo layoutInfo = new() {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 1,
                PSetLayouts = &uniformLayout
            };

            if (vk.CreatePipelineLayout(device, in layoutInfo, null, out var layout) != Result.Success)
                throw new InvalidOperationException("Failed to create the water pipeline layout.");

            PipelineLayout = layout;

            GraphicsPipelineCreateInfo pipelineInfo = new() {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                Layout = PipelineLayout,
                RenderPass = renderPass,
                Subpass = 0,
                PVertexInputState = &vertexInput,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewport,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PDepthStencilState = &depthStencil,
                PColorBlendState = &colorBlending,
                PDynamicState = &dynamicState
            };

            fixed (PipelineShaderStageCreateInfo* stagesPointer = shaderStages)
            fixed (DynamicState* dynamicStatesPointer = dynamicStates) {
                pipelineInfo.PStages = stagesPointer;
                dynamicState.PDynamicStates = dynamicStatesPointer;
                if (vk.CreateGraphicsPipelines(device, default, 1, in pipelineInfo, null, out var pipeline) != Result.Success)
                    throw new InvalidOperationException("Failed to create the water graphics pipeline.");
                Pipeline = pipeline;
            }
        }
        finally {
            if (entryPoint != IntPtr.Zero)
                Marshal.FreeHGlobal(entryPoint);

            if (vertModule.Handle != 0)
                vk.DestroyShaderModule(device, vertModule, null);

            if (fragModule.Handle != 0)
                vk.DestroyShaderModule(device, fragModule, null);
        }
    }

    public void UploadMesh(WaterMeshData mesh)
    {
        if (mesh.Vertices.Length == 0 || mesh.Indices.Length == 0) {
            ClearMesh();
            return;
        }

        var vertexSize = checked((ulong)(Marshal.SizeOf<Vector3>() * mesh.Vertices.Length));
        var indexSize = checked((ulong)(sizeof(uint) * mesh.Indices.Length));

        Buffer vertexBuffer = default;
        DeviceMemory vertexMemory = default;
        Buffer indexBuffer = default;
        DeviceMemory indexMemory = default;

        try {
            AllocateBuffer(vertexSize, BufferUsageFlags.VertexBufferBit, out vertexBuffer, out vertexMemory);
            void* vertexData;
            if (context.Api.MapMemory(context.LogicalDevice, vertexMemory, 0, vertexSize, 0, &vertexData) != Result.Success)
                throw new InvalidOperationException("Failed to map the water vertex buffer.");

            mesh.Vertices.AsSpan().CopyTo(new Span<Vector3>(vertexData, mesh.Vertices.Length));
            context.Api.UnmapMemory(context.LogicalDevice, vertexMemory);

            AllocateBuffer(indexSize, BufferUsageFlags.IndexBufferBit, out indexBuffer, out indexMemory);
            void* indexData;
            if (context.Api.MapMemory(context.LogicalDevice, indexMemory, 0, indexSize, 0, &indexData) != Result.Success)
                throw new InvalidOperationException("Failed to map the water index buffer.");

            mesh.Indices.AsSpan().CopyTo(new Span<uint>(indexData, mesh.Indices.Length));
            context.Api.UnmapMemory(context.LogicalDevice, indexMemory);
        }
        catch {
            DestroyBuffer(vertexBuffer, vertexMemory);
            DestroyBuffer(indexBuffer, indexMemory);
            throw;
        }

        DestroyMeshBuffers();
        _vertexBuffer = vertexBuffer;
        _vertexBufferMemory = vertexMemory;
        _indexBuffer = indexBuffer;
        _indexBufferMemory = indexMemory;
        IndexCount = (uint)mesh.Indices.Length;
    }

    private ShaderModule CreateShaderModule(byte[] code)
    {
        var vk = context.Api;
        fixed (byte* codePointer = code) {
            ShaderModuleCreateInfo info = new() {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)code.Length,
                PCode = (uint*)codePointer
            };

            if (vk.CreateShaderModule(context.LogicalDevice, in info, null, out var module) != Result.Success)
                throw new InvalidOperationException("Failed to create a water shader module.");

            return module;
        }
    }

    private void AllocateBuffer(ulong size, BufferUsageFlags usage, out Buffer buffer, out DeviceMemory memory)
    {
        var vk = context.Api;
        var device = context.LogicalDevice;
        BufferCreateInfo bufferInfo = new() {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive
        };

        if (vk.CreateBuffer(device, in bufferInfo, null, out buffer) != Result.Success)
            throw new InvalidOperationException("Failed to create a water mesh buffer.");

        vk.GetBufferMemoryRequirements(device, buffer, out var requirements);
        MemoryAllocateInfo allocationInfo = new() {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = context.FindMemoryType(requirements.MemoryTypeBits,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit)
        };

        if (vk.AllocateMemory(device, in allocationInfo, null, out memory) != Result.Success) {
            vk.DestroyBuffer(device, buffer, null);
            buffer = default;

            throw new InvalidOperationException("Failed to allocate water mesh buffer memory.");
        }

        if (vk.BindBufferMemory(device, buffer, memory, 0) != Result.Success) {
            DestroyBuffer(buffer, memory);
            buffer = default;
            memory = default;

            throw new InvalidOperationException("Failed to bind water mesh buffer memory.");
        }
    }

    private void DestroyMeshBuffers()
    {
        DestroyBuffer(_vertexBuffer, _vertexBufferMemory);
        DestroyBuffer(_indexBuffer, _indexBufferMemory);

        _vertexBuffer = default;
        _vertexBufferMemory = default;
        _indexBuffer = default;
        _indexBufferMemory = default;
    }

    public void ClearMesh()
    {
        DestroyMeshBuffers();
        IndexCount = 0;
    }

    private void DestroyBuffer(Buffer buffer, DeviceMemory memory)
    {
        var vk = context.Api;
        var device = context.LogicalDevice;

        if (buffer.Handle != 0)
            vk.DestroyBuffer(device, buffer, null);

        if (memory.Handle != 0)
            vk.FreeMemory(device, memory, null);
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

        Pipeline = default;
        PipelineLayout = default;
        IndexCount = 0;
    }
}
