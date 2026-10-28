using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace RCT3D.Render.Buffers;

internal sealed unsafe class UniformBufferResources : IDisposable
{
    private readonly VulkanDeviceContext context;

    private readonly Buffer[] _cameraBuffers;
    private readonly DeviceMemory[] _cameraMemories;
    private readonly Buffer[] _temporalBuffers;
    private readonly DeviceMemory[] _temporalMemories;
    private readonly Buffer[] _sceneBuffers;
    private readonly DeviceMemory[] _sceneMemories;
    private readonly Buffer[] _shadowBuffers;
    private readonly DeviceMemory[] _shadowMemories;
    private DescriptorPool _descriptorPool;
    private DescriptorSet[] _descriptorSets;
    private bool _disposed;

    public DescriptorSetLayout DescriptorSetLayout { get; private set; }


    public UniformBufferResources(VulkanDeviceContext context, uint framesInFlight)
    {
        if (framesInFlight == 0) throw new ArgumentOutOfRangeException(nameof(framesInFlight));

        this.context = context;

        _cameraBuffers = new Buffer[framesInFlight];
        _cameraMemories = new DeviceMemory[framesInFlight];
        _temporalBuffers = new Buffer[framesInFlight];
        _temporalMemories = new DeviceMemory[framesInFlight];
        _sceneBuffers = new Buffer[framesInFlight];
        _sceneMemories = new DeviceMemory[framesInFlight];
        _shadowBuffers = new Buffer[framesInFlight];
        _shadowMemories = new DeviceMemory[framesInFlight];
        _descriptorSets = new DescriptorSet[framesInFlight];

        try {
            CreateDescriptorSetLayout();
            CreateBuffers();
            CreateDescriptors();
        }
        catch {
            Dispose();
            throw;
        }
    }

    public DescriptorSet GetDescriptorSet(uint frameIndex)
    {
        if (frameIndex >= _descriptorSets.Length)
            throw new ArgumentOutOfRangeException(nameof(frameIndex));

        return _descriptorSets[frameIndex];
    }

    public void Update(uint frameIndex, in CameraUniforms camera, in TemporalUniforms temporal, in SceneUniforms scene, in ShadowUniforms shadow)
    {
        if (frameIndex >= _descriptorSets.Length)
            throw new ArgumentOutOfRangeException(nameof(frameIndex));

        Write(_cameraMemories[frameIndex], camera);
        Write(_temporalMemories[frameIndex], temporal);
        Write(_sceneMemories[frameIndex], scene);
        Write(_shadowMemories[frameIndex], shadow);
    }

