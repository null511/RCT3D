using Silk.NET.Vulkan;

namespace RCT3DViewer.Render;

internal sealed class DeferredPassResources : IDisposable
{
    public ComputePassResources Composite { get; }
    public ComputePassResources TaaResolve { get; }
    public ComputePassResources TaaSharpen { get; }
    public FullscreenPassResources Tonemap { get; }

    public DeferredPassResources(VulkanDeviceContext context, RenderPass swapchainRenderPass, DeferredRenderTargets targets, UniformBufferResources uniformBuffers)
    {
        ComputePassResources? composite = null;
        ComputePassResources? taaResolve = null;
        ComputePassResources? taaSharpen = null;
        FullscreenPassResources? tonemap = null;
        try {
            composite = new ComputePassResources(context, "composite", uniformBuffers.DescriptorSetLayout,
                new[] {
                    new[] {
                        targets.AlbedoView,
                        targets.NormalsView,
                        targets.SpecularView,
                        targets.DepthView,
                    },
                },
                new[] {
                    targets.SceneOutputView,
                },
                new[] {
                    new[] {
                        ImageLayout.ShaderReadOnlyOptimal,
                        ImageLayout.ShaderReadOnlyOptimal,
                        ImageLayout.ShaderReadOnlyOptimal,
                        ImageLayout.DepthStencilReadOnlyOptimal,
                    },
                });

            taaResolve = new ComputePassResources(context, "taa_resolve", uniformBuffers.DescriptorSetLayout,
                new[] {
                    new[] { targets.SceneOutputView, targets.TaaHistoryView, targets.DepthView },
                    new[] { targets.SceneOutputView, targets.SceneOutputView, targets.DepthView }
                },
                new[] {
                    new[] { targets.TaaResolveView },
                    new[] { targets.TaaResolveView }
                },
                new[] {
                    new[] { ImageLayout.ShaderReadOnlyOptimal, ImageLayout.ShaderReadOnlyOptimal, ImageLayout.DepthStencilReadOnlyOptimal },
                    new[] { ImageLayout.ShaderReadOnlyOptimal, ImageLayout.ShaderReadOnlyOptimal, ImageLayout.DepthStencilReadOnlyOptimal }
                });

            taaSharpen = new ComputePassResources(context, "taa_sharpen", uniformBuffers.DescriptorSetLayout,
                new[] {
                    new[] { targets.TaaResolveView }
                },
                new[] {
                    new[] { targets.SceneOutputView, targets.TaaHistoryView }
                });

            tonemap = new FullscreenPassResources(context, swapchainRenderPass, "tonemap",
                new[] {
                    new[] { targets.TaaHistoryView }
                });

            Composite = composite;
            TaaResolve = taaResolve;
            TaaSharpen = taaSharpen;
            Tonemap = tonemap;
        }
        catch {
            composite?.Dispose();
            taaResolve?.Dispose();
            taaSharpen?.Dispose();
            tonemap?.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        Tonemap.Dispose();
        TaaSharpen.Dispose();
        TaaResolve.Dispose();
        Composite.Dispose();
    }
}
