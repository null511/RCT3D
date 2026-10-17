using RCT3DViewer.Simulation;
using RCT3DViewer.Interop;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System.Numerics;
using System.Runtime.InteropServices;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace RCT3DViewer.Render;

public sealed class Renderer : IDisposable
{
    private const string DefaultParkPath = @"C:\Users\null5\Documents\OpenRCT2\save\Mothball Mountain.park";
    private const string DefaultRCT2Path = @"Z:\SteamLibrary\steamapps\common\Rollercoaster Tycoon 2";
    private const string DefaultOpenRCT2Path = "openrct2-data";

    private IWindow? _window;
    private Vk? _vk;
    private Instance _instance;
    private KhrSurface? _khrSurface;
    private SurfaceKHR _surface;
    private PhysicalDevice _physicalDevice;
    private Device _logicalDevice;
    private Queue _graphicsQueue;
    private uint _graphicsQueueFamilyIndex;

    private KhrSwapchain? _khrSwapchain;
    private SwapchainKHR _swapchain;
    private Image[] _swapchainImages = Array.Empty<Image>();
    private ImageView[] _swapchainImageViews = Array.Empty<ImageView>();
    private Framebuffer[] _framebuffers = Array.Empty<Framebuffer>();
    private Format _swapchainFormat;
    private Extent2D _swapchainExtent;

    private RenderPass _renderPass;
    private PipelineLayout _pipelineLayout;
    private Pipeline _graphicsPipeline;
    private CommandPool _commandPool;
    private CommandBuffer[] _commandBuffers = Array.Empty<CommandBuffer>();

    private const int MaxFramesInFlight = 2;
    private Silk.NET.Vulkan.Semaphore[] _imageAvailableSemaphores = Array.Empty<Silk.NET.Vulkan.Semaphore>();
    private Silk.NET.Vulkan.Semaphore[] _renderFinishedSemaphores = Array.Empty<Silk.NET.Vulkan.Semaphore>();
    private Fence[] _inFlightFences = Array.Empty<Fence>();
    private Fence[] _imagesInFlight = Array.Empty<Fence>();
    private uint _currentFrame;

    private Buffer _vertexBuffer;
    private DeviceMemory _vertexBufferMemory;
    private Buffer _indexBuffer;
    private DeviceMemory _indexBufferMemory;

    private IInputContext? _inputContext;
    private readonly FlightCamera _camera = new();
    private readonly List<TerrainVertex> _vertices = new();
    private readonly List<uint> _indices = new();
    private SimulationBridge? _simulation;
    private RCTTileSurfaceData[]? _surfaceData;
    private int _mapWidth;
    private int _mapHeight;

    public void Run()
    {
        var options = WindowOptions.DefaultVulkan;
        options.Size = new Vector2D<int>(1280, 720);
        options.Title = "RCT Cross-Platform 3D Real-Time Viewer";

        _window = Window.Create(options);
        _window.Load += Initialize;
        _window.Render += OnRenderFrame;
        _window.Update += OnUpdateFrame;
        _window.Closing += OnEngineShutdown;
        try
        {
            _window.Run();
        }
        finally
        {
            Dispose();
        }
    }

