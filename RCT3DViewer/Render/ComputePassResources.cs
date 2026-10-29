using System.Runtime.InteropServices;
using RCT3D.Render.Shaders;
using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace RCT3D.Render;

internal sealed unsafe class ComputePassResources : IDisposable
{
    private readonly VulkanDeviceContext context;
    private readonly DescriptorSetLayout _descriptorSetLayout;
    private readonly DescriptorSetLayout _uniformDescriptorSetLayout;

    private DescriptorPool descriptorPool;
    private Sampler _sampler;
    private Sampler _comparisonSampler;
    private DescriptorSet[] _descriptorSets = Array.Empty<DescriptorSet>();
    private bool isDisposed;
    private readonly bool _useComparisonSampler;

    public PipelineLayout PipelineLayout { get; private set; }
    public Pipeline Pipeline { get; private set; }


    public ComputePassResources(VulkanDeviceContext context, string shaderName, DescriptorSetLayout uniformDescriptorSetLayout, ImageView[][] sampledImageSets, ImageView[] storageImageViews, ImageLayout[][]? sampledImageLayouts = null, bool useComparisonSampler = false)
        : this(context, shaderName, uniformDescriptorSetLayout, sampledImageSets, storageImageViews.Select(view => new[] { view }).ToArray(), sampledImageLayouts, useComparisonSampler) {}

    public ComputePassResources(VulkanDeviceContext context, string shaderName, DescriptorSetLayout uniformDescriptorSetLayout, ImageView[][] sampledImageSets, ImageView[][] storageImageSets, ImageLayout[][]? sampledImageLayouts = null, bool useComparisonSampler = false)
    {
        this.context = context;
        _uniformDescriptorSetLayout = uniformDescriptorSetLayout;
        _useComparisonSampler = useComparisonSampler;

        if (sampledImageSets.Length == 0 || sampledImageSets.Length != storageImageSets.Length
            || sampledImageSets.Any(set => set.Length != sampledImageSets[0].Length))
            throw new ArgumentException("Compute descriptor sets require matching sampled-image bindings and storage-image sets.");

        if (storageImageSets.Any(set => set.Length == 0 || set.Length != storageImageSets[0].Length))
            throw new ArgumentException("Compute descriptor sets require the same nonzero number of storage images.");

        if (useComparisonSampler && sampledImageSets[0].Length == 0)
            throw new ArgumentException("A comparison sampler requires a sampled image.", nameof(useComparisonSampler));

        if (sampledImageLayouts is not null && (sampledImageLayouts.Length != sampledImageSets.Length
            || sampledImageLayouts.Any(set => set.Length != sampledImageSets[0].Length)))
            throw new ArgumentException("Compute sampled-image layouts must match the sampled-image descriptor sets.");

        try {
            _descriptorSetLayout = CreateDescriptorSetLayout(sampledImageSets[0].Length, storageImageSets[0].Length);
            CreateDescriptors(sampledImageSets, storageImageSets, sampledImageLayouts);
            CreatePipeline(shaderName);
        }
        catch {
            Dispose();
            throw;
        }
    }

