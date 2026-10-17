using ImGuiNET;
using NativeFileDialogSharp;
using RCT3DViewer.Interop;
using RCT3DViewer.Simulation;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System.Numerics;
using System.Runtime.InteropServices;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace RCT3DViewer.Render;

public sealed class Renderer : IDisposable
{
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
    private Image[] _depthImages = Array.Empty<Image>();
    private DeviceMemory[] _depthImageMemories = Array.Empty<DeviceMemory>();
    private ImageView[] _depthImageViews = Array.Empty<ImageView>();
    private Format _depthFormat;
    private bool _framebufferResized;

    private RenderPass _renderPass;
    private PipelineLayout _pipelineLayout;
    private Pipeline _graphicsPipeline;
    private DescriptorSetLayout _terrainDescriptorSetLayout;
    private DescriptorPool _terrainDescriptorPool;
    private DescriptorSet _terrainDescriptorSet;
    private Image _terrainTextureImage;
    private DeviceMemory _terrainTextureMemory;
    private ImageView _terrainTextureView;
    private Sampler _terrainTextureSampler;
    private TerrainMaterialLibrary? _terrainMaterials;
    private CommandPool _commandPool;
    private CommandBuffer[] _commandBuffers = Array.Empty<CommandBuffer>();

    private const int MaxFramesInFlight = 2;
    private Semaphore[] _imageAvailableSemaphores = Array.Empty<Semaphore>();
    private Semaphore[] _renderFinishedSemaphores = Array.Empty<Semaphore>();
    private Fence[] _inFlightFences = Array.Empty<Fence>();
    private Fence[] _imagesInFlight = Array.Empty<Fence>();
    private uint _currentFrame;

    private Buffer _vertexBuffer;
    private DeviceMemory _vertexBufferMemory;
    private Buffer _indexBuffer;
    private DeviceMemory _indexBufferMemory;

    private IInputContext? _inputContext;
    private ImGuiVulkanRenderer? _imguiRenderer;
    private string? _loadedParkPath;
    private string? _parkLoadError;
    private bool _showSkyModal;
    private bool _showParkLoadError;
    private bool _exitRequested;
    private float _timeOfDayHours = 12.0f;
    private float _sunAngleDegrees = 45.0f;
    private float _displayedFps;
    private readonly FlightCamera _camera = new();
    private readonly List<TerrainVertex> _vertices = new();
    private readonly List<uint> _indices = new();
    private SimulationBridge? _simulation;
    private RCTTileSurfaceData[]? _surfaceData;
    //private string? _parkPath;
    private int _mapWidth;
    private int _mapHeight;
    private int _terrainSkirtSections;
    private int _terrainSkirtTriangles;
    private int _terrainSkirtFlatEdges;

    public string? LibraryPath { get; set; }
    public string? OpenRCT2Path { get; set; }
    public string? Rct2Path { get; set; }
    public float TerrainSkirtFloor { get; set; } = -24.0f;


    public void Run()
    {
        var options = WindowOptions.DefaultVulkan;
        options.Size = new Vector2D<int>(1280, 720);
        options.Title = "RCT Cross-Platform 3D Real-Time Viewer";

        _window = Window.Create(options);
        _window.FramebufferResize += _ => _framebufferResized = true;
        _window.Load += Initialize;
        _window.Render += OnRenderFrame;
        _window.Update += OnUpdateFrame;
        _window.Closing += OnEngineShutdown;

        try {
            _window.Run();
        }
        finally {
            Dispose();
        }
    }

    public void Initialize()
    {
        if (string.IsNullOrEmpty(OpenRCT2Path))
            throw new InvalidOperationException("OpenRCT2Path is undefined!");

        if (string.IsNullOrEmpty(Rct2Path))
            throw new InvalidOperationException("Rct2Path is undefined!");
        
        //if (string.IsNullOrEmpty(_parkPath))
        //    throw new InvalidOperationException("ParkPath is undefined!");

        if (string.IsNullOrEmpty(LibraryPath))
            throw new InvalidOperationException("LibraryPath is undefined!");

        try {
            if (!Path.IsPathRooted(OpenRCT2Path))
                OpenRCT2Path = Path.Combine(AppContext.BaseDirectory, OpenRCT2Path);

            _simulation = new SimulationBridge(LibraryPath, OpenRCT2Path, Rct2Path);
            //_simulation.LoadPark(_parkPath);
            //_loadedParkPath = _parkPath;
            //var (width, height) = _simulation.GetDimensions();
            //if (width <= 0 || height <= 0 || (long)width * height > int.MaxValue)
            //    throw new InvalidOperationException($"OpenRCT2 returned invalid map dimensions {width}x{height}.");

            //_surfaceData = new RCTTileSurfaceData[checked(width * height)];
            //_simulation.FillSurfaceData(_surfaceData);
            _terrainMaterials = TerrainMaterialLibrary.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Textures", "Terrain"));
            //_mapWidth = width;
            //_mapHeight = height;
            //_camera.Position = new Vector3(width * 1.5f, 120.0f, -height * 1.5f);
            //Console.WriteLine($"Loaded park '{_parkPath}' ({width}x{height}) through the OpenRCT2 adapter.");
        }
        catch (Exception ex) {
            _simulation?.Dispose();
            _simulation = null;
            _surfaceData = null;

            throw new InvalidOperationException("Failed to initialize OpenRCT2!", ex);
        }

        Console.WriteLine("Initializing Graphics Subsystems via Silk.NET Vulkan...");
        _vk = Vk.GetApi();

        CreateVulkanInstance();
        CreateSurface();
        PickPhysicalDevice();
        CreateLogicalDevice();

        CreateSwapchain();
        CreateImageViews();
        CreateDepthResources();
        CreateRenderPass();
        CreateGraphicsPipeline();
        CreateFramebuffers();

        SetupInputs();

        //if (_surfaceData != null) {
        //    GenerateTerrainMesh(_surfaceData!, _mapWidth, _mapHeight);
        //    UploadMeshToGPU();
        //}

        CreateCommandInfrastructure();
        CreateTerrainTextureResources();
        CreateSyncPrimitives();

        _imguiRenderer = new ImGuiVulkanRenderer(_vk, _physicalDevice, _logicalDevice, _graphicsQueue, _renderPass, _commandPool,
            Path.Combine(AppContext.BaseDirectory, "Shaders"), _swapchainImages.Length);

        Console.WriteLine("\n=== Vulkan Engine Pipeline Fully Operational ===");
        Console.WriteLine("Hold down RIGHT-MOUSE BUTTON to look around. Use W,A,S,D to fly.");
    }

    private unsafe void CreateVulkanInstance()
    {
        ApplicationInfo appInfo = new() {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("RCT3DViewer"),
            ApplicationVersion = Vk.MakeVersion(1, 0),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi("Custom"),
            EngineVersion = Vk.MakeVersion(1, 0),
            ApiVersion = Vk.Version12
        };

        var extensions = _window!.VkSurface!.GetRequiredExtensions(out var extCount);

        InstanceCreateInfo createInfo = new() {
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
        fixed (PhysicalDevice* devicesPtr = devices) {
            _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, devicesPtr);
        }

        _physicalDevice = devices[0];
    }

