using Silk.NET.Vulkan;
using System.Numerics;
using System.Runtime.InteropServices;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace RCT3D.Render;

internal sealed unsafe class ModelTextureHeap : IDisposable
{
    private const uint TexturesPerMaterial = 3;
    private const uint FallbackTextureCount = 3;
    private readonly VulkanDeviceContext _context;
    private readonly List<TextureResource> _textures = new();
    private CommandPool _commandPool;
    private uint _nextTextureIndex;
    private uint _parkTextureStartIndex = FallbackTextureCount;
    private bool _initialized;
    private bool _disposed;

    public DescriptorSetLayout DescriptorSetLayout { get; private set; }
    public DescriptorSetLayout SamplerDescriptorSetLayout { get; private set; }
    public DescriptorPool DescriptorPool { get; private set; }
    public DescriptorPool SamplerDescriptorPool { get; private set; }
    public DescriptorSet DescriptorSet { get; private set; }
    public DescriptorSet SamplerDescriptorSet { get; private set; }
    public Sampler Sampler { get; private set; }

    public ModelTextureHeap(VulkanDeviceContext context, CommandPool commandPool)
    {
        _context = context;
        _commandPool = commandPool;
        try {
            CreateDescriptorResources();
            Initialize();
        }
        catch {
            Dispose();
            throw;
        }
    }

    public void SetCommandPool(CommandPool commandPool)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (commandPool.Handle == 0)
            throw new ArgumentException("A valid Vulkan command pool is required.", nameof(commandPool));

