using System.Diagnostics;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace RCT3D.Render;

internal sealed unsafe class FrameCoordinator(IWindow window, VulkanRenderResources resources)
{
    private bool framebufferResized;
    private uint _currentFrame;

    public double ActiveFrameTimeMilliseconds { get; private set; }
    public uint CurrentFrame => _currentFrame;


    public void RenderFrame(
        double deltaTime,
        Action<double> prepareFrame,
        Action<CommandBuffer, uint, uint> recordFrame,
        Action frameSubmitted,
        Action<bool> swapchainRecreated,
        Action<CommandBuffer, uint>? drawPresentation = null)
    {
        if (window.FramebufferSize.X == 0 || window.FramebufferSize.Y == 0) return;
        if (framebufferResized && !RecreateSwapchain(swapchainRecreated)) return;

        var frameStart = Stopwatch.GetTimestamp();
        long synchronizationTicks = 0;
        uint imageIndex = 0;
        var synchronizationStart = Stopwatch.GetTimestamp();
        resources.Vk!.WaitForFences(resources.LogicalDevice, 1, in resources.InFlightFences[_currentFrame], true, ulong.MaxValue);
        synchronizationTicks += Stopwatch.GetTimestamp() - synchronizationStart;
        synchronizationStart = Stopwatch.GetTimestamp();

        var acquireResult = resources.DeviceContext!.SwapchainExtension!.AcquireNextImage(
            resources.LogicalDevice, resources.Swapchain, ulong.MaxValue,
            resources.ImageAvailableSemaphores[_currentFrame], default, ref imageIndex);

        synchronizationTicks += Stopwatch.GetTimestamp() - synchronizationStart;

        if (acquireResult == Result.ErrorOutOfDateKhr) {
            RecreateSwapchain(swapchainRecreated);
            return;
        }

        if (acquireResult != Result.Success && acquireResult != Result.SuboptimalKhr)
            throw new Exception($"Failed to acquire a swapchain image: {acquireResult}.");

        var recreateAfterPresent = acquireResult == Result.SuboptimalKhr;
        if (resources.ImagesInFlight[imageIndex].Handle != 0) {
            synchronizationStart = Stopwatch.GetTimestamp();
            resources.Vk.WaitForFences(resources.LogicalDevice, 1, in resources.ImagesInFlight[imageIndex], true, ulong.MaxValue);
            synchronizationTicks += Stopwatch.GetTimestamp() - synchronizationStart;
        }

        resources.ImagesInFlight[imageIndex] = resources.InFlightFences[_currentFrame];
        var commandBuffer = resources.CommandBuffers[imageIndex];
        resources.ImGuiRenderer?.BeginFrame((int)resources.SwapchainExtent.Width, (int)resources.SwapchainExtent.Height, (float)deltaTime);
        prepareFrame(ActiveFrameTimeMilliseconds);
        resources.BeginCommands(commandBuffer);
        recordFrame(commandBuffer, imageIndex, _currentFrame);
        resources.EndCommands(commandBuffer, imageIndex,
            drawPresentation is null ? null : buffer => drawPresentation(buffer, _currentFrame));

        var waitStages = PipelineStageFlags.AllCommandsBit;
        var imageAvailableSemaphore = resources.ImageAvailableSemaphores[_currentFrame];
        var renderFinishedSemaphore = resources.RenderFinishedSemaphores[imageIndex];

        SubmitInfo submitInfo = new() {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &imageAvailableSemaphore,
            PWaitDstStageMask = &waitStages,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = &renderFinishedSemaphore
        };

        resources.Vk.ResetFences(resources.LogicalDevice, 1, in resources.InFlightFences[_currentFrame]);
        var submitResult = resources.Vk.QueueSubmit(resources.GraphicsQueue, 1, in submitInfo, resources.InFlightFences[_currentFrame]);
        if (submitResult != Result.Success)
            throw new Exception($"Failed to submit the frame to the graphics queue: {submitResult}.");

        frameSubmitted();

        var swapchain = resources.Swapchain;

        PresentInfoKHR presentInfo = new() {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &renderFinishedSemaphore,
            SwapchainCount = 1,
            PSwapchains = &swapchain,
            PImageIndices = &imageIndex
        };

        synchronizationStart = Stopwatch.GetTimestamp();
        var presentResult = resources.DeviceContext.SwapchainExtension.QueuePresent(resources.GraphicsQueue, in presentInfo);
        synchronizationTicks += Stopwatch.GetTimestamp() - synchronizationStart;
        if (presentResult != Result.Success && presentResult != Result.SuboptimalKhr && presentResult != Result.ErrorOutOfDateKhr)
            throw new Exception($"Failed to present a swapchain image: {presentResult}.");

        ActiveFrameTimeMilliseconds = Math.Max(
            0.0,
            Stopwatch.GetElapsedTime(frameStart).TotalMilliseconds - synchronizationTicks * 1000.0 / Stopwatch.Frequency);

        _currentFrame = (_currentFrame + 1) % VulkanRenderResources.MaxFramesInFlight;
        if (recreateAfterPresent || framebufferResized || presentResult == Result.SuboptimalKhr || presentResult == Result.ErrorOutOfDateKhr)
            RecreateSwapchain(swapchainRecreated);
    }

    public void MarkFramebufferResized()
    {
        framebufferResized = true;
    }

    private bool RecreateSwapchain(Action<bool> swapchainRecreated)
    {
        var framebufferSize = window.FramebufferSize;
        if (framebufferSize.X == 0 || framebufferSize.Y == 0) return false;

        var previousExtent = resources.SwapchainExtent;
        resources.RecreateSwapchain();

        var extent = resources.SwapchainExtent;
        swapchainRecreated(previousExtent.Width != extent.Width || previousExtent.Height != extent.Height);
        _currentFrame = 0;
        framebufferResized = false;

        return true;
    }
}
