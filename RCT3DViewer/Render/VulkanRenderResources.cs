using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace RCT3D.Render;

internal sealed unsafe class VulkanRenderResources : IDisposable
{
    public const int MaxFramesInFlight = 2;

    private bool isDisposed;

    public VulkanDeviceContext? DeviceContext { get; private set; }
    public SwapchainResources? SwapchainResources { get; private set; }
    public CommandPool CommandPool { get; private set; }
    public CommandBuffer[] CommandBuffers { get; private set; } = Array.Empty<CommandBuffer>();
    public Semaphore[] ImageAvailableSemaphores { get; private set; } = Array.Empty<Semaphore>();
    public Semaphore[] RenderFinishedSemaphores { get; private set; } = Array.Empty<Semaphore>();
    public Fence[] InFlightFences { get; private set; } = Array.Empty<Fence>();
    public Fence[] ImagesInFlight { get; private set; } = Array.Empty<Fence>();
    public ImGuiVulkanRenderer? ImGuiRenderer { get; private set; }

    public Vk? Vk => DeviceContext?.Api;
    public Device LogicalDevice => DeviceContext?.LogicalDevice ?? default;
    public Queue GraphicsQueue => DeviceContext?.GraphicsQueue ?? default;
    public uint GraphicsQueueFamilyIndex => DeviceContext?.GraphicsQueueFamilyIndex ?? 0;
    public SwapchainKHR Swapchain => SwapchainResources?.Swapchain ?? default;
    public Image[] SwapchainImages => SwapchainResources?.Images ?? Array.Empty<Image>();
    public Framebuffer[] Framebuffers => SwapchainResources?.Framebuffers ?? Array.Empty<Framebuffer>();
    public Extent2D SwapchainExtent => SwapchainResources?.Extent ?? default;
    public RenderPass RenderPass => SwapchainResources?.RenderPass ?? default;

    public VulkanRenderResources(IWindow window)
    {
        try {
            Initialize(window);
        }
        catch {
            Dispose();
            throw;
        }
    }

    private void Initialize(IWindow window)
    {
        DeviceContext = VulkanDeviceContext.Create(window);
        SwapchainResources = new SwapchainResources(DeviceContext, window);
        SwapchainResources.CreateSwapchain();
        SwapchainResources.CreateImageViews();
        SwapchainResources.CreateDepthResources();
        SwapchainResources.CreateRenderPass();
        SwapchainResources.CreateFramebuffers();

        CreateCommandInfrastructure();
        CreateSyncPrimitives();

        ImGuiRenderer = new ImGuiVulkanRenderer(Vk!, DeviceContext.PhysicalDevice, LogicalDevice, GraphicsQueue, RenderPass, CommandPool,
            Path.Combine(AppContext.BaseDirectory, "Shaders"), SwapchainImages.Length);
    }

    public void RecreateSwapchain()
    {
        if (Vk!.DeviceWaitIdle(LogicalDevice) != Result.Success)
            throw new Exception("Failed to wait for the Vulkan device before recreating the swapchain.");

        DestroySyncPrimitives();
        if (CommandPool.Handle != 0)
            Vk.DestroyCommandPool(LogicalDevice, CommandPool, null);

        CommandPool = default;
        CommandBuffers = Array.Empty<CommandBuffer>();
        SwapchainResources!.Recreate();
        CreateCommandInfrastructure();
        CreateSyncPrimitives();
        ImGuiRenderer?.ResizeBuffers(SwapchainImages.Length);
    }

    private void CreateCommandInfrastructure()
    {
        CommandPoolCreateInfo poolInfo = new() {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = GraphicsQueueFamilyIndex,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit
        };

        if (Vk!.CreateCommandPool(LogicalDevice, in poolInfo, null, out var commandPool) != Result.Success)
            throw new InvalidOperationException("Failed to create the Vulkan command pool.");
        CommandPool = commandPool;

        CommandBufferAllocateInfo allocInfo = new() {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = CommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = (uint)Framebuffers.Length,
        };

        CommandBuffers = new CommandBuffer[Framebuffers.Length];
        fixed (CommandBuffer* cbPtr = CommandBuffers)
            if (Vk.AllocateCommandBuffers(LogicalDevice, in allocInfo, cbPtr) != Result.Success)
                throw new InvalidOperationException("Failed to allocate the frame command buffers.");
    }