    public void Initialize()
    {
        try
        {
            var libraryPath = Environment.GetEnvironmentVariable("RCT3DVIEWER_OPENRCT2_DLL") ?? LibOpenRCT2.DefaultLibraryPath;
            var openRCT2Path = Environment.GetEnvironmentVariable("RCT3DVIEWER_OPENRCT2_DATA") ?? DefaultOpenRCT2Path;
            var rct2Path = Environment.GetEnvironmentVariable("RCT3DVIEWER_RCT2_DATA") ?? DefaultRCT2Path;
            var parkPath = Environment.GetEnvironmentVariable("RCT3DVIEWER_PARK") ?? DefaultParkPath;
            if (!Path.IsPathRooted(openRCT2Path))
                openRCT2Path = Path.Combine(AppContext.BaseDirectory, openRCT2Path);

            _simulation = new SimulationBridge(libraryPath, openRCT2Path, rct2Path);
            _simulation.LoadPark(parkPath);
            var (width, height) = _simulation.GetDimensions();
            if (width <= 0 || height <= 0 || (long)width * height > int.MaxValue)
                throw new InvalidOperationException($"OpenRCT2 returned invalid map dimensions {width}x{height}.");

            _surfaceData = new RCTTileSurfaceData[checked(width * height)];
            _simulation.FillSurfaceData(_surfaceData);
            _mapWidth = width;
            _mapHeight = height;
            _camera.Position = new Vector3(width * 1.5f, 120.0f, -height * 1.5f);
            Console.WriteLine($"Loaded park '{parkPath}' ({width}x{height}) through the OpenRCT2 adapter.");
        }
        catch (Exception ex)
        {
            _simulation?.Dispose();
            _simulation = null;
            _surfaceData = null;
            throw new InvalidOperationException("Failed to initialize OpenRCT2 and load the configured park.", ex);
        }

        Console.WriteLine("Initializing Graphics Subsystems via Silk.NET Vulkan...");
        _vk = Vk.GetApi();

        CreateVulkanInstance();
        CreateSurface();
        PickPhysicalDevice();
        CreateLogicalDevice();

        CreateSwapchain();
        CreateImageViews();
        CreateRenderPass();
        CreateGraphicsPipeline();
        CreateFramebuffers();

        SetupInputs();
        GenerateTerrainMesh(_surfaceData!, _mapWidth, _mapHeight);
        UploadMeshToGPU();

        CreateCommandInfrastructure();
        CreateSyncPrimitives();

        Console.WriteLine("\n=== Vulkan Engine Pipeline Fully Operational ===");
        Console.WriteLine("Hold down RIGHT-MOUSE BUTTON to look around. Use W,A,S,D to fly.");
    }

    private unsafe void CreateVulkanInstance()
    {
        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("RCT3DViewer"),
            ApplicationVersion = Vk.MakeVersion(1, 0),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi("Custom"),
            EngineVersion = Vk.MakeVersion(1, 0),
            ApiVersion = Vk.Version12
        };

