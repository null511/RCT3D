using Silk.NET.Vulkan;

namespace RCT3D.Render;

internal sealed unsafe class DeferredRenderTargets : IDisposable
{
    private readonly VulkanDeviceContext _context;
    private readonly List<(Image Image, DeviceMemory Memory, ImageView View)> _colorImages = new();
    private Image _depthImage;
    private DeviceMemory _depthMemory;
    private ImageView _depthView;
    private bool _disposed;

    public Extent2D Extent { get; private set; }
    public Format DepthFormat { get; }
    public ImageView AlbedoView { get; private set; }
    public ImageView NormalsView { get; private set; }
    public ImageView SpecularView { get; private set; }
    public ImageView DepthView => _depthView;
    public Image SceneOutputImage { get; private set; }
    public ImageView SceneOutputView { get; private set; }
    public Image TaaHistoryImage { get; private set; }
    public ImageView TaaHistoryView { get; private set; }
    public Image TaaResolveImage { get; private set; }
    public ImageView TaaResolveView { get; private set; }
    public RenderPass GBufferRenderPass { get; private set; }
    public RenderPass WaterRenderPass { get; private set; }
    public Framebuffer GBufferFramebuffer { get; private set; }
    public Framebuffer WaterFramebuffer { get; private set; }
    public ShadowRenderTargets? ShadowTargets { get; private set; }

    public DeferredRenderTargets(VulkanDeviceContext context, Extent2D extent)
    {
        _context = context;
        Extent = extent;
        DepthFormat = FindDepthFormat();

        try {
            ValidateFormats();
            ShadowTargets = new ShadowRenderTargets(context, DepthFormat);
            CreateImages();
            CreateRenderPasses();
            CreateFramebuffers();
        }
        catch {
            Dispose();
            throw;
        }
    }

    private void ValidateFormats()
    {
        _context.Api.GetPhysicalDeviceProperties(_context.PhysicalDevice, out var properties);
        if (properties.Limits.MaxColorAttachments < 3)
            throw new NotSupportedException("The selected Vulkan device supports fewer than three simultaneous color attachments.");

        ValidateColorFormat(Format.R8G8B8A8Unorm);
        ValidateColorFormat(Format.R16G16B16A16Sfloat, requireStorage: true);

        _context.Api.GetPhysicalDeviceFormatProperties(_context.PhysicalDevice, DepthFormat, out var depthProperties);
        var requiredDepthFeatures = FormatFeatureFlags.DepthStencilAttachmentBit | FormatFeatureFlags.SampledImageBit
            | FormatFeatureFlags.SampledImageFilterLinearBit;
        if ((depthProperties.OptimalTilingFeatures & requiredDepthFeatures) != requiredDepthFeatures)
            throw new NotSupportedException($"Depth format {DepthFormat} cannot be used as a linearly sampled attachment.");
    }

    private void ValidateColorFormat(Format format, bool requireStorage = false)
    {
        _context.Api.GetPhysicalDeviceFormatProperties(_context.PhysicalDevice, format, out var properties);
        var required = FormatFeatureFlags.ColorAttachmentBit | FormatFeatureFlags.SampledImageBit;
        if (requireStorage)
            required |= FormatFeatureFlags.StorageImageBit;
        if ((properties.OptimalTilingFeatures & required) != required)
            throw new NotSupportedException($"Deferred format {format} does not support all required attachment, sampled-image, and storage-image usages.");
    }

    private Format FindDepthFormat()
    {
        foreach (var format in new[] { Format.D32Sfloat, Format.D32SfloatS8Uint, Format.D24UnormS8Uint }) {
            _context.Api.GetPhysicalDeviceFormatProperties(_context.PhysicalDevice, format, out var properties);
            var requiredFeatures = FormatFeatureFlags.DepthStencilAttachmentBit | FormatFeatureFlags.SampledImageBit
                | FormatFeatureFlags.SampledImageFilterLinearBit;
            if ((properties.OptimalTilingFeatures & requiredFeatures) == requiredFeatures)
                return format;
        }

        throw new NotSupportedException("The selected Vulkan device has no supported deferred depth attachment format.");
    }

