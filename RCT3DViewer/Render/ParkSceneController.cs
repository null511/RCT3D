using RCT3D.Render.Paths;
using RCT3D.Render.Paths.Meshing;
using RCT3D.Render.Terrain;
using RCT3D.Simulation;
using System.Numerics;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Entrances;
using RCT3D.Render.Rides;
using RCT3D.Interop.Models;
using RCT3D.Render.Scenery;
using Serilog;

namespace RCT3D.Render;

internal sealed class ParkSceneController(
    SimulationBridge simulation,
    VulkanRenderResources resources,
    ViewerUiController ui,
    FlightCamera camera,
    FrameRenderer frameRenderer) : IDisposable
{
    private readonly TerrainMeshBuilder terrainMeshBuilder = new(resources.TerrainMaterials!);
    private readonly PathMeshBuilder pathMeshBuilder = new(resources.PathMaterials!);
    private readonly PathAccessoryMeshBuilder pathAccessoryMeshBuilder = new(resources.PathAccessoryAssets!);
    private readonly TreeSceneryMeshBuilder treeSceneryMeshBuilder = new(resources.TreeSceneryAssets!);
    private readonly TrackMeshBuilder trackMeshBuilder = new();
    private readonly RideEntranceExitMeshBuilder rideEntranceExitMeshBuilder = new();

    private Task<ParkLoadResult>? parkLoadTask;
    private ParkLoadResult? pendingParkLoadResult;
    private string parkLoadProgress = string.Empty;
    private int loadedMapWidth;
    private int loadedMapHeight;
    private Dictionary<(int TileX, int TileY), RCTTrackElementData[]> trackElementsByTile = new();
    private Dictionary<ushort, float> carVerticalOffsetsByRide = new();
    private HashSet<ushort> smoothMovementRideIds = [];
    private TrackCenterlinePiece[] rideCenterlines = [];
    private readonly Dictionary<RenderRegionCoordinate, ParkRenderRegion> renderRegionsByCoordinate = new();
    private bool isDisposed;

    public TerrainMeshData? TerrainMesh { get; private set; }
    public PathMeshData? PathMesh { get; private set; }
    public WaterMeshData? WaterMesh { get; private set; }
    public TrackMeshData? TrackMesh { get; private set; }
    public RideSupportMeshData? RideSupportMesh { get; private set; }
    public RideEntranceExitMeshData? RideEntranceExitMesh { get; private set; }
    public ParkRenderRegion[] RenderRegions { get; private set; } = [];
    public PathNavigation? Navigation { get; private set; }
    public RCTRideEntranceData[] RideEntrances { get; private set; } = [];
    internal SimulationBridge Simulation => simulation;
    public float? GameTimeOfDayHours => simulation.GameTimeOfDayHours;
    public bool? IsPaused => simulation.IsPaused;

    public void TogglePause() => simulation.TogglePause();

    public IReadOnlyList<RCTTrackElementData> GetTrackElementsAtTile(int tileX, int tileY) =>
        trackElementsByTile.TryGetValue((tileX, tileY), out var elements) ? elements : Array.Empty<RCTTrackElementData>();

    public float GetCarVerticalOffset(ushort rideId) =>
        carVerticalOffsetsByRide.TryGetValue(rideId, out var offset) ? offset : 0.0f;

    public bool UsesSmoothMovement(ushort rideId) =>
        SmoothMovementSettings.EnableSmoothMovement && smoothMovementRideIds.Contains(rideId);

    public bool TryGetCenterlinePose(
        in RCTVehicleData previous,
        in RCTVehicleData current,
        float amount,
        out Vector3 position,
        out TrackCenterlineFrame frame) =>
        TrackCenterlinePoseResolver.TryInterpolate(previous, current, amount, rideCenterlines, out position, out frame);


    public void LoadPark(string parkPath, float terrainSkirtFloor)
    {
        if (ui.IsParkLoading || parkLoadTask is not null || pendingParkLoadResult is not null) return;

        treeSceneryMeshBuilder.ClearCaches();
        ui.BeginParkLoad(parkPath);
        Volatile.Write(ref parkLoadProgress, "Loading park file...");
        parkLoadTask = BuildParkMeshesAsync(parkPath, terrainSkirtFloor);
    }

    private async Task<ParkLoadResult> BuildParkMeshesAsync(string parkPath, float terrainSkirtFloor)
    {
        try {
            var snapshot = await simulation.LoadParkAsync(parkPath, progress => Volatile.Write(ref parkLoadProgress, progress));
            Volatile.Write(ref parkLoadProgress, "Generating terrain, water, path, accessory, tree, track, support, and station meshes...");

            var meshData = await Task.Run(() => {
                var terrain = terrainMeshBuilder.Build(snapshot.SurfaceData, snapshot.Width, snapshot.Height, terrainSkirtFloor);
                var tracks = trackMeshBuilder.Build(snapshot.TrackData, snapshot.PathData, snapshot.RideEntranceData, snapshot.SurfaceData, snapshot.Width, snapshot.Height, snapshot.PaletteData);
                return (
                    Terrain: terrain,
                    Water: WaterMeshData.Combine(terrain.Water, tracks.Water),
                    Paths: pathMeshBuilder.Build(snapshot.PathData, snapshot.SurfaceData, snapshot.Width, snapshot.Height),
                    Navigation: new PathNavigation(snapshot.PathData),
                    PathAccessories: pathAccessoryMeshBuilder.Build(snapshot.PathData, snapshot.Width, snapshot.Height),
                    Trees: treeSceneryMeshBuilder.Build(snapshot.TreeData, snapshot.Width, snapshot.Height),
                    Tracks: tracks,
                    RideEntrances: rideEntranceExitMeshBuilder.Build(snapshot.RideEntranceData, snapshot.Width, snapshot.Height));
            });

            return new ParkLoadResult(
                parkPath, snapshot.Width, snapshot.Height,
                meshData.Terrain.Terrain, meshData.Water, meshData.Paths, meshData.Navigation, meshData.PathAccessories,
                meshData.Tracks.Tracks, meshData.Tracks.RideSupports, meshData.RideEntrances, meshData.Trees, meshData.Tracks.TrackElements, snapshot.RideEntranceData,
                meshData.Tracks.Centerlines);
        }
        catch (Exception ex) {
            throw new InvalidOperationException($"Failed to load '{parkPath}': {ex.Message}", ex);
        }
    }

    public void Update(uint currentFrame)
    {
        var loadCompletedThisFrame = false;

        if (parkLoadTask is { IsCompleted: true } completedTask) {
            parkLoadTask = null;

            try {
                pendingParkLoadResult = completedTask.GetAwaiter().GetResult();
                ui.SetParkLoadStatus("Uploading terrain, water, path, accessory, tree, track, support, and station meshes...");
                loadCompletedThisFrame = true;
            }
            catch (Exception ex) {
                ClearParkState(ex.Message);
            }
        }

        if (!loadCompletedThisFrame && pendingParkLoadResult is { } result) {
            try {
                if (resources.Vk!.DeviceWaitIdle(resources.LogicalDevice) != Silk.NET.Vulkan.Result.Success)
                    throw new InvalidOperationException("Could not wait for the Vulkan device before replacing terrain buffers.");

                resources.TerrainGraphicsResources!.ClearMesh();
                resources.TerrainGraphicsResources.UploadMesh(result.Terrain);
                resources.PathGraphicsResources!.ClearMesh();
                resources.PathGraphicsResources.UploadMesh(result.Paths);
                resources.WaterGraphicsResources!.UploadMesh(result.Water);
                resources.CreateTrackGraphicsResources();
                resources.TrackGraphicsResources!.UploadMesh(result.Tracks);
                resources.CreateRideSupportGraphicsResources();
                resources.RideSupportGraphicsResources!.UploadMesh(result.RideSupports);
                resources.CreateRideEntranceGraphicsResources();
                resources.RideEntranceGraphicsResources!.UploadMesh(result.RideEntrances);
                DisposePathAccessoryResources();
                DisposeTreeSceneryResources();
                resources.ModelTextureHeap!.ResetParkTextures();

                foreach (var batch in result.PathAccessories) {
                    var accessoryResources = new ModelGraphicsResources(resources.DeviceContext!, resources.DeferredTargets!.GBufferRenderPass,
                        resources.UniformBufferResources!.DescriptorSetLayout, batch.Material, resources.ModelTextureHeap!);

                    try {
                        accessoryResources.CreatePipeline();
                        accessoryResources.CreateTextureResources(resources.CommandPool);
                        accessoryResources.UploadMesh(batch.Mesh);
                        accessoryResources.UploadInstances(batch.Regions);
                        resources.PathAccessoryGraphicsResources.Add((batch, accessoryResources));
                    }
                    catch {
                        accessoryResources.Dispose();
                        throw;
                    }
                }

                foreach (var batch in result.Trees) {
                    var treeResources = new ModelGraphicsResources(resources.DeviceContext!, resources.DeferredTargets!.GBufferRenderPass,
                        resources.UniformBufferResources!.DescriptorSetLayout, batch.Material, resources.ModelTextureHeap!, createWireframePipeline: true);

                    try {
                        treeResources.CreatePipeline();
                        treeResources.CreateTextureResources(resources.CommandPool);
                        treeResources.UploadMesh(batch.Mesh);
                        treeResources.UploadInstances(batch.Regions);
                        resources.TreeSceneryGraphicsResources.Add((batch, treeResources));
                    }
                    catch {
                        treeResources.Dispose();
                        throw;
                    }
                }

                TerrainMesh = result.Terrain;
                PathMesh = result.Paths;
                Navigation = result.Navigation;
                WaterMesh = result.Water;
                TrackMesh = result.Tracks;
                RideSupportMesh = result.RideSupports;
                RideEntranceExitMesh = result.RideEntrances;
                RideEntrances = result.RideEntranceData;
                rideCenterlines = result.Centerlines;
                trackElementsByTile = result.TrackElements
                    .GroupBy(track => (track.TileX, track.TileY))
                    .ToDictionary(group => group.Key, group => group.ToArray());

                var carOffsetsByRideType = TrackRideConfigurations.All
                    .ToDictionary(configuration => configuration.RideType, configuration => configuration.CenterlineHeightOffset + configuration.CarVerticalOffset);

                carVerticalOffsetsByRide = result.TrackElements
                    .Where(track => carOffsetsByRideType.ContainsKey(track.RideType))
                    .GroupBy(track => track.RideId)
                    .ToDictionary(group => group.Key, group => carOffsetsByRideType[group.First().RideType]);

                var smoothRideTypes = TrackRideConfigurations.All
                    .Where(configuration => configuration.SmoothMovement)
                    .Select(configuration => configuration.RideType)
                    .ToHashSet();

                smoothMovementRideIds = result.TrackElements
                    .Where(track => smoothRideTypes.Contains(track.RideType))
                    .Select(track => track.RideId)
                    .ToHashSet();

                loadedMapWidth = result.Width;
                loadedMapHeight = result.Height;
                RebuildRenderRegions();

                camera.Position = new Vector3(result.Width * 1.5f, 120.0f, -result.Height * 1.5f);
                frameRenderer.InvalidateHistory();
                pendingParkLoadResult = null;

                ui.CompleteParkLoad(result.ParkPath);
            }
            catch (Exception ex) {
                ClearParkState($"Failed to upload meshes for '{result.ParkPath}': {ex.Message}");
            }
        }

        if (parkLoadTask is not null)
            ui.SetParkLoadStatus(Volatile.Read(ref parkLoadProgress));

        UpdateVehicleCubeInstances(currentFrame);
    }

    private void ClearParkState(string failure)
    {
        TerrainMesh = null;
        PathMesh = null;
        WaterMesh = null;
        TrackMesh = null;
        RideSupportMesh = null;
        RideEntranceExitMesh = null;
        Navigation = null;
        RideEntrances = [];
        trackElementsByTile.Clear();
        carVerticalOffsetsByRide.Clear();
        smoothMovementRideIds.Clear();
        rideCenterlines = [];
        renderRegionsByCoordinate.Clear();
        RenderRegions = [];
        loadedMapWidth = 0;
        loadedMapHeight = 0;
        treeSceneryMeshBuilder.ClearCaches();
        frameRenderer.InvalidateHistory();
        pendingParkLoadResult = null;
        parkLoadTask = null;

        try {
            if (resources.Vk is not null && resources.LogicalDevice.Handle != 0) {
                if (resources.Vk.DeviceWaitIdle(resources.LogicalDevice) != Silk.NET.Vulkan.Result.Success)
                    throw new InvalidOperationException("Could not wait for the Vulkan device before clearing park meshes.");

                DisposePathAccessoryResources();
                DisposeTreeSceneryResources();
                resources.ModelTextureHeap!.ResetParkTextures();
                resources.TerrainGraphicsResources?.ClearMesh();
                resources.PathGraphicsResources?.ClearMesh();
                resources.WaterGraphicsResources?.ClearMesh();
                resources.TrackGraphicsResources?.ClearMesh();
                resources.RideSupportGraphicsResources?.ClearMesh();
                resources.RideEntranceGraphicsResources?.ClearMesh();
                ClearVehicleCubeInstances();
            }
        }
        catch (Exception clearException) {
            failure += $" Park graphics could not be fully cleared: {clearException.Message}";
        }

        ui.ClearParkAfterLoadFailure(failure);
    }

    private void RebuildRenderRegions()
    {
        renderRegionsByCoordinate.Clear();

        if (TerrainMesh is { } terrain) {
            foreach (var range in terrain.Regions) {
                if (range.IsEmpty) continue;
                var region = GetOrCreateRenderRegion(range.Region);
                region.Terrain = range;
                region.IncludeStaticBounds(range.Bounds);
            }
        }

        if (WaterMesh is { } water) {
            foreach (var range in water.Regions) {
                if (range.IsEmpty) continue;
                var region = GetOrCreateRenderRegion(range.Region);
                region.Water = range;
                region.IncludeStaticBounds(range.Bounds);
            }
        }

        if (PathMesh is { } paths) {
            foreach (var range in paths.Regions) {
                if (range.IsEmpty) continue;
                var region = GetOrCreateRenderRegion(range.Region);
                region.Paths = range;
                region.IncludeStaticBounds(range.Bounds);
            }
        }

        if (TrackMesh is { } tracks) {
            foreach (var range in tracks.Regions) {
                if (range.IsEmpty) continue;
                var region = GetOrCreateRenderRegion(range.Region);
                region.Tracks = range;
                region.IncludeStaticBounds(range.Bounds);
            }
        }

        if (RideSupportMesh is { } supports) {
            foreach (var range in supports.Regions) {
                if (range.IsEmpty) continue;
                var region = GetOrCreateRenderRegion(range.Region);
                region.RideSupports = range;
                region.IncludeStaticBounds(range.Bounds);
            }
        }

        if (RideEntranceExitMesh is { } entrances) {
            foreach (var range in entrances.Regions) {
                if (range.IsEmpty) continue;
                var region = GetOrCreateRenderRegion(range.Region);
                region.RideEntrances = range;
                region.IncludeStaticBounds(range.Bounds);
            }
        }

        foreach (var (_, modelResources) in resources.PathAccessoryGraphicsResources) {
            foreach (var batch in modelResources.RegionBatches) {
                if (batch.InstanceCount == 0) continue;
                var region = GetOrCreateRenderRegion(batch.Region);
                region.PathAccessories.Add(new RenderRegionModelBatch(modelResources, batch));
                region.IncludeStaticBounds(batch.Bounds);
            }
        }

        foreach (var (_, modelResources) in resources.TreeSceneryGraphicsResources) {
            foreach (var batch in modelResources.RegionBatches) {
                if (batch.InstanceCount == 0) continue;
                var region = GetOrCreateRenderRegion(batch.Region);
                region.Trees.Add(new RenderRegionModelBatch(modelResources, batch));
                region.IncludeStaticBounds(batch.Bounds);
            }
        }

        RenderRegions = renderRegionsByCoordinate.Values.ToArray();
    }

    private ParkRenderRegion GetOrCreateRenderRegion(RenderRegionCoordinate coordinate)
    {
        if (!renderRegionsByCoordinate.TryGetValue(coordinate, out var region)) {
            region = new ParkRenderRegion(coordinate);
            renderRegionsByCoordinate.Add(coordinate, region);
        }

        return region;
    }

    private void UpdateVehicleCubeInstances(uint currentFrame)
    {
        var vehicleResources = resources.VehicleCubeGraphicsResources;
        foreach (var region in RenderRegions)
            region.ClearDynamicContents();

        if (vehicleResources.Length == 0) return;

        var snapshots = parkLoadTask is null && pendingParkLoadResult is null
            ? simulation.VehicleSnapshots : null;

        var instances = VehicleCubeRenderer.BuildInstances(
            snapshots, loadedMapWidth, loadedMapHeight, carVerticalOffsetsByRide, UsesSmoothMovement, TryGetCenterlinePose);
        var currentVehicleResources = vehicleResources[(int)currentFrame];
        currentVehicleResources.UploadInstances(instances);

        var regionsChanged = false;
        foreach (var batch in currentVehicleResources.RegionBatches) {
            if (batch.InstanceCount == 0) continue;

            if (!renderRegionsByCoordinate.TryGetValue(batch.Region, out var region)) {
                region = GetOrCreateRenderRegion(batch.Region);
                regionsChanged = true;
            }

            region.Vehicles = new RenderRegionModelBatch(currentVehicleResources, batch);
            region.IncludeDynamicBounds(batch.Bounds);
        }

        if (regionsChanged)
            RenderRegions = renderRegionsByCoordinate.Values.ToArray();
    }

    private void ClearVehicleCubeInstances()
    {
        foreach (var resources1 in resources.VehicleCubeGraphicsResources)
            resources1.UploadInstances(Array.Empty<ModelRegionBatch>());
    }

    private void DisposePathAccessoryResources()
    {
        foreach (var (_, resources1) in resources.PathAccessoryGraphicsResources)
            resources1.Dispose();

        resources.PathAccessoryGraphicsResources.Clear();
    }

    private void DisposeTreeSceneryResources()
    {
        foreach (var (_, resources1) in resources.TreeSceneryGraphicsResources)
            resources1.Dispose();

        resources.TreeSceneryGraphicsResources.Clear();
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        try {
            parkLoadTask?.GetAwaiter().GetResult();
        }
        catch {
            // ignore
        }

        parkLoadTask = null;
        pendingParkLoadResult = null;

        try {
            simulation.Dispose();
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to shut down OpenRCT2");
        }
    }

    private sealed record ParkLoadResult(
        string ParkPath, int Width, int Height, TerrainMeshData Terrain, WaterMeshData Water, PathMeshData Paths,
        PathNavigation Navigation, PathAccessoryBatch[] PathAccessories, TrackMeshData Tracks, RideSupportMeshData RideSupports,
        RideEntranceExitMeshData RideEntrances, TreeSceneryBatch[] Trees, RCTTrackElementData[] TrackElements, RCTRideEntranceData[] RideEntranceData,
        TrackCenterlinePiece[] Centerlines);
}
