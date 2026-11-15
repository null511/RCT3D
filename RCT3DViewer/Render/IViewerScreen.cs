using Silk.NET.Vulkan;

namespace RCT3D.Render;

internal interface IViewerScreen : IDisposable
{
    void Update(double deltaTime);
    void BeforeRender();
    void PrepareFrame(double deltaTime, double activeFrameTimeMilliseconds);
    void Record(CommandBuffer commandBuffer, uint imageIndex, uint currentFrame);
    void DrawPresentation(CommandBuffer commandBuffer, uint currentFrame);
    void FrameSubmitted();
    void Resize(bool extentChanged);
}