    private void CreateDescriptorSetLayout()
    {
        var bindings = new[] {
            new DescriptorSetLayoutBinding {
                Binding = 0,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit | ShaderStageFlags.ComputeBit
            },
            new DescriptorSetLayoutBinding {
                Binding = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit | ShaderStageFlags.ComputeBit
            },
            new DescriptorSetLayoutBinding {
                Binding = 2,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit | ShaderStageFlags.ComputeBit
            },
            new DescriptorSetLayoutBinding {
                Binding = 3,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit | ShaderStageFlags.ComputeBit
            }
        };

        fixed (DescriptorSetLayoutBinding* bindingsPointer = bindings) {
            var createInfo = new DescriptorSetLayoutCreateInfo {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)bindings.Length,
                PBindings = bindingsPointer
            };

            if (context.Api.CreateDescriptorSetLayout(context.LogicalDevice, in createInfo, null, out var layout) != Result.Success)
                throw new InvalidOperationException("Failed to create the shared uniform descriptor-set layout.");

            DescriptorSetLayout = layout;
        }
    }

    private void CreateBuffers()
    {
        for (var i = 0; i < _cameraBuffers.Length; i++) {
            CreateBuffer((ulong)Marshal.SizeOf<CameraUniforms>(), out _cameraBuffers[i], out _cameraMemories[i]);
            CreateBuffer((ulong)Marshal.SizeOf<TemporalUniforms>(), out _temporalBuffers[i], out _temporalMemories[i]);
            CreateBuffer((ulong)Marshal.SizeOf<SceneUniforms>(), out _sceneBuffers[i], out _sceneMemories[i]);
            CreateBuffer((ulong)Marshal.SizeOf<ShadowUniforms>(), out _shadowBuffers[i], out _shadowMemories[i]);
        }
    }

    private void CreateDescriptors()
    {
        var vk = context.Api;
        var device = context.LogicalDevice;
        var frameCount = _descriptorSets.Length;
        var poolSize = new DescriptorPoolSize {
            Type = DescriptorType.UniformBuffer,
            DescriptorCount = checked((uint)(frameCount * 4))
        };

        var poolInfo = new DescriptorPoolCreateInfo {
            SType = StructureType.DescriptorPoolCreateInfo,
            MaxSets = (uint)frameCount,
            PoolSizeCount = 1,
            PPoolSizes = &poolSize
        };

        if (vk.CreateDescriptorPool(device, in poolInfo, null, out _descriptorPool) != Result.Success)
            throw new InvalidOperationException("Failed to create the shared uniform descriptor pool.");

        var layouts = Enumerable.Repeat(DescriptorSetLayout, frameCount).ToArray();
        fixed (DescriptorSetLayout* layoutsPointer = layouts)
        fixed (DescriptorSet* setsPointer = _descriptorSets) {
            var allocateInfo = new DescriptorSetAllocateInfo {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _descriptorPool,
                DescriptorSetCount = (uint)frameCount,
                PSetLayouts = layoutsPointer
            };

            if (vk.AllocateDescriptorSets(device, in allocateInfo, setsPointer) != Result.Success)
                throw new InvalidOperationException("Failed to allocate shared uniform descriptor sets.");
        }

        for (var i = 0; i < frameCount; i++) {
            var bufferInfos = new[] {
                new DescriptorBufferInfo {
                    Buffer = _cameraBuffers[i],
                    Range = (ulong)Marshal.SizeOf<CameraUniforms>(),
                },
                new DescriptorBufferInfo {
                    Buffer = _temporalBuffers[i],
                    Range = (ulong)Marshal.SizeOf<TemporalUniforms>(),
                },
                new DescriptorBufferInfo {
                    Buffer = _sceneBuffers[i],
                    Range = (ulong)Marshal.SizeOf<SceneUniforms>(),
                },
                new DescriptorBufferInfo {
                    Buffer = _shadowBuffers[i],
                    Range = (ulong)Marshal.SizeOf<ShadowUniforms>(),
                },
            };

            var writes = new WriteDescriptorSet[bufferInfos.Length];

            for (uint binding = 0; binding < writes.Length; binding++) {
                writes[binding] = new WriteDescriptorSet {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = _descriptorSets[i],
                    DstBinding = binding,
                    DescriptorCount = 1,
                    DescriptorType = DescriptorType.UniformBuffer
                };
            }

            fixed (DescriptorBufferInfo* bufferInfosPointer = bufferInfos)
            fixed (WriteDescriptorSet* writesPointer = writes) {
                for (var binding = 0; binding < writes.Length; binding++)
                    writesPointer[binding].PBufferInfo = &bufferInfosPointer[binding];

                vk.UpdateDescriptorSets(device, (uint)writes.Length, writesPointer, 0, null);
            }
        }
    }

    private void CreateBuffer(ulong size, out Buffer buffer, out DeviceMemory memory)
    {
        var vk = context.Api;
        var device = context.LogicalDevice;
        buffer = default;
        memory = default;

        var createInfo = new BufferCreateInfo {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = BufferUsageFlags.UniformBufferBit,
            SharingMode = SharingMode.Exclusive
        };

        if (vk.CreateBuffer(device, in createInfo, null, out buffer) != Result.Success)
            throw new InvalidOperationException("Failed to create a uniform buffer.");

        try {
            vk.GetBufferMemoryRequirements(device, buffer, out var requirements);
            var allocationInfo = new MemoryAllocateInfo {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = requirements.Size,
                MemoryTypeIndex = context.FindMemoryType(requirements.MemoryTypeBits,
                    MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit)
            };

            if (vk.AllocateMemory(device, in allocationInfo, null, out memory) != Result.Success)
                throw new InvalidOperationException("Failed to allocate uniform-buffer memory.");

            if (vk.BindBufferMemory(device, buffer, memory, 0) != Result.Success)
                throw new InvalidOperationException("Failed to bind uniform-buffer memory.");
        }
        catch {
            if (memory.Handle != 0)
                vk.FreeMemory(device, memory, null);

            vk.DestroyBuffer(device, buffer, null);
            buffer = default;
            memory = default;
            throw;
        }
    }

    private void Write<T>(DeviceMemory memory, in T value) where T : unmanaged
    {
        var vk = context.Api;
        var size = (ulong)Marshal.SizeOf<T>();
        void* mapped = null;
        if (vk.MapMemory(context.LogicalDevice, memory, 0, size, 0, &mapped) != Result.Success)
            throw new InvalidOperationException("Failed to map uniform-buffer memory.");

        *(T*)mapped = value;
        vk.UnmapMemory(context.LogicalDevice, memory);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var vk = context.Api;
        var device = context.LogicalDevice;
        if (device.Handle == 0) return;

        if (_descriptorPool.Handle != 0)
            vk.DestroyDescriptorPool(device, _descriptorPool, null);

        for (var i = 0; i < _cameraBuffers.Length; i++) {
            DestroyBuffer(_cameraBuffers[i], _cameraMemories[i]);
            DestroyBuffer(_temporalBuffers[i], _temporalMemories[i]);
            DestroyBuffer(_sceneBuffers[i], _sceneMemories[i]);
            DestroyBuffer(_shadowBuffers[i], _shadowMemories[i]);
        }

        if (DescriptorSetLayout.Handle != 0)
            vk.DestroyDescriptorSetLayout(device, DescriptorSetLayout, null);

        _descriptorPool = default;
        DescriptorSetLayout = default;
        _descriptorSets = Array.Empty<DescriptorSet>();
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
}
