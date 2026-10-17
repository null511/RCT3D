using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace RCT3DViewer.Render;

internal sealed unsafe class SwapchainResources(VulkanDeviceContext context, IWindow window) : IDisposable
{
    private Image[] _depthImages = Array.Empty<Image>();
    private DeviceMemory[] _depthImageMemories = Array.Empty<DeviceMemory>();
    private ImageView[] _depthImageViews = Array.Empty<ImageView>();

    public SwapchainKHR Swapchain { get; private set; }
    public Image[] Images { get; private set; } = Array.Empty<Image>();
    public ImageView[] ImageViews { get; private set; } = Array.Empty<ImageView>();
    public Framebuffer[] Framebuffers { get; private set; } = Array.Empty<Framebuffer>();
    public Format Format { get; private set; }
    public Extent2D Extent { get; private set; }
    public Format DepthFormat { get; private set; }
    public RenderPass RenderPass { get; private set; }


    public void CreateSwapchain(SwapchainKHR oldSwapchain = default)
    {
        //var vk = context.Api;
        Format = Format.B8G8R8A8Unorm;

        if (context.SurfaceExtension!.GetPhysicalDeviceSurfaceCapabilities(context.PhysicalDevice, context.Surface, out var capabilities) != Result.Success)
            throw new Exception("Failed to query Vulkan surface capabilities.");

        if ((capabilities.SupportedUsageFlags & ImageUsageFlags.ColorAttachmentBit) == 0)
            throw new NotSupportedException("The Vulkan surface does not support color attachment swapchain images.");

        var framebufferSize = window.FramebufferSize;
        Extent = capabilities.CurrentExtent.Width != uint.MaxValue
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
            Surface = context.Surface,
            MinImageCount = requestedImageCount,
            ImageFormat = Format,
            ImageColorSpace = ColorSpaceKHR.PaceSrgbNonlinearKhr,
            ImageExtent = Extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            ImageSharingMode = SharingMode.Exclusive,
            PreTransform = capabilities.CurrentTransform,
            CompositeAlpha = compositeAlpha,
            PresentMode = PresentModeKHR.FifoKhr,
            Clipped = true,
            OldSwapchain = oldSwapchain
        };

        if (context.SwapchainExtension!.CreateSwapchain(context.LogicalDevice, in createInfo, null, out var newSwapchain) != Result.Success)
            throw new Exception("Failed to generate Vulkan Presentation Swapchain.");

        Swapchain = newSwapchain;

        uint imageCount = 0;
        context.SwapchainExtension.GetSwapchainImages(context.LogicalDevice, Swapchain, ref imageCount, null);
        Images = new Image[imageCount];