    private void CreateImages()
    {
        var albedo = CreateColorImage(Format.R8G8B8A8Srgb);
        AlbedoView = albedo.View;

        var normals = CreateColorImage(Format.R16G16B16A16Sfloat);
        NormalsView = normals.View;

        var specular = CreateColorImage(Format.R8G8B8A8Unorm);
        SpecularView = specular.View;

        var scene = CreateColorImage(Format.R16G16B16A16Sfloat, storage: true);
        SceneOutputImage = scene.Image;
        SceneOutputView = scene.View;

        var history = CreateColorImage(Format.R16G16B16A16Sfloat, storage: true);
        TaaHistoryImage = history.Image;
        TaaHistoryView = history.View;

        var resolved = CreateColorImage(Format.R16G16B16A16Sfloat, storage: true);
        TaaResolveImage = resolved.Image;
        TaaResolveView = resolved.View;

        var imageInfo = new ImageCreateInfo {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D(Extent.Width, Extent.Height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Format = DepthFormat,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = ImageUsageFlags.DepthStencilAttachmentBit | ImageUsageFlags.SampledBit,
            Samples = SampleCountFlags.Count1Bit,
            SharingMode = SharingMode.Exclusive
        };

        if (_context.Api.CreateImage(_context.LogicalDevice, in imageInfo, null, out _depthImage) != Result.Success)
            throw new Exception("Failed to create deferred depth image.");

        _context.Api.GetImageMemoryRequirements(_context.LogicalDevice, _depthImage, out var requirements);
        var allocationInfo = new MemoryAllocateInfo {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = _context.FindMemoryType(requirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
        };

        if (_context.Api.AllocateMemory(_context.LogicalDevice, in allocationInfo, null, out _depthMemory) != Result.Success)
            throw new Exception("Failed to allocate deferred depth image memory.");

        if (_context.Api.BindImageMemory(_context.LogicalDevice, _depthImage, _depthMemory, 0) != Result.Success)
            throw new Exception("Failed to bind deferred depth image memory.");

        var depthViewInfo = new ImageViewCreateInfo {
            SType = StructureType.ImageViewCreateInfo,
            Image = _depthImage,
            ViewType = ImageViewType.Type2D,
            Format = DepthFormat,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.DepthBit, 0, 1, 0, 1)
        };

        if (_context.Api.CreateImageView(_context.LogicalDevice, in depthViewInfo, null, out _depthView) != Result.Success)
            throw new Exception("Failed to create deferred depth image view.");
    }

    private (Image Image, DeviceMemory Memory, ImageView View) CreateColorImage(Format format, bool storage = false)
    {
        var usage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit;
        if (storage) usage |= ImageUsageFlags.StorageBit;

        var imageInfo = new ImageCreateInfo {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D(Extent.Width, Extent.Height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Format = format,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = usage,
            Samples = SampleCountFlags.Count1Bit,
            SharingMode = SharingMode.Exclusive
        };

        if (_context.Api.CreateImage(_context.LogicalDevice, in imageInfo, null, out var image) != Result.Success)
            throw new Exception($"Failed to create deferred color image ({format}).");

        var memory = default(DeviceMemory);
        var view = default(ImageView);

        try {
            _context.Api.GetImageMemoryRequirements(_context.LogicalDevice, image, out var requirements);

            var allocationInfo = new MemoryAllocateInfo {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = requirements.Size,
                MemoryTypeIndex = _context.FindMemoryType(requirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
            };

            if (_context.Api.AllocateMemory(_context.LogicalDevice, in allocationInfo, null, out memory) != Result.Success)
                throw new Exception($"Failed to allocate deferred color image memory ({format}).");

            if (_context.Api.BindImageMemory(_context.LogicalDevice, image, memory, 0) != Result.Success)
                throw new Exception($"Failed to bind deferred color image memory ({format}).");

            var viewInfo = new ImageViewCreateInfo {
                SType = StructureType.ImageViewCreateInfo,
                Image = image,
                ViewType = ImageViewType.Type2D,
                Format = format,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
            };

            if (_context.Api.CreateImageView(_context.LogicalDevice, in viewInfo, null, out view) != Result.Success)
                throw new Exception($"Failed to create deferred color image view ({format}).");

            var resource = (image, memory, view);
            _colorImages.Add(resource);
            return resource;
        }
        catch {
            if (view.Handle != 0)
                _context.Api.DestroyImageView(_context.LogicalDevice, view, null);

            if (image.Handle != 0)
                _context.Api.DestroyImage(_context.LogicalDevice, image, null);

            if (memory.Handle != 0)
                _context.Api.FreeMemory(_context.LogicalDevice, memory, null);

            throw;
        }
    }

    private void CreateRenderPasses()
    {
        CreateGBufferRenderPass();
        CreateWaterRenderPass();
    }

    private void CreateGBufferRenderPass()
    {
        var attachments = new[] {
            ColorAttachment(Format.R8G8B8A8Unorm, ImageLayout.Undefined, ImageLayout.ShaderReadOnlyOptimal),
            ColorAttachment(Format.R16G16B16A16Sfloat, ImageLayout.Undefined, ImageLayout.ShaderReadOnlyOptimal),
            ColorAttachment(Format.R8G8B8A8Unorm, ImageLayout.Undefined, ImageLayout.ShaderReadOnlyOptimal),
            DepthAttachment(ImageLayout.Undefined, ImageLayout.DepthStencilReadOnlyOptimal, storeOp: AttachmentStoreOp.Store)
        };

        var colorReferences = new[] {
            new AttachmentReference(0, ImageLayout.ColorAttachmentOptimal),
            new AttachmentReference(1, ImageLayout.ColorAttachmentOptimal),
            new AttachmentReference(2, ImageLayout.ColorAttachmentOptimal)
        };

        var depthReference = new AttachmentReference(3, ImageLayout.DepthStencilAttachmentOptimal);

        var dependencies = new[] {
            new SubpassDependency {
                SrcSubpass = Vk.SubpassExternal,
                DstSubpass = 0,
                SrcStageMask = PipelineStageFlags.ComputeShaderBit | PipelineStageFlags.EarlyFragmentTestsBit | PipelineStageFlags.LateFragmentTestsBit,
                DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
                SrcAccessMask = AccessFlags.ShaderReadBit | AccessFlags.DepthStencilAttachmentReadBit | AccessFlags.DepthStencilAttachmentWriteBit,
                DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit,
                DependencyFlags = DependencyFlags.ByRegionBit
            },
            GBufferToShaderDependency()
        };

        fixed (AttachmentDescription* attachmentPointer = attachments)
        fixed (AttachmentReference* colorPointer = colorReferences)
        fixed (SubpassDependency* dependencyPointer = dependencies) {
            var subpass = new SubpassDescription {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = (uint)colorReferences.Length,
                PColorAttachments = colorPointer,
                PDepthStencilAttachment = &depthReference
            };

            var createInfo = new RenderPassCreateInfo {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = (uint)attachments.Length,
                PAttachments = attachmentPointer,
                SubpassCount = 1,
                PSubpasses = &subpass,
                DependencyCount = (uint)dependencies.Length,
                PDependencies = dependencyPointer
            };

            Check(_context.Api.CreateRenderPass(_context.LogicalDevice, in createInfo, null, out var renderPass), "G-buffer render pass");
            GBufferRenderPass = renderPass;
        }
    }

    private void CreateWaterRenderPass()
    {
        var attachments = new[] {
            ColorAttachment(Format.R16G16B16A16Sfloat, ImageLayout.General, ImageLayout.ShaderReadOnlyOptimal, AttachmentLoadOp.Load),
            DepthAttachment(ImageLayout.DepthStencilReadOnlyOptimal, ImageLayout.DepthStencilReadOnlyOptimal, AttachmentLoadOp.Load, AttachmentStoreOp.Store)
        };

        var colorReference = new AttachmentReference(0, ImageLayout.ColorAttachmentOptimal);
        var depthReference = new AttachmentReference(1, ImageLayout.DepthStencilReadOnlyOptimal);

        var dependencies = new[] {
            new SubpassDependency {
                SrcSubpass = Vk.SubpassExternal,
                DstSubpass = 0,
                SrcStageMask = PipelineStageFlags.ComputeShaderBit | PipelineStageFlags.EarlyFragmentTestsBit | PipelineStageFlags.LateFragmentTestsBit,
                DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
                SrcAccessMask = AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit | AccessFlags.DepthStencilAttachmentWriteBit,
                DstAccessMask = AccessFlags.ColorAttachmentReadBit | AccessFlags.ColorAttachmentWriteBit
                    | AccessFlags.DepthStencilAttachmentReadBit | AccessFlags.DepthStencilAttachmentWriteBit,
                DependencyFlags = DependencyFlags.ByRegionBit
            },
            new SubpassDependency {
                SrcSubpass = 0,
                DstSubpass = Vk.SubpassExternal,
                SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
                DstStageMask = PipelineStageFlags.ComputeShaderBit | PipelineStageFlags.FragmentShaderBit,
                SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
                DstAccessMask = AccessFlags.ShaderReadBit,
                DependencyFlags = DependencyFlags.ByRegionBit
            }
        };

        fixed (AttachmentDescription* attachmentPointer = attachments)
        fixed (SubpassDependency* dependencyPointer = dependencies) {
            var subpass = new SubpassDescription {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = 1,
                PColorAttachments = &colorReference,
                PDepthStencilAttachment = &depthReference
            };

            var createInfo = new RenderPassCreateInfo {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = (uint)attachments.Length,
                PAttachments = attachmentPointer,
                SubpassCount = 1,
                PSubpasses = &subpass,
                DependencyCount = (uint)dependencies.Length,
                PDependencies = dependencyPointer
            };

            Check(_context.Api.CreateRenderPass(_context.LogicalDevice, in createInfo, null, out var renderPass), "water render pass");
            WaterRenderPass = renderPass;
        }
    }

    private void CreateFramebuffers()
    {
        GBufferFramebuffer = CreateFramebuffer(GBufferRenderPass, new[] { AlbedoView, NormalsView, SpecularView, _depthView });
        WaterFramebuffer = CreateFramebuffer(WaterRenderPass, new[] { SceneOutputView, _depthView });
    }

    public void Resize(Extent2D extent)
    {
        if (Extent.Width == extent.Width && Extent.Height == extent.Height) return;

        DestroyFramebuffers();
        DestroyImages();
        Extent = extent;
        CreateImages();
        CreateFramebuffers();
    }

    private Framebuffer CreateFramebuffer(RenderPass renderPass, ImageView[] attachments)
    {
        fixed (ImageView* attachmentPointer = attachments) {
            var createInfo = new FramebufferCreateInfo {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = renderPass,
                AttachmentCount = (uint)attachments.Length,
                PAttachments = attachmentPointer,
                Width = Extent.Width,
                Height = Extent.Height,
                Layers = 1
            };

            Check(_context.Api.CreateFramebuffer(_context.LogicalDevice, in createInfo, null, out var framebuffer), "deferred framebuffer");
            return framebuffer;
        }
    }

    private static AttachmentDescription ColorAttachment(Format format, ImageLayout initialLayout, ImageLayout finalLayout, AttachmentLoadOp loadOp = AttachmentLoadOp.Clear) => new() {
        Format = format,
        Samples = SampleCountFlags.Count1Bit,
        LoadOp = loadOp,
        StoreOp = AttachmentStoreOp.Store,
        StencilLoadOp = AttachmentLoadOp.DontCare,
        StencilStoreOp = AttachmentStoreOp.DontCare,
        InitialLayout = initialLayout,
        FinalLayout = finalLayout
    };

    private AttachmentDescription DepthAttachment(ImageLayout initialLayout, ImageLayout finalLayout, AttachmentLoadOp loadOp = AttachmentLoadOp.Clear, AttachmentStoreOp storeOp = AttachmentStoreOp.DontCare) => new() {
        Format = DepthFormat,
        Samples = SampleCountFlags.Count1Bit,
        LoadOp = loadOp,
        StoreOp = storeOp,
        StencilLoadOp = AttachmentLoadOp.DontCare,
        StencilStoreOp = AttachmentStoreOp.DontCare,
        InitialLayout = initialLayout,
        FinalLayout = finalLayout
    };

    private static SubpassDependency GBufferToShaderDependency() => new() {
        SrcSubpass = 0,
        DstSubpass = Vk.SubpassExternal,
        SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.LateFragmentTestsBit,
        DstStageMask = PipelineStageFlags.ComputeShaderBit | PipelineStageFlags.FragmentShaderBit,
        SrcAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit,
        DstAccessMask = AccessFlags.ShaderReadBit,
        DependencyFlags = DependencyFlags.ByRegionBit
    };

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
        ShadowTargets?.Dispose();
        ShadowTargets = null;
        DestroyFramebuffers();

        if (WaterRenderPass.Handle != 0) vk.DestroyRenderPass(device, WaterRenderPass, null);
        if (GBufferRenderPass.Handle != 0) vk.DestroyRenderPass(device, GBufferRenderPass, null);
        WaterRenderPass = default;
        GBufferRenderPass = default;

        DestroyImages();
    }

    private void DestroyFramebuffers()
    {
        var vk = _context.Api;
        var device = _context.LogicalDevice;
        if (WaterFramebuffer.Handle != 0) vk.DestroyFramebuffer(device, WaterFramebuffer, null);
        if (GBufferFramebuffer.Handle != 0) vk.DestroyFramebuffer(device, GBufferFramebuffer, null);
        WaterFramebuffer = default;
        GBufferFramebuffer = default;
    }

    private void DestroyImages()
    {
        var vk = _context.Api;
        var device = _context.LogicalDevice;

        foreach (var (_, _, view) in _colorImages)
            if (view.Handle != 0) vk.DestroyImageView(device, view, null);

        foreach (var (image, memory, _) in _colorImages) {
            if (image.Handle != 0) vk.DestroyImage(device, image, null);
            if (memory.Handle != 0) vk.FreeMemory(device, memory, null);
        }

        _colorImages.Clear();
        if (_depthView.Handle != 0) vk.DestroyImageView(device, _depthView, null);
        if (_depthImage.Handle != 0) vk.DestroyImage(device, _depthImage, null);
        if (_depthMemory.Handle != 0) vk.FreeMemory(device, _depthMemory, null);

        _depthView = default;
        _depthImage = default;
        _depthMemory = default;
        AlbedoView = default;
        NormalsView = default;
        SpecularView = default;
        SceneOutputView = default;
        SceneOutputImage = default;
        TaaHistoryImage = default;
        TaaHistoryView = default;
        TaaResolveImage = default;
        TaaResolveView = default;
    }
}
