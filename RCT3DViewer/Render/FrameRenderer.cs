using RCT3DViewer.Render.Buffers;
using RCT3DViewer.Render.Paths.Meshing;
using RCT3DViewer.Render.Terrain;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using System.Numerics;

namespace RCT3DViewer.Render;

internal sealed unsafe class FrameRenderer(VulkanRenderResources resources, ViewerUiController ui, FlightCamera camera)
{
    private uint frameCounter;
    private Vector3 cameraPositionLast;
    private Matrix4x4 viewProjectionLast = Matrix4x4.Identity;
    private bool taaHistoryValid;
    private bool taaEnabledLastFrame = true;
    private bool sceneOutputInitialized;
    private bool taaHistoryInitialized;
    private bool taaResolveInitialized;
    private ulong taaFrameIndex;
    private Vector3 cameraPositionCurrent;
    private Matrix4x4 viewProjectionCurrent = Matrix4x4.Identity;

    private Vk Vk => resources.Vk!;


    public void Record(
        CommandBuffer commandBuffer,
        uint imageIndex,
        uint currentFrame,
        TerrainMeshData? terrainMesh,
        PathMeshData? pathMesh,
        WaterMeshData? waterMesh)
    {
        if (taaEnabledLastFrame != ui.TaaEnabled) {
            taaHistoryValid = false;
            taaEnabledLastFrame = ui.TaaEnabled;
        }

        Vk.ResetCommandBuffer(commandBuffer, 0);

        CommandBufferBeginInfo beginInfo = new() {
            SType = StructureType.CommandBufferBeginInfo,
        };

        Vk.BeginCommandBuffer(commandBuffer, in beginInfo);

        var extent = resources.SwapchainExtent;
        var deferredTargets = resources.DeferredTargets!;
        var deferredPassResources = resources.DeferredPassResources!;

        var clearValues = stackalloc ClearValue[4];
        clearValues[0] = new ClearValue {
            Color = new ClearColorValue(0.0f, 0.0f, 0.0f, 0.0f),
        };
        clearValues[1] = new ClearValue {
            Color = new ClearColorValue(0.0f, 0.0f, 0.0f, 0.0f),
        };
        clearValues[2] = new ClearValue {
            Color = new ClearColorValue(0.0f, 0.0f, 0.0f, 0.0f),
        };
        clearValues[3] = new ClearValue {
            DepthStencil = new ClearDepthStencilValue {
                Depth = 1.0f,
                Stencil = 0,
            },
        };

        RenderPassBeginInfo renderPassInfo = new() {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = deferredTargets.GBufferRenderPass,
            Framebuffer = deferredTargets.GBufferFramebuffer,
            RenderArea = new Rect2D(new Offset2D(0, 0), extent),
            ClearValueCount = 4,
            PClearValues = clearValues
        };

        Vk.CmdBeginRenderPass(commandBuffer, in renderPassInfo, SubpassContents.Inline);
        var terrainResources = resources.TerrainGraphicsResources!;
        var terrainPipeline = ui.WireframeEnabled ? terrainResources.WireframePipeline : terrainResources.Pipeline;
        Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, terrainPipeline);

        Viewport viewport = new() {
            X = 0.0f,
            Y = 0.0f,
            Width = extent.Width,
            Height = extent.Height,
            MinDepth = 0.0f,
            MaxDepth = 1.0f,
        };

        Rect2D scissor = new() {
            Offset = new Offset2D(0, 0),
            Extent = extent,
        };

        Vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);
        Vk.CmdSetScissor(commandBuffer, 0, 1, &scissor);

        var terrainDescriptorSet = resources.TerrainDescriptorSet;
        var uniformDescriptorSet = resources.UniformBufferResources!.GetDescriptorSet(currentFrame);
        var terrainDescriptorSets = stackalloc DescriptorSet[2];
        terrainDescriptorSets[0] = uniformDescriptorSet;
        terrainDescriptorSets[1] = terrainDescriptorSet;
        Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, resources.PipelineLayout, 0, 2, terrainDescriptorSets, 0, null);

        var aspect = (float)extent.Width / extent.Height;
        var viewProjection = camera.GetViewMatrix() * camera.GetProjectionMatrix(aspect);
        if (!Matrix4x4.Invert(viewProjection, out var inverseViewProjection))
            throw new InvalidOperationException("The camera view-projection matrix is not invertible.");

        CalculateSunDirection(out var sunElevation, out var sunDirection);
        var viewFrustum = ViewFrustum.FromViewProjection(viewProjection);
        var ambientLight = 0.15f + 0.15f * MathF.Max(sunElevation, 0.0f);
        var cameraPosition = camera.Position;
        var renderDimensions = new Vector2D<uint>(extent.Width, extent.Height);
        CameraUniforms cameraUniforms = new() {
            CameraPosition = new Vector4(cameraPosition, 1.0f),
            ViewProjection = viewProjection,
            ViewProjectionInverse = inverseViewProjection,
        };

        var taaJitter = ui.TaaEnabled ? GetR2Jitter(taaFrameIndex + 1) : Vector2.Zero;
        TemporalUniforms temporalUniforms = new() {
            CameraPositionLast = new Vector4(frameCounter == 0 ? cameraPosition : cameraPositionLast, 1.0f),
            ViewProjectionLast = frameCounter == 0 ? viewProjection : viewProjectionLast,
        };

        SceneUniforms sceneUniforms = new() {
            RenderDimensions = renderDimensions,
            TaaJitter = taaJitter,
            SunDirection = new Vector4(sunDirection, 0.0f),
            Lighting = new Vector4(ambientLight, 1.25f, 0.0f, 0.0f),
            FrameCounter = frameCounter,
            TaaSharpen = ui.Sharpening,
            MipBias = ui.MipBias,
            Padding = 0.0f,
        };

        resources.UniformBufferResources!.Update(currentFrame, cameraUniforms, temporalUniforms, sceneUniforms);

        ulong offset = 0;
        if (terrainMesh is { Vertices.Length: > 0, Indices.Length: > 0 } && resources.VertexBuffer.Handle != 0 && resources.IndexBuffer.Handle != 0) {
            var vertexBuffer = resources.VertexBuffer;
            Vk.CmdBindVertexBuffers(commandBuffer, 0, 1, in vertexBuffer, in offset);
            Vk.CmdBindIndexBuffer(commandBuffer, resources.IndexBuffer, 0, IndexType.Uint32);
            foreach (var region in terrainMesh.Regions) {
                if (viewFrustum.Intersects(region.Bounds))
                    Vk.CmdDrawIndexed(commandBuffer, region.IndexCount, 1, region.FirstIndex, 0, 0);
            }
        }

        var vehicleResourcesList = resources.VehicleCubeGraphicsResources;
        if (vehicleResourcesList.Length > 0) {
            var vehicleResources = vehicleResourcesList[(int)currentFrame];

            foreach (var region in vehicleResources.RegionBatches) {
                if (viewFrustum.Intersects(region.Bounds))
                    vehicleResources.DrawInstanced(commandBuffer, uniformDescriptorSet, region);
            }
        }

        if (pathMesh is { Vertices.Length: > 0, Indices.Length: > 0 } && resources.PathGraphicsResources is { IndexCount: > 0 } pathResources) {
            var pathPipeline = ui.WireframeEnabled ? pathResources.WireframePipeline : pathResources.Pipeline;
            Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, pathPipeline);
            
            var pathDescriptorSet = pathResources.DescriptorSet;
            var pathDescriptorSets = stackalloc DescriptorSet[2];
            pathDescriptorSets[0] = uniformDescriptorSet;
            pathDescriptorSets[1] = pathDescriptorSet;
            Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, pathResources.PipelineLayout, 0, 2, pathDescriptorSets, 0, null);
            
            var pathVertexBuffer = pathResources.VertexBuffer;
            Vk.CmdBindVertexBuffers(commandBuffer, 0, 1, in pathVertexBuffer, in offset);
            Vk.CmdBindIndexBuffer(commandBuffer, pathResources.IndexBuffer, 0, IndexType.Uint32);

            foreach (var region in pathMesh.Regions) {
                if (viewFrustum.Intersects(region.Bounds))
                    Vk.CmdDrawIndexed(commandBuffer, region.IndexCount, 1, region.FirstIndex, 0, 0);
            }
        }

        if (terrainMesh is not null) {
            foreach (var (_, resources1) in resources.PathAccessoryGraphicsResources)
            foreach (var region in resources1.RegionBatches) {
                if (viewFrustum.Intersects(region.Bounds))
                    resources1.DrawInstanced(commandBuffer, uniformDescriptorSet, region);
            }
        }

        Vk.CmdEndRenderPass(commandBuffer);

        RecordImageBarrier(commandBuffer, deferredTargets.SceneOutputImage,
            sceneOutputInitialized ? ImageLayout.ShaderReadOnlyOptimal : ImageLayout.Undefined,
            ImageLayout.General,
            sceneOutputInitialized ? PipelineStageFlags.ComputeShaderBit : PipelineStageFlags.TopOfPipeBit,
            sceneOutputInitialized ? AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit : AccessFlags.None,
            PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit);

        deferredPassResources.Composite.Dispatch(commandBuffer, 0, uniformDescriptorSet, renderDimensions);
        sceneOutputInitialized = true;

        RenderPassBeginInfo waterRenderPassInfo = new() {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = deferredTargets.WaterRenderPass,
            Framebuffer = deferredTargets.WaterFramebuffer,
            RenderArea = new Rect2D(new Offset2D(0, 0), extent)
        };

        Vk.CmdBeginRenderPass(commandBuffer, in waterRenderPassInfo, SubpassContents.Inline);

        if (waterMesh is { Indices.Length: > 0 } && resources.WaterGraphicsResources is { IndexCount: > 0 } waterResources) {
            var visibleWaterRegions = new List<RenderRegionIndexRange>();

            foreach (var region in waterMesh.Regions) {
                if (viewFrustum.Intersects(region.Bounds))
                    visibleWaterRegions.Add(region);
            }

            visibleWaterRegions.Sort((left, right) =>
                Vector3.DistanceSquared(cameraPosition, right.Bounds.Center)
                    .CompareTo(Vector3.DistanceSquared(cameraPosition, left.Bounds.Center)));

            Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, waterResources.Pipeline);
            Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, waterResources.PipelineLayout, 0, 1, in uniformDescriptorSet, 0, null);
            var waterVertexBuffer = waterResources.VertexBuffer;
            Vk.CmdBindVertexBuffers(commandBuffer, 0, 1, in waterVertexBuffer, in offset);
            Vk.CmdBindIndexBuffer(commandBuffer, waterResources.IndexBuffer, 0, IndexType.Uint32);

            foreach (var region in visibleWaterRegions)
                Vk.CmdDrawIndexed(commandBuffer, region.IndexCount, 1, region.FirstIndex, 0, 0);
        }

        Vk.CmdEndRenderPass(commandBuffer);

        if (ui.TaaEnabled)
            RecordTaa(commandBuffer, uniformDescriptorSet, renderDimensions, deferredTargets, deferredPassResources);

        clearValues[0] = new ClearValue {
            Color = new ClearColorValue(0.0f, 0.0f, 0.0f, 1.0f),
        };

        clearValues[1] = new ClearValue {
            DepthStencil = new ClearDepthStencilValue {
                Depth = 1.0f,
                Stencil = 0,
            },
        };

        RenderPassBeginInfo presentationRenderPassInfo = new() {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = resources.RenderPass,
            Framebuffer = resources.Framebuffers[imageIndex],
            RenderArea = new Rect2D(new Offset2D(0, 0), extent),
            ClearValueCount = 2,
            PClearValues = clearValues
        };

        Vk.CmdBeginRenderPass(commandBuffer, in presentationRenderPassInfo, SubpassContents.Inline);
        Vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);
        Vk.CmdSetScissor(commandBuffer, 0, 1, &scissor);
        deferredPassResources.Tonemap.Draw(commandBuffer, 0);
        resources.ImGuiRenderer?.Render(commandBuffer, imageIndex);
        Vk.CmdEndRenderPass(commandBuffer);
        Vk.EndCommandBuffer(commandBuffer);

        cameraPositionCurrent = cameraPosition;
        viewProjectionCurrent = viewProjection;
    }

    public void FrameSubmitted()
    {
        cameraPositionLast = cameraPositionCurrent;
        viewProjectionLast = viewProjectionCurrent;
        frameCounter = frameCounter == uint.MaxValue ? 1u : frameCounter + 1u;
        taaFrameIndex++;
        taaHistoryValid = ui.TaaEnabled;
    }

    public void ResetForSwapchain(bool extentChanged)
    {
        frameCounter = 0;
        if (!extentChanged) return;

        taaHistoryValid = false;
        sceneOutputInitialized = false;
        taaHistoryInitialized = false;
        taaResolveInitialized = false;
    }

    public void InvalidateHistory()
    {
        taaHistoryValid = false;
    }

    private void RecordTaa(CommandBuffer commandBuffer, DescriptorSet uniformDescriptorSet, Vector2D<uint> renderDimensions,
        DeferredRenderTargets deferredTargets, DeferredPassResources deferredPassResources)
    {
        var taaDescriptorIndex = taaHistoryValid ? 0 : 1;
        var resolveOldLayout = taaResolveInitialized ? ImageLayout.ShaderReadOnlyOptimal : ImageLayout.Undefined;

        RecordImageBarrier(commandBuffer, deferredTargets.TaaResolveImage, resolveOldLayout, ImageLayout.General,
            taaResolveInitialized ? PipelineStageFlags.ComputeShaderBit : PipelineStageFlags.TopOfPipeBit,
            taaResolveInitialized ? AccessFlags.ShaderReadBit : AccessFlags.None,
            PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit);

        deferredPassResources.TaaResolve.Dispatch(commandBuffer, taaDescriptorIndex, uniformDescriptorSet, renderDimensions);

        RecordImageBarrier(commandBuffer, deferredTargets.TaaResolveImage, ImageLayout.General, ImageLayout.ShaderReadOnlyOptimal,
            PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit,
            PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderReadBit);

        taaResolveInitialized = true;

        var historyOldLayout = taaHistoryInitialized ? ImageLayout.ShaderReadOnlyOptimal : ImageLayout.Undefined;

        RecordImageBarrier(commandBuffer, deferredTargets.TaaHistoryImage, historyOldLayout, ImageLayout.General,
            taaHistoryInitialized ? PipelineStageFlags.ComputeShaderBit | PipelineStageFlags.FragmentShaderBit : PipelineStageFlags.TopOfPipeBit,
            taaHistoryInitialized ? AccessFlags.ShaderReadBit : AccessFlags.None,
            PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit);

        RecordImageBarrier(commandBuffer, deferredTargets.SceneOutputImage, ImageLayout.ShaderReadOnlyOptimal, ImageLayout.General,
            PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.ComputeShaderBit,
            AccessFlags.ColorAttachmentWriteBit | AccessFlags.ShaderReadBit,
            PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit);

        deferredPassResources.TaaSharpen.Dispatch(commandBuffer, 0, uniformDescriptorSet, renderDimensions);

        RecordImageBarrier(commandBuffer, deferredTargets.SceneOutputImage, ImageLayout.General, ImageLayout.ShaderReadOnlyOptimal,
            PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit,
            PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderReadBit);

        RecordImageBarrier(commandBuffer, deferredTargets.TaaHistoryImage, ImageLayout.General, ImageLayout.ShaderReadOnlyOptimal,
            PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit,
            PipelineStageFlags.ComputeShaderBit | PipelineStageFlags.FragmentShaderBit, AccessFlags.ShaderReadBit);

        taaHistoryInitialized = true;
    }

    private static Vector2 GetR2Jitter(ulong sampleIndex)
    {
        const double generator = 1.324717957244746;
        const double alphaX = 1.0 / generator;
        const double alphaY = 1.0 / (generator * generator);
        var sample = (double)sampleIndex;

        return new Vector2(
            (float)(0.5 + sample * alphaX - Math.Floor(0.5 + sample * alphaX)) - 0.5f,
            (float)(0.5 + sample * alphaY - Math.Floor(0.5 + sample * alphaY)) - 0.5f);
    }

    private void RecordImageBarrier(CommandBuffer commandBuffer, Image image, ImageLayout oldLayout, ImageLayout newLayout,
        PipelineStageFlags sourceStage, AccessFlags sourceAccess, PipelineStageFlags destinationStage, AccessFlags destinationAccess)
    {
        var barrier = new ImageMemoryBarrier {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
            SrcAccessMask = sourceAccess,
            DstAccessMask = destinationAccess
        };

        Vk.CmdPipelineBarrier(commandBuffer, sourceStage, destinationStage, 0, 0, null, 0, null, 1, &barrier);
    }

    private void CalculateSunDirection(out float sunElevation, out Vector3 sunDirection)
    {
        var timeAngle = (ui.EffectiveTimeOfDayHours - 6.0f) * (MathF.PI / 12.0f);
        var zenithAngle = ui.ZenithDegrees * (MathF.PI / 180.0f);
        sunElevation = MathF.Sin(timeAngle) * MathF.Cos(zenithAngle);

        var horizontalLength = MathF.Sqrt(MathF.Max(0.0f, 1.0f - sunElevation * sunElevation));
        var sunAzimuth = ui.AzimuthDegrees * (MathF.PI / 180.0f);
        sunDirection = Vector3.Normalize(new Vector3(MathF.Cos(sunAzimuth) * horizontalLength, sunElevation, MathF.Sin(sunAzimuth) * horizontalLength));
    }
}