        fixed (Image* imagePointer = Images) {
            context.SwapchainExtension.GetSwapchainImages(context.LogicalDevice, Swapchain, ref imageCount, imagePointer);
        }
    }

    public void CreateImageViews()
    {
        var vk = context.Api;
        ImageViews = new ImageView[Images.Length];

        for (var i = 0; i < Images.Length; i++) {
            ImageViewCreateInfo createInfo = new() {
                SType = StructureType.ImageViewCreateInfo,
                Image = Images[i],
                ViewType = ImageViewType.Type2D,
                Format = Format,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
            };

            if (vk.CreateImageView(context.LogicalDevice, in createInfo, null, out ImageViews[i]) != Result.Success)
                throw new Exception($"Failed to generate View Target for Frame buffer index: {i}");
        }
    }

    public void CreateDepthResources()
    {
        DepthFormat = FindDepthFormat();

        _depthImages = new Image[Images.Length];
        _depthImageMemories = new DeviceMemory[Images.Length];
        _depthImageViews = new ImageView[Images.Length];

        try {
            for (var i = 0; i < _depthImages.Length; i++)
                CreateDepthResource(i);
        }
        catch {
            DestroyDepthResources();
            throw;
        }
    }

    public void CreateRenderPass()
    {
        var vk = context.Api;

        AttachmentDescription colorAttachment = new() {
            Format = Format,
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
            Format = DepthFormat,
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

        fixed (AttachmentDescription* attachmentsPointer = attachments) {
            RenderPassCreateInfo renderPassInfo = new() {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = (uint)attachments.Length,
                PAttachments = attachmentsPointer,
                SubpassCount = 1,
                PSubpasses = &subpass
            };

            if (vk.CreateRenderPass(context.LogicalDevice, in renderPassInfo, null, out var renderPass) != Result.Success)
                throw new Exception("Failed to configure Vulkan RenderPass structures.");

            RenderPass = renderPass;
        }
    }

    public void CreateFramebuffers()
    {
        var vk = context.Api;

        Framebuffers = new Framebuffer[ImageViews.Length];

        for (var i = 0; i < ImageViews.Length; i++) {
            var attachments = new[] {
                ImageViews[i],
                _depthImageViews[i],
            };

            fixed (ImageView* attachmentsPointer = attachments) {
                FramebufferCreateInfo framebufferInfo = new() {
                    SType = StructureType.FramebufferCreateInfo,
                    RenderPass = RenderPass,
                    AttachmentCount = (uint)attachments.Length,
                    PAttachments = attachmentsPointer,
                    Width = Extent.Width,
                    Height = Extent.Height,
                    Layers = 1
                };

                if (vk.CreateFramebuffer(context.LogicalDevice, in framebufferInfo, null, out Framebuffers[i]) != Result.Success)
                    throw new Exception($"Failed to create framebuffer {i}.");
            }
        }
    }

    public void Recreate()
    {
        //var vk = _context.Api;

        var oldSwapchain = Swapchain;
        DestroyFramebuffers();
        DestroyDepthResources();
        DestroyImageViews();

        CreateSwapchain(oldSwapchain);
        context.SwapchainExtension!.DestroySwapchain(context.LogicalDevice, oldSwapchain, null);
        CreateImageViews();
        CreateDepthResources();
        CreateFramebuffers();
    }

    private Format FindDepthFormat()
    {
        var candidates = new[] {
            Format.D32Sfloat,
            Format.D32SfloatS8Uint,
            Format.D24UnormS8Uint
        };

        foreach (var candidate in candidates) {
            context.Api.GetPhysicalDeviceFormatProperties(context.PhysicalDevice, candidate, out var properties);

            if ((properties.OptimalTilingFeatures & FormatFeatureFlags.DepthStencilAttachmentBit) != 0)
                return candidate;
        }

        throw new NotSupportedException("The selected Vulkan device has no supported depth attachment format.");
    }

    private void CreateDepthResource(int index)
    {
        var vk = context.Api;

        ImageCreateInfo imageInfo = new() {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D(Extent.Width, Extent.Height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Format = DepthFormat,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = ImageUsageFlags.DepthStencilAttachmentBit,
            Samples = SampleCountFlags.Count1Bit,
            SharingMode = SharingMode.Exclusive
        };

        if (vk.CreateImage(context.LogicalDevice, ref imageInfo, null, out _depthImages[index]) != Result.Success)
            throw new Exception("Failed to create a Vulkan depth image.");

        vk.GetImageMemoryRequirements(context.LogicalDevice, _depthImages[index], out var requirements);

        MemoryAllocateInfo allocationInfo = new() {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = context.FindMemoryType(requirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
        };

        if (vk.AllocateMemory(context.LogicalDevice, ref allocationInfo, null, out _depthImageMemories[index]) != Result.Success)
            throw new Exception("Failed to allocate Vulkan depth image memory.");

        if (vk.BindImageMemory(context.LogicalDevice, _depthImages[index], _depthImageMemories[index], 0) != Result.Success)
            throw new Exception("Failed to bind Vulkan depth image memory.");

        ImageViewCreateInfo viewInfo = new() {
            SType = StructureType.ImageViewCreateInfo,
            Image = _depthImages[index],
            ViewType = ImageViewType.Type2D,
            Format = DepthFormat,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.DepthBit, 0, 1, 0, 1)
        };

        if (vk.CreateImageView(context.LogicalDevice, in viewInfo, null, out _depthImageViews[index]) != Result.Success)
            throw new Exception("Failed to create a Vulkan depth image view.");
    }

    private void DestroyDepthResources()
    {
        var vk = context.Api;

        if (context.LogicalDevice.Handle == 0) return;

        for (var i = 0; i < _depthImages.Length; i++) {
            if (_depthImageViews[i].Handle != 0)
                vk.DestroyImageView(context.LogicalDevice, _depthImageViews[i], null);

            if (_depthImages[i].Handle != 0)
                vk.DestroyImage(context.LogicalDevice, _depthImages[i], null);

            if (_depthImageMemories[i].Handle != 0)
                vk.FreeMemory(context.LogicalDevice, _depthImageMemories[i], null);
        }

        _depthImages = Array.Empty<Image>();
        _depthImageMemories = Array.Empty<DeviceMemory>();
        _depthImageViews = Array.Empty<ImageView>();
    }

    private void DestroyFramebuffers()
    {
        foreach (var framebuffer in Framebuffers) {
            if (framebuffer.Handle != 0)
                context.Api.DestroyFramebuffer(context.LogicalDevice, framebuffer, null);
        }

        Framebuffers = Array.Empty<Framebuffer>();
    }

    private void DestroyImageViews()
    {
        foreach (var view in ImageViews) {
            if (view.Handle != 0)
                context.Api.DestroyImageView(context.LogicalDevice, view, null);
        }

        ImageViews = Array.Empty<ImageView>();
    }

    public void Dispose()
    {
        if (context.LogicalDevice.Handle == 0) return;

        DestroyFramebuffers();
        DestroyDepthResources();

        if (RenderPass.Handle != 0)
            context.Api.DestroyRenderPass(context.LogicalDevice, RenderPass, null);

        RenderPass = default;
        DestroyImageViews();

        if (Swapchain.Handle != 0)
            context.SwapchainExtension!.DestroySwapchain(context.LogicalDevice, Swapchain, null);

        Swapchain = default;
        Images = Array.Empty<Image>();
    }
}
