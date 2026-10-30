using System.Diagnostics;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace RCT3D.Render;

internal sealed unsafe class VulkanDeviceContext : IDisposable
{
    public const uint ModelTextureDescriptorCapacity = 256;
#if DEBUG
    private static readonly DebugUtilsMessengerCallbackFunctionEXT ValidationCallback = DebugCallback;
    private ExtDebugUtils? _debugUtils;
    private DebugUtilsMessengerEXT _debugMessenger;
#endif

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
    public bool SupportsModelTextureIndexing { get; private set; }


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
#if DEBUG
        var validationLayerName = Marshal.StringToHGlobalAnsi("VK_LAYER_KHRONOS_validation");
        var debugUtilsExtensionName = Marshal.StringToHGlobalAnsi(ExtDebugUtils.ExtensionName);
        var validationFeaturesExtensionName = Marshal.StringToHGlobalAnsi("VK_EXT_validation_features");
#endif

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
#if DEBUG
            var enabledExtensions = stackalloc byte*[(int)extCount + 2];
            for (var index = 0; index < extCount; index++)
                enabledExtensions[index] = extensions[index];
            enabledExtensions[extCount] = (byte*)debugUtilsExtensionName;
            enabledExtensions[extCount + 1] = (byte*)validationFeaturesExtensionName;

            var enabledLayers = stackalloc byte*[1];
            enabledLayers[0] = (byte*)validationLayerName;

            var synchronizationValidation = ValidationFeatureEnableEXT.SynchronizationValidationExt;
            ValidationFeaturesEXT validationFeatures = new() {
                SType = StructureType.ValidationFeaturesExt,
                EnabledValidationFeatureCount = 1,
                PEnabledValidationFeatures = &synchronizationValidation
            };
            var debugMessengerInfo = CreateDebugMessengerInfo();
            validationFeatures.PNext = &debugMessengerInfo;
#endif
            InstanceCreateInfo createInfo = new() {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
#if DEBUG
                PNext = &validationFeatures,
                EnabledLayerCount = 1,
                PpEnabledLayerNames = enabledLayers,
                EnabledExtensionCount = extCount + 2,
                PpEnabledExtensionNames = enabledExtensions
#else
                EnabledExtensionCount = extCount,
                PpEnabledExtensionNames = extensions
#endif
            };

            if (Api.CreateInstance(in createInfo, null, out var instance) != Result.Success)
                throw new Exception("CRITICAL: Failed to spin up Native Vulkan Instance.");

            Instance = instance;
#if DEBUG
            if (!Api.TryGetInstanceExtension<ExtDebugUtils>(Instance, out _debugUtils))
                throw new NotSupportedException("VK_EXT_debug_utils is required for Vulkan validation output.");

            var messengerInfo = CreateDebugMessengerInfo();
            if (_debugUtils.CreateDebugUtilsMessenger(Instance, in messengerInfo, null, out _debugMessenger) != Result.Success)
                throw new Exception("Failed to create the Vulkan validation debug messenger.");
#endif
        }
        finally {
            Marshal.FreeHGlobal(applicationName);
            Marshal.FreeHGlobal(engineName);
#if DEBUG
            Marshal.FreeHGlobal(validationLayerName);
            Marshal.FreeHGlobal(debugUtilsExtensionName);
            Marshal.FreeHGlobal(validationFeaturesExtensionName);
#endif
        }
    }

#if DEBUG
    private static DebugUtilsMessengerCreateInfoEXT CreateDebugMessengerInfo() => new() {
        SType = StructureType.DebugUtilsMessengerCreateInfoExt,
        MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.WarningBitExt | DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
        MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt,
        PfnUserCallback = ValidationCallback
    };

    private static uint DebugCallback(
        DebugUtilsMessageSeverityFlagsEXT severity,
        DebugUtilsMessageTypeFlagsEXT types,
        DebugUtilsMessengerCallbackDataEXT* callbackData,
        void* userData)
    {
        var message = Marshal.PtrToStringAnsi((nint)callbackData->PMessage) ?? "(validation message unavailable)";
        Debug.WriteLine($"[Vulkan {severity} {types}] {message}");
        return 0;
    }
#endif

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

        foreach (var device in devices) {
            Api.GetPhysicalDeviceFeatures(device, out var features);
            Api.GetPhysicalDeviceProperties(device, out var properties);
            if (features.ShaderSampledImageArrayDynamicIndexing &&
                properties.Limits.MaxPerStageDescriptorSampledImages >= ModelTextureDescriptorCapacity &&
                properties.Limits.MaxDescriptorSetSampledImages >= ModelTextureDescriptorCapacity) {
                PhysicalDevice = device;
                SupportsModelTextureIndexing = true;
                return;
            }
        }

        throw new NotSupportedException($"No Vulkan physical device supports dynamically indexed sampled-image arrays of {ModelTextureDescriptorCapacity} descriptors.");
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

        if (GraphicsQueueFamilyIndex >= queueFamilyCount || (queueFamilies[GraphicsQueueFamilyIndex].QueueFlags & QueueFlags.GraphicsBit) == 0)
            throw new NotSupportedException("No Vulkan graphics-capable queue family is available.");

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

            PhysicalDeviceVulkan11Features supportedVulkan11Features = new() {
                SType = StructureType.PhysicalDeviceVulkan11Features
            };

            PhysicalDeviceFeatures2 supportedFeatures2 = new() {
                SType = StructureType.PhysicalDeviceFeatures2,
                PNext = &supportedVulkan11Features
            };

            Api.GetPhysicalDeviceFeatures2(PhysicalDevice, &supportedFeatures2);
            if (!supportedVulkan11Features.ShaderDrawParameters)
                throw new NotSupportedException("The tonemap shader requires Vulkan 1.1 shaderDrawParameters support.");

            PhysicalDeviceFeatures deviceFeatures = new() {
                SamplerAnisotropy = supportedFeatures.SamplerAnisotropy,
                FillModeNonSolid = supportedFeatures.FillModeNonSolid,
                ShaderStorageImageExtendedFormats = supportedFeatures.ShaderStorageImageExtendedFormats,
                ShaderSampledImageArrayDynamicIndexing = true,
            };

            PhysicalDeviceVulkan11Features enabledVulkan11Features = new() {
                SType = StructureType.PhysicalDeviceVulkan11Features,
                ShaderDrawParameters = 1
            };

            DeviceCreateInfo deviceCreateInfo = new() {
                SType = StructureType.DeviceCreateInfo,
                PNext = &enabledVulkan11Features,
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
#if DEBUG
            if (_debugMessenger.Handle != 0)
                _debugUtils?.DestroyDebugUtilsMessenger(Instance, _debugMessenger, null);

            _debugMessenger = default;
            _debugUtils = null;
#endif
            Api.DestroyInstance(Instance, null);
            Instance = default;
        }

        Api.Dispose();
        _disposed = true;
    }
}
