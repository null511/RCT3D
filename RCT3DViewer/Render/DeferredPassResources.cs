using RCT3D.Render.Buffers;
using Silk.NET.Vulkan;

namespace RCT3D.Render;

internal sealed class DeferredPassResources : IDisposable
{
    public ComputePassResources Composite { get; }
    public ComputePassResources SkyTransmit { get; }
    public ComputePassResources SkyMultiscatter { get; }
    public ComputePassResources SkyView { get; }
    public ComputePassResources SkyIrradiance { get; }
    public ComputePassResources TaaResolve { get; }
    public ComputePassResources TaaSharpen { get; }
    public ComputePassResources Tonemap { get; }
    public ComputePassResources Debug { get; }
    public FullscreenPassResources Final { get; }

    public DeferredPassResources(VulkanDeviceContext context, RenderPass swapchainRenderPass, DeferredRenderTargets targets, SkyRenderTargets skyTargets, UniformBufferResources uniformBuffers)
    {
        ComputePassResources? composite = null;
        ComputePassResources? skyTransmit = null;
        ComputePassResources? skyMultiscatter = null;
        ComputePassResources? skyView = null;
        ComputePassResources? skyIrradiance = null;
        ComputePassResources? taaResolve = null;
        ComputePassResources? taaSharpen = null;
        ComputePassResources? tonemap = null;
        ComputePassResources? debug = null;
        FullscreenPassResources? final = null;
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

            skyIrradiance = new ComputePassResources(context, "post/sky_irradiance", uniformBuffers.DescriptorSetLayout,
                new[] { new[] { skyTargets.SkyViewView, skyTargets.SkyTransmitView, skyTargets.SkyIrradianceView } },
                new[] { skyTargets.SkyIrradianceView },
                new[] { new[] { ImageLayout.ShaderReadOnlyOptimal, ImageLayout.ShaderReadOnlyOptimal, ImageLayout.General } });

            composite = new ComputePassResources(context, "post/composite", uniformBuffers.DescriptorSetLayout,
                new[] {
                    new[] {
                        targets.AlbedoView,
                        targets.NormalsView,
                        targets.SpecularView,
                        targets.DepthView,
                        skyTargets.SkyTransmitView,
                        skyTargets.SkyViewView,
                        skyTargets.SkyIrradianceView,
                        targets.ShadowTargets!.DepthView,
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
                        ImageLayout.General,
                        ImageLayout.DepthStencilReadOnlyOptimal,
                    },
                },
                useComparisonSampler: true);

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

            tonemap = new ComputePassResources(context, "post/tonemap", uniformBuffers.DescriptorSetLayout,
                new[] { Array.Empty<ImageView>() },
                new[] { targets.SceneOutputView });

            debug = new ComputePassResources(context, "post/debug", uniformBuffers.DescriptorSetLayout,
                new[] { new[] { targets.AlbedoView, targets.NormalsView, targets.SpecularView } },
                new[] { targets.SceneOutputView });

            final = new FullscreenPassResources(context, swapchainRenderPass, "post/final", uniformBuffers.DescriptorSetLayout,
                new[] {
                    new[] { targets.SceneOutputView }
                });

            Composite = composite;
            SkyTransmit = skyTransmit;
            SkyMultiscatter = skyMultiscatter;
            SkyView = skyView;
            SkyIrradiance = skyIrradiance;
            TaaResolve = taaResolve;
            TaaSharpen = taaSharpen;
            Tonemap = tonemap;
            Debug = debug;
            Final = final;
        }
        catch {
            composite?.Dispose();
            skyTransmit?.Dispose();
            skyMultiscatter?.Dispose();
            skyView?.Dispose();
            skyIrradiance?.Dispose();
            taaResolve?.Dispose();
            taaSharpen?.Dispose();
            tonemap?.Dispose();
            debug?.Dispose();
            final?.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        Final.Dispose();
        Debug.Dispose();
        Tonemap.Dispose();
        TaaSharpen.Dispose();
        TaaResolve.Dispose();
        Composite.Dispose();
        SkyIrradiance.Dispose();
        SkyView.Dispose();
        SkyMultiscatter.Dispose();
        SkyTransmit.Dispose();
    }
}