        var extensions = _window!.VkSurface!.GetRequiredExtensions(out var extCount);
        InstanceCreateInfo createInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo,
            EnabledExtensionCount = extCount,
            PpEnabledExtensionNames = extensions
        };

        if (_vk!.CreateInstance(in createInfo, null, out _instance) != Result.Success)
            throw new Exception("CRITICAL: Failed to spin up Native Vulkan Instance.");

        Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
        Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);
    }

    private unsafe void CreateSurface()
    {
        if (!_vk!.TryGetInstanceExtension<KhrSurface>(_instance, out _khrSurface))
            throw new NotSupportedException("Vulkan Window Surface extension is not available.");

        var instanceHandle = new VkHandle(_instance.Handle);
        var rawHandle = _window!.VkSurface!.Create<AllocationCallbacks>(instanceHandle, null);
        _surface = new SurfaceKHR { Handle = rawHandle.Handle };
    }

    private unsafe void PickPhysicalDevice()
    {
        uint deviceCount = 0;
        _vk!.EnumeratePhysicalDevices(_instance, ref deviceCount, null);
        var devices = new PhysicalDevice[deviceCount];
        fixed (PhysicalDevice* devicesPtr = devices)
        {
            _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, devicesPtr);
        }

        _physicalDevice = devices[0];
    }

    private unsafe void CreateLogicalDevice()
    {
        uint queueFamilyCount = 0;
        _vk!.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, ref queueFamilyCount, null);
        var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* qPtr = queueFamilies)
        {
            _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, ref queueFamilyCount, qPtr);
        }

        for (uint i = 0; i < queueFamilyCount; i++)
        {
            if ((queueFamilies[i].QueueFlags & QueueFlags.GraphicsBit) != 0)
            {
                _graphicsQueueFamilyIndex = i;
                break;
            }
        }

        var queuePriority = 1.0f;
        DeviceQueueCreateInfo queueCreateInfo = new()
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = _graphicsQueueFamilyIndex,
            QueueCount = 1,
            PQueuePriorities = &queuePriority
        };

        var swapchainExtension = KhrSwapchain.ExtensionName;
        var swapchainExtPtr = Marshal.StringToHGlobalAnsi(swapchainExtension);
        var pSwapchainExt = (byte*)swapchainExtPtr;

        _vk.GetPhysicalDeviceFeatures(_physicalDevice, out PhysicalDeviceFeatures supportedFeatures);
        PhysicalDeviceFeatures deviceFeatures = new()
        {
            SamplerAnisotropy = supportedFeatures.SamplerAnisotropy
        };

        DeviceCreateInfo deviceCreateInfo = new()
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = 1,
            PQueueCreateInfos = &queueCreateInfo,
            EnabledExtensionCount = 1,
            PpEnabledExtensionNames = &pSwapchainExt,
            PEnabledFeatures = &deviceFeatures
        };

        if (_vk.CreateDevice(_physicalDevice, in deviceCreateInfo, null, out _logicalDevice) != Result.Success)
            throw new Exception("CRITICAL: Failed to instantiate Vulkan Logical Device.");

        _vk.GetDeviceQueue(_logicalDevice, _graphicsQueueFamilyIndex, 0, out _graphicsQueue);
        if (!_vk.TryGetDeviceExtension<KhrSwapchain>(_instance, _logicalDevice, out _khrSwapchain))
            throw new NotSupportedException("Vulkan Swapchain extension not available on device.");

        Marshal.FreeHGlobal(swapchainExtPtr);
    }

    private unsafe void CreateSwapchain()
    {
        _swapchainFormat = Format.B8G8R8A8Unorm;
        _swapchainExtent = new Extent2D((uint)_window!.Size.X, (uint)_window.Size.Y);

        SwapchainCreateInfoKHR createInfo = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surface,
            MinImageCount = 2,
            ImageFormat = _swapchainFormat,
            ImageColorSpace = ColorSpaceKHR.PaceSrgbNonlinearKhr,
            ImageExtent = _swapchainExtent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            ImageSharingMode = SharingMode.Exclusive,
            PreTransform = SurfaceTransformFlagsKHR.IdentityBitKhr,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = PresentModeKHR.FifoKhr,
            Clipped = true
        };

        if (_khrSwapchain!.CreateSwapchain(_logicalDevice, in createInfo, null, out _swapchain) != Result.Success)
            throw new Exception("Failed to generate Vulkan Presentation Swapchain.");

        uint imageCount = 0;
        _khrSwapchain.GetSwapchainImages(_logicalDevice, _swapchain, ref imageCount, null);
        _swapchainImages = new Image[imageCount];
        fixed (Image* imgPtr = _swapchainImages)
        {
            _khrSwapchain.GetSwapchainImages(_logicalDevice, _swapchain, ref imageCount, imgPtr);
        }
    }

    private unsafe void CreateImageViews()
    {
        _swapchainImageViews = new ImageView[_swapchainImages.Length];
        for (var i = 0; i < _swapchainImages.Length; i++)
        {
            ImageViewCreateInfo createInfo = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _swapchainImages[i],
                ViewType = ImageViewType.Type2D,
                Format = _swapchainFormat,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
            };
            if (_vk!.CreateImageView(_logicalDevice, in createInfo, null, out _swapchainImageViews[i]) != Result.Success)
                throw new Exception($"Failed to generate View Target for Frame buffer index: {i}");
        }
    }

    private unsafe void CreateRenderPass()
    {
        AttachmentDescription colorAttachment = new()
        {
            Format = _swapchainFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };
        AttachmentReference colorAttachmentRef = new()
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal
        };
        SubpassDescription subpass = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentRef
        };
        RenderPassCreateInfo renderPassInfo = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            PAttachments = &colorAttachment,
            SubpassCount = 1,
            PSubpasses = &subpass
        };
        if (_vk!.CreateRenderPass(_logicalDevice, in renderPassInfo, null, out _renderPass) != Result.Success)
            throw new Exception("Failed to configure Vulkan RenderPass structures.");
    }

    private unsafe void CreateGraphicsPipeline()
    {
        var vertPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Shaders", "terrain.vert.spv");
        var fragPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Shaders", "terrain.frag.spv");

        byte[] vertShaderCode;
        byte[] fragShaderCode;

        if (File.Exists(vertPath) && File.Exists(fragPath))
        {
            Console.WriteLine($"Successfully loaded native SPIR-V shaders from output: {vertPath}");
            vertShaderCode = File.ReadAllBytes(vertPath);
            fragShaderCode = File.ReadAllBytes(fragPath);
        }
        else
        {
            throw new FileNotFoundException($"CRITICAL: Shader binaries missing even after compilation pass. Check: {vertPath}");
        }

        ShaderModule vertModule;
        ShaderModule fragModule;
        fixed (byte* codePtr = vertShaderCode)
        {
            ShaderModuleCreateInfo info = new() { SType = StructureType.ShaderModuleCreateInfo, CodeSize = (nuint)vertShaderCode.Length, PCode = (uint*)codePtr };
            _vk!.CreateShaderModule(_logicalDevice, in info, null, out vertModule);
        }

        fixed (byte* codePtr = fragShaderCode)
        {
            ShaderModuleCreateInfo info = new() { SType = StructureType.ShaderModuleCreateInfo, CodeSize = (nuint)fragShaderCode.Length, PCode = (uint*)codePtr };
            _vk!.CreateShaderModule(_logicalDevice, in info, null, out fragModule);
        }

        PipelineShaderStageCreateInfo vertStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = vertModule,
            PName = (byte*)Marshal.StringToHGlobalAnsi("main")
        };
        PipelineShaderStageCreateInfo fragStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = fragModule,
            PName = (byte*)Marshal.StringToHGlobalAnsi("main")
        };
        var shaderStages = new[] { vertStageInfo, fragStageInfo };

        VertexInputBindingDescription bindingDescription = new()
        {
            Binding = 0,
            Stride = (uint)Marshal.SizeOf<TerrainVertex>(),
            InputRate = VertexInputRate.Vertex
        };

        var attributeDescriptions = new VertexInputAttributeDescription[]
        {
            new() { Binding = 0, Location = 0, Format = Format.R32G32B32Sfloat, Offset = (uint)Marshal.OffsetOf<TerrainVertex>(nameof(TerrainVertex.Position)) },
            new() { Binding = 0, Location = 1, Format = Format.R32G32B32Sfloat, Offset = (uint)Marshal.OffsetOf<TerrainVertex>(nameof(TerrainVertex.Normal)) },
            new() { Binding = 0, Location = 2, Format = Format.R32G32B32Sfloat, Offset = (uint)Marshal.OffsetOf<TerrainVertex>(nameof(TerrainVertex.Color)) }
        };

        PipelineVertexInputStateCreateInfo vertexInputInfo = new()
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            VertexBindingDescriptionCount = 1,
            PVertexBindingDescriptions = &bindingDescription,
            VertexAttributeDescriptionCount = (uint)attributeDescriptions.Length
        };

        PipelineInputAssemblyStateCreateInfo inputAssembly = new() { SType = StructureType.PipelineInputAssemblyStateCreateInfo, Topology = PrimitiveTopology.TriangleList };
        Viewport viewport = new() { X = 0.0f, Y = 0.0f, Width = _swapchainExtent.Width, Height = _swapchainExtent.Height, MinDepth = 0.0f, MaxDepth = 1.0f };
        Rect2D scissor = new() { Offset = new Offset2D(0, 0), Extent = _swapchainExtent };
        PipelineViewportStateCreateInfo viewportState = new() { SType = StructureType.PipelineViewportStateCreateInfo, ViewportCount = 1, PViewports = &viewport, ScissorCount = 1, PScissors = &scissor };
        PipelineRasterizationStateCreateInfo rasterizer = new() { SType = StructureType.PipelineRasterizationStateCreateInfo, PolygonMode = PolygonMode.Fill, CullMode = CullModeFlags.None, FrontFace = FrontFace.CounterClockwise, LineWidth = 1.0f };
        PipelineMultisampleStateCreateInfo multisampling = new() { SType = StructureType.PipelineMultisampleStateCreateInfo, RasterizationSamples = SampleCountFlags.Count1Bit };
        PipelineColorBlendAttachmentState colorBlendAttachment = new() { ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit, BlendEnable = false };
        PipelineColorBlendStateCreateInfo colorBlending = new() { SType = StructureType.PipelineColorBlendStateCreateInfo, AttachmentCount = 1, PAttachments = &colorBlendAttachment };
        PushConstantRange pushConstantRange = new() { StageFlags = ShaderStageFlags.VertexBit, Offset = 0, Size = (uint)Marshal.SizeOf<UniformBufferObject>() };
        PipelineLayoutCreateInfo pipelineLayoutInfo = new() { SType = StructureType.PipelineLayoutCreateInfo, PushConstantRangeCount = 1, PPushConstantRanges = &pushConstantRange };
        _vk!.CreatePipelineLayout(_logicalDevice, in pipelineLayoutInfo, null, out _pipelineLayout);
        GraphicsPipelineCreateInfo pipelineInfo = new()
        {
            SType = StructureType.GraphicsPipelineCreateInfo,
            StageCount = 2,
            Layout = _pipelineLayout,
            RenderPass = _renderPass,
            Subpass = 0,
            PVertexInputState = &vertexInputInfo,
            PInputAssemblyState = &inputAssembly,
            PViewportState = &viewportState,
            PRasterizationState = &rasterizer,
            PMultisampleState = &multisampling,
            PColorBlendState = &colorBlending
        };

        fixed (PipelineShaderStageCreateInfo* stagePtr = shaderStages)
        fixed (VertexInputAttributeDescription* attrPtr = attributeDescriptions)
        {
            vertexInputInfo.PVertexAttributeDescriptions = attrPtr;
            pipelineInfo.PStages = stagePtr;
            var result = _vk.CreateGraphicsPipelines(_logicalDevice, default, 1, in pipelineInfo, null, out _graphicsPipeline);
            if (result != Result.Success || _graphicsPipeline.Handle == 0UL)
            {
                throw new Exception($"Failed to create graphics pipeline. VkResult={result}, pipelineHandle={_graphicsPipeline.Handle}");
            }
        }

        _vk.DestroyShaderModule(_logicalDevice, vertModule, null);
        _vk.DestroyShaderModule(_logicalDevice, fragModule, null);
        Marshal.FreeHGlobal((IntPtr)vertStageInfo.PName);
        Marshal.FreeHGlobal((IntPtr)fragStageInfo.PName);
    }

    private unsafe void CreateFramebuffers()
    {
        _framebuffers = new Framebuffer[_swapchainImageViews.Length];
        for (var i = 0; i < _swapchainImageViews.Length; i++)
        {
            var attachment = _swapchainImageViews[i];
            FramebufferCreateInfo framebufferInfo = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _renderPass,
                AttachmentCount = 1,
                PAttachments = &attachment,
                Width = _swapchainExtent.Width,
                Height = _swapchainExtent.Height,
                Layers = 1
            };
            _vk!.CreateFramebuffer(_logicalDevice, in framebufferInfo, null, out _framebuffers[i]);
        }
    }

    private void GenerateTerrainMesh(ReadOnlySpan<RCTTileSurfaceData> surfaces, int width, int height)
    {
        _vertices.Clear();
        _indices.Clear();
        const float tileSize = 3.0f;
        const float baseHeightScale = 0.75f;
        const float cornerHeightScale = 1.5f;

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var surface = surfaces[y * width + x];
            var baseHeight = surface.BaseZ * baseHeightScale;
            var north = baseHeight + surface.NorthCornerHeight * cornerHeightScale;
            var east = baseHeight + surface.EastCornerHeight * cornerHeightScale;
            var south = baseHeight + surface.SouthCornerHeight * cornerHeightScale;
            var west = baseHeight + surface.WestCornerHeight * cornerHeightScale;
            // OpenRCT2's corner flags map North/East/South/West to the tile's
            // northeast/southeast/southwest/northwest vertices, respectively.
            var cNorth = new Vector3((x + 1) * tileSize, north, (y + 1) * tileSize);
            var cEast = new Vector3((x + 1) * tileSize, east, y * tileSize);
            var cSouth = new Vector3(x * tileSize, south, y * tileSize);
            var cWest = new Vector3(x * tileSize, west, (y + 1) * tileSize);
            // Keep both triangles counter-clockwise from above (positive Y normal).
            var normal1 = Vector3.Normalize(Vector3.Cross(cEast - cNorth, cWest - cNorth));
            var normal2 = Vector3.Normalize(Vector3.Cross(cSouth - cEast, cWest - cEast));
            var groundColor = GetTerrainColor(surface.MapColour);
            var baseIdx = (uint)_vertices.Count;
            _vertices.Add(new TerrainVertex(cNorth, normal1, groundColor));
            _vertices.Add(new TerrainVertex(cEast, normal1, groundColor));
            _vertices.Add(new TerrainVertex(cWest, normal1, groundColor));
            _vertices.Add(new TerrainVertex(cSouth, normal2, groundColor));
            _indices.Add(baseIdx);
            _indices.Add(baseIdx + 1);
            _indices.Add(baseIdx + 2);
            _indices.Add(baseIdx + 1);
            _indices.Add(baseIdx + 3);
            _indices.Add(baseIdx + 2);
        }
    }

    private static Vector3 GetTerrainColor(byte mapColour)
    {
        var value = mapColour & 0x0F;
        var shade = (mapColour >> 4) & 0x07;
        var intensity = 0.45f + shade * 0.08f;
        return value switch
        {
            0 => new Vector3(0.42f, 0.36f, 0.30f) * intensity,
            1 => new Vector3(0.30f, 0.56f, 0.25f) * intensity,
            2 => new Vector3(0.63f, 0.55f, 0.36f) * intensity,
            3 => new Vector3(0.26f, 0.48f, 0.64f) * intensity,
            4 => new Vector3(0.52f, 0.42f, 0.32f) * intensity,
            5 => new Vector3(0.58f, 0.38f, 0.25f) * intensity,
            _ => new Vector3(0.34f, 0.52f, 0.28f) * intensity,
        };
    }

    private unsafe void UploadMeshToGPU()
    {
        var vertexBufferSize = (ulong)(Marshal.SizeOf<TerrainVertex>() * _vertices.Count);
        AllocateBuffer(vertexBufferSize, BufferUsageFlags.VertexBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out _vertexBuffer, out _vertexBufferMemory);

        void* vData;
        _vk!.MapMemory(_logicalDevice, _vertexBufferMemory, 0, vertexBufferSize, 0, &vData);
        var vSpan = new Span<TerrainVertex>(vData, _vertices.Count);
        CollectionsMarshal.AsSpan(_vertices).CopyTo(vSpan);
        _vk.UnmapMemory(_logicalDevice, _vertexBufferMemory);

        var indexBufferSize = (ulong)(sizeof(uint) * _indices.Count);
        AllocateBuffer(indexBufferSize, BufferUsageFlags.IndexBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out _indexBuffer, out _indexBufferMemory);

        void* iData;
        _vk.MapMemory(_logicalDevice, _indexBufferMemory, 0, indexBufferSize, 0, &iData);
        var iSpan = new Span<uint>(iData, _indices.Count);
        CollectionsMarshal.AsSpan(_indices).CopyTo(iSpan);
        _vk.UnmapMemory(_logicalDevice, _indexBufferMemory);

        if (_vertices.Count > 0)
        {
            var v = _vertices[0];
            Console.WriteLine($"[Mesh] Uploaded vertices={_vertices.Count}, indices={_indices.Count}, firstPos=({v.Position.X:F2},{v.Position.Y:F2},{v.Position.Z:F2})");
        }
    }

    private unsafe void AllocateBuffer(ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties, out Buffer buffer, out DeviceMemory bufferMemory)
    {
        BufferCreateInfo bufferInfo = new() { SType = StructureType.BufferCreateInfo, Size = size, Usage = usage, SharingMode = SharingMode.Exclusive };

        if (_vk!.CreateBuffer(_logicalDevice, ref bufferInfo, null, out buffer) != Result.Success)
            throw new Exception("Failed to generate structural buffer layout.");

        _vk.GetBufferMemoryRequirements(_logicalDevice, buffer, out MemoryRequirements memRequirements);
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out PhysicalDeviceMemoryProperties memProperties);

        uint memoryTypeIndex = uint.MaxValue;
        for (uint i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((memRequirements.MemoryTypeBits & (1 << (int)i)) != 0 && (memProperties.MemoryTypes[(int)i].PropertyFlags & properties) == properties)
            {
                memoryTypeIndex = i;
                break;
            }
        }

        MemoryAllocateInfo allocInfo = new() { SType = StructureType.MemoryAllocateInfo, AllocationSize = memRequirements.Size, MemoryTypeIndex = memoryTypeIndex };
        _vk.AllocateMemory(_logicalDevice, ref allocInfo, null, out bufferMemory);
        _vk.BindBufferMemory(_logicalDevice, buffer, bufferMemory, 0);
    }

    private unsafe void CreateCommandInfrastructure()
    {
        CommandPoolCreateInfo poolInfo = new() { SType = StructureType.CommandPoolCreateInfo, QueueFamilyIndex = _graphicsQueueFamilyIndex, Flags = CommandPoolCreateFlags.ResetCommandBufferBit };
        _vk!.CreateCommandPool(_logicalDevice, in poolInfo, null, out _commandPool);
        CommandBufferAllocateInfo allocInfo = new() { SType = StructureType.CommandBufferAllocateInfo, CommandPool = _commandPool, Level = CommandBufferLevel.Primary, CommandBufferCount = (uint)_framebuffers.Length };
        _commandBuffers = new CommandBuffer[_framebuffers.Length];
        fixed (CommandBuffer* cbPtr = _commandBuffers)
        {
            _vk.AllocateCommandBuffers(_logicalDevice, in allocInfo, cbPtr);
        }
    }

    private unsafe void CreateSyncPrimitives()
    {
        SemaphoreCreateInfo semaphoreInfo = new() { SType = StructureType.SemaphoreCreateInfo };
        FenceCreateInfo fenceInfo = new() { SType = StructureType.FenceCreateInfo, Flags = FenceCreateFlags.SignaledBit };

        var imageCount = _swapchainImages.Length;
        _imageAvailableSemaphores = new Silk.NET.Vulkan.Semaphore[imageCount];
        _renderFinishedSemaphores = new Silk.NET.Vulkan.Semaphore[imageCount];
        _inFlightFences = new Fence[MaxFramesInFlight];

        for (var i = 0; i < imageCount; i++)
        {
            _vk!.CreateSemaphore(_logicalDevice, in semaphoreInfo, null, out _imageAvailableSemaphores[i]);
            _vk.CreateSemaphore(_logicalDevice, in semaphoreInfo, null, out _renderFinishedSemaphores[i]);
        }

        for (uint i = 0; i < MaxFramesInFlight; i++)
        {
            _vk!.CreateFence(_logicalDevice, in fenceInfo, null, out _inFlightFences[i]);
        }

        _imagesInFlight = new Fence[imageCount];
        for (var i = 0; i < _imagesInFlight.Length; i++) _imagesInFlight[i] = new Fence();
    }

    private void SetupInputs()
    {
        _inputContext = _window!.CreateInput();
        foreach (var mouse in _inputContext.Mice)
        {
            mouse.MouseMove += (m, pos) =>
            {
                if (m.IsButtonPressed(MouseButton.Right))
                {
                    _camera.HandleMouse(new Vector2(pos.X, pos.Y));
                    m.Cursor.CursorMode = CursorMode.Raw;
                }
                else
                {
                    m.Cursor.CursorMode = CursorMode.Normal;
                }
            };
        }
    }

    private void OnUpdateFrame(double deltaTime)
    {
        if (_inputContext != null && _inputContext.Keyboards.Count > 0)
            _camera.HandleKeyboard(_inputContext.Keyboards[0], (float)deltaTime);
    }

    private unsafe void OnRenderFrame(double deltaTime)
    {
        uint imageIndex = 0;
        _vk!.WaitForFences(_logicalDevice, 1, in _inFlightFences[_currentFrame], true, ulong.MaxValue);
        _khrSwapchain!.AcquireNextImage(_logicalDevice, _swapchain, ulong.MaxValue, _imageAvailableSemaphores[_currentFrame], default, ref imageIndex);

        if (_imagesInFlight[imageIndex].Handle != 0)
        {
            _vk.WaitForFences(_logicalDevice, 1, in _imagesInFlight[imageIndex], true, ulong.MaxValue);
        }

        _imagesInFlight[imageIndex] = _inFlightFences[_currentFrame];
        var cmd = _commandBuffers[imageIndex];
        _vk.ResetCommandBuffer(cmd, 0);
        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };
        _vk.BeginCommandBuffer(cmd, in beginInfo);

        ClearValue clearColor = new() { Color = new ClearColorValue(0.4f, 0.6f, 0.9f, 1.0f) };
        RenderPassBeginInfo renderPassInfo = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _renderPass,
            Framebuffer = _framebuffers[imageIndex],
            RenderArea = new Rect2D(new Offset2D(0, 0), _swapchainExtent),
            ClearValueCount = 1,
            PClearValues = &clearColor
        };

        _vk.CmdBeginRenderPass(cmd, in renderPassInfo, SubpassContents.Inline);
        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _graphicsPipeline);

        var aspect = (float)_window!.Size.X / _window.Size.Y;
        // Row-vector convention in shader: clip = position * viewProjection
        var viewProjection = _camera.GetViewMatrix() * _camera.GetProjectionMatrix(aspect);
        UniformBufferObject ubo = new() { ViewProjection = viewProjection };
        _vk.CmdPushConstants(cmd, _pipelineLayout, ShaderStageFlags.VertexBit, 0, (uint)Marshal.SizeOf<UniformBufferObject>(), &ubo);

        ulong offset = 0;
        var vBuf = _vertexBuffer;
        _vk.CmdBindVertexBuffers(cmd, 0, 1, in vBuf, in offset);
        _vk.CmdBindIndexBuffer(cmd, _indexBuffer, 0, IndexType.Uint32);
        _vk.CmdDrawIndexed(cmd, (uint)_indices.Count, 1, 0, 0, 0);
        _vk.CmdEndRenderPass(cmd);
        _vk.EndCommandBuffer(cmd);

        var waitStages = PipelineStageFlags.ColorAttachmentOutputBit;
        var imgAnimSema = _imageAvailableSemaphores[_currentFrame];
        var rendFinSema = _renderFinishedSemaphores[_currentFrame];

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &imgAnimSema,
            PWaitDstStageMask = &waitStages,
            CommandBufferCount = 1,
            PCommandBuffers = &cmd,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = &rendFinSema
        };

        _vk.ResetFences(_logicalDevice, 1, in _inFlightFences[_currentFrame]);
        _vk.QueueSubmit(_graphicsQueue, 1, in submitInfo, _inFlightFences[_currentFrame]);

        var swap = _swapchain;
        PresentInfoKHR presentInfo = new()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &rendFinSema,
            SwapchainCount = 1,
            PSwapchains = &swap,
            PImageIndices = &imageIndex
        };
        _khrSwapchain.QueuePresent(_graphicsQueue, in presentInfo);
        _currentFrame = (uint)((_currentFrame + 1) % MaxFramesInFlight);
    }

    private unsafe void OnEngineShutdown()
    {
        _vk!.DeviceWaitIdle(_logicalDevice);

        for (var i = 0; i < _imageAvailableSemaphores.Length; i++)
        {
            _vk.DestroySemaphore(_logicalDevice, _imageAvailableSemaphores[i], null);
        }

        for (var i = 0; i < _renderFinishedSemaphores.Length; i++)
        {
            _vk.DestroySemaphore(_logicalDevice, _renderFinishedSemaphores[i], null);
        }

        for (var i = 0; i < _inFlightFences.Length; i++)
        {
            _vk.DestroyFence(_logicalDevice, _inFlightFences[i], null);
        }

        _vk.DestroyBuffer(_logicalDevice, _vertexBuffer, null);
        _vk.FreeMemory(_logicalDevice, _vertexBufferMemory, null);
        _vk.DestroyBuffer(_logicalDevice, _indexBuffer, null);
        _vk.FreeMemory(_logicalDevice, _indexBufferMemory, null);
        _vk.DestroyCommandPool(_logicalDevice, _commandPool, null);
        foreach (var fb in _framebuffers) _vk.DestroyFramebuffer(_logicalDevice, fb, null);
        _vk.DestroyPipeline(_logicalDevice, _graphicsPipeline, null);
        _vk.DestroyPipelineLayout(_logicalDevice, _pipelineLayout, null);
        _vk.DestroyRenderPass(_logicalDevice, _renderPass, null);
        foreach (var view in _swapchainImageViews) _vk.DestroyImageView(_logicalDevice, view, null);
        _khrSwapchain!.DestroySwapchain(_logicalDevice, _swapchain, null);
        _vk.DestroyDevice(_logicalDevice, null);
        _khrSurface!.DestroySurface(_instance, _surface, null);
        _vk.DestroyInstance(_instance, null);
        Dispose();
    }

    public void Dispose()
    {
        var simulation = _simulation;
        _simulation = null;
        simulation?.Dispose();
    }
}
