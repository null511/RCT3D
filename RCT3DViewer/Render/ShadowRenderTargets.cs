using Silk.NET.Vulkan;

namespace RCT3D.Render;

internal sealed unsafe class ShadowRenderTargets : IDisposable
{
    public const uint DefaultResolution = 2048;
    public const float DefaultRadius = 128.0f;

    private readonly VulkanDeviceContext _context;
    private Image _image;
    private DeviceMemory _memory;
    private bool _disposed;

    public uint Resolution { get; }
    public float Radius { get; private set; }
    public Format DepthFormat { get; }
    public Image Image => _image;
    public ImageView DepthView { get; private set; }
    public RenderPass RenderPass { get; private set; }
    public Framebuffer Framebuffer { get; private set; }
    public Extent2D Extent { get; }

    public ShadowRenderTargets(VulkanDeviceContext context, Format depthFormat, uint resolution = DefaultResolution, float radius = DefaultRadius)
    {
        if (resolution is not (512 or 1024 or 2048 or 4096))
            throw new ArgumentOutOfRangeException(nameof(resolution));
        if (!float.IsFinite(radius) || radius < 16.0f || radius > 512.0f)
            throw new ArgumentOutOfRangeException(nameof(radius));

        _context = context;
        DepthFormat = depthFormat;
        Resolution = resolution;
        Radius = radius;
        Extent = new Extent2D(resolution, resolution);

        try {
            CreateImage();
            CreateRenderPass();
            CreateFramebuffer();
        }
        catch {
            Dispose();
            throw;
        }
    }

    public void UpdateRadius(float radius)
    {
        if (!float.IsFinite(radius) || radius < 16.0f || radius > 512.0f)
            throw new ArgumentOutOfRangeException(nameof(radius));

        Radius = radius;
    }

    private void CreateImage()
    {
        var imageInfo = new ImageCreateInfo {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D(Resolution, Resolution, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Format = DepthFormat,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = ImageUsageFlags.DepthStencilAttachmentBit | ImageUsageFlags.SampledBit,
            Samples = SampleCountFlags.Count1Bit,
            SharingMode = SharingMode.Exclusive
        };

        Check(_context.Api.CreateImage(_context.LogicalDevice, in imageInfo, null, out _image), "shadow depth image");
        _context.Api.GetImageMemoryRequirements(_context.LogicalDevice, _image, out var requirements);
        var allocationInfo = new MemoryAllocateInfo {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = _context.FindMemoryType(requirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
        };

        Check(_context.Api.AllocateMemory(_context.LogicalDevice, in allocationInfo, null, out _memory), "shadow depth image memory");
        Check(_context.Api.BindImageMemory(_context.LogicalDevice, _image, _memory, 0), "shadow depth image memory binding");

        var viewInfo = new ImageViewCreateInfo {
            SType = StructureType.ImageViewCreateInfo,
            Image = _image,
            ViewType = ImageViewType.Type2D,
            Format = DepthFormat,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.DepthBit, 0, 1, 0, 1)
        };

        Check(_context.Api.CreateImageView(_context.LogicalDevice, in viewInfo, null, out var view), "shadow depth image view");
        DepthView = view;
    }

    private void CreateRenderPass()
    {
        var attachment = new AttachmentDescription {
            Format = DepthFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.DepthStencilAttachmentOptimal,
            FinalLayout = ImageLayout.DepthStencilReadOnlyOptimal
        };
        var depthReference = new AttachmentReference(0, ImageLayout.DepthStencilAttachmentOptimal);
        var dependencies = new[] {
            new SubpassDependency {
                SrcSubpass = Vk.SubpassExternal,
                DstSubpass = 0,
                SrcStageMask = PipelineStageFlags.ComputeShaderBit | PipelineStageFlags.FragmentShaderBit,
                DstStageMask = PipelineStageFlags.EarlyFragmentTestsBit | PipelineStageFlags.LateFragmentTestsBit,
                SrcAccessMask = AccessFlags.ShaderReadBit,
                DstAccessMask = AccessFlags.DepthStencilAttachmentWriteBit,
                DependencyFlags = DependencyFlags.ByRegionBit
            },
            new SubpassDependency {
                SrcSubpass = 0,
                DstSubpass = Vk.SubpassExternal,
                SrcStageMask = PipelineStageFlags.LateFragmentTestsBit,
                DstStageMask = PipelineStageFlags.ComputeShaderBit,
                SrcAccessMask = AccessFlags.DepthStencilAttachmentWriteBit,
                DstAccessMask = AccessFlags.ShaderReadBit,
                DependencyFlags = DependencyFlags.ByRegionBit
            }
        };

        fixed (SubpassDependency* dependencyPointer = dependencies) {
            var subpass = new SubpassDescription {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                PDepthStencilAttachment = &depthReference
            };
            var createInfo = new RenderPassCreateInfo {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = 1,
                PAttachments = &attachment,
                SubpassCount = 1,
                PSubpasses = &subpass,
                DependencyCount = (uint)dependencies.Length,
                PDependencies = dependencyPointer
            };

            Check(_context.Api.CreateRenderPass(_context.LogicalDevice, in createInfo, null, out var renderPass), "shadow render pass");
            RenderPass = renderPass;
        }
    }

    private void CreateFramebuffer()
    {
        var attachment = DepthView;
        var createInfo = new FramebufferCreateInfo {
            SType = StructureType.FramebufferCreateInfo,
            RenderPass = RenderPass,
            AttachmentCount = 1,
            PAttachments = &attachment,
            Width = Resolution,
            Height = Resolution,
            Layers = 1
        };

        Check(_context.Api.CreateFramebuffer(_context.LogicalDevice, in createInfo, null, out var framebuffer), "shadow framebuffer");
        Framebuffer = framebuffer;
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
        if (Framebuffer.Handle != 0) vk.DestroyFramebuffer(device, Framebuffer, null);
        if (RenderPass.Handle != 0) vk.DestroyRenderPass(device, RenderPass, null);
        if (DepthView.Handle != 0) vk.DestroyImageView(device, DepthView, null);
        if (_image.Handle != 0) vk.DestroyImage(device, _image, null);
        if (_memory.Handle != 0) vk.FreeMemory(device, _memory, null);

        Framebuffer = default;
        RenderPass = default;
        DepthView = default;
        _image = default;
        _memory = default;
    }
}
