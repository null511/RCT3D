using RCT3D.Render.Buffers;
using RCT3D.Render.Paths.Meshing;
using RCT3D.Render.Rides.Entrances;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Terrain;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using System.Numerics;

namespace RCT3D.Render;

internal sealed unsafe class FrameRenderer(VulkanRenderResources resources, ViewerUiController ui, FlightCamera camera)
{
    private uint frameCounter;
    private Vector3 cameraPositionLast;
    private Matrix4x4 viewRotationLast = Matrix4x4.Identity;
    private Matrix4x4 projectionLast = Matrix4x4.Identity;
    private bool taaHistoryValid;
    private bool taaEnabledLastFrame = true;
    private bool sceneOutputInitialized;
    private bool shadowMapInitialized;
    private bool skyViewInitialized;
    private bool taaHistoryInitialized;
    private bool taaResolveInitialized;
    private ulong taaFrameIndex;
    private Vector3 cameraPositionCurrent;
    private Matrix4x4 viewRotationCurrent = Matrix4x4.Identity;
    private Matrix4x4 projectionCurrent = Matrix4x4.Identity;
    private readonly List<ParkRenderRegion> visibleWaterRegions = new();
    private readonly WaterRegionDepthComparer waterRegionDepthComparer = new();
    private readonly Dictionary<RenderRegionCoordinate, ParkRenderRegion> regionsByCoordinate = new();
    private ParkRenderRegion[]? cachedParkRegions;

    private Vk Vk => resources.Vk!;
    public (int Visible, int Total) SectionMetrics { get; private set; }