    private void CreateSyncPrimitives()
    {
        SemaphoreCreateInfo semaphoreInfo = new() { SType = StructureType.SemaphoreCreateInfo };
        FenceCreateInfo fenceInfo = new() { SType = StructureType.FenceCreateInfo, Flags = FenceCreateFlags.SignaledBit };

        var imageCount = SwapchainImages.Length;
        ImageAvailableSemaphores = new Semaphore[MaxFramesInFlight];
        RenderFinishedSemaphores = new Semaphore[imageCount];
        InFlightFences = new Fence[MaxFramesInFlight];

        for (var i = 0; i < MaxFramesInFlight; i++)
            Vk!.CreateSemaphore(LogicalDevice, in semaphoreInfo, null, out ImageAvailableSemaphores[i]);

        for (var i = 0; i < imageCount; i++)
            Vk!.CreateSemaphore(LogicalDevice, in semaphoreInfo, null, out RenderFinishedSemaphores[i]);

        for (uint i = 0; i < MaxFramesInFlight; i++)
            Vk!.CreateFence(LogicalDevice, in fenceInfo, null, out InFlightFences[i]);

        ImagesInFlight = new Fence[imageCount];
        for (var i = 0; i < ImagesInFlight.Length; i++)
            ImagesInFlight[i] = new Fence();
    }

    private void DestroySyncPrimitives()
    {
        if (Vk is null || LogicalDevice.Handle == 0) return;

        foreach (var semaphore in ImageAvailableSemaphores) {
            if (semaphore.Handle != 0)
                Vk.DestroySemaphore(LogicalDevice, semaphore, null);
        }

        foreach (var semaphore in RenderFinishedSemaphores) {
            if (semaphore.Handle != 0)
                Vk.DestroySemaphore(LogicalDevice, semaphore, null);
        }

        foreach (var fence in InFlightFences) {
            if (fence.Handle != 0)
                Vk.DestroyFence(LogicalDevice, fence, null);
        }

        ImageAvailableSemaphores = Array.Empty<Semaphore>();
        RenderFinishedSemaphores = Array.Empty<Semaphore>();
        InFlightFences = Array.Empty<Fence>();
        ImagesInFlight = Array.Empty<Fence>();
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        var vk = Vk;
        var logicalDevice = LogicalDevice;
        if (vk is not null && logicalDevice.Handle != 0) {
            vk.DeviceWaitIdle(logicalDevice);
            ImGuiRenderer?.Dispose();
            ImGuiRenderer = null;
            DestroySyncPrimitives();

            if (CommandPool.Handle != 0)
                vk.DestroyCommandPool(logicalDevice, CommandPool, null);

            CommandPool = default;
            CommandBuffers = Array.Empty<CommandBuffer>();
        }

        SwapchainResources?.Dispose();
        SwapchainResources = null;
        DeviceContext?.Dispose();
        DeviceContext = null;
    }

    public void BeginCommands(CommandBuffer commandBuffer)
    {
        if (Vk!.ResetCommandBuffer(commandBuffer, 0) != Result.Success)
            throw new InvalidOperationException($"Failed to reset command buffer {commandBuffer}.");

        var beginInfo = new CommandBufferBeginInfo {
            SType = StructureType.CommandBufferBeginInfo,
        };

        if (Vk.BeginCommandBuffer(commandBuffer, in beginInfo) != Result.Success)
            throw new InvalidOperationException("Failed to begin command buffer.");
    }

    public void EndCommands(CommandBuffer commandBuffer, uint imageIndex, Action<CommandBuffer>? drawSceneOutput = null)
    {
        var clearValues = stackalloc ClearValue[2];
        clearValues[0] = new ClearValue { Color = new ClearColorValue(0, 0, 0, 1) };
        clearValues[1] = new ClearValue { DepthStencil = new ClearDepthStencilValue { Depth = 1.0f, Stencil = 0 } };

        var renderPassBeginInfo = new RenderPassBeginInfo {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = RenderPass,
            Framebuffer = Framebuffers[imageIndex],
            RenderArea = new Rect2D {
                Offset = new Offset2D(0, 0),
                Extent = SwapchainExtent,
            },
            ClearValueCount = 2,
            PClearValues = clearValues,
        };

        Vk!.CmdBeginRenderPass(commandBuffer, in renderPassBeginInfo, SubpassContents.Inline);

        var viewport = new Viewport {
            X = 0,
            Y = 0,
            Width = (float)SwapchainExtent.Width,
            Height = (float)SwapchainExtent.Height,
            MinDepth = 0,
            MaxDepth = 1,
        };
        Vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);

        var scissor = new Rect2D {
            Offset = new Offset2D(0, 0),
            Extent = SwapchainExtent,
        };
        Vk.CmdSetScissor(commandBuffer, 0, 1, &scissor);

        drawSceneOutput?.Invoke(commandBuffer);

        ImGuiRenderer?.Render(commandBuffer, imageIndex);

        Vk.CmdEndRenderPass(commandBuffer);

        if (Vk.EndCommandBuffer(commandBuffer) != Result.Success)
            throw new InvalidOperationException("Failed to end command buffer.");
    }
}
