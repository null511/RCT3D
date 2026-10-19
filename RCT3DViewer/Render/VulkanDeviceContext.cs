using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System.Runtime.InteropServices;

namespace RCT3DViewer.Render;

internal sealed unsafe class VulkanDeviceContext : IDisposable
{
    private bool _disposed;

    public Vk Api { get; }
    public Instance Instance { get; private set; }
    public KhrSurface? SurfaceExtension { get; private set; }
    public SurfaceKHR Surface { get; private set; }
    public PhysicalDevice PhysicalDevice { get; private set; }
    public Device LogicalDevice { get; private set; }
    public Queue GraphicsQueue { get; private set; }
    public uint GraphicsQueueFamilyIndex { get; private set; }
    public KhrSwapchain? SwapchainExtension { get; private set; }
    public bool SupportsWireframe { get; private set; }


    private VulkanDeviceContext(Vk vk)
    {
        Api = vk;
    }

    public static VulkanDeviceContext Create(IWindow window)
    {
        var context = new VulkanDeviceContext(Vk.GetApi());

        try {
            context.Initialize(window);
            return context;
        }
        catch {
            context.Dispose();
            throw;
        }
    }

    public uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        if (TryFindMemoryType(typeFilter, properties, out var memoryTypeIndex))
            return memoryTypeIndex;

        throw new InvalidOperationException($"No Vulkan memory type supports {properties}.");
    }

    public bool TryFindMemoryType(uint typeFilter, MemoryPropertyFlags properties, out uint memoryTypeIndex)
    {
        Api.GetPhysicalDeviceMemoryProperties(PhysicalDevice, out var memoryProperties);

        for (uint i = 0; i < memoryProperties.MemoryTypeCount; i++) {
            if ((typeFilter & (1 << (int)i)) != 0 &&
                (memoryProperties.MemoryTypes[(int)i].PropertyFlags & properties) == properties) {
                memoryTypeIndex = i;
                return true;
            }
        }

        memoryTypeIndex = uint.MaxValue;
        return false;
    }

    private void Initialize(IWindow window)
    {
        CreateInstance(window);
        CreateSurface(window);
        PickPhysicalDevice();
        CreateLogicalDevice();
    }

    private void CreateInstance(IWindow window)
    {
        var applicationName = Marshal.StringToHGlobalAnsi("RCT3DViewer");
        var engineName = Marshal.StringToHGlobalAnsi("Custom");

        try {
            ApplicationInfo appInfo = new() {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*)applicationName,
                ApplicationVersion = Vk.MakeVersion(1, 0),
                PEngineName = (byte*)engineName,
                EngineVersion = Vk.MakeVersion(1, 0),
                ApiVersion = Vk.Version12
            };

            var extensions = window.VkSurface!.GetRequiredExtensions(out var extCount);
            InstanceCreateInfo createInfo = new() {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = extCount,
                PpEnabledExtensionNames = extensions
            };

            if (Api.CreateInstance(in createInfo, null, out var instance) != Result.Success)
                throw new Exception("CRITICAL: Failed to spin up Native Vulkan Instance.");

            Instance = instance;
        }
        finally {
            Marshal.FreeHGlobal(applicationName);
            Marshal.FreeHGlobal(engineName);
        }
    }

    private void CreateSurface(IWindow window)
    {
        if (!Api.TryGetInstanceExtension<KhrSurface>(Instance, out var surfaceExtension))
            throw new NotSupportedException("Vulkan Window Surface extension is not available.");

        SurfaceExtension = surfaceExtension;
        var instanceHandle = new VkHandle(Instance.Handle);
        var rawHandle = window.VkSurface!.Create<AllocationCallbacks>(instanceHandle, null);
        Surface = new SurfaceKHR { Handle = rawHandle.Handle };
    }

    private void PickPhysicalDevice()
    {
        uint deviceCount = 0;
        Api.EnumeratePhysicalDevices(Instance, ref deviceCount, null);
        var devices = new PhysicalDevice[deviceCount];
        fixed (PhysicalDevice* devicesPtr = devices) {
            Api.EnumeratePhysicalDevices(Instance, ref deviceCount, devicesPtr);
        }

        PhysicalDevice = devices[0];
    }

    private void CreateLogicalDevice()
    {
        uint queueFamilyCount = 0;
        Api.GetPhysicalDeviceQueueFamilyProperties(PhysicalDevice, ref queueFamilyCount, null);
        var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies) {
            Api.GetPhysicalDeviceQueueFamilyProperties(PhysicalDevice, ref queueFamilyCount, queueFamiliesPtr);
        }

        for (uint i = 0; i < queueFamilyCount; i++) {
            if ((queueFamilies[i].QueueFlags & QueueFlags.GraphicsBit) != 0) {
                GraphicsQueueFamilyIndex = i;
                break;
            }
        }

        var queuePriority = 1.0f;
        DeviceQueueCreateInfo queueCreateInfo = new() {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = GraphicsQueueFamilyIndex,
            QueueCount = 1,
            PQueuePriorities = &queuePriority
        };

        var swapchainExtensionName = KhrSwapchain.ExtensionName;
        var swapchainExtensionPointer = Marshal.StringToHGlobalAnsi(swapchainExtensionName);

        try {
            var swapchainExtension = (byte*)swapchainExtensionPointer;
            Api.GetPhysicalDeviceFeatures(PhysicalDevice, out PhysicalDeviceFeatures supportedFeatures);
            SupportsWireframe = supportedFeatures.FillModeNonSolid;

            PhysicalDeviceFeatures deviceFeatures = new() {
                SamplerAnisotropy = supportedFeatures.SamplerAnisotropy,
                FillModeNonSolid = supportedFeatures.FillModeNonSolid,
            };

            DeviceCreateInfo deviceCreateInfo = new() {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queueCreateInfo,
                EnabledExtensionCount = 1,
                PpEnabledExtensionNames = &swapchainExtension,
                PEnabledFeatures = &deviceFeatures,
            };

            if (Api.CreateDevice(PhysicalDevice, in deviceCreateInfo, null, out var logicalDevice) != Result.Success)
                throw new Exception("CRITICAL: Failed to instantiate Vulkan Logical Device.");

            LogicalDevice = logicalDevice;
        }
        finally {
            Marshal.FreeHGlobal(swapchainExtensionPointer);
        }

        Api.GetDeviceQueue(LogicalDevice, GraphicsQueueFamilyIndex, 0, out var graphicsQueue);
        GraphicsQueue = graphicsQueue;
        if (!Api.TryGetDeviceExtension<KhrSwapchain>(Instance, LogicalDevice, out var swapchainExtensionApi))
            throw new NotSupportedException("Vulkan Swapchain extension not available on device.");

        SwapchainExtension = swapchainExtensionApi;
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (LogicalDevice.Handle != 0) {
            Api.DeviceWaitIdle(LogicalDevice);
            Api.DestroyDevice(LogicalDevice, null);
            LogicalDevice = default;
        }

        if (Instance.Handle != 0 && Surface.Handle != 0)
            SurfaceExtension?.DestroySurface(Instance, Surface, null);

        Surface = default;

        if (Instance.Handle != 0) {
            Api.DestroyInstance(Instance, null);
            Instance = default;
        }

        Api.Dispose();
        _disposed = true;
    }
}
