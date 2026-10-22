using Silk.NET.Vulkan;

namespace RCT3D.Render;

internal sealed unsafe class SkyRenderTargets : IDisposable
{
    private readonly VulkanDeviceContext context;
    private readonly List<(Image Image, DeviceMemory Memory, ImageView View)> _images = new();

    private bool isDisposed;

    public Image SkyTransmitImage { get; private set; }
    public ImageView SkyTransmitView { get; private set; }
    public Image SkyMultiscatterImage { get; private set; }
    public ImageView SkyMultiscatterView { get; private set; }
    public Image SkyViewImage { get; private set; }
    public ImageView SkyViewView { get; private set; }


    public SkyRenderTargets(VulkanDeviceContext context)
    {
        this.context = context;

        try {
            ValidateFormat();

            (SkyTransmitImage, _, SkyTransmitView) = CreateImage(256, 64);
            (SkyMultiscatterImage, _, SkyMultiscatterView) = CreateImage(32, 32);
            (SkyViewImage, _, SkyViewView) = CreateImage(256, 256);
        }
        catch {
            Dispose();
            throw;
        }
    }

    private void ValidateFormat()
    {
        context.Api.GetPhysicalDeviceFormatProperties(context.PhysicalDevice, Format.R16G16B16A16Sfloat, out var properties);
        var required = FormatFeatureFlags.SampledImageBit | FormatFeatureFlags.StorageImageBit;

        if ((properties.OptimalTilingFeatures & required) != required)
            throw new NotSupportedException("RGBA16F does not support sampled and storage images on the selected Vulkan device.");
    }

    private (Image Image, DeviceMemory Memory, ImageView View) CreateImage(uint width, uint height)
    {
        var imageInfo = new ImageCreateInfo {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D(width, height, 1),
            Format = Format.R16G16B16A16Sfloat,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = ImageUsageFlags.SampledBit | ImageUsageFlags.StorageBit,
            Samples = SampleCountFlags.Count1Bit,
            SharingMode = SharingMode.Exclusive,
            ArrayLayers = 1,
            MipLevels = 1,
        };

        if (context.Api.CreateImage(context.LogicalDevice, in imageInfo, null, out var image) != Result.Success)
            throw new Exception($"Failed to create sky image ({width}x{height}).");

        var memory = default(DeviceMemory);
        var view = default(ImageView);

        try {
            context.Api.GetImageMemoryRequirements(context.LogicalDevice, image, out var requirements);

            var allocationInfo = new MemoryAllocateInfo {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = requirements.Size,
                MemoryTypeIndex = context.FindMemoryType(requirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
            };

            if (context.Api.AllocateMemory(context.LogicalDevice, in allocationInfo, null, out memory) != Result.Success)
                throw new Exception($"Failed to allocate sky image memory ({width}x{height}).");

            if (context.Api.BindImageMemory(context.LogicalDevice, image, memory, 0) != Result.Success)
                throw new Exception($"Failed to bind sky image memory ({width}x{height}).");

            var viewInfo = new ImageViewCreateInfo {
                SType = StructureType.ImageViewCreateInfo,
                Image = image,
                ViewType = ImageViewType.Type2D,
                Format = Format.R16G16B16A16Sfloat,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
            };

            if (context.Api.CreateImageView(context.LogicalDevice, in viewInfo, null, out view) != Result.Success)
                throw new Exception($"Failed to create sky image view ({width}x{height}).");

            var resource = (image, memory, view);
            _images.Add(resource);
            return resource;
        }
        catch {
            if (view.Handle != 0)
                context.Api.DestroyImageView(context.LogicalDevice, view, null);

            if (image.Handle != 0)
                context.Api.DestroyImage(context.LogicalDevice, image, null);

            if (memory.Handle != 0)
                context.Api.FreeMemory(context.LogicalDevice, memory, null);

            throw;
        }
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        foreach (var (image, memory, view) in _images) {
            if (view.Handle != 0)
                context.Api.DestroyImageView(context.LogicalDevice, view, null);

            if (image.Handle != 0)
                context.Api.DestroyImage(context.LogicalDevice, image, null);

            if (memory.Handle != 0)
                context.Api.FreeMemory(context.LogicalDevice, memory, null);
        }

        _images.Clear();
    }
}