    public void Record(
        CommandBuffer commandBuffer,
        uint imageIndex,
        uint currentFrame,
        TerrainMeshData? terrainMesh,
        PathMeshData? pathMesh,
        WaterMeshData? waterMesh,
        TrackMeshData? trackMesh,
        RideSupportMeshData? rideSupportMesh,
        RideEntranceExitMeshData? rideEntranceExitMesh,
        ParkRenderRegion[] parkRegions)
    {
        SectionMetrics = (0, 0);

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
        var aspect = (float)extent.Width / extent.Height;
        var view = camera.GetViewMatrix();
        var projection = camera.GetProjectionMatrix(aspect);
        var viewProjection = view * projection;
        if (!Matrix4x4.Invert(projection, out var projectionInverse))
            throw new InvalidOperationException("The camera projection matrix is not invertible.");
        if (!Matrix4x4.Invert(view, out var viewInverse))
            throw new InvalidOperationException("The camera view matrix is not invertible.");

        CalculateSunDirection(out _, out var sunDirection);
        var cameraPosition = camera.Position;
        var viewRotation = view;
        viewRotation.M41 = 0.0f;
        viewRotation.M42 = 0.0f;
        viewRotation.M43 = 0.0f;
        var previousViewRotation = frameCounter == 0 ? viewRotation : viewRotationLast;
        var previousProjection = frameCounter == 0 ? projection : projectionLast;
        var previousCameraPosition = frameCounter == 0 ? cameraPosition : cameraPositionLast;
        if (!Matrix4x4.Invert(viewRotation, out var inverseViewRotation))
            throw new InvalidOperationException("The camera view rotation matrix is not invertible.");
        var previousCameraDeltaInView = Vector3.TransformNormal(previousCameraPosition - cameraPosition, viewRotation);
        var currentViewToPreviousView = Matrix4x4.CreateTranslation(-previousCameraDeltaInView)
            * inverseViewRotation
            * previousViewRotation;
        var currentViewToPreviousClip = currentViewToPreviousView * previousProjection;
        var (shadowViewProjections, cascadeSplits) = CreateShadowViewProjections(camera, aspect, sunDirection, ui.ShadowRadius,
            ui.ShadowResolution);
        var viewFrustum = ViewFrustum.FromViewProjection(viewProjection);
        foreach (var region in parkRegions)
            region.CameraVisible = region.HasBounds && viewFrustum.Intersects(region.Bounds);
        EnsureRegionLookup(parkRegions);

        var renderDimensions = new Vector2D<uint>(extent.Width, extent.Height);
        var uniformDescriptorSet = resources.UniformBufferResources!.GetDescriptorSet(currentFrame);
        CameraUniforms cameraUniforms = new() {
            CameraPosition = new Vector4(cameraPosition, 1.0f),
            View = view,
            Projection = projection,
            ViewProjection = viewProjection,
            ProjectionInverse = projectionInverse,
            ViewInverse = viewInverse,
        };

        var taaJitter = ui.TaaEnabled ? GetR2Jitter(taaFrameIndex + 1) : Vector2.Zero;
        TemporalUniforms temporalUniforms = new() {
            CurrentViewToPreviousClip = currentViewToPreviousClip,
        };

        SceneUniforms sceneUniforms = new() {
            RenderDimensions = renderDimensions,
            TaaJitter = taaJitter,
            SunDirection = new Vector4(sunDirection, 0.0f),
            FrameCounter = frameCounter,
            TaaSharpen = ui.Sharpening,
            MipBias = ui.MipBias,
            DebugView = ui.DebugView,
            ShadowMappingEnabled = ui.ShadowMappingEnabled ? 1u : 0u,
        };

        ShadowUniforms shadowUniforms = new() { CascadeSplits = cascadeSplits };
        for (var cascade = 0; cascade < ShadowRenderTargets.CascadeCount; cascade++)
            shadowUniforms.ShadowViewProjection[cascade] = shadowViewProjections[cascade];

        resources.UniformBufferResources.Update(currentFrame, cameraUniforms, temporalUniforms, sceneUniforms, shadowUniforms);

        var skyImageOldLayout = skyViewInitialized ? ImageLayout.ShaderReadOnlyOptimal : ImageLayout.Undefined;

        RecordImageBarrier(commandBuffer, resources.SkyTargets!.SkyViewImage, skyImageOldLayout, ImageLayout.General,
            skyViewInitialized ? PipelineStageFlags.ComputeShaderBit | PipelineStageFlags.FragmentShaderBit : PipelineStageFlags.TopOfPipeBit,
            skyViewInitialized ? AccessFlags.ShaderReadBit : AccessFlags.None,
            PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit);

        deferredPassResources.SkyView.Dispatch(commandBuffer, 0, uniformDescriptorSet, new Vector2D<uint>(256, 256));

        RecordImageBarrier(commandBuffer, resources.SkyTargets.SkyViewImage, ImageLayout.General, ImageLayout.ShaderReadOnlyOptimal,
            PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit,
            PipelineStageFlags.ComputeShaderBit | PipelineStageFlags.FragmentShaderBit, AccessFlags.ShaderReadBit);

        skyViewInitialized = true;

        var irradianceAccess = AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit;
        RecordImageBarrier(commandBuffer, resources.SkyTargets.SkyIrradianceImage, ImageLayout.General, ImageLayout.General,
            PipelineStageFlags.ComputeShaderBit, irradianceAccess,
            PipelineStageFlags.ComputeShaderBit, irradianceAccess);

        deferredPassResources.SkyIrradiance.Dispatch(commandBuffer, 0, uniformDescriptorSet, new Vector2D<uint>(16, 16));

        RecordImageBarrier(commandBuffer, resources.SkyTargets.SkyIrradianceImage, ImageLayout.General, ImageLayout.General,
            PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit,
            PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderReadBit);

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
        var terrainDescriptorSets = stackalloc DescriptorSet[2];
        terrainDescriptorSets[0] = uniformDescriptorSet;
        terrainDescriptorSets[1] = terrainDescriptorSet;
        Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, resources.PipelineLayout, 0, 2, terrainDescriptorSets, 0, null);

        ulong offset = 0;
        if (terrainMesh is { Vertices.Length: > 0, Indices.Length: > 0 } && resources.VertexBuffer.Handle != 0 && resources.IndexBuffer.Handle != 0) {
            var vertexBuffer = resources.VertexBuffer;
            Vk.CmdBindVertexBuffers(commandBuffer, 0, 1, in vertexBuffer, in offset);
            Vk.CmdBindIndexBuffer(commandBuffer, resources.IndexBuffer, 0, IndexType.Uint32);
            var visibleSections = 0;
            foreach (var region in parkRegions) {
                if (region.Terrain is { } range && region.CameraVisible) {
                    visibleSections++;
                    Vk.CmdDrawIndexed(commandBuffer, range.IndexCount, 1, range.FirstIndex, 0, 0);
                }
            }
            SectionMetrics = (visibleSections, terrainMesh.Regions.Length);
        }

