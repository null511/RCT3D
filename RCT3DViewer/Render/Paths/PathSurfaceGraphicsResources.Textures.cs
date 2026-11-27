using RCT3D.Render.Paths.Meshing;
using Silk.NET.Vulkan;
using System.Runtime.InteropServices;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace RCT3D.Render.Paths;

internal sealed unsafe partial class PathSurfaceGraphicsResources
{
    private void CreateTextures(PathSurfaceMaterials materials)
    {
        var vk = context.Api; var device = context.LogicalDevice;
        var bindings = stackalloc DescriptorSetLayoutBinding[4];
        for (uint i = 0; i < 3; i++)
            bindings[i] = new DescriptorSetLayoutBinding { Binding = i, DescriptorType = DescriptorType.SampledImage, DescriptorCount = DescriptorCapacity, StageFlags = ShaderStageFlags.FragmentBit };
        bindings[3] = new DescriptorSetLayoutBinding { Binding = 3, DescriptorType = DescriptorType.Sampler, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit };
        var layoutInfo = new DescriptorSetLayoutCreateInfo { SType = StructureType.DescriptorSetLayoutCreateInfo, BindingCount = 4, PBindings = bindings };
        Check(vk.CreateDescriptorSetLayout(device, in layoutInfo, null, out textureLayout), "create path descriptor layout");
        var sizes = stackalloc DescriptorPoolSize[2];
        sizes[0] = new DescriptorPoolSize { Type = DescriptorType.SampledImage, DescriptorCount = DescriptorCapacity * 3 };
        sizes[1] = new DescriptorPoolSize { Type = DescriptorType.Sampler, DescriptorCount = 1 };
        var poolInfo = new DescriptorPoolCreateInfo { SType = StructureType.DescriptorPoolCreateInfo, MaxSets = 1, PoolSizeCount = 2, PPoolSizes = sizes };
        Check(vk.CreateDescriptorPool(device, in poolInfo, null, out pool), "create path descriptor pool");
        var layout = textureLayout;
        var alloc = new DescriptorSetAllocateInfo { SType = StructureType.DescriptorSetAllocateInfo, DescriptorPool = pool, DescriptorSetCount = 1, PSetLayouts = &layout };
        Check(vk.AllocateDescriptorSets(device, in alloc, out var set), "allocate path descriptors");
        DescriptorSet = set;
        var samplerInfo = new SamplerCreateInfo { SType = StructureType.SamplerCreateInfo, MagFilter = Filter.Nearest, MinFilter = Filter.Nearest,
            MipmapMode = SamplerMipmapMode.Nearest, AddressModeU = SamplerAddressMode.ClampToEdge, AddressModeV = SamplerAddressMode.ClampToEdge,
            AddressModeW = SamplerAddressMode.ClampToEdge, MaxLod = 5, MaxAnisotropy = 1 };
        Check(vk.CreateSampler(device, in samplerInfo, null, out sampler), "create path sampler");
        for (var map = 0; map < 3; map++) {
            var infos = new DescriptorImageInfo[DescriptorCapacity];
            for (var slot = 0; slot < materials.Arrays.Count; slot++) {
                var array = materials.Arrays[slot];
                var pixels = map == 0 ? array.ColorPixels : map == 1 ? array.NormalPixels : array.SpecularPixels;
                var view = CreateImage(array, pixels, map == 0);
                infos[slot] = new DescriptorImageInfo { ImageView = view, ImageLayout = ImageLayout.ShaderReadOnlyOptimal };
            }
            for (var slot = materials.Arrays.Count; slot < DescriptorCapacity; slot++) infos[slot] = infos[0];
            fixed (DescriptorImageInfo* ptr = infos) {
                var write = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = DescriptorSet, DstBinding = (uint)map,
                    DescriptorCount = DescriptorCapacity, DescriptorType = DescriptorType.SampledImage, PImageInfo = ptr };
                vk.UpdateDescriptorSets(device, 1, &write, 0, null);
            }
        }
        var samplerDescriptor = new DescriptorImageInfo { Sampler = sampler };
        var samplerWrite = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = DescriptorSet, DstBinding = 3, DescriptorCount = 1,
            DescriptorType = DescriptorType.Sampler, PImageInfo = &samplerDescriptor };
        vk.UpdateDescriptorSets(device, 1, &samplerWrite, 0, null);
    }

    private ImageView CreateImage(PathSurfaceTextureArray array, byte[] source, bool color)
    {
        var vk = context.Api; var device = context.LogicalDevice;
        var (pixels, regions) = PathSurfaceMipmaps.Build(source, array.LayerCount, color);
        Buffer staging = default; DeviceMemory stagingMemory = default; Image image = default; DeviceMemory memory = default; ImageView view = default;
        try {
            AllocateBuffer((ulong)pixels.Length, BufferUsageFlags.TransferSrcBit, out staging, out stagingMemory);
            void* ptr;
            Check(vk.MapMemory(device, stagingMemory, 0, (ulong)pixels.Length, 0, &ptr), "map path texture staging");
            Marshal.Copy(pixels, 0, (nint)ptr, pixels.Length);
            vk.UnmapMemory(device, stagingMemory);
            var format = color ? Format.R8G8B8A8Srgb : Format.R8G8B8A8Unorm;
            var imageInfo = new ImageCreateInfo { SType = StructureType.ImageCreateInfo, ImageType = ImageType.Type2D, Format = format,
                Extent = new Extent3D(32, 32, 1), MipLevels = 6, ArrayLayers = (uint)array.LayerCount, Samples = SampleCountFlags.Count1Bit,
                Tiling = ImageTiling.Optimal, Usage = ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit, SharingMode = SharingMode.Exclusive };
            Check(vk.CreateImage(device, in imageInfo, null, out image), "create path image");
            vk.GetImageMemoryRequirements(device, image, out var req);
            var memoryInfo = new MemoryAllocateInfo { SType = StructureType.MemoryAllocateInfo, AllocationSize = req.Size,
                MemoryTypeIndex = context.FindMemoryType(req.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit) };
            Check(vk.AllocateMemory(device, in memoryInfo, null, out memory), "allocate path image");
            Check(vk.BindImageMemory(device, image, memory, 0), "bind path image");
            var cmdInfo = new CommandBufferAllocateInfo { SType = StructureType.CommandBufferAllocateInfo, CommandPool = commandPool, Level = CommandBufferLevel.Primary, CommandBufferCount = 1 };
            Check(vk.AllocateCommandBuffers(device, in cmdInfo, out var cmd), "allocate path upload commands");
            try {
                var begin = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
                Check(vk.BeginCommandBuffer(cmd, in begin), "begin path upload");
                var barrier = new ImageMemoryBarrier { SType = StructureType.ImageMemoryBarrier, OldLayout = ImageLayout.Undefined, NewLayout = ImageLayout.TransferDstOptimal,
                    SrcQueueFamilyIndex = Vk.QueueFamilyIgnored, DstQueueFamilyIndex = Vk.QueueFamilyIgnored, Image = image,
                    SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 6, 0, (uint)array.LayerCount), DstAccessMask = AccessFlags.TransferWriteBit };
                vk.CmdPipelineBarrier(cmd, PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit, 0, 0, null, 0, null, 1, &barrier);
                fixed (BufferImageCopy* copies = regions)
                    vk.CmdCopyBufferToImage(cmd, staging, image, ImageLayout.TransferDstOptimal, (uint)regions.Length, copies);
                barrier.OldLayout = ImageLayout.TransferDstOptimal; barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
                barrier.SrcAccessMask = AccessFlags.TransferWriteBit; barrier.DstAccessMask = AccessFlags.ShaderReadBit;
                vk.CmdPipelineBarrier(cmd, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, 0, 0, null, 0, null, 1, &barrier);
                Check(vk.EndCommandBuffer(cmd), "end path upload");
                var submit = new SubmitInfo { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &cmd };
                Check(vk.QueueSubmit(context.GraphicsQueue, 1, in submit, default), "submit path upload");
                Check(vk.QueueWaitIdle(context.GraphicsQueue), "wait for path upload");
            }
            finally { vk.FreeCommandBuffers(device, commandPool, 1, &cmd); }
            var viewInfo = new ImageViewCreateInfo { SType = StructureType.ImageViewCreateInfo, Image = image, Format = format, ViewType = ImageViewType.Type2DArray,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 6, 0, (uint)array.LayerCount) };
            Check(vk.CreateImageView(device, in viewInfo, null, out view), "create path image view");
            images.Add((image, memory, view));
            return view;
        }
        catch {
            if (view.Handle != 0) vk.DestroyImageView(device, view, null);
            if (image.Handle != 0) vk.DestroyImage(device, image, null);
            if (memory.Handle != 0) vk.FreeMemory(device, memory, null);
            throw;
        }
        finally {
            if (staging.Handle != 0) vk.DestroyBuffer(device, staging, null);
            if (stagingMemory.Handle != 0) vk.FreeMemory(device, stagingMemory, null);
        }
    }
}