        _commandPool = commandPool;
    }

    private void Initialize()
    {
        if (_initialized)
            throw new InvalidOperationException("Model texture heap is already initialized.");

        var fallbackViews = new[] {
            CreateFallback(255, 255, 255, 255),
            CreateFallback(128, 128, 255, 255),
            CreateFallback(255, 0, 0, 0)
        };
        var imageInfos = new DescriptorImageInfo[VulkanDeviceContext.ModelTextureDescriptorCapacity];
        for (var index = 0; index < imageInfos.Length; index++)
            imageInfos[index] = new DescriptorImageInfo { ImageView = fallbackViews[index % fallbackViews.Length], ImageLayout = ImageLayout.ShaderReadOnlyOptimal };

        var imageWrite = new WriteDescriptorSet {
            SType = StructureType.WriteDescriptorSet,
            DstSet = DescriptorSet,
            DstBinding = 0,
            DescriptorCount = VulkanDeviceContext.ModelTextureDescriptorCapacity,
            DescriptorType = DescriptorType.SampledImage
        };
        fixed (DescriptorImageInfo* imagePointer = imageInfos) {
            imageWrite.PImageInfo = imagePointer;
            _context.Api.UpdateDescriptorSets(_context.LogicalDevice, 1, &imageWrite, 0, null);
        }
        _nextTextureIndex = FallbackTextureCount;
        _initialized = true;
    }

    public uint AddMaterial(ModelMaterialLibrary material)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
            throw new InvalidOperationException("Model texture heap has not been initialized.");
        if (_nextTextureIndex > VulkanDeviceContext.ModelTextureDescriptorCapacity - TexturesPerMaterial)
            throw new InvalidOperationException($"Model texture heap capacity of {VulkanDeviceContext.ModelTextureDescriptorCapacity} images was exceeded.");

        var baseIndex = _nextTextureIndex;
        var sources = new[] {
            (material.BaseColorPixels, Format.R8G8B8A8Srgb),
            (material.NormalPixels, Format.R8G8B8A8Unorm),
            (material.SpecularPixels, Format.R8G8B8A8Unorm)
        };

        var descriptors = new DescriptorImageInfo[TexturesPerMaterial];
        for (var map = 0; map < sources.Length; map++) {
            var (image, memory, view) = CreateTexture(sources[map].Item1, material.Width, material.Height, sources[map].Item2);
            _textures.Add(new TextureResource(image, memory, view));

            descriptors[map] = new DescriptorImageInfo {
                ImageView = view,
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            };
        }

        var writes = new WriteDescriptorSet[TexturesPerMaterial];
        for (uint map = 0; map < TexturesPerMaterial; map++) {
            writes[map] = new WriteDescriptorSet {
                SType = StructureType.WriteDescriptorSet,
                DstSet = DescriptorSet,
                DstBinding = 0,
                DstArrayElement = baseIndex + map,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.SampledImage
            };
        }

        fixed (WriteDescriptorSet* writePointer = writes)
        fixed (DescriptorImageInfo* descriptorPointer = descriptors) {
            for (var map = 0; map < writes.Length; map++)
                writes[map].PImageInfo = &descriptorPointer[map];

            _context.Api.UpdateDescriptorSets(_context.LogicalDevice, (uint)writes.Length, writePointer, 0, null);
        }

        _nextTextureIndex += TexturesPerMaterial;
        return baseIndex;
    }

    public void ResetParkTextures()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
            throw new InvalidOperationException("Model texture heap has not been initialized.");

        var vk = _context.Api;
        var device = _context.LogicalDevice;
        var fallbackViews = _textures.Take(checked((int)_parkTextureStartIndex))
            .Select(texture => new DescriptorImageInfo { ImageView = texture.View, ImageLayout = ImageLayout.ShaderReadOnlyOptimal })
            .ToArray();
        var descriptorInfos = new DescriptorImageInfo[VulkanDeviceContext.ModelTextureDescriptorCapacity];
        for (var index = 0; index < descriptorInfos.Length; index++)
            descriptorInfos[index] = index < fallbackViews.Length ? fallbackViews[index] : fallbackViews[index % FallbackTextureCount];
        var imageWrite = new WriteDescriptorSet {
            SType = StructureType.WriteDescriptorSet,
            DstSet = DescriptorSet,
            DstBinding = 0,
            DescriptorCount = VulkanDeviceContext.ModelTextureDescriptorCapacity,
            DescriptorType = DescriptorType.SampledImage
        };
        fixed (DescriptorImageInfo* descriptorPointer = descriptorInfos) {
            imageWrite.PImageInfo = descriptorPointer;
            vk.UpdateDescriptorSets(device, 1, &imageWrite, 0, null);
        }

        for (var index = _textures.Count - 1; index >= _parkTextureStartIndex; index--) {
            var texture = _textures[index];
            vk.DestroyImageView(device, texture.View, null);
            vk.DestroyImage(device, texture.Image, null);
            vk.FreeMemory(device, texture.Memory, null);
            _textures.RemoveAt(index);
        }

        _nextTextureIndex = _parkTextureStartIndex;
    }

    public void SetParkTextureStartIndex()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
            throw new InvalidOperationException("Model texture heap has not been initialized.");
        _parkTextureStartIndex = _nextTextureIndex;
    }

    private void CreateDescriptorResources()
    {
        var vk = _context.Api;
        var device = _context.LogicalDevice;
        var imageBinding = new DescriptorSetLayoutBinding {
            Binding = 0,
            DescriptorType = DescriptorType.SampledImage,
            DescriptorCount = VulkanDeviceContext.ModelTextureDescriptorCapacity,
            StageFlags = ShaderStageFlags.FragmentBit
        };
        var imageLayoutInfo = new DescriptorSetLayoutCreateInfo {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &imageBinding
        };
        Check(vk.CreateDescriptorSetLayout(device, in imageLayoutInfo, null, out var imageLayout), "model texture heap descriptor-set layout");
        DescriptorSetLayout = imageLayout;

        var samplerBinding = new DescriptorSetLayoutBinding {
            Binding = 0,
            DescriptorType = DescriptorType.Sampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit
        };
        var samplerLayoutInfo = new DescriptorSetLayoutCreateInfo {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &samplerBinding
        };
        Check(vk.CreateDescriptorSetLayout(device, in samplerLayoutInfo, null, out var samplerLayout), "model sampler descriptor-set layout");
        SamplerDescriptorSetLayout = samplerLayout;

        var imagePoolSize = new DescriptorPoolSize { Type = DescriptorType.SampledImage, DescriptorCount = VulkanDeviceContext.ModelTextureDescriptorCapacity };
        var imagePoolInfo = new DescriptorPoolCreateInfo {
            SType = StructureType.DescriptorPoolCreateInfo,
            MaxSets = 1,
            PoolSizeCount = 1,
            PPoolSizes = &imagePoolSize
        };
        Check(vk.CreateDescriptorPool(device, in imagePoolInfo, null, out var imagePool), "model texture heap descriptor pool");
        DescriptorPool = imagePool;
        var imageLayoutHandle = DescriptorSetLayout;
        var imageAllocateInfo = new DescriptorSetAllocateInfo {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = DescriptorPool,
            DescriptorSetCount = 1,
            PSetLayouts = &imageLayoutHandle
        };
        Check(vk.AllocateDescriptorSets(device, in imageAllocateInfo, out var imageSet), "model texture heap descriptor set");
        DescriptorSet = imageSet;

        var samplerPoolSize = new DescriptorPoolSize { Type = DescriptorType.Sampler, DescriptorCount = 1 };
        var samplerPoolInfo = new DescriptorPoolCreateInfo {
            SType = StructureType.DescriptorPoolCreateInfo,
            MaxSets = 1,
            PoolSizeCount = 1,
            PPoolSizes = &samplerPoolSize
        };
        Check(vk.CreateDescriptorPool(device, in samplerPoolInfo, null, out var samplerPool), "model sampler descriptor pool");
        SamplerDescriptorPool = samplerPool;
        var samplerLayoutHandle = SamplerDescriptorSetLayout;
        var samplerAllocateInfo = new DescriptorSetAllocateInfo {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = SamplerDescriptorPool,
            DescriptorSetCount = 1,
            PSetLayouts = &samplerLayoutHandle
        };
        Check(vk.AllocateDescriptorSets(device, in samplerAllocateInfo, out var samplerSet), "model sampler descriptor set");
        SamplerDescriptorSet = samplerSet;

        var samplerInfo = new SamplerCreateInfo {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Nearest,
            MinFilter = Filter.Nearest,
            MipmapMode = SamplerMipmapMode.Nearest,
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            MaxAnisotropy = 1.0f,
            MinLod = 0.0f,
            MaxLod = 16.0f,
            BorderColor = BorderColor.IntOpaqueBlack
        };
        Check(vk.CreateSampler(device, in samplerInfo, null, out var sampler), "model texture heap sampler");
        Sampler = sampler;
        var samplerInfoDescriptor = new DescriptorImageInfo { Sampler = Sampler };
        var samplerWrite = new WriteDescriptorSet {
            SType = StructureType.WriteDescriptorSet,
            DstSet = SamplerDescriptorSet,
            DstBinding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.Sampler,
            PImageInfo = &samplerInfoDescriptor
        };
        vk.UpdateDescriptorSets(device, 1, &samplerWrite, 0, null);
    }

    private ImageView CreateFallback(byte r, byte g, byte b, byte a)
    {
        var (image, memory, view) = CreateTexture([r, g, b, a], 1, 1, Format.R8G8B8A8Unorm);
        _textures.Add(new TextureResource(image, memory, view));
        return view;
    }

    private (Image Image, DeviceMemory Memory, ImageView View) CreateTexture(byte[] pixels, int width, int height, Format format)
    {
        if (width <= 0 || height <= 0 || pixels.Length != checked(width * height * 4))
            throw new InvalidDataException("Model texture dimensions do not match its RGBA pixel data.");

        var vk = _context.Api;
        var device = _context.LogicalDevice;
        var mipLevels = checked((uint)BitOperations.Log2((uint)Math.Max(width, height)) + 1);
        vk.GetPhysicalDeviceFormatProperties(_context.PhysicalDevice, format, out var formatProperties);
        var requiredFeatures = FormatFeatureFlags.SampledImageBit;
        if (mipLevels > 1)
            requiredFeatures |= FormatFeatureFlags.BlitSrcBit | FormatFeatureFlags.BlitDstBit | FormatFeatureFlags.SampledImageFilterLinearBit;
        if ((formatProperties.OptimalTilingFeatures & requiredFeatures) != requiredFeatures)
            throw new NotSupportedException($"The Vulkan device cannot sample and generate filtered mipmaps for model texture format {format}.");

        var byteCount = checked((ulong)pixels.Length);
        Buffer stagingBuffer = default;
        DeviceMemory stagingMemory = default;
        Image image = default;
        DeviceMemory memory = default;
        ImageView view = default;
        try {
            AllocateBuffer(byteCount, out stagingBuffer, out stagingMemory);
            void* mapped;
            Check(vk.MapMemory(device, stagingMemory, 0, byteCount, 0, &mapped), "map model texture staging buffer");
            Marshal.Copy(pixels, 0, (IntPtr)mapped, pixels.Length);
            vk.UnmapMemory(device, stagingMemory);

            var imageInfo = new ImageCreateInfo {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.Type2D,
                Format = format,
                Extent = new Extent3D((uint)width, (uint)height, 1),
                MipLevels = mipLevels,
                ArrayLayers = 1,
                Samples = SampleCountFlags.Count1Bit,
                Tiling = ImageTiling.Optimal,
                Usage = ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
                SharingMode = SharingMode.Exclusive,
                InitialLayout = ImageLayout.Undefined
            };
            Check(vk.CreateImage(device, in imageInfo, null, out image), "model texture image");
            vk.GetImageMemoryRequirements(device, image, out var requirements);
            var memoryInfo = new MemoryAllocateInfo {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = requirements.Size,
                MemoryTypeIndex = _context.FindMemoryType(requirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
            };
            Check(vk.AllocateMemory(device, in memoryInfo, null, out memory), "model texture image memory");
            Check(vk.BindImageMemory(device, image, memory, 0), "bind model texture image memory");
            UploadTexture(image, stagingBuffer, width, height, mipLevels);

            var viewInfo = new ImageViewCreateInfo {
                SType = StructureType.ImageViewCreateInfo,
                Image = image,
                ViewType = ImageViewType.Type2D,
                Format = format,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, mipLevels, 0, 1)
            };
            Check(vk.CreateImageView(device, in viewInfo, null, out view), "model texture image view");
            return (image, memory, view);
        }
        catch {
            if (view.Handle != 0) vk.DestroyImageView(device, view, null);
            if (image.Handle != 0) vk.DestroyImage(device, image, null);
            if (memory.Handle != 0) vk.FreeMemory(device, memory, null);
            throw;
        }
        finally {
            if (stagingBuffer.Handle != 0) vk.DestroyBuffer(device, stagingBuffer, null);
            if (stagingMemory.Handle != 0) vk.FreeMemory(device, stagingMemory, null);
        }
    }

    private void UploadTexture(Image image, Buffer stagingBuffer, int width, int height, uint mipLevels)
    {
        var vk = _context.Api;
        var device = _context.LogicalDevice;
        var allocationInfo = new CommandBufferAllocateInfo {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };
        Check(vk.AllocateCommandBuffers(device, in allocationInfo, out var commandBuffer), "model texture upload command buffer");
        try {
            var beginInfo = new CommandBufferBeginInfo {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit
            };
            Check(vk.BeginCommandBuffer(commandBuffer, in beginInfo), "begin model texture upload command buffer");
            var toTransfer = new ImageMemoryBarrier {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.TransferDstOptimal,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = image,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, mipLevels, 0, 1),
                DstAccessMask = AccessFlags.TransferWriteBit
            };
            vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit, 0, 0, null, 0, null, 1, &toTransfer);
            var copy = new BufferImageCopy {
                ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
                ImageExtent = new Extent3D((uint)width, (uint)height, 1)
            };
            vk.CmdCopyBufferToImage(commandBuffer, stagingBuffer, image, ImageLayout.TransferDstOptimal, 1, &copy);
            for (uint level = 1; level < mipLevels; level++) {
                var previous = level - 1;
                var sourceWidth = Math.Max(1, width >> (int)previous);
                var sourceHeight = Math.Max(1, height >> (int)previous);
                var destinationWidth = Math.Max(1, width >> (int)level);
                var destinationHeight = Math.Max(1, height >> (int)level);
                var sourceBarrier = new ImageMemoryBarrier {
                    SType = StructureType.ImageMemoryBarrier,
                    OldLayout = ImageLayout.TransferDstOptimal,
                    NewLayout = ImageLayout.TransferSrcOptimal,
                    SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                    DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                    Image = image,
                    SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, previous, 1, 0, 1),
                    SrcAccessMask = AccessFlags.TransferWriteBit,
                    DstAccessMask = AccessFlags.TransferReadBit
                };
                vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.TransferBit, 0, 0, null, 0, null, 1, &sourceBarrier);
                var blit = new ImageBlit {
                    SrcSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, previous, 0, 1),
                    DstSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, level, 0, 1)
                };
                blit.SrcOffsets[1] = new Offset3D(sourceWidth, sourceHeight, 1);
                blit.DstOffsets[1] = new Offset3D(destinationWidth, destinationHeight, 1);
                vk.CmdBlitImage(commandBuffer, image, ImageLayout.TransferSrcOptimal, image, ImageLayout.TransferDstOptimal, 1, &blit, Filter.Linear);
                var shaderBarrier = new ImageMemoryBarrier {
                    SType = StructureType.ImageMemoryBarrier,
                    OldLayout = ImageLayout.TransferSrcOptimal,
                    NewLayout = ImageLayout.ShaderReadOnlyOptimal,
                    SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                    DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                    Image = image,
                    SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, previous, 1, 0, 1),
                    SrcAccessMask = AccessFlags.TransferReadBit,
                    DstAccessMask = AccessFlags.ShaderReadBit
                };
                vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, 0, 0, null, 0, null, 1, &shaderBarrier);
            }
            var lastBarrier = new ImageMemoryBarrier {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.TransferDstOptimal,
                NewLayout = ImageLayout.ShaderReadOnlyOptimal,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = image,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, mipLevels - 1, 1, 0, 1),
                SrcAccessMask = AccessFlags.TransferWriteBit,
                DstAccessMask = AccessFlags.ShaderReadBit
            };
            vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, 0, 0, null, 0, null, 1, &lastBarrier);
            Check(vk.EndCommandBuffer(commandBuffer), "end model texture upload command buffer");
            var submit = new SubmitInfo { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &commandBuffer };
            Check(vk.QueueSubmit(_context.GraphicsQueue, 1, in submit, default), "submit model texture upload");
            Check(vk.QueueWaitIdle(_context.GraphicsQueue), "wait for model texture upload");
        }
        finally {
            vk.FreeCommandBuffers(device, _commandPool, 1, &commandBuffer);
        }
    }

    private void AllocateBuffer(ulong size, out Buffer buffer, out DeviceMemory memory)
    {
        var vk = _context.Api;
        var device = _context.LogicalDevice;
        var bufferInfo = new BufferCreateInfo { SType = StructureType.BufferCreateInfo, Size = size, Usage = BufferUsageFlags.TransferSrcBit, SharingMode = SharingMode.Exclusive };
        Check(vk.CreateBuffer(device, in bufferInfo, null, out buffer), "model texture staging buffer");
        vk.GetBufferMemoryRequirements(device, buffer, out var requirements);
        var allocationInfo = new MemoryAllocateInfo {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = _context.FindMemoryType(requirements.MemoryTypeBits, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit)
        };
        Check(vk.AllocateMemory(device, in allocationInfo, null, out memory), "model texture staging memory");
        Check(vk.BindBufferMemory(device, buffer, memory, 0), "bind model texture staging memory");
    }

    private static void Check(Result result, string operation)
    {
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to {operation}: {result}.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var vk = _context.Api;
        var device = _context.LogicalDevice;
        if (SamplerDescriptorPool.Handle != 0) vk.DestroyDescriptorPool(device, SamplerDescriptorPool, null);
        if (DescriptorPool.Handle != 0) vk.DestroyDescriptorPool(device, DescriptorPool, null);
        foreach (var texture in _textures) {
            if (texture.View.Handle != 0) vk.DestroyImageView(device, texture.View, null);
            if (texture.Image.Handle != 0) vk.DestroyImage(device, texture.Image, null);
            if (texture.Memory.Handle != 0) vk.FreeMemory(device, texture.Memory, null);
        }
        if (Sampler.Handle != 0) vk.DestroySampler(device, Sampler, null);
        if (SamplerDescriptorSetLayout.Handle != 0) vk.DestroyDescriptorSetLayout(device, SamplerDescriptorSetLayout, null);
        if (DescriptorSetLayout.Handle != 0) vk.DestroyDescriptorSetLayout(device, DescriptorSetLayout, null);
    }

    private readonly record struct TextureResource(Image Image, DeviceMemory Memory, ImageView View);
}