    private unsafe void CreateLogicalDevice()
    {
        uint queueFamilyCount = 0;
        _vk!.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, ref queueFamilyCount, null);
        var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* qPtr = queueFamilies) {
            _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, ref queueFamilyCount, qPtr);
        }

        for (uint i = 0; i < queueFamilyCount; i++) {
            if ((queueFamilies[i].QueueFlags & QueueFlags.GraphicsBit) != 0) {
                _graphicsQueueFamilyIndex = i;
                break;
            }
        }

        var queuePriority = 1.0f;
        DeviceQueueCreateInfo queueCreateInfo = new() {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = _graphicsQueueFamilyIndex,
            QueueCount = 1,
            PQueuePriorities = &queuePriority
        };

        var swapchainExtension = KhrSwapchain.ExtensionName;
        var swapchainExtPtr = Marshal.StringToHGlobalAnsi(swapchainExtension);
        var pSwapchainExt = (byte*)swapchainExtPtr;

        _vk.GetPhysicalDeviceFeatures(_physicalDevice, out PhysicalDeviceFeatures supportedFeatures);

        PhysicalDeviceFeatures deviceFeatures = new() {
            SamplerAnisotropy = supportedFeatures.SamplerAnisotropy,
        };

        DeviceCreateInfo deviceCreateInfo = new() {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = 1,
            PQueueCreateInfos = &queueCreateInfo,
            EnabledExtensionCount = 1,
            PpEnabledExtensionNames = &pSwapchainExt,
            PEnabledFeatures = &deviceFeatures,
        };

        if (_vk.CreateDevice(_physicalDevice, in deviceCreateInfo, null, out _logicalDevice) != Result.Success)
            throw new Exception("CRITICAL: Failed to instantiate Vulkan Logical Device.");

        _vk.GetDeviceQueue(_logicalDevice, _graphicsQueueFamilyIndex, 0, out _graphicsQueue);
        if (!_vk.TryGetDeviceExtension<KhrSwapchain>(_instance, _logicalDevice, out _khrSwapchain))
            throw new NotSupportedException("Vulkan Swapchain extension not available on device.");

        Marshal.FreeHGlobal(swapchainExtPtr);
    }

    private unsafe void CreateSwapchain(SwapchainKHR oldSwapchain = default)
    {
        _swapchainFormat = Format.B8G8R8A8Unorm;

        if (_khrSurface!.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice, _surface, out var capabilities) != Result.Success)
            throw new Exception("Failed to query Vulkan surface capabilities.");

        if ((capabilities.SupportedUsageFlags & ImageUsageFlags.ColorAttachmentBit) == 0)
            throw new NotSupportedException("The Vulkan surface does not support color attachment swapchain images.");

        var framebufferSize = _window!.FramebufferSize;
        _swapchainExtent = capabilities.CurrentExtent.Width != uint.MaxValue
            ? capabilities.CurrentExtent
            : new Extent2D(
                Math.Clamp((uint)Math.Max(0, framebufferSize.X), capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width),
                Math.Clamp((uint)Math.Max(0, framebufferSize.Y), capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height));

        var requestedImageCount = Math.Max(2u, capabilities.MinImageCount);
        if (capabilities.MaxImageCount > 0)
            requestedImageCount = Math.Min(requestedImageCount, capabilities.MaxImageCount);

        var compositeAlpha = (capabilities.SupportedCompositeAlpha & CompositeAlphaFlagsKHR.OpaqueBitKhr) != 0
            ? CompositeAlphaFlagsKHR.OpaqueBitKhr
            : (capabilities.SupportedCompositeAlpha & CompositeAlphaFlagsKHR.PreMultipliedBitKhr) != 0
                ? CompositeAlphaFlagsKHR.PreMultipliedBitKhr
                : (capabilities.SupportedCompositeAlpha & CompositeAlphaFlagsKHR.PostMultipliedBitKhr) != 0
                    ? CompositeAlphaFlagsKHR.PostMultipliedBitKhr
                    : CompositeAlphaFlagsKHR.InheritBitKhr;

        SwapchainCreateInfoKHR createInfo = new() {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surface,
            MinImageCount = requestedImageCount,
            ImageFormat = _swapchainFormat,
            ImageColorSpace = ColorSpaceKHR.PaceSrgbNonlinearKhr,
            ImageExtent = _swapchainExtent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            ImageSharingMode = SharingMode.Exclusive,
            PreTransform = capabilities.CurrentTransform,
            CompositeAlpha = compositeAlpha,
            PresentMode = PresentModeKHR.FifoKhr,
            Clipped = true,
            OldSwapchain = oldSwapchain
        };

        if (_khrSwapchain!.CreateSwapchain(_logicalDevice, in createInfo, null, out var newSwapchain) != Result.Success)
            throw new Exception("Failed to generate Vulkan Presentation Swapchain.");

        _swapchain = newSwapchain;

        uint imageCount = 0;
        _khrSwapchain.GetSwapchainImages(_logicalDevice, _swapchain, ref imageCount, null);
        _swapchainImages = new Image[imageCount];
        fixed (Image* imgPtr = _swapchainImages) {
            _khrSwapchain.GetSwapchainImages(_logicalDevice, _swapchain, ref imageCount, imgPtr);
        }
    }

    private unsafe void CreateImageViews()
    {
        _swapchainImageViews = new ImageView[_swapchainImages.Length];

        for (var i = 0; i < _swapchainImages.Length; i++) {
            ImageViewCreateInfo createInfo = new() {
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

    private Format FindDepthFormat()
    {
        var candidates = new[] {
            Format.D32Sfloat,
            Format.D32SfloatS8Uint,
            Format.D24UnormS8Uint
        };

        foreach (var candidate in candidates) {
            _vk!.GetPhysicalDeviceFormatProperties(_physicalDevice, candidate, out var properties);

            if ((properties.OptimalTilingFeatures & FormatFeatureFlags.DepthStencilAttachmentBit) != 0)
                return candidate;
        }

        throw new NotSupportedException("The selected Vulkan device has no supported depth attachment format.");
    }

    private unsafe void CreateDepthResources()
    {
        _depthFormat = FindDepthFormat();
        _depthImages = new Image[_swapchainImages.Length];
        _depthImageMemories = new DeviceMemory[_swapchainImages.Length];
        _depthImageViews = new ImageView[_swapchainImages.Length];

        try {
            for (var i = 0; i < _depthImages.Length; i++)
                CreateDepthResource(i);
        }
        catch {
            DestroyDepthResources();
            throw;
        }
    }

    private unsafe void CreateDepthResource(int index)
    {
        ImageCreateInfo imageInfo = new() {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D(_swapchainExtent.Width, _swapchainExtent.Height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Format = _depthFormat,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = ImageUsageFlags.DepthStencilAttachmentBit,
            Samples = SampleCountFlags.Count1Bit,
            SharingMode = SharingMode.Exclusive
        };

        if (_vk!.CreateImage(_logicalDevice, ref imageInfo, null, out _depthImages[index]) != Result.Success)
            throw new Exception("Failed to create a Vulkan depth image.");

        _vk.GetImageMemoryRequirements(_logicalDevice, _depthImages[index], out var requirements);

        MemoryAllocateInfo allocationInfo = new() {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = FindMemoryType(requirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
        };

        if (_vk.AllocateMemory(_logicalDevice, ref allocationInfo, null, out _depthImageMemories[index]) != Result.Success)
            throw new Exception("Failed to allocate Vulkan depth image memory.");

        if (_vk.BindImageMemory(_logicalDevice, _depthImages[index], _depthImageMemories[index], 0) != Result.Success)
            throw new Exception("Failed to bind Vulkan depth image memory.");

        ImageViewCreateInfo viewInfo = new() {
            SType = StructureType.ImageViewCreateInfo,
            Image = _depthImages[index],
            ViewType = ImageViewType.Type2D,
            Format = _depthFormat,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.DepthBit, 0, 1, 0, 1)
        };

        if (_vk.CreateImageView(_logicalDevice, in viewInfo, null, out _depthImageViews[index]) != Result.Success)
            throw new Exception("Failed to create a Vulkan depth image view.");
    }

    private unsafe void DestroyDepthResources()
    {
        if (_vk is null || _logicalDevice.Handle == 0) return;

        for (var i = 0; i < _depthImages.Length; i++) {
            if (_depthImageViews[i].Handle != 0)
                _vk.DestroyImageView(_logicalDevice, _depthImageViews[i], null);

            if (_depthImages[i].Handle != 0)
                _vk.DestroyImage(_logicalDevice, _depthImages[i], null);

            if (_depthImageMemories[i].Handle != 0)
                _vk.FreeMemory(_logicalDevice, _depthImageMemories[i], null);
        }

        _depthImages = Array.Empty<Image>();
        _depthImageMemories = Array.Empty<DeviceMemory>();
        _depthImageViews = Array.Empty<ImageView>();
    }

    private unsafe void CreateTerrainTextureResources()
    {
        var materials = _terrainMaterials ?? throw new InvalidOperationException("Terrain materials were not loaded.");
        var layerBytes = checked((ulong)materials.Width * (ulong)materials.Height * 4);
        var textureBytes = checked(layerBytes * (ulong)materials.LayerCount);

        AllocateBuffer(textureBytes, BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out var stagingBuffer, out var stagingMemory);

        try {
            void* mapped;
            if (_vk!.MapMemory(_logicalDevice, stagingMemory, 0, textureBytes, 0, &mapped) != Result.Success)
                throw new Exception("Failed to map terrain texture staging memory.");

            Marshal.Copy(materials.Pixels, 0, (IntPtr)mapped, materials.Pixels.Length);
            _vk.UnmapMemory(_logicalDevice, stagingMemory);

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

            if (_vk.CreateImage(_logicalDevice, ref imageInfo, null, out _terrainTextureImage) != Result.Success)
                throw new Exception("Failed to create terrain texture array image.");

            _vk.GetImageMemoryRequirements(_logicalDevice, _terrainTextureImage, out var requirements);

            MemoryAllocateInfo allocationInfo = new() {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = requirements.Size,
                MemoryTypeIndex = FindMemoryType(requirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
            };

            if (_vk.AllocateMemory(_logicalDevice, ref allocationInfo, null, out _terrainTextureMemory) != Result.Success)
                throw new Exception("Failed to allocate terrain texture array memory.");

            if (_vk.BindImageMemory(_logicalDevice, _terrainTextureImage, _terrainTextureMemory, 0) != Result.Success)
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
            _vk!.DestroyBuffer(_logicalDevice, stagingBuffer, null);
            _vk.FreeMemory(_logicalDevice, stagingMemory, null);
        }

        ImageViewCreateInfo viewInfo = new() {
            SType = StructureType.ImageViewCreateInfo,
            Image = _terrainTextureImage,
            ViewType = ImageViewType.Type2DArray,
            Format = Format.R8G8B8A8Srgb,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, (uint)materials.LayerCount)
        };

        if (_vk!.CreateImageView(_logicalDevice, in viewInfo, null, out _terrainTextureView) != Result.Success)
            throw new Exception("Failed to create terrain texture array view.");

        SamplerCreateInfo samplerInfo = new() {
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

        if (_vk.CreateSampler(_logicalDevice, in samplerInfo, null, out _terrainTextureSampler) != Result.Success)
            throw new Exception("Failed to create terrain texture sampler.");

        var poolSizes = new DescriptorPoolSize[] {
            new() { Type = DescriptorType.SampledImage, DescriptorCount = 1 },
            new() { Type = DescriptorType.Sampler, DescriptorCount = 1 }
        };

        fixed (DescriptorPoolSize* poolSizesPtr = poolSizes) {
            DescriptorPoolCreateInfo poolInfo = new() {
                SType = StructureType.DescriptorPoolCreateInfo,
                MaxSets = 1,
                PoolSizeCount = (uint)poolSizes.Length,
                PPoolSizes = poolSizesPtr
            };

            if (_vk.CreateDescriptorPool(_logicalDevice, in poolInfo, null, out _terrainDescriptorPool) != Result.Success)
                throw new Exception("Failed to create terrain texture descriptor pool.");
        }

        var setLayout = _terrainDescriptorSetLayout;
        DescriptorSetAllocateInfo setInfo = new() {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _terrainDescriptorPool,
            DescriptorSetCount = 1,
            PSetLayouts = &setLayout
        };

        if (_vk.AllocateDescriptorSets(_logicalDevice, in setInfo, out _terrainDescriptorSet) != Result.Success)
            throw new Exception("Failed to allocate terrain texture descriptor set.");

        DescriptorImageInfo imageDescriptor = new() { ImageView = _terrainTextureView, ImageLayout = ImageLayout.ShaderReadOnlyOptimal };
        DescriptorImageInfo samplerDescriptor = new() { Sampler = _terrainTextureSampler };

        var writes = new WriteDescriptorSet[] {
            new() { SType = StructureType.WriteDescriptorSet, DstSet = _terrainDescriptorSet, DstBinding = 0, DescriptorCount = 1, DescriptorType = DescriptorType.SampledImage },
            new() { SType = StructureType.WriteDescriptorSet, DstSet = _terrainDescriptorSet, DstBinding = 1, DescriptorCount = 1, DescriptorType = DescriptorType.Sampler }
        };

        fixed (WriteDescriptorSet* writesPtr = writes) {
            writes[0].PImageInfo = &imageDescriptor;
            writes[1].PImageInfo = &samplerDescriptor;
            _vk.UpdateDescriptorSets(_logicalDevice, (uint)writes.Length, writesPtr, 0, null);
        }
    }

    private unsafe void UploadTerrainTexture(Buffer stagingBuffer, BufferImageCopy[] regions)
    {
        CommandBufferAllocateInfo allocationInfo = new() {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };

        if (_vk!.AllocateCommandBuffers(_logicalDevice, in allocationInfo, out var commandBuffer) != Result.Success)
            throw new Exception("Failed to allocate a one-time command buffer.");

        try {
            CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
            _vk.BeginCommandBuffer(commandBuffer, in beginInfo);

            ImageMemoryBarrier toTransfer = new() {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.TransferDstOptimal,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = _terrainTextureImage,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, (uint)_terrainMaterials!.LayerCount),
                SrcAccessMask = AccessFlags.None,
                DstAccessMask = AccessFlags.TransferWriteBit
            };

            _vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit, 0, 0, null, 0, null, 1, &toTransfer);

            fixed (BufferImageCopy* regionsPtr = regions)
                _vk.CmdCopyBufferToImage(commandBuffer, stagingBuffer, _terrainTextureImage,
                    ImageLayout.TransferDstOptimal, (uint)regions.Length, regionsPtr);

            ImageMemoryBarrier toShader = new() {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.TransferDstOptimal,
                NewLayout = ImageLayout.ShaderReadOnlyOptimal,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = _terrainTextureImage,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, (uint)_terrainMaterials.LayerCount),
                SrcAccessMask = AccessFlags.TransferWriteBit,
                DstAccessMask = AccessFlags.ShaderReadBit
            };

            _vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, 0, 0, null, 0, null, 1, &toShader);

            _vk.EndCommandBuffer(commandBuffer);
            SubmitInfo submitInfo = new() { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &commandBuffer };
            if (_vk.QueueSubmit(_graphicsQueue, 1, in submitInfo, default) != Result.Success || _vk.QueueWaitIdle(_graphicsQueue) != Result.Success)
                throw new Exception("Failed to submit terrain texture upload commands.");
        }
        finally {
            _vk.FreeCommandBuffers(_logicalDevice, _commandPool, 1, in commandBuffer);
        }
    }

    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _vk!.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memoryProperties);
        for (uint i = 0; i < memoryProperties.MemoryTypeCount; i++) {
            if ((typeFilter & (1 << (int)i)) != 0 &&
                (memoryProperties.MemoryTypes[(int)i].PropertyFlags & properties) == properties)
                return i;
        }

        throw new InvalidOperationException($"No Vulkan memory type supports {properties}.");
    }

    private unsafe void CreateRenderPass()
    {
        AttachmentDescription colorAttachment = new() {
            Format = _swapchainFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };

        AttachmentReference colorAttachmentRef = new() {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal
        };

        AttachmentDescription depthAttachment = new() {
            Format = _depthFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
        };

        AttachmentReference depthAttachmentRef = new() {
            Attachment = 1,
            Layout = ImageLayout.DepthStencilAttachmentOptimal
        };

        SubpassDescription subpass = new() {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentRef,
            PDepthStencilAttachment = &depthAttachmentRef
        };

        var attachments = new[] { colorAttachment, depthAttachment };

        fixed (AttachmentDescription* attachmentsPtr = attachments) {
            RenderPassCreateInfo renderPassInfo = new() {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = (uint)attachments.Length,
                PAttachments = attachmentsPtr,
                SubpassCount = 1,
                PSubpasses = &subpass
            };

            if (_vk!.CreateRenderPass(_logicalDevice, in renderPassInfo, null, out _renderPass) != Result.Success)
                throw new Exception("Failed to configure Vulkan RenderPass structures.");
        }
    }

    private unsafe void CreateGraphicsPipeline()
    {
        var vertPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Shaders", "terrain.vert.spv");
        var fragPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Shaders", "terrain.frag.spv");

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
            _vk!.CreateShaderModule(_logicalDevice, in info, null, out vertModule);
        }

        fixed (byte* codePtr = fragShaderCode) {
            ShaderModuleCreateInfo info = new() { SType = StructureType.ShaderModuleCreateInfo, CodeSize = (nuint)fragShaderCode.Length, PCode = (uint*)codePtr };
            _vk!.CreateShaderModule(_logicalDevice, in info, null, out fragModule);
        }

        PipelineShaderStageCreateInfo vertStageInfo = new() {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = vertModule,
            PName = (byte*)Marshal.StringToHGlobalAnsi("main")
        };

        PipelineShaderStageCreateInfo fragStageInfo = new() {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = fragModule,
            PName = (byte*)Marshal.StringToHGlobalAnsi("main")
        };

        var shaderStages = new[] { vertStageInfo, fragStageInfo };

        VertexInputBindingDescription bindingDescription = new() {
            Binding = 0,
            Stride = (uint)Marshal.SizeOf<TerrainVertex>(),
            InputRate = VertexInputRate.Vertex
        };

        var attributeDescriptions = new VertexInputAttributeDescription[] {
            new() { Binding = 0, Location = 0, Format = Format.R32G32B32Sfloat, Offset = (uint)Marshal.OffsetOf<TerrainVertex>(nameof(TerrainVertex.Position)) },
            new() { Binding = 0, Location = 1, Format = Format.R32G32B32Sfloat, Offset = (uint)Marshal.OffsetOf<TerrainVertex>(nameof(TerrainVertex.Normal)) },
            new() { Binding = 0, Location = 2, Format = Format.R32G32Sfloat, Offset = (uint)Marshal.OffsetOf<TerrainVertex>(nameof(TerrainVertex.TextureCoordinate)) },
            new() { Binding = 0, Location = 3, Format = Format.R32Sfloat, Offset = (uint)Marshal.OffsetOf<TerrainVertex>(nameof(TerrainVertex.MaterialId)) }
        };

        PipelineVertexInputStateCreateInfo vertexInputInfo = new() {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            VertexBindingDescriptionCount = 1,
            PVertexBindingDescriptions = &bindingDescription,
            VertexAttributeDescriptionCount = (uint)attributeDescriptions.Length
        };

        PipelineInputAssemblyStateCreateInfo inputAssembly = new() { SType = StructureType.PipelineInputAssemblyStateCreateInfo, Topology = PrimitiveTopology.TriangleList };
        PipelineViewportStateCreateInfo viewportState = new() { SType = StructureType.PipelineViewportStateCreateInfo, ViewportCount = 1, ScissorCount = 1 };
        var dynamicStates = new[] { DynamicState.Viewport, DynamicState.Scissor };

        PipelineDynamicStateCreateInfo dynamicState = new() {
            SType = StructureType.PipelineDynamicStateCreateInfo,
            DynamicStateCount = (uint)dynamicStates.Length
        };

        PipelineRasterizationStateCreateInfo rasterizer = new() { SType = StructureType.PipelineRasterizationStateCreateInfo, PolygonMode = PolygonMode.Fill, CullMode = CullModeFlags.None, FrontFace = FrontFace.CounterClockwise, LineWidth = 1.0f };
        PipelineMultisampleStateCreateInfo multisampling = new() { SType = StructureType.PipelineMultisampleStateCreateInfo, RasterizationSamples = SampleCountFlags.Count1Bit };
        
        PipelineDepthStencilStateCreateInfo depthStencil = new() {
            SType = StructureType.PipelineDepthStencilStateCreateInfo,
            DepthTestEnable = true,
            DepthWriteEnable = true,
            DepthCompareOp = CompareOp.Less,
            MinDepthBounds = 0.0f,
            MaxDepthBounds = 1.0f
        };

        PipelineColorBlendAttachmentState colorBlendAttachment = new() { ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit, BlendEnable = false };
        PipelineColorBlendStateCreateInfo colorBlending = new() { SType = StructureType.PipelineColorBlendStateCreateInfo, AttachmentCount = 1, PAttachments = &colorBlendAttachment };
        
        var descriptorBindings = new DescriptorSetLayoutBinding[] {
            new() { Binding = 0, DescriptorType = DescriptorType.SampledImage, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit },
            new() { Binding = 1, DescriptorType = DescriptorType.Sampler, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit }
        };

        fixed (DescriptorSetLayoutBinding* bindingsPtr = descriptorBindings) {
            DescriptorSetLayoutCreateInfo descriptorLayoutInfo = new() {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)descriptorBindings.Length,
                PBindings = bindingsPtr
            };

            if (_vk!.CreateDescriptorSetLayout(_logicalDevice, in descriptorLayoutInfo, null, out _terrainDescriptorSetLayout) != Result.Success)
                throw new Exception("Failed to create the terrain texture descriptor-set layout.");
        }

        var descriptorLayout = _terrainDescriptorSetLayout;
        PushConstantRange pushConstantRange = new() {
            StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
            Size = (uint)Marshal.SizeOf<UniformBufferObject>(),
            Offset = 0,
        };
        
        PipelineLayoutCreateInfo pipelineLayoutInfo = new() {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = &descriptorLayout,
            PushConstantRangeCount = 1,
            PPushConstantRanges = &pushConstantRange
        };

        _vk!.CreatePipelineLayout(_logicalDevice, in pipelineLayoutInfo, null, out _pipelineLayout);

        GraphicsPipelineCreateInfo pipelineInfo = new() {
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
            PDepthStencilState = &depthStencil,
            PColorBlendState = &colorBlending,
            PDynamicState = &dynamicState
        };

        fixed (PipelineShaderStageCreateInfo* stagePtr = shaderStages)
        fixed (VertexInputAttributeDescription* attrPtr = attributeDescriptions)
        fixed (DynamicState* dynamicStatesPtr = dynamicStates) {
            vertexInputInfo.PVertexAttributeDescriptions = attrPtr;
            dynamicState.PDynamicStates = dynamicStatesPtr;
            pipelineInfo.PStages = stagePtr;
            var result = _vk.CreateGraphicsPipelines(_logicalDevice, default, 1, in pipelineInfo, null, out _graphicsPipeline);
            if (result != Result.Success || _graphicsPipeline.Handle == 0UL) {
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
        for (var i = 0; i < _swapchainImageViews.Length; i++) {
            var attachments = new[] {
                _swapchainImageViews[i],
                _depthImageViews[i],
            };

            fixed (ImageView* attachmentsPtr = attachments) {
                FramebufferCreateInfo framebufferInfo = new() {
                    SType = StructureType.FramebufferCreateInfo,
                    RenderPass = _renderPass,
                    AttachmentCount = (uint)attachments.Length,
                    PAttachments = attachmentsPtr,
                    Width = _swapchainExtent.Width,
                    Height = _swapchainExtent.Height,
                    Layers = 1
                };

                if (_vk!.CreateFramebuffer(_logicalDevice, in framebufferInfo, null, out _framebuffers[i]) != Result.Success)
                    throw new Exception($"Failed to create framebuffer {i}.");
            }
        }
    }

    private void GenerateTerrainMesh(ReadOnlySpan<RCTTileSurfaceData> surfaces, int width, int height)
    {
        _vertices.Clear();
        _indices.Clear();
        _terrainSkirtSections = 0;
        _terrainSkirtTriangles = 0;
        _terrainSkirtFlatEdges = 0;
        const float tileSize = 3.0f;
        const float baseHeightScale = 0.75f;
        const float cornerHeightScale = 1.5f;
        var perimeterSkirtFloor = TerrainSkirtFloor;

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++) {
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
            var materialId = GetTerrainMaterialId(surface.TerrainSurfaceIdHash);
            var baseIdx = (uint)_vertices.Count;
            _vertices.Add(new TerrainVertex(cNorth, normal1, new Vector2(x + 1, y + 1), materialId));
            _vertices.Add(new TerrainVertex(cEast, normal1, new Vector2(x + 1, y), materialId));
            _vertices.Add(new TerrainVertex(cWest, normal1, new Vector2(x, y + 1), materialId));
            _vertices.Add(new TerrainVertex(cSouth, normal2, new Vector2(x, y), materialId));
            _indices.Add(baseIdx);
            _indices.Add(baseIdx + 1);
            _indices.Add(baseIdx + 2);
            _indices.Add(baseIdx + 1);
            _indices.Add(baseIdx + 3);
            _indices.Add(baseIdx + 2);

            var eastEdgeNormal = Vector3.UnitX;
            if (x + 1 < width) {
                var eastNeighbor = surfaces[y * width + x + 1];
                var neighborBase = eastNeighbor.BaseZ * baseHeightScale;
                var neighborSouth = neighborBase + eastNeighbor.SouthCornerHeight * cornerHeightScale;
                var neighborWest = neighborBase + eastNeighbor.WestCornerHeight * cornerHeightScale;
                AddSkirtEdge(cEast, east, cNorth, north, neighborSouth, neighborWest,
                    materialId, GetTerrainMaterialId(eastNeighbor.TerrainSurfaceIdHash), eastEdgeNormal);
            }
            else {
                AddSkirtEdge(cEast, east, cNorth, north, perimeterSkirtFloor, perimeterSkirtFloor,
                    materialId, materialId, eastEdgeNormal);
            }

            var northEdgeNormal = Vector3.UnitZ;
            if (y + 1 < height) {
                var northNeighbor = surfaces[(y + 1) * width + x];
                var neighborBase = northNeighbor.BaseZ * baseHeightScale;
                var neighborSouth = neighborBase + northNeighbor.SouthCornerHeight * cornerHeightScale;
                var neighborEast = neighborBase + northNeighbor.EastCornerHeight * cornerHeightScale;
                AddSkirtEdge(cWest, west, cNorth, north, neighborSouth, neighborEast,
                    materialId, GetTerrainMaterialId(northNeighbor.TerrainSurfaceIdHash), northEdgeNormal);
            }
            else {
                AddSkirtEdge(cWest, west, cNorth, north, perimeterSkirtFloor, perimeterSkirtFloor,
                    materialId, materialId, northEdgeNormal);
            }

            if (x == 0)
                AddSkirtEdge(cSouth, south, cWest, west, perimeterSkirtFloor, perimeterSkirtFloor,
                    materialId, materialId, -Vector3.UnitX);
            if (y == 0)
                AddSkirtEdge(cSouth, south, cEast, east, perimeterSkirtFloor, perimeterSkirtFloor,
                    materialId, materialId, -Vector3.UnitZ);
        }
    }

    private void AddSkirtEdge(Vector3 edgeStart, float surfaceAStart, Vector3 edgeEnd, float surfaceAEnd,
        float surfaceBStart, float surfaceBEnd, uint materialA, uint materialB, Vector3 normalA)
    {
        var startDifference = surfaceAStart - surfaceBStart;
        var endDifference = surfaceAEnd - surfaceBEnd;
        if (MathF.Abs(startDifference) < 0.0001f && MathF.Abs(endDifference) < 0.0001f) {
            _terrainSkirtFlatEdges++;
            return;
        }

        if (startDifference * endDifference < 0.0f) {
            var crossing = startDifference / (startDifference - endDifference);
            AddSkirtSection(edgeStart, edgeEnd, surfaceAStart, surfaceAEnd, surfaceBStart, surfaceBEnd,
                0.0f, crossing, materialA, materialB, normalA);
            AddSkirtSection(edgeStart, edgeEnd, surfaceAStart, surfaceAEnd, surfaceBStart, surfaceBEnd,
                crossing, 1.0f, materialA, materialB, normalA);
        }
        else {
            AddSkirtSection(edgeStart, edgeEnd, surfaceAStart, surfaceAEnd, surfaceBStart, surfaceBEnd,
                0.0f, 1.0f, materialA, materialB, normalA);
        }
    }

    private void AddSkirtSection(Vector3 edgeStart, Vector3 edgeEnd, float surfaceAStart, float surfaceAEnd,
        float surfaceBStart, float surfaceBEnd, float startT, float endT, uint materialA, uint materialB, Vector3 normalA)
    {
        var startA = float.Lerp(surfaceAStart, surfaceAEnd, startT);
        var endA = float.Lerp(surfaceAStart, surfaceAEnd, endT);
        var startB = float.Lerp(surfaceBStart, surfaceBEnd, startT);
        var endB = float.Lerp(surfaceBStart, surfaceBEnd, endT);
        var aIsHigher = (startA + endA) >= (startB + endB);
        var startHigh = aIsHigher ? startA : startB;
        var endHigh = aIsHigher ? endA : endB;
        var startLow = aIsHigher ? startB : startA;
        var endLow = aIsHigher ? endB : endA;
        if (MathF.Abs(startHigh - startLow) < 0.0001f && MathF.Abs(endHigh - endLow) < 0.0001f)
            return;

        var materialId = aIsHigher ? materialA : materialB;
        var normal = aIsHigher ? normalA : -normalA;
        var startPosition = Vector3.Lerp(edgeStart, edgeEnd, startT);
        var endPosition = Vector3.Lerp(edgeStart, edgeEnd, endT);
        var topStart = new Vector3(startPosition.X, startHigh, startPosition.Z);
        var topEnd = new Vector3(endPosition.X, endHigh, endPosition.Z);
        var bottomStart = new Vector3(startPosition.X, startLow, startPosition.Z);
        var bottomEnd = new Vector3(endPosition.X, endLow, endPosition.Z);
        var edgeUStart = (MathF.Abs(edgeEnd.X - edgeStart.X) > 0.0001f ? startPosition.X : startPosition.Z) / 3.0f;
        var edgeUEnd = (MathF.Abs(edgeEnd.X - edgeStart.X) > 0.0001f ? endPosition.X : endPosition.Z) / 3.0f;
        var baseIndex = (uint)_vertices.Count;
        _terrainSkirtSections++;
        _vertices.Add(new TerrainVertex(topStart, normal, new Vector2(edgeUStart, startHigh / 3.0f), materialId));
        _vertices.Add(new TerrainVertex(bottomStart, normal, new Vector2(edgeUStart, startLow / 3.0f), materialId));
        _vertices.Add(new TerrainVertex(topEnd, normal, new Vector2(edgeUEnd, endHigh / 3.0f), materialId));
        _vertices.Add(new TerrainVertex(bottomEnd, normal, new Vector2(edgeUEnd, endLow / 3.0f), materialId));

        if (MathF.Abs(startHigh - startLow) < 0.0001f) {
            AddSkirtTriangle(baseIndex, baseIndex + 2, baseIndex + 3, normal);
        }
        else if (MathF.Abs(endHigh - endLow) < 0.0001f) {
            AddSkirtTriangle(baseIndex, baseIndex + 1, baseIndex + 2, normal);
        }
        else {
            AddSkirtTriangle(baseIndex, baseIndex + 1, baseIndex + 2, normal);
            AddSkirtTriangle(baseIndex + 2, baseIndex + 1, baseIndex + 3, normal);
        }
    }

    private void AddSkirtTriangle(uint indexA, uint indexB, uint indexC, Vector3 expectedNormal)
    {
        var positionA = _vertices[(int)indexA].Position;
        var positionB = _vertices[(int)indexB].Position;
        var positionC = _vertices[(int)indexC].Position;
        if (Vector3.Dot(Vector3.Cross(positionB - positionA, positionC - positionA), expectedNormal) < 0.0f)
            (indexB, indexC) = (indexC, indexB);

        _indices.Add(indexA);
        _indices.Add(indexB);
        _indices.Add(indexC);
        _terrainSkirtTriangles++;
    }

    private uint GetTerrainMaterialId(uint surfaceObjectIdHash)
    {
        return _terrainMaterials!.GetLayer(surfaceObjectIdHash);
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

        if (_vertices.Count > 0) {
            var v = _vertices[0];
            Console.WriteLine($"[Mesh] Uploaded vertices={_vertices.Count}, indices={_indices.Count}, firstPos=({v.Position.X:F2},{v.Position.Y:F2},{v.Position.Z:F2})");
        }
        Console.WriteLine($"[Mesh] Terrain skirt sections={_terrainSkirtSections}, triangles={_terrainSkirtTriangles}, flat edges skipped={_terrainSkirtFlatEdges}");
    }

    private unsafe void AllocateBuffer(ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties, out Buffer buffer, out DeviceMemory bufferMemory)
    {
        BufferCreateInfo bufferInfo = new() { SType = StructureType.BufferCreateInfo, Size = size, Usage = usage, SharingMode = SharingMode.Exclusive };

        if (_vk!.CreateBuffer(_logicalDevice, ref bufferInfo, null, out buffer) != Result.Success)
            throw new Exception("Failed to generate structural buffer layout.");

        _vk.GetBufferMemoryRequirements(_logicalDevice, buffer, out MemoryRequirements memRequirements);
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out PhysicalDeviceMemoryProperties memProperties);

        uint memoryTypeIndex = uint.MaxValue;
        for (uint i = 0; i < memProperties.MemoryTypeCount; i++) {
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
        fixed (CommandBuffer* cbPtr = _commandBuffers) {
            _vk.AllocateCommandBuffers(_logicalDevice, in allocInfo, cbPtr);
        }
    }

    private unsafe void CreateSyncPrimitives()
    {
        SemaphoreCreateInfo semaphoreInfo = new() { SType = StructureType.SemaphoreCreateInfo };
        FenceCreateInfo fenceInfo = new() { SType = StructureType.FenceCreateInfo, Flags = FenceCreateFlags.SignaledBit };

        var imageCount = _swapchainImages.Length;
        _imageAvailableSemaphores = new Semaphore[MaxFramesInFlight];
        _renderFinishedSemaphores = new Semaphore[imageCount];
        _inFlightFences = new Fence[MaxFramesInFlight];

        for (var i = 0; i < MaxFramesInFlight; i++)
            _vk!.CreateSemaphore(_logicalDevice, in semaphoreInfo, null, out _imageAvailableSemaphores[i]);

        for (var i = 0; i < imageCount; i++)
            _vk.CreateSemaphore(_logicalDevice, in semaphoreInfo, null, out _renderFinishedSemaphores[i]);

        for (uint i = 0; i < MaxFramesInFlight; i++) {
            _vk!.CreateFence(_logicalDevice, in fenceInfo, null, out _inFlightFences[i]);
        }

        _imagesInFlight = new Fence[imageCount];
        for (var i = 0; i < _imagesInFlight.Length; i++) _imagesInFlight[i] = new Fence();
    }

    private unsafe void DestroySyncPrimitives()
    {
        if (_vk is null || _logicalDevice.Handle == 0) return;

        foreach (var semaphore in _imageAvailableSemaphores) {
            if (semaphore.Handle != 0)
                _vk.DestroySemaphore(_logicalDevice, semaphore, null);
        }

        foreach (var semaphore in _renderFinishedSemaphores) {
            if (semaphore.Handle != 0)
                _vk.DestroySemaphore(_logicalDevice, semaphore, null);
        }

        foreach (var fence in _inFlightFences) {
            if (fence.Handle != 0)
                _vk.DestroyFence(_logicalDevice, fence, null);
        }

        _imageAvailableSemaphores = Array.Empty<Semaphore>();
        _renderFinishedSemaphores = Array.Empty<Semaphore>();
        _inFlightFences = Array.Empty<Fence>();
        _imagesInFlight = Array.Empty<Fence>();
    }

    private unsafe void DestroyFramebuffers()
    {
        foreach (var framebuffer in _framebuffers) {
            if (framebuffer.Handle != 0)
                _vk!.DestroyFramebuffer(_logicalDevice, framebuffer, null);
        }

        _framebuffers = Array.Empty<Framebuffer>();
    }

    private unsafe bool RecreateSwapchain()
    {
        var framebufferSize = _window!.FramebufferSize;
        if (framebufferSize.X == 0 || framebufferSize.Y == 0)
            return false;

        if (_vk!.DeviceWaitIdle(_logicalDevice) != Result.Success)
            throw new Exception("Failed to wait for the Vulkan device before recreating the swapchain.");

        var oldSwapchain = _swapchain;
        DestroySyncPrimitives();
        _vk.DestroyCommandPool(_logicalDevice, _commandPool, null);
        _commandPool = default;
        _commandBuffers = Array.Empty<CommandBuffer>();
        DestroyFramebuffers();
        DestroyDepthResources();
        foreach (var view in _swapchainImageViews)
            _vk.DestroyImageView(_logicalDevice, view, null);
        _swapchainImageViews = Array.Empty<ImageView>();

        CreateSwapchain(oldSwapchain);
        _khrSwapchain!.DestroySwapchain(_logicalDevice, oldSwapchain, null);
        CreateImageViews();
        CreateDepthResources();
        CreateFramebuffers();
        CreateCommandInfrastructure();
        CreateSyncPrimitives();
        _imguiRenderer?.ResizeBuffers(_swapchainImages.Length);
        _currentFrame = 0;
        _framebufferResized = false;
        return true;
    }

    private void SetupInputs()
    {
        _inputContext = _window!.CreateInput();

        foreach (var mouse in _inputContext.Mice) {
            mouse.MouseMove += (m, pos) => {
                if (m.IsButtonPressed(MouseButton.Right) && !(_imguiRenderer?.WantCaptureMouse ?? false)) {
                    _camera.HandleMouse(new Vector2(pos.X, pos.Y));
                    m.Cursor.CursorMode = CursorMode.Raw;
                }
                else {
                    m.Cursor.CursorMode = CursorMode.Normal;
                }
            };
        }
    }

    private void OnUpdateFrame(double deltaTime)
    {
        if (_exitRequested) {
            _exitRequested = false;
            _window?.Close();
            return;
        }

        if (_imguiRenderer is not null && _inputContext is not null) {
            var io = ImGui.GetIO();
            if (_inputContext.Mice.Count > 0) {
                var mouse = _inputContext.Mice[0];
                var position = mouse.Position;
                io.AddMousePosEvent(position.X, position.Y);
                io.AddMouseButtonEvent(0, mouse.IsButtonPressed(MouseButton.Left));
                io.AddMouseButtonEvent(1, mouse.IsButtonPressed(MouseButton.Right));
            }

            if (_inputContext.Keyboards.Count > 0) {
                var keyboard = _inputContext.Keyboards[0];
                io.AddKeyEvent(ImGuiKey.Tab, keyboard.IsKeyPressed(Key.Tab));
                io.AddKeyEvent(ImGuiKey.LeftArrow, keyboard.IsKeyPressed(Key.Left));
                io.AddKeyEvent(ImGuiKey.RightArrow, keyboard.IsKeyPressed(Key.Right));
                io.AddKeyEvent(ImGuiKey.UpArrow, keyboard.IsKeyPressed(Key.Up));
                io.AddKeyEvent(ImGuiKey.DownArrow, keyboard.IsKeyPressed(Key.Down));
                io.AddKeyEvent(ImGuiKey.Enter, keyboard.IsKeyPressed(Key.Enter));
                io.AddKeyEvent(ImGuiKey.Escape, keyboard.IsKeyPressed(Key.Escape));
                io.AddKeyEvent(ImGuiKey.LeftCtrl, keyboard.IsKeyPressed(Key.ControlLeft));
                io.AddKeyEvent(ImGuiKey.LeftShift, keyboard.IsKeyPressed(Key.ShiftLeft));
            }
        }

        if (_inputContext != null && _inputContext.Keyboards.Count > 0 && !(_imguiRenderer?.WantCaptureKeyboard ?? false))
            _camera.HandleKeyboard(_inputContext.Keyboards[0], (float)deltaTime);
    }

    private void BuildMenuBar(double deltaTime)
    {
        var instantFps = deltaTime > 0.0 ? (float)(1.0 / deltaTime) : 0.0f;
        var smoothing = 1.0f - MathF.Exp(-Math.Max((float)deltaTime, 0.0f) * 5.0f);
        _displayedFps = _displayedFps <= 0.0f ? instantFps : float.Lerp(_displayedFps, instantFps, smoothing);

        if (ImGui.BeginMainMenuBar()) {
            if (ImGui.BeginMenu("File")) {
                if (ImGui.MenuItem("Load"))
                    OpenParkPicker();
                if (ImGui.MenuItem("Exit"))
                    _exitRequested = true;
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("World")) {
                if (ImGui.MenuItem("Sky"))
                    _showSkyModal = true;
                ImGui.EndMenu();
            }

            // Open the modal from the main-menu-bar window, not the nested World
            // menu window. Popup IDs are scoped to the current ImGui window.
            if (_showSkyModal) {
                ImGui.OpenPopup("Sky settings");
                _showSkyModal = false;
            }

            ImGui.PushStyleColor(ImGuiCol.ModalWindowDimBg, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
            var skyModalVisible = ImGui.BeginPopupModal("Sky settings", ImGuiWindowFlags.AlwaysAutoResize);
            ImGui.PopStyleColor();
            if (skyModalVisible) {
                ImGui.SliderFloat("Time of day", ref _timeOfDayHours, 0.0f, 24.0f, "%.1f h");
                ImGui.SliderFloat("Sun angle", ref _sunAngleDegrees, 0.0f, 360.0f, "%.0f°");
                if (ImGui.Button("Close"))
                    ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }

            var fpsText = $"FPS: {_displayedFps:0}";
            ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(), ImGui.GetWindowWidth() - ImGui.CalcTextSize(fpsText).X - ImGui.GetStyle().WindowPadding.X));
            ImGui.TextUnformatted(fpsText);
            ImGui.EndMainMenuBar();
        }

        if (_showParkLoadError && ImGui.Begin("Park load error", ref _showParkLoadError, ImGuiWindowFlags.AlwaysAutoResize)) {
            ImGui.TextWrapped(_parkLoadError ?? "The park could not be loaded.");
            if (ImGui.Button("OK"))
                _showParkLoadError = false;
            ImGui.End();
        }
    }

    private void OpenParkPicker()
    {
        var defaultPath = _loadedParkPath is null ? string.Empty : Path.GetDirectoryName(_loadedParkPath) ?? string.Empty;
        try {
            var result = Dialog.FileOpen("park", defaultPath);
            if (result.IsError) {
                _parkLoadError = result.ErrorMessage;
                _showParkLoadError = true;
            }
            else if (result.IsOk) {
                LoadPark(result.Path);
            }
        }
        catch (Exception ex) {
            _parkLoadError = ex.Message;
            _showParkLoadError = true;
        }
    }

    private void LoadPark(string parkPath)
    {
        var simulation = _simulation ?? throw new InvalidOperationException("OpenRCT2 is not initialized.");
        _parkLoadError = null;
        var previousVertices = _vertices.ToArray();
        var previousIndices = _indices.ToArray();
        var previousSkirtSections = _terrainSkirtSections;
        var previousSkirtTriangles = _terrainSkirtTriangles;
        var previousFlatEdges = _terrainSkirtFlatEdges;

        try {
            simulation.LoadPark(parkPath);
            var (width, height) = simulation.GetDimensions();
            if (width <= 0 || height <= 0 || (long)width * height > int.MaxValue)
                throw new InvalidOperationException($"OpenRCT2 returned invalid map dimensions {width}x{height}.");

            var surfaceData = new RCTTileSurfaceData[checked(width * height)];
            simulation.FillSurfaceData(surfaceData);
            GenerateTerrainMesh(surfaceData, width, height);
            if (_vk!.DeviceWaitIdle(_logicalDevice) != Result.Success)
                throw new InvalidOperationException("Could not wait for the Vulkan device before replacing terrain buffers.");
            UploadMeshToGPU();

            _surfaceData = surfaceData;
            _mapWidth = width;
            _mapHeight = height;
            _loadedParkPath = parkPath;
            //_parkPath = parkPath;
            _camera.Position = new Vector3(width * 1.5f, 120.0f, -height * 1.5f);
            _parkLoadError = null;
            _showParkLoadError = false;
        }
        catch (Exception ex) {
            if (_loadedParkPath is not null) {
                try {
                    simulation.LoadPark(_loadedParkPath);
                }
                catch (Exception restoreException) {
                    _parkLoadError = $"Failed to load '{parkPath}': {ex.Message} The previous park could not be restored: {restoreException.Message}";
                }
            }

            _vertices.Clear();
            _vertices.AddRange(previousVertices);
            _indices.Clear();
            _indices.AddRange(previousIndices);
            _terrainSkirtSections = previousSkirtSections;
            _terrainSkirtTriangles = previousSkirtTriangles;
            _terrainSkirtFlatEdges = previousFlatEdges;
            _parkLoadError ??= $"Failed to load '{parkPath}': {ex.Message}";
            _showParkLoadError = true;
        }
    }

    private unsafe void OnRenderFrame(double deltaTime)
    {
        if (_window!.FramebufferSize.X == 0 || _window.FramebufferSize.Y == 0) return;
        if (_framebufferResized && !RecreateSwapchain()) return;

        uint imageIndex = 0;
        _vk!.WaitForFences(_logicalDevice, 1, in _inFlightFences[_currentFrame], true, ulong.MaxValue);
        var acquireResult = _khrSwapchain!.AcquireNextImage(_logicalDevice, _swapchain, ulong.MaxValue, _imageAvailableSemaphores[_currentFrame], default, ref imageIndex);
        if (acquireResult == Result.ErrorOutOfDateKhr) {
            RecreateSwapchain();
            return;
        }

        if (acquireResult != Result.Success && acquireResult != Result.SuboptimalKhr)
            throw new Exception($"Failed to acquire a swapchain image: {acquireResult}.");

        var recreateAfterPresent = acquireResult == Result.SuboptimalKhr;

        if (_imagesInFlight[imageIndex].Handle != 0) {
            _vk.WaitForFences(_logicalDevice, 1, in _imagesInFlight[imageIndex], true, ulong.MaxValue);
        }

        _imagesInFlight[imageIndex] = _inFlightFences[_currentFrame];
        var cmd = _commandBuffers[imageIndex];
        _imguiRenderer?.BeginFrame((int)_swapchainExtent.Width, (int)_swapchainExtent.Height, (float)deltaTime);
        BuildMenuBar(deltaTime);
        _vk.ResetCommandBuffer(cmd, 0);
        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };
        _vk.BeginCommandBuffer(cmd, in beginInfo);

        ClearValue* clearValues = stackalloc ClearValue[2];
        clearValues[0] = new ClearValue { Color = new ClearColorValue(0.4f, 0.6f, 0.9f, 1.0f) };
        clearValues[1] = new ClearValue { DepthStencil = new ClearDepthStencilValue { Depth = 1.0f, Stencil = 0 } };

        RenderPassBeginInfo renderPassInfo = new() {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _renderPass,
            Framebuffer = _framebuffers[imageIndex],
            RenderArea = new Rect2D(new Offset2D(0, 0), _swapchainExtent),
            ClearValueCount = 2,
            PClearValues = clearValues
        };

        _vk.CmdBeginRenderPass(cmd, in renderPassInfo, SubpassContents.Inline);
        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _graphicsPipeline);
        Viewport viewport = new() {
            X = 0.0f,
            Y = 0.0f,
            Width = _swapchainExtent.Width,
            Height = _swapchainExtent.Height,
            MinDepth = 0.0f,
            MaxDepth = 1.0f
        };

        Rect2D scissor = new() { Offset = new Offset2D(0, 0), Extent = _swapchainExtent };
        _vk.CmdSetViewport(cmd, 0, 1, &viewport);
        _vk.CmdSetScissor(cmd, 0, 1, &scissor);
        _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _pipelineLayout, 0, 1, in _terrainDescriptorSet, 0, null);

        var aspect = (float)_swapchainExtent.Width / _swapchainExtent.Height;
        // Row-vector convention in shader: clip = position * viewProjection
        var viewProjection = _camera.GetViewMatrix() * _camera.GetProjectionMatrix(aspect);
        var timeAngle = (_timeOfDayHours - 6.0f) * (MathF.PI / 12.0f);
        var sunElevation = MathF.Sin(timeAngle);
        var horizontalLength = MathF.Cos(timeAngle);
        var sunAzimuth = _sunAngleDegrees * (MathF.PI / 180.0f);
        var sunDirection = Vector3.Normalize(new Vector3(MathF.Cos(sunAzimuth) * horizontalLength, sunElevation, MathF.Sin(sunAzimuth) * horizontalLength));
        var ambientLight = 0.15f + 0.15f * MathF.Max(sunElevation, 0.0f);
        UniformBufferObject ubo = new() {
            ViewProjection = viewProjection,
            SunDirection = new Vector4(sunDirection, 0.0f),
            Lighting = new Vector4(ambientLight, 0.85f, 0.0f, 0.0f)
        };
        _vk.CmdPushConstants(cmd, _pipelineLayout, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0, (uint)Marshal.SizeOf<UniformBufferObject>(), &ubo);

        ulong offset = 0;
        var vBuf = _vertexBuffer;
        _vk.CmdBindVertexBuffers(cmd, 0, 1, in vBuf, in offset);
        _vk.CmdBindIndexBuffer(cmd, _indexBuffer, 0, IndexType.Uint32);
        _vk.CmdDrawIndexed(cmd, (uint)_indices.Count, 1, 0, 0, 0);
        _imguiRenderer?.Render(cmd, imageIndex);
        _vk.CmdEndRenderPass(cmd);
        _vk.EndCommandBuffer(cmd);

        var waitStages = PipelineStageFlags.ColorAttachmentOutputBit;
        var imgAnimSema = _imageAvailableSemaphores[_currentFrame];
        var rendFinSema = _renderFinishedSemaphores[imageIndex];

        SubmitInfo submitInfo = new() {
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
        PresentInfoKHR presentInfo = new() {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &rendFinSema,
            SwapchainCount = 1,
            PSwapchains = &swap,
            PImageIndices = &imageIndex
        };

        var presentResult = _khrSwapchain.QueuePresent(_graphicsQueue, in presentInfo);
        if (presentResult != Result.Success && presentResult != Result.SuboptimalKhr && presentResult != Result.ErrorOutOfDateKhr)
            throw new Exception($"Failed to present a swapchain image: {presentResult}.");

        _currentFrame = (uint)((_currentFrame + 1) % MaxFramesInFlight);
        if (recreateAfterPresent || _framebufferResized || presentResult == Result.SuboptimalKhr || presentResult == Result.ErrorOutOfDateKhr)
            RecreateSwapchain();
    }

    private unsafe void OnEngineShutdown()
    {
        if (_vk is not null && _logicalDevice.Handle != 0) {
            _vk.DeviceWaitIdle(_logicalDevice);
            _imguiRenderer?.Dispose();
            _imguiRenderer = null;
            DestroySyncPrimitives();

            if (_commandPool.Handle != 0)
                _vk.DestroyCommandPool(_logicalDevice, _commandPool, null);
            _commandPool = default;
            _commandBuffers = Array.Empty<CommandBuffer>();

            DestroyFramebuffers();
            DestroyDepthResources();

            if (_vertexBuffer.Handle != 0)
                _vk.DestroyBuffer(_logicalDevice, _vertexBuffer, null);
            if (_vertexBufferMemory.Handle != 0)
                _vk.FreeMemory(_logicalDevice, _vertexBufferMemory, null);
            if (_indexBuffer.Handle != 0)
                _vk.DestroyBuffer(_logicalDevice, _indexBuffer, null);
            if (_indexBufferMemory.Handle != 0)
                _vk.FreeMemory(_logicalDevice, _indexBufferMemory, null);
            if (_graphicsPipeline.Handle != 0)
                _vk.DestroyPipeline(_logicalDevice, _graphicsPipeline, null);

            if (_pipelineLayout.Handle != 0)
                _vk.DestroyPipelineLayout(_logicalDevice, _pipelineLayout, null);
            DestroyTerrainTextureResources();
            if (_terrainDescriptorSetLayout.Handle != 0)
                _vk.DestroyDescriptorSetLayout(_logicalDevice, _terrainDescriptorSetLayout, null);
            if (_renderPass.Handle != 0)
                _vk.DestroyRenderPass(_logicalDevice, _renderPass, null);

            foreach (var view in _swapchainImageViews) {
                if (view.Handle != 0)
                    _vk.DestroyImageView(_logicalDevice, view, null);
            }
            _swapchainImageViews = Array.Empty<ImageView>();

            if (_swapchain.Handle != 0)
                _khrSwapchain!.DestroySwapchain(_logicalDevice, _swapchain, null);
            _vk.DestroyDevice(_logicalDevice, null);
            _logicalDevice = default;
        }

        if (_vk is not null && _surface.Handle != 0)
            _khrSurface!.DestroySurface(_instance, _surface, null);

        if (_vk is not null && _instance.Handle != 0)
            _vk.DestroyInstance(_instance, null);

        Dispose();
    }

    private unsafe void DestroyTerrainTextureResources()
    {
        if (_vk is null || _logicalDevice.Handle == 0)
            return;

        if (_terrainDescriptorPool.Handle != 0)
            _vk.DestroyDescriptorPool(_logicalDevice, _terrainDescriptorPool, null);
        if (_terrainTextureSampler.Handle != 0)
            _vk.DestroySampler(_logicalDevice, _terrainTextureSampler, null);
        if (_terrainTextureView.Handle != 0)
            _vk.DestroyImageView(_logicalDevice, _terrainTextureView, null);
        if (_terrainTextureImage.Handle != 0)
            _vk.DestroyImage(_logicalDevice, _terrainTextureImage, null);
        if (_terrainTextureMemory.Handle != 0)
            _vk.FreeMemory(_logicalDevice, _terrainTextureMemory, null);

        _terrainDescriptorPool = default;
        _terrainDescriptorSet = default;
        _terrainTextureSampler = default;
        _terrainTextureView = default;
        _terrainTextureImage = default;
        _terrainTextureMemory = default;
    }

    public void Dispose()
    {
        var simulation = _simulation;
        _simulation = null;
        simulation?.Dispose();
    }
}