        if (rideSupportMesh is { Vertices.Length: > 0, Indices.Length: > 0 }
            && resources.RideSupportGraphicsResources is { IndexCount: > 0 } supportResources) {
            var supportPipeline = ui.WireframeEnabled ? supportResources.WireframePipeline : supportResources.Pipeline;
            Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, supportPipeline);
            Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, supportResources.PipelineLayout,
                0, 1, &uniformDescriptorSet, 0, null);

            var supportVertexBuffer = supportResources.VertexBuffer;
            Vk.CmdBindVertexBuffers(commandBuffer, 0, 1, in supportVertexBuffer, in offset);
            Vk.CmdBindIndexBuffer(commandBuffer, supportResources.IndexBuffer, 0, IndexType.Uint32);

            foreach (var region in parkRegions) {
                if (region.RideSupports is { } range && region.CameraVisible)
                    Vk.CmdDrawIndexed(commandBuffer, range.IndexCount, 1, range.FirstIndex, 0, 0);
            }
        }

        if (trackMesh is { Vertices.Length: > 0, Indices.Length: > 0 }
            && resources.TrackGraphicsResources is { IndexCount: > 0 } trackResources) {
            var trackPipeline = ui.WireframeEnabled ? trackResources.WireframePipeline : trackResources.Pipeline;
            Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, trackPipeline);

            if (trackResources.UsesTrackVertexColor) {
                Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, trackResources.PipelineLayout,
                    0, 1, &uniformDescriptorSet, 0, null);
            }
            else {
                var trackDescriptorSets = stackalloc DescriptorSet[2];
                trackDescriptorSets[0] = uniformDescriptorSet;
                trackDescriptorSets[1] = trackResources.DescriptorSet;
                Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, trackResources.PipelineLayout,
                    0, 2, trackDescriptorSets, 0, null);
            }

            var trackVertexBuffer = trackResources.VertexBuffer;
            Vk.CmdBindVertexBuffers(commandBuffer, 0, 1, in trackVertexBuffer, in offset);
            Vk.CmdBindIndexBuffer(commandBuffer, trackResources.IndexBuffer, 0, IndexType.Uint32);

            foreach (var region in parkRegions) {
                if (region.Tracks is { } range && region.CameraVisible)
                    Vk.CmdDrawIndexed(commandBuffer, range.IndexCount, 1, range.FirstIndex, 0, 0);
            }
        }

        if (rideEntranceExitMesh is { Vertices.Length: > 0, Indices.Length: > 0 }
            && resources.RideEntranceGraphicsResources is { IndexCount: > 0 } rideEntranceResources) {
            var entrancePipeline = ui.WireframeEnabled ? rideEntranceResources.WireframePipeline : rideEntranceResources.Pipeline;
            Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, entrancePipeline);
            Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, rideEntranceResources.PipelineLayout,
                0, 1, &uniformDescriptorSet, 0, null);

            var entranceVertexBuffer = rideEntranceResources.VertexBuffer;
            Vk.CmdBindVertexBuffers(commandBuffer, 0, 1, in entranceVertexBuffer, in offset);
            Vk.CmdBindIndexBuffer(commandBuffer, rideEntranceResources.IndexBuffer, 0, IndexType.Uint32);

            foreach (var region in parkRegions) {
                if (region.RideEntrances is { } range && region.CameraVisible)
                    Vk.CmdDrawIndexed(commandBuffer, range.IndexCount, 1, range.FirstIndex, 0, 0);
            }
        }

        foreach (var region in parkRegions) {
            if (region.Vehicles is { } vehicleBatch && region.CameraVisible)
                vehicleBatch.Resources.DrawInstanced(commandBuffer, uniformDescriptorSet, vehicleBatch.Batch);
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

            foreach (var region in parkRegions) {
                if (region.Paths is { } range && region.CameraVisible)
                    Vk.CmdDrawIndexed(commandBuffer, range.IndexCount, 1, range.FirstIndex, 0, 0);
            }
        }

        foreach (var region in parkRegions) {
            if (!region.CameraVisible) continue;

            foreach (var batch in region.PathAccessories)
                batch.Resources.DrawInstanced(commandBuffer, uniformDescriptorSet, batch.Batch);

            foreach (var batch in region.Trees)
                batch.Resources.DrawInstanced(commandBuffer, uniformDescriptorSet, batch.Batch, ui.WireframeEnabled);
        }

        Vk.CmdEndRenderPass(commandBuffer);

        if (ui.ShadowMappingEnabled || !shadowMapInitialized)
            RecordShadowPass(commandBuffer, uniformDescriptorSet, currentFrame, terrainMesh, pathMesh, trackMesh,
                rideSupportMesh, rideEntranceExitMesh, parkRegions, shadowViewProjections, ui.ShadowMappingEnabled);

        RenderPassBeginInfo waterRenderPassInfo = new() {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = deferredTargets.WaterRenderPass,
            Framebuffer = deferredTargets.WaterFramebuffer,
            RenderArea = new Rect2D(new Offset2D(0, 0), extent),
            ClearValueCount = 1,
            PClearValues = clearValues
        };

        Vk.CmdBeginRenderPass(commandBuffer, in waterRenderPassInfo, SubpassContents.Inline);
        // The shadow pass uses its own viewport and scissor. Dynamic state persists
        // across render passes, so restore the frame-sized state before drawing water.
        Vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);
        Vk.CmdSetScissor(commandBuffer, 0, 1, &scissor);

        if (waterMesh is { Indices.Length: > 0 } && resources.WaterGraphicsResources is { IndexCount: > 0 } waterResources) {
            visibleWaterRegions.Clear();

            foreach (var region in parkRegions) {
                if (region.CameraVisible && region.Water.HasValue)
                    visibleWaterRegions.Add(region);
            }

            waterRegionDepthComparer.CameraPosition = cameraPosition;
            visibleWaterRegions.Sort(waterRegionDepthComparer);

            Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, waterResources.Pipeline);

            var waterDescriptorSets = stackalloc DescriptorSet[2];
            waterDescriptorSets[0] = uniformDescriptorSet;
            waterDescriptorSets[1] = waterResources.SkyDescriptorSet;
            Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, waterResources.PipelineLayout, 0, 2, waterDescriptorSets, 0, null);
            
            var waterVertexBuffer = waterResources.VertexBuffer;
            Vk.CmdBindVertexBuffers(commandBuffer, 0, 1, in waterVertexBuffer, in offset);
            Vk.CmdBindIndexBuffer(commandBuffer, waterResources.IndexBuffer, 0, IndexType.Uint32);

            foreach (var region in visibleWaterRegions) {
                var range = region.Water!.Value;
                Vk.CmdDrawIndexed(commandBuffer, range.IndexCount, 1, range.FirstIndex, 0, 0);
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

        if (ui.TaaEnabled)
            RecordTaa(commandBuffer, uniformDescriptorSet, renderDimensions, deferredTargets, deferredPassResources);

        RecordImageBarrier(commandBuffer, deferredTargets.SceneOutputImage,
            ImageLayout.ShaderReadOnlyOptimal, ImageLayout.General,
            PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.ComputeShaderBit,
            AccessFlags.ColorAttachmentWriteBit | AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit,
            PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit);

        deferredPassResources.Tonemap.Dispatch(commandBuffer, 0, uniformDescriptorSet, renderDimensions);

        if (ui.DebugView != 0) {
            RecordImageBarrier(commandBuffer, deferredTargets.SceneOutputImage,
                ImageLayout.General, ImageLayout.General,
                PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit,
                PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit);

            deferredPassResources.Debug.Dispatch(commandBuffer, 0, uniformDescriptorSet, renderDimensions);
        }

        RecordImageBarrier(commandBuffer, deferredTargets.SceneOutputImage,
            ImageLayout.General, ImageLayout.ShaderReadOnlyOptimal,
            PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit,
            PipelineStageFlags.FragmentShaderBit, AccessFlags.ShaderReadBit);

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
        deferredPassResources.Final.Draw(commandBuffer, 0, uniformDescriptorSet);
        resources.ImGuiRenderer?.Render(commandBuffer, imageIndex);
        Vk.CmdEndRenderPass(commandBuffer);
        Vk.EndCommandBuffer(commandBuffer);

        cameraPositionCurrent = cameraPosition;
        viewRotationCurrent = viewRotation;
        projectionCurrent = projection;
    }

    public void FrameSubmitted()
    {
        cameraPositionLast = cameraPositionCurrent;
        viewRotationLast = viewRotationCurrent;
        projectionLast = projectionCurrent;
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

    public void InvalidateShadowMap()
    {
        shadowMapInitialized = false;
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

    private void RecordShadowPass(CommandBuffer commandBuffer, DescriptorSet uniformDescriptorSet, uint currentFrame,
        TerrainMeshData? terrainMesh, PathMeshData? pathMesh, TrackMeshData? trackMesh,
        RideSupportMeshData? rideSupportMesh, RideEntranceExitMeshData? rideEntranceExitMesh,
        ParkRenderRegion[] parkRegions, Matrix4x4[] shadowViewProjections, bool renderGeometry)
    {
        var targets = resources.DeferredTargets!.ShadowTargets!;
        var shadowResources = resources.ShadowGraphicsResources!;
        RecordImageBarrier(commandBuffer, targets.Image,
            shadowMapInitialized ? ImageLayout.DepthStencilReadOnlyOptimal : ImageLayout.Undefined,
            ImageLayout.DepthStencilAttachmentOptimal,
            shadowMapInitialized ? PipelineStageFlags.ComputeShaderBit | PipelineStageFlags.FragmentShaderBit : PipelineStageFlags.TopOfPipeBit,
            shadowMapInitialized ? AccessFlags.ShaderReadBit : AccessFlags.None,
            PipelineStageFlags.EarlyFragmentTestsBit | PipelineStageFlags.LateFragmentTestsBit,
            AccessFlags.DepthStencilAttachmentWriteBit,
            ImageAspectFlags.DepthBit,
            ShadowRenderTargets.CascadeCount);

        for (var cascade = 0; cascade < ShadowRenderTargets.CascadeCount; cascade++) {
            if (renderGeometry) {
                var shadowFrustum = ViewFrustum.FromViewProjection(shadowViewProjections[cascade]);
                foreach (var region in parkRegions)
                    region.ShadowVisible = region.HasBounds && shadowFrustum.Intersects(region.Bounds);
            }

            DrawShadowCascade(commandBuffer, uniformDescriptorSet, currentFrame, terrainMesh, pathMesh, trackMesh,
                rideSupportMesh, rideEntranceExitMesh, parkRegions, targets, shadowResources, cascade, renderGeometry);
        }

        shadowMapInitialized = true;
    }

    private void DrawShadowCascade(CommandBuffer commandBuffer, DescriptorSet uniformDescriptorSet, uint currentFrame,
        TerrainMeshData? terrainMesh, PathMeshData? pathMesh, TrackMeshData? trackMesh,
        RideSupportMeshData? rideSupportMesh, RideEntranceExitMeshData? rideEntranceExitMesh,
        ParkRenderRegion[] parkRegions, ShadowRenderTargets targets, ShadowGraphicsResources shadowResources, int cascade, bool renderGeometry)
    {
        var clearValue = new ClearValue {
            DepthStencil = new ClearDepthStencilValue { Depth = 1.0f, Stencil = 0 }
        };
        var beginInfo = new RenderPassBeginInfo {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = targets.RenderPass,
            Framebuffer = targets.CascadeFramebuffers[cascade],
            RenderArea = new Rect2D(new Offset2D(0, 0), targets.Extent),
            ClearValueCount = 1,
            PClearValues = &clearValue
        };

        Vk.CmdBeginRenderPass(commandBuffer, in beginInfo, SubpassContents.Inline);
        if (!renderGeometry) {
            Vk.CmdEndRenderPass(commandBuffer);
            return;
        }
        var viewport = new Viewport {
            X = 0.0f,
            Y = 0.0f,
            Width = targets.Extent.Width,
            Height = targets.Extent.Height,
            MinDepth = 0.0f,
            MaxDepth = 1.0f
        };
        var scissor = new Rect2D {
            Offset = new Offset2D(0, 0),
            Extent = targets.Extent
        };
        Vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);
        Vk.CmdSetScissor(commandBuffer, 0, 1, &scissor);
        var shadowDescriptorSets = stackalloc DescriptorSet[3];
        shadowDescriptorSets[0] = uniformDescriptorSet;
        shadowDescriptorSets[1] = resources.ModelTextureHeap!.DescriptorSet;
        shadowDescriptorSets[2] = resources.ModelTextureHeap.SamplerDescriptorSet;
        Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, shadowResources.PipelineLayout,
            0, 3, shadowDescriptorSets, 0, null);
        var cascadeIndex = (uint)cascade;
        Vk.CmdPushConstants(commandBuffer, shadowResources.PipelineLayout, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
            sizeof(uint), sizeof(uint), &cascadeIndex);

        if (terrainMesh is { Vertices.Length: > 0, Indices.Length: > 0 } && resources.TerrainGraphicsResources is { IndexCount: > 0 } terrainResources)
            DrawShadowRegions(commandBuffer, shadowResources.TerrainPipeline, terrainResources.VertexBuffer, terrainResources.IndexBuffer,
                parkRegions, ShadowRegionCategory.Terrain);

        if (pathMesh is { Vertices.Length: > 0, Indices.Length: > 0 } && resources.PathGraphicsResources is { IndexCount: > 0 } pathResources)
            DrawShadowRegions(commandBuffer, shadowResources.TerrainPipeline, pathResources.VertexBuffer, pathResources.IndexBuffer,
                parkRegions, ShadowRegionCategory.Paths);

        if (trackMesh is { Vertices.Length: > 0, Indices.Length: > 0 } && resources.TrackGraphicsResources is { IndexCount: > 0 } trackResources)
            DrawShadowRegions(commandBuffer, shadowResources.TrackPipeline, trackResources.VertexBuffer, trackResources.IndexBuffer,
                parkRegions, ShadowRegionCategory.Tracks);

        if (rideSupportMesh is { Vertices.Length: > 0, Indices.Length: > 0 } && resources.RideSupportGraphicsResources is { IndexCount: > 0 } supportResources)
            DrawShadowRegions(commandBuffer, shadowResources.TrackPipeline, supportResources.VertexBuffer, supportResources.IndexBuffer,
                parkRegions, ShadowRegionCategory.RideSupports);

        if (rideEntranceExitMesh is { Vertices.Length: > 0, Indices.Length: > 0 } && resources.RideEntranceGraphicsResources is { IndexCount: > 0 } entranceResources)
            DrawShadowRegions(commandBuffer, shadowResources.TrackPipeline, entranceResources.VertexBuffer, entranceResources.IndexBuffer,
                parkRegions, ShadowRegionCategory.RideEntrances);

        var vehicleResources = resources.VehicleCubeGraphicsResources;
        if (vehicleResources.Length > 0)
            DrawShadowModels(commandBuffer, shadowResources.ModelPipeline, shadowResources.PipelineLayout,
                vehicleResources[(int)currentFrame], regionsByCoordinate, (uint)cascade);

        foreach (var (_, modelResources) in resources.PathAccessoryGraphicsResources)
            DrawShadowModels(commandBuffer, shadowResources.ModelPipeline, shadowResources.PipelineLayout, modelResources, regionsByCoordinate, (uint)cascade);

        foreach (var (_, modelResources) in resources.TreeSceneryGraphicsResources)
            DrawShadowModels(commandBuffer, shadowResources.ModelPipeline, shadowResources.PipelineLayout, modelResources, regionsByCoordinate, (uint)cascade);

        Vk.CmdEndRenderPass(commandBuffer);
    }

    private void DrawShadowRegions(CommandBuffer commandBuffer, Pipeline pipeline, Silk.NET.Vulkan.Buffer vertexBuffer, Silk.NET.Vulkan.Buffer indexBuffer,
        ParkRenderRegion[] regions, ShadowRegionCategory category)
    {
        Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, pipeline);
        ulong offset = 0;
        Vk.CmdBindVertexBuffers(commandBuffer, 0, 1, in vertexBuffer, in offset);
        Vk.CmdBindIndexBuffer(commandBuffer, indexBuffer, 0, IndexType.Uint32);

        foreach (var region in regions) {
            if (region.ShadowVisible && GetShadowRange(region, category) is { } range)
                Vk.CmdDrawIndexed(commandBuffer, range.IndexCount, 1, range.FirstIndex, 0, 0);
        }
    }

    private void DrawShadowModels(CommandBuffer commandBuffer, Pipeline pipeline, PipelineLayout pipelineLayout,
        ModelGraphicsResources modelResources, Dictionary<RenderRegionCoordinate, ParkRenderRegion> regionsByCoordinate, uint cascadeIndex)
    {
        if (modelResources.IndexCount == 0 || modelResources.RegionBatches.Length == 0 || modelResources.InstanceBuffer.Handle == 0)
            return;

        Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, pipeline);
        var baseTextureIndex = modelResources.BaseTextureIndex;
        Vk.CmdPushConstants(commandBuffer, pipelineLayout, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0, sizeof(uint), &baseTextureIndex);
        Vk.CmdPushConstants(commandBuffer, pipelineLayout, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, sizeof(uint), sizeof(uint), &cascadeIndex);
        var vertexBuffers = stackalloc Silk.NET.Vulkan.Buffer[2];
        vertexBuffers[0] = modelResources.VertexBuffer;
        vertexBuffers[1] = modelResources.InstanceBuffer;
        var offsets = stackalloc ulong[2];
        offsets[0] = 0;
        offsets[1] = 0;
        Vk.CmdBindVertexBuffers(commandBuffer, 0, 2, vertexBuffers, offsets);
        Vk.CmdBindIndexBuffer(commandBuffer, modelResources.IndexBuffer, 0, IndexType.Uint32);

        foreach (var batch in modelResources.RegionBatches) {
            if (batch.InstanceCount > 0
                && regionsByCoordinate.TryGetValue(batch.Region, out var region)
                && region.ShadowVisible)
                Vk.CmdDrawIndexed(commandBuffer, modelResources.IndexCount, batch.InstanceCount, 0, 0, batch.FirstInstance);
        }
    }

    private static RenderRegionIndexRange? GetShadowRange(ParkRenderRegion region, ShadowRegionCategory category) => category switch {
        ShadowRegionCategory.Terrain => region.Terrain,
        ShadowRegionCategory.Paths => region.Paths,
        ShadowRegionCategory.Tracks => region.Tracks,
        ShadowRegionCategory.RideSupports => region.RideSupports,
        ShadowRegionCategory.RideEntrances => region.RideEntrances,
        _ => null
    };

    private void EnsureRegionLookup(ParkRenderRegion[] parkRegions)
    {
        if (ReferenceEquals(cachedParkRegions, parkRegions)) return;

        regionsByCoordinate.Clear();
        foreach (var region in parkRegions)
            regionsByCoordinate.Add(region.Coordinate, region);

        cachedParkRegions = parkRegions;
    }

    private enum ShadowRegionCategory
    {
        Terrain,
        Paths,
        Tracks,
        RideSupports,
        RideEntrances
    }

    private sealed class WaterRegionDepthComparer : IComparer<ParkRenderRegion>
    {
        public Vector3 CameraPosition { get; set; }

        public int Compare(ParkRenderRegion? left, ParkRenderRegion? right)
        {
            var leftCenter = left!.Water!.Value.Bounds.Center;
            var rightCenter = right!.Water!.Value.Bounds.Center;
            return Vector3.DistanceSquared(CameraPosition, rightCenter)
                .CompareTo(Vector3.DistanceSquared(CameraPosition, leftCenter));
        }
    }

    private static (Matrix4x4[] ViewProjections, Vector4 Splits) CreateShadowViewProjections(FlightCamera camera, float aspect,
        Vector3 sunDirection, float shadowRadius, uint shadowResolution)
    {
        const float splitLambda = 0.5f;
        var nearDistance = FlightCamera.NearPlane;
        var farDistance = MathF.Min(FlightCamera.FarPlane, shadowRadius);
        var projection = camera.GetProjectionMatrix(aspect);
        var viewProjection = camera.GetViewMatrix() * projection;
        if (!Matrix4x4.Invert(viewProjection, out var inverseViewProjection))
            throw new InvalidOperationException("The camera view-projection matrix is not invertible.");

        var cameraForward = Vector3.Normalize(camera.Front);
        var rayDirections = new Vector3[4];
        var cornerIndex = 0;
        for (var y = 0; y < 2; y++) {
            for (var x = 0; x < 2; x++) {
                var clip = Vector4.Transform(new Vector4(x == 0 ? -1.0f : 1.0f, y == 0 ? -1.0f : 1.0f, 1.0f, 1.0f), inverseViewProjection);
                var farPoint = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
                rayDirections[cornerIndex++] = Vector3.Normalize(farPoint - camera.Position);
            }
        }

        var splitValues = new float[ShadowRenderTargets.CascadeCount];
        var viewProjections = new Matrix4x4[ShadowRenderTargets.CascadeCount];
        var previousSplit = nearDistance;
        for (var cascade = 0; cascade < ShadowRenderTargets.CascadeCount; cascade++) {
            var fraction = (cascade + 1.0f) / ShadowRenderTargets.CascadeCount;
            var logarithmicSplit = nearDistance * MathF.Pow(farDistance / nearDistance, fraction);
            var uniformSplit = nearDistance + (farDistance - nearDistance) * fraction;
            var split = splitLambda * logarithmicSplit + (1.0f - splitLambda) * uniformSplit;
            splitValues[cascade] = split;

            var corners = new Vector3[8];
            for (var corner = 0; corner < 4; corner++) {
                var forwardDot = Vector3.Dot(rayDirections[corner], cameraForward);
                corners[corner] = camera.Position + rayDirections[corner] * (previousSplit / forwardDot);
                corners[corner + 4] = camera.Position + rayDirections[corner] * (split / forwardDot);
            }

            viewProjections[cascade] = CreateCascadeViewProjection(corners, sunDirection, shadowRadius, shadowResolution);
            previousSplit = split;
        }

        return (viewProjections, new Vector4(splitValues[0], splitValues[1], splitValues[2], splitValues[3]));
    }

    private static Matrix4x4 CreateCascadeViewProjection(Vector3[] corners, Vector3 sunDirection, float shadowRadius, uint shadowResolution)
    {
        var center = Vector3.Zero;
        foreach (var corner in corners)
            center += corner;
        center /= corners.Length;

        var radius = 0.0f;
        foreach (var corner in corners)
            radius = MathF.Max(radius, Vector3.Distance(center, corner));
        radius = MathF.Ceiling(radius * 16.0f) / 16.0f;

        var up = MathF.Abs(Vector3.Dot(sunDirection, Vector3.UnitY)) > 0.98f ? Vector3.UnitZ : Vector3.UnitY;
        var lightPosition = center + sunDirection * (radius + shadowRadius);
        var lightView = Matrix4x4.CreateLookAt(lightPosition, center, up);
        var extent = radius * 2.0f;
        var texelSize = extent / shadowResolution;
        var nearPlane = 0.01f;
        var farPlane = (radius + shadowRadius) * 2.0f;
        var lightProjection = Matrix4x4.CreateOrthographic(extent, extent, nearPlane, farPlane);
        lightProjection.M22 *= -1.0f;
        var viewProjection = lightView * Matrix4x4.CreateScale(-1.0f, 1.0f, 1.0f) * lightProjection;
        var centerClip = Vector4.Transform(new Vector4(center, 1.0f), viewProjection);
        var centerNdc = new Vector2(centerClip.X, centerClip.Y) / centerClip.W;
        var texelNdcSize = 2.0f / shadowResolution;
        var snappedNdc = new Vector2(
            MathF.Round(centerNdc.X / texelNdcSize) * texelNdcSize,
            MathF.Round(centerNdc.Y / texelNdcSize) * texelNdcSize);
        viewProjection.M41 += snappedNdc.X - centerNdc.X;
        viewProjection.M42 += snappedNdc.Y - centerNdc.Y;
        return viewProjection;
    }

    private void RecordImageBarrier(CommandBuffer commandBuffer, Image image, ImageLayout oldLayout, ImageLayout newLayout,
        PipelineStageFlags sourceStage, AccessFlags sourceAccess, PipelineStageFlags destinationStage, AccessFlags destinationAccess,
        ImageAspectFlags aspect = ImageAspectFlags.ColorBit, uint layerCount = 1)
    {
        var barrier = new ImageMemoryBarrier {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange(aspect, 0, 1, 0, layerCount),
            SrcAccessMask = sourceAccess,
            DstAccessMask = destinationAccess
        };

        Vk.CmdPipelineBarrier(commandBuffer, sourceStage, destinationStage, 0, 0, null, 0, null, 1, &barrier);
    }

    private void CalculateSunDirection(out float sunElevation, out Vector3 sunDirection)
    {
        var timeOfDayHours = ui.EffectiveTimeOfDayHours % 24.0f;
        if (timeOfDayHours < 0.0f) timeOfDayHours += 24.0f;

        var timeAngle = (timeOfDayHours - 6.0f) * (MathF.Tau / 24.0f);
        var sunAzimuth = ui.AzimuthDegrees * (MathF.PI / 180.0f);
        var zenithAngle = ui.ZenithDegrees * (MathF.PI / 180.0f);
        var sunriseDirection = new Vector3(MathF.Cos(sunAzimuth), 0.0f, MathF.Sin(sunAzimuth));

        var orbitDirection = new Vector3(
            -MathF.Sin(sunAzimuth) * MathF.Sin(zenithAngle),
            MathF.Cos(zenithAngle),
            MathF.Cos(sunAzimuth) * MathF.Sin(zenithAngle));

        sunDirection = Vector3.Normalize(sunriseDirection * MathF.Cos(timeAngle) + orbitDirection * MathF.Sin(timeAngle));
        sunElevation = sunDirection.Y;
    }
}
