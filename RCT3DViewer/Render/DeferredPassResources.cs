using RCT3DViewer.Render.Buffers;
using Silk.NET.Vulkan;

namespace RCT3DViewer.Render;

internal sealed class DeferredPassResources : IDisposable
{
    public ComputePassResources Composite { get; }
    public ComputePassResources SkyTransmit { get; }
    public ComputePassResources SkyMultiscatter { get; }
    public ComputePassResources SkyView { get; }
    public ComputePassResources TaaResolve { get; }
    public ComputePassResources TaaSharpen { get; }
    public FullscreenPassResources Tonemap { get; }

    public DeferredPassResources(VulkanDeviceContext context, RenderPass swapchainRenderPass, DeferredRenderTargets targets, SkyRenderTargets skyTargets, UniformBufferResources uniformBuffers)
    {
        ComputePassResources? composite = null;
        ComputePassResources? skyTransmit = null;
        ComputePassResources? skyMultiscatter = null;
        ComputePassResources? skyView = null;
        ComputePassResources? taaResolve = null;
        ComputePassResources? taaSharpen = null;
        FullscreenPassResources? tonemap = null;
        try {
            skyTransmit = new ComputePassResources(context, "pre/sky_transmit", uniformBuffers.DescriptorSetLayout,
                new[] { Array.Empty<ImageView>() },
                new[] { skyTargets.SkyTransmitView });

            skyMultiscatter = new ComputePassResources(context, "pre/sky_multiscatter", uniformBuffers.DescriptorSetLayout,
                new[] { new[] { skyTargets.SkyTransmitView } },
                new[] { skyTargets.SkyMultiscatterView });

            skyView = new ComputePassResources(context, "post/sky_view", uniformBuffers.DescriptorSetLayout,
                new[] { new[] { skyTargets.SkyTransmitView, skyTargets.SkyMultiscatterView } },
                new[] { skyTargets.SkyViewView });

            composite = new ComputePassResources(context, "post/composite", uniformBuffers.DescriptorSetLayout,
                new[] {
                    new[] {
                        targets.AlbedoView,
                        targets.NormalsView,
                        targets.SpecularView,
                        targets.DepthView,
                        skyTargets.SkyTransmitView,
                        skyTargets.SkyViewView,
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
                        ImageLayout.ShaderReadOnlyOptimal,
                        ImageLayout.ShaderReadOnlyOptimal,
                    },
                });

            taaResolve = new ComputePassResources(context, "post/taa_resolve", uniformBuffers.DescriptorSetLayout,
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

            taaSharpen = new ComputePassResources(context, "post/taa_sharpen", uniformBuffers.DescriptorSetLayout,
                new[] {
                    new[] { targets.TaaResolveView }
                },
                new[] {
                    new[] { targets.SceneOutputView, targets.TaaHistoryView }
                });

            tonemap = new FullscreenPassResources(context, swapchainRenderPass, "post/tonemap",
                new[] {
                    new[] { targets.SceneOutputView }
                });

            Composite = composite;
            SkyTransmit = skyTransmit;
            SkyMultiscatter = skyMultiscatter;
            SkyView = skyView;
            TaaResolve = taaResolve;
            TaaSharpen = taaSharpen;
            Tonemap = tonemap;
        }
        catch {
            composite?.Dispose();
            skyTransmit?.Dispose();
            skyMultiscatter?.Dispose();
            skyView?.Dispose();
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
        SkyView.Dispose();
        SkyMultiscatter.Dispose();
        SkyTransmit.Dispose();
    }
}