    private DescriptorSetLayout CreateDescriptorSetLayout(int sampledImageCount, int storageImageCount)
    {
        var samplerBindingCount = sampledImageCount > 0 ? 1 + (_useComparisonSampler ? 1 : 0) : 0;
        var storageBindingStart = sampledImageCount + samplerBindingCount;
        var bindings = new DescriptorSetLayoutBinding[sampledImageCount + storageImageCount + samplerBindingCount];

        for (uint binding = 0; binding < sampledImageCount; binding++) {
            bindings[binding] = new DescriptorSetLayoutBinding {
                Binding = binding,
                DescriptorType = DescriptorType.SampledImage,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.ComputeBit
            };

        }

        if (samplerBindingCount > 0)
            bindings[sampledImageCount] = new DescriptorSetLayoutBinding {
                Binding = (uint)sampledImageCount,
                DescriptorType = DescriptorType.Sampler,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.ComputeBit
            };

        if (_useComparisonSampler)
            bindings[sampledImageCount + 1] = new DescriptorSetLayoutBinding {
                Binding = (uint)(sampledImageCount + 1),
                DescriptorType = DescriptorType.Sampler,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.ComputeBit
            };

        for (var imageIndex = 0; imageIndex < storageImageCount; imageIndex++)
            bindings[storageBindingStart + imageIndex] = new DescriptorSetLayoutBinding {
                Binding = (uint)(storageBindingStart + imageIndex),
                DescriptorType = DescriptorType.StorageImage,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.ComputeBit
            };

        fixed (DescriptorSetLayoutBinding* bindingPointer = bindings) {
            var createInfo = new DescriptorSetLayoutCreateInfo {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)bindings.Length,
                PBindings = bindingPointer
            };

            if (context.Api.CreateDescriptorSetLayout(context.LogicalDevice, in createInfo, null, out var layout) != Result.Success)
                throw new Exception("Failed to create compute-pass descriptor-set layout.");

            return layout;
        }
    }

    private void CreateDescriptors(ImageView[][] sampledImageSets, ImageView[][] storageImageSets, ImageLayout[][]? sampledImageLayouts)
    {
        var vk = context.Api;
        var device = context.LogicalDevice;
        var sampledImageCount = sampledImageSets[0].Length;
        var storageImageCount = storageImageSets[0].Length;
        var setCount = sampledImageSets.Length;
        var samplerBindingCount = sampledImageCount > 0 ? 1 + (_useComparisonSampler ? 1 : 0) : 0;

        if (sampledImageCount > 0) {
            var samplerInfo = new SamplerCreateInfo {
                SType = StructureType.SamplerCreateInfo,
                MagFilter = Filter.Linear,
                MinFilter = Filter.Linear,
                MipmapMode = SamplerMipmapMode.Nearest,
                AddressModeU = SamplerAddressMode.ClampToEdge,
                AddressModeV = SamplerAddressMode.ClampToEdge,
                AddressModeW = SamplerAddressMode.ClampToEdge,
                MaxAnisotropy = 1,
                MinLod = 0,
                MaxLod = 0,
                BorderColor = BorderColor.IntOpaqueBlack
            };

            if (vk.CreateSampler(device, in samplerInfo, null, out _sampler) != Result.Success)
                throw new Exception("Failed to create compute-pass sampler.");

            if (_useComparisonSampler) {
                var comparisonSamplerInfo = new SamplerCreateInfo {
                    SType = StructureType.SamplerCreateInfo,
                    MagFilter = Filter.Linear,
                    MinFilter = Filter.Linear,
                    MipmapMode = SamplerMipmapMode.Nearest,
                    AddressModeU = SamplerAddressMode.ClampToBorder,
                    AddressModeV = SamplerAddressMode.ClampToBorder,
                    AddressModeW = SamplerAddressMode.ClampToBorder,
                    MaxAnisotropy = 1,
                    CompareEnable = true,
                    CompareOp = CompareOp.LessOrEqual,
                    MinLod = 0,
                    MaxLod = 0,
                    BorderColor = BorderColor.FloatOpaqueWhite
                };

                if (vk.CreateSampler(device, in comparisonSamplerInfo, null, out _comparisonSampler) != Result.Success)
                    throw new Exception("Failed to create compute-pass comparison sampler.");
            }
        }

        var poolSizes = new List<DescriptorPoolSize>();
        if (sampledImageCount > 0) {
            poolSizes.Add(new DescriptorPoolSize { Type = DescriptorType.SampledImage, DescriptorCount = checked((uint)(sampledImageCount * setCount)) });
            poolSizes.Add(new DescriptorPoolSize { Type = DescriptorType.Sampler, DescriptorCount = checked((uint)(samplerBindingCount * setCount)) });
        }
        poolSizes.Add(new DescriptorPoolSize { Type = DescriptorType.StorageImage, DescriptorCount = checked((uint)(storageImageCount * setCount)) });

        var poolSizeArray = poolSizes.ToArray();
        fixed (DescriptorPoolSize* poolSizePointer = poolSizeArray) {
            var poolInfo = new DescriptorPoolCreateInfo {
                SType = StructureType.DescriptorPoolCreateInfo,
                MaxSets = (uint)setCount,
                PoolSizeCount = (uint)poolSizeArray.Length,
                PPoolSizes = poolSizePointer
            };

            if (vk.CreateDescriptorPool(device, in poolInfo, null, out descriptorPool) != Result.Success)
                throw new Exception("Failed to create compute-pass descriptor pool.");
        }

        var layouts = Enumerable.Repeat(_descriptorSetLayout, setCount).ToArray();
        _descriptorSets = new DescriptorSet[setCount];

        fixed (DescriptorSetLayout* layoutPointer = layouts)
        fixed (DescriptorSet* descriptorPointer = _descriptorSets) {
            var allocateInfo = new DescriptorSetAllocateInfo {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = descriptorPool,
                DescriptorSetCount = (uint)setCount,
                PSetLayouts = layoutPointer
            };

            if (vk.AllocateDescriptorSets(device, in allocateInfo, descriptorPointer) != Result.Success)
                throw new Exception("Failed to allocate compute-pass descriptor sets.");
        }

        for (var setIndex = 0; setIndex < setCount; setIndex++) {
            for (var imageIndex = 0; imageIndex < sampledImageCount; imageIndex++) {
                var sampledInfo = new DescriptorImageInfo {
                    ImageView = sampledImageSets[setIndex][imageIndex],
                    ImageLayout = sampledImageLayouts?[setIndex][imageIndex] ?? ImageLayout.ShaderReadOnlyOptimal
                };

                var sampledWrite = new WriteDescriptorSet {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = _descriptorSets[setIndex],
                    DstBinding = (uint)imageIndex,
                    DescriptorCount = 1,
                    DescriptorType = DescriptorType.SampledImage,
                    PImageInfo = &sampledInfo
                };

                vk.UpdateDescriptorSets(device, 1, &sampledWrite, 0, null);
            }

            if (sampledImageCount > 0) {
                var samplerDescriptor = new DescriptorImageInfo { Sampler = _sampler };

                var samplerWrite = new WriteDescriptorSet {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = _descriptorSets[setIndex],
                    DstBinding = (uint)sampledImageCount,
                    DescriptorCount = 1,
                    DescriptorType = DescriptorType.Sampler,
                    PImageInfo = &samplerDescriptor
                };

                vk.UpdateDescriptorSets(device, 1, &samplerWrite, 0, null);
            }

            if (_useComparisonSampler) {
                var comparisonSamplerDescriptor = new DescriptorImageInfo { Sampler = _comparisonSampler };
                var comparisonSamplerWrite = new WriteDescriptorSet {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = _descriptorSets[setIndex],
                    DstBinding = (uint)(sampledImageCount + 1),
                    DescriptorCount = 1,
                    DescriptorType = DescriptorType.Sampler,
                    PImageInfo = &comparisonSamplerDescriptor
                };

                vk.UpdateDescriptorSets(device, 1, &comparisonSamplerWrite, 0, null);
            }

            for (var imageIndex = 0; imageIndex < storageImageCount; imageIndex++) {
                var storageInfo = new DescriptorImageInfo {
                    ImageView = storageImageSets[setIndex][imageIndex],
                    ImageLayout = ImageLayout.General
                };

                var storageWrite = new WriteDescriptorSet {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = _descriptorSets[setIndex],
                    DstBinding = (uint)(sampledImageCount + samplerBindingCount + imageIndex),
                    DescriptorCount = 1,
                    DescriptorType = DescriptorType.StorageImage,
                    PImageInfo = &storageInfo
                };

                vk.UpdateDescriptorSets(device, 1, &storageWrite, 0, null);
            }
        }
    }

    private void CreatePipeline(string shaderName)
    {
        var vk = context.Api;
        var device = context.LogicalDevice;

        var shaderPath = ShaderFile.Get(shaderName, "comp");

        if (!File.Exists(shaderPath))
            throw new FileNotFoundException($"Compute shader binary is missing: {shaderPath}");

        ShaderModule shaderModule = default;
        var entryPoint = Marshal.StringToHGlobalAnsi("main");

        try {
            var code = File.ReadAllBytes(shaderPath);
            fixed (byte* codePointer = code) {
                var moduleInfo = new ShaderModuleCreateInfo {
                    SType = StructureType.ShaderModuleCreateInfo,
                    CodeSize = (nuint)code.Length,
                    PCode = (uint*)codePointer
                };
                if (vk.CreateShaderModule(device, in moduleInfo, null, out shaderModule) != Result.Success)
                    throw new Exception($"Failed to create {shaderName} compute shader module.");
            }

            var descriptorLayouts = new[] { _uniformDescriptorSetLayout, _descriptorSetLayout };

            fixed (DescriptorSetLayout* descriptorLayoutsPointer = descriptorLayouts) {
                var layoutInfo = new PipelineLayoutCreateInfo {
                    SType = StructureType.PipelineLayoutCreateInfo,
                    SetLayoutCount = (uint)descriptorLayouts.Length,
                    PSetLayouts = descriptorLayoutsPointer
                };

                if (vk.CreatePipelineLayout(device, in layoutInfo, null, out var pipelineLayout) != Result.Success)
                    throw new Exception($"Failed to create {shaderName} compute pipeline layout.");

                PipelineLayout = pipelineLayout;
            }

            var stage = new PipelineShaderStageCreateInfo {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.ComputeBit,
                Module = shaderModule,
                PName = (byte*)entryPoint
            };

            var pipelineInfo = new ComputePipelineCreateInfo {
                SType = StructureType.ComputePipelineCreateInfo,
                Stage = stage,
                Layout = PipelineLayout
            };

            if (vk.CreateComputePipelines(device, default, 1, in pipelineInfo, null, out var pipeline) != Result.Success)
                throw new Exception($"Failed to create {shaderName} compute pipeline.");

            Pipeline = pipeline;
        }
        finally {
            if (shaderModule.Handle != 0)
                vk.DestroyShaderModule(device, shaderModule, null);

            Marshal.FreeHGlobal(entryPoint);
        }
    }

    public void Dispatch(CommandBuffer commandBuffer, int descriptorSetIndex, DescriptorSet uniformDescriptorSet, Vector2D<uint> renderDimensions)
    {
        if ((uint)descriptorSetIndex >= _descriptorSets.Length)
            throw new ArgumentOutOfRangeException(nameof(descriptorSetIndex));

        var vk = context.Api;
        vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Compute, Pipeline);

        var descriptorSets = stackalloc DescriptorSet[2];
        descriptorSets[0] = uniformDescriptorSet;
        descriptorSets[1] = _descriptorSets[descriptorSetIndex];
        vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Compute, PipelineLayout, 0, 2, descriptorSets, 0, null);

        var groupsX = (renderDimensions.X + 15u) / 16u;
        var groupsY = (renderDimensions.Y + 15u) / 16u;
        vk.CmdDispatch(commandBuffer, groupsX, groupsY, 1);
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        var vk = context.Api;
        var device = context.LogicalDevice;

        if (Pipeline.Handle != 0) vk.DestroyPipeline(device, Pipeline, null);
        if (PipelineLayout.Handle != 0) vk.DestroyPipelineLayout(device, PipelineLayout, null);
        if (descriptorPool.Handle != 0) vk.DestroyDescriptorPool(device, descriptorPool, null);
        if (_sampler.Handle != 0) vk.DestroySampler(device, _sampler, null);
        if (_comparisonSampler.Handle != 0) vk.DestroySampler(device, _comparisonSampler, null);
        if (_descriptorSetLayout.Handle != 0) vk.DestroyDescriptorSetLayout(device, _descriptorSetLayout, null);

        Pipeline = default;
        PipelineLayout = default;
        descriptorPool = default;
        _sampler = default;
        _comparisonSampler = default;
        _descriptorSets = Array.Empty<DescriptorSet>();
    }
}
