using RCT3D.Interop.Models;
using RCT3D.Render.Paths;
using RCT3D.Render.Paths.Meshing;
using RCT3D.Render.Rides;
using RCT3D.Render.Rides.Entrances;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Scenery;
using RCT3D.Render.Terrain;
using RCT3D.Simulation;
using Serilog;
using System.Diagnostics;
using System.Numerics;

namespace RCT3D.Render.Park;

internal sealed class ParkSceneController(
    SimulationBridge simulation,
    ParkRenderResources resources,
    ViewerUiController ui,
    FlightCamera camera,
    FrameRenderer frameRenderer) : IDisposable
{
    private readonly TerrainMeshBuilder terrainMeshBuilder = new(resources.TerrainMaterials!);
    private readonly PathMeshBuilder pathMeshBuilder = new(resources.PathMaterials!);
    private readonly PathAccessoryMeshBuilder pathAccessoryMeshBuilder = new(resources.PathAccessoryAssets!);
    private readonly TreeSceneryMeshBuilder treeSceneryMeshBuilder = new(resources.TreeSceneryAssets!);
    private readonly TrackMeshBuilder trackMeshBuilder = new();
    private readonly TunnelLayoutBuilder tunnelLayoutBuilder = new();
    private readonly RideEntranceModelBuilder rideEntranceModelBuilder = new();
    private readonly VehiclePoseResolver vehiclePoseResolver = new();
    private readonly VehicleRenderer vehicleRenderer = new();

    private Func<ushort, bool>? usesSmoothMovement;
    private TryGetCenterlinePoseDelegate? tryGetCenterlinePose;

    private Task<ParkLoadResult>? parkLoadTask;
    private ParkLoadResult? pendingParkLoadResult;
    private Task<VehicleModel[]>? vehicleModelLoadTask;
    private string parkLoadProgress = string.Empty;
    private int loadedMapWidth;
    private int loadedMapHeight;
    private Dictionary<(int TileX, int TileY), RCTTrackElementData[]> trackElementsByTile = new();
    private RCTPaletteEntry[] vehiclePalette = [];
    private Dictionary<ushort, VehiclePose> vehiclePoses = new();
    private HashSet<ushort> smoothMovementRideIds = [];
    private TrackCenterlinePiece[] rideCenterlines = [];
    private TrackMeshData? vehiclePathMesh;
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
    public bool IsReady { get; private set; }
    public string? LoadError { get; private set; }

    public void TogglePause() => simulation.TogglePause();

    public IReadOnlyList<RCTTrackElementData> GetTrackElementsAtTile(int tileX, int tileY) =>
        trackElementsByTile.TryGetValue((tileX, tileY), out var elements) ? elements : Array.Empty<RCTTrackElementData>();

    public bool TryGetVehiclePose(ushort entityId, ushort rideId, out VehiclePose pose) =>
        vehiclePoses.TryGetValue(entityId, out pose) && pose.RideId == rideId;

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
        if (parkLoadTask is not null || pendingParkLoadResult is not null)
            throw new InvalidOperationException("A park load is already in progress.");

        CompleteVehicleModelLoad(upload: false);
        IsReady = false;
        LoadError = null;
        ResetVehicleState();
        vehiclePalette = [];
        treeSceneryMeshBuilder.ClearCaches();
        ui.BeginParkLoad(parkPath);
        Volatile.Write(ref parkLoadProgress, "Loading park file...");
        parkLoadTask = BuildParkMeshesAsync(parkPath, terrainSkirtFloor);
    }

    private async Task<ParkLoadResult> BuildParkMeshesAsync(string parkPath, float terrainSkirtFloor)
    {
        try {
            var snapshot = await simulation.LoadParkAsync(parkPath, progress => Volatile.Write(ref parkLoadProgress, progress));
            var vehicles = simulation.VehicleSnapshots?.Current.Vehicles ?? ReadOnlyMemory<RCTVehicleData>.Empty;
            Volatile.Write(ref parkLoadProgress, "Generating terrain, tunnel, water, path, accessory, tree, track, support, and station meshes...");

            var meshData = await Task.Run(() => {
                var occupancy = new MapOccupancyMap(snapshot.SurfaceData, snapshot.Width, snapshot.Height);

                foreach (var obstacle in snapshot.ObstacleData)
                    occupancy.AddObstacle(obstacle);

                var tracks = trackMeshBuilder.Build(snapshot.TrackData, snapshot.PathData, snapshot.RideEntranceData,
                    snapshot.SurfaceData, occupancy, snapshot.Width, snapshot.Height, snapshot.PaletteData);

                var tunnelLayout = tunnelLayoutBuilder.Build(occupancy, terrainSkirtFloor);

                var terrain = terrainMeshBuilder.Build(snapshot.SurfaceData, snapshot.Width, snapshot.Height, terrainSkirtFloor, tunnelLayout);

                var rideEntranceExitMeshBuilder = new RideEntranceExitMeshBuilder(snapshot.Width, snapshot.Height);

                return (
                    Terrain: terrain,
                    Water: WaterMeshData.Combine(terrain.Water, tracks.Water),
                    Paths: pathMeshBuilder.Build(snapshot.PathData, snapshot.SurfaceData, occupancy, snapshot.Width, snapshot.Height),
                    Navigation: new PathNavigation(snapshot.PathData),
                    PathAccessories: pathAccessoryMeshBuilder.Build(snapshot.PathData, snapshot.Width, snapshot.Height),
                    Trees: treeSceneryMeshBuilder.Build(snapshot.TreeData, snapshot.Width, snapshot.Height),
                    Tracks: tracks,
                    Vehicles: vehicles.ToArray()
                        .Where(vehicle => RideVehicleDefinitions.Resolve(vehicle).ShouldRender(vehicle))
                        .Select(vehicle => RideVehicleDefinitions.Resolve(vehicle)).Distinct()
                        .Select(VehicleModel.Load).ToArray(),
                    RideEntrances: rideEntranceExitMeshBuilder.Build(snapshot.RideEntranceData, snapshot.TrackData),
                    RideEntranceModels: rideEntranceModelBuilder.Build(snapshot.RideEntranceData, snapshot.Width, snapshot.Height));
            });

            return new ParkLoadResult(
                parkPath, snapshot.Width, snapshot.Height,
                meshData.Terrain.Terrain, meshData.Water, meshData.Paths, meshData.Navigation, meshData.PathAccessories,
                meshData.Tracks.Tracks, meshData.Tracks.RideSupports, meshData.RideEntrances, meshData.RideEntranceModels, meshData.Trees, meshData.Tracks.TrackElements, snapshot.RideEntranceData,
                meshData.Tracks.Centerlines, snapshot.PaletteData, meshData.Vehicles);
        }
        catch (Exception ex) {
            throw new InvalidOperationException($"Failed to load '{parkPath}': {ex.Message}", ex);
        }
    }

    public void Update()
    {
        var loadCompletedThisFrame = false;

        if (parkLoadTask is { IsCompleted: true } completedTask) {
            parkLoadTask = null;

            try {
                pendingParkLoadResult = completedTask.GetAwaiter().GetResult();
                ui.SetParkLoadStatus("Uploading terrain, tunnel, water, path, accessory, tree, track, support, and station meshes...");
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
                vehiclePathMesh = VehiclePathDebugRenderer.Build(simulation.VehiclePathSamples.Span);
                resources.CreateVehiclePathGraphicsResources();
                resources.VehiclePathGraphicsResources!.UploadMesh(vehiclePathMesh);
                DisposePathAccessoryResources();
                DisposeTreeSceneryResources();
                DisposeRideEntranceModelResources();
                resources.DisposeVehicleResources();
                vehicleRenderer.Clear();
                resources.ModelTextureHeap!.ResetParkTextures();

                foreach (var model in result.Vehicles)
                    resources.CreateVehicleResources(vehicleRenderer.AddModel(model));

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

                foreach (var batch in result.RideEntranceModels) {
                    var modelResources = new ModelGraphicsResources(resources.DeviceContext!, resources.DeferredTargets!.GBufferRenderPass,
                        resources.UniformBufferResources!.DescriptorSetLayout, batch.Mesh.Material, resources.ModelTextureHeap!, createWireframePipeline: true);

                    try {
                        modelResources.CreatePipeline();
                        modelResources.CreateTextureResources(resources.CommandPool);
                        modelResources.UploadMesh(batch.Mesh);
                        modelResources.UploadInstances(batch.Regions);
                        resources.RideEntranceModelResources.Add(modelResources);
                    }
                    catch {
                        modelResources.Dispose();
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
                vehiclePalette = result.PaletteData;
                trackElementsByTile = result.TrackElements
                    .GroupBy(track => (track.TileX, track.TileY))
                    .ToDictionary(group => group.Key, group => group.ToArray());

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
                IsReady = true;
            }
            catch (Exception ex) {
                ClearParkState($"Failed to upload meshes for '{result.ParkPath}': {ex.Message}");
            }
        }

        if (parkLoadTask is not null)
            ui.SetParkLoadStatus(Volatile.Read(ref parkLoadProgress));

        PrepareVehiclePoses();
    }

    private void ClearParkState(string failure)
    {
        IsReady = false;
        TerrainMesh = null;
        PathMesh = null;
        WaterMesh = null;
        TrackMesh = null;
        RideSupportMesh = null;
        RideEntranceExitMesh = null;
        Navigation = null;
        RideEntrances = [];
        trackElementsByTile.Clear();
        vehiclePalette = [];
        ResetVehicleState();
        smoothMovementRideIds.Clear();
        rideCenterlines = [];
        vehiclePathMesh = null;
        renderRegionsByCoordinate.Clear();
        RenderRegions = [];
        loadedMapWidth = 0;
        loadedMapHeight = 0;
        treeSceneryMeshBuilder.ClearCaches();
        frameRenderer.InvalidateHistory();
        pendingParkLoadResult = null;
        parkLoadTask = null;

        try {
            CompleteVehicleModelLoad(upload: false);
            if (resources.Vk is not null && resources.LogicalDevice.Handle != 0) {
                if (resources.Vk.DeviceWaitIdle(resources.LogicalDevice) != Silk.NET.Vulkan.Result.Success)
                    throw new InvalidOperationException("Could not wait for the Vulkan device before clearing park meshes.");

                DisposePathAccessoryResources();
                DisposeTreeSceneryResources();
                DisposeRideEntranceModelResources();
                resources.DisposeVehicleResources();
                vehicleRenderer.Clear();
                resources.ModelTextureHeap!.ResetParkTextures();
                resources.TerrainGraphicsResources?.ClearMesh();
                resources.PathGraphicsResources?.ClearMesh();
                resources.WaterGraphicsResources?.ClearMesh();
                resources.TrackGraphicsResources?.ClearMesh();
                resources.VehiclePathGraphicsResources?.ClearMesh();
                resources.RideSupportGraphicsResources?.ClearMesh();
                resources.RideEntranceGraphicsResources?.ClearMesh();
                ClearVehicleInstances();
            }
        }
        catch (Exception clearException) {
            failure += $" Park graphics could not be fully cleared: {clearException.Message}";
        }

        ui.ClearParkAfterLoadFailure(failure);
        LoadError = failure;
    }

    private void RebuildRenderRegions()
    {
        ResetVehicleState();
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

            foreach (var range in paths.SupportRegions) {
                if (range.IsEmpty) continue;
                var region = GetOrCreateRenderRegion(range.Region);
                region.PathSupports = range;
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

        if (vehiclePathMesh is { } vehiclePaths) {
            foreach (var range in vehiclePaths.Regions) {
                if (range.IsEmpty) continue;
                var region = GetOrCreateRenderRegion(range.Region);
                region.VehiclePaths = range;
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

        foreach (var modelResources in resources.RideEntranceModelResources) {
            foreach (var batch in modelResources.RegionBatches) {
                if (batch.InstanceCount == 0) continue;
                var region = GetOrCreateRenderRegion(batch.Region);
                region.RideEntranceModels.Add(new RenderRegionModelBatch(modelResources, batch));
                region.IncludeStaticBounds(batch.Bounds);
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

    private void PrepareVehiclePoses()
    {
        foreach (var previousRegion in vehicleRenderer.Regions.Keys) {
            if (renderRegionsByCoordinate.TryGetValue(previousRegion, out var region))
                region.ClearDynamicContents();
        }

        var snapshots = IsReady && parkLoadTask is null && pendingParkLoadResult is null
            ? simulation.VehicleSnapshots : null;

        vehiclePoses = vehiclePoseResolver.ResolveFrame(
            snapshots, Stopwatch.GetTimestamp(),
            usesSmoothMovement ??= UsesSmoothMovement, tryGetCenterlinePose ??= TryGetCenterlinePose);

        try {
            if (vehicleModelLoadTask is { IsCompleted: true })
                CompleteVehicleModelLoad(upload: true);

            if (IsReady && vehicleModelLoadTask is null) {
                var missingDefinitions = vehiclePoses.Values
                    .Where(pose => pose.IsVisible && !vehicleRenderer.HasModel(pose.Definition))
                    .Select(pose => pose.Definition).Distinct().ToArray();
                if (missingDefinitions.Length > 0)
                    vehicleModelLoadTask = Task.Run(() => missingDefinitions.Select(VehicleModel.Load).ToArray());
            }
        }
        catch (Exception ex) {
            ClearParkState($"Failed to prepare vehicle models: {ex.Message}");
            return;
        }

        vehicleRenderer.PrepareRegions(vehiclePoses, loadedMapWidth, loadedMapHeight);
        var previousRegionCount = renderRegionsByCoordinate.Count;
        foreach (var (coordinate, bounds) in vehicleRenderer.Regions) {
            var region = GetOrCreateRenderRegion(coordinate);
            region.IncludeDynamicBounds(bounds);
        }

        if (renderRegionsByCoordinate.Count != previousRegionCount)
            RenderRegions = renderRegionsByCoordinate.Values.ToArray();
    }

    private void CompleteVehicleModelLoad(bool upload)
    {
        if (vehicleModelLoadTask is not { } task) return;
        vehicleModelLoadTask = null;
        var models = task.GetAwaiter().GetResult();
        if (!upload) return;

        if (resources.Vk!.DeviceWaitIdle(resources.LogicalDevice) != Silk.NET.Vulkan.Result.Success)
            throw new InvalidOperationException("Could not wait for the Vulkan device before uploading vehicle models.");

        foreach (var model in models)
            resources.CreateVehicleResources(vehicleRenderer.AddModel(model));
    }

    public void UpdateVehicleInstances(uint currentFrame)
    {
        foreach (var (model, meshes) in resources.VehicleGraphicsResources) {
            model.BuildInstances(renderRegionsByCoordinate, vehiclePalette);
            foreach (var mesh in meshes) {
                mesh.UploadInstances(model.Instances, model.RegionBatches, currentFrame);
                foreach (var batch in mesh.RegionBatches)
                    renderRegionsByCoordinate[batch.Region].Vehicles.Add(new RenderRegionModelBatch(mesh, batch));
            }
        }
    }

    private void ResetVehicleState()
    {
        foreach (var vehicleRegion in vehicleRenderer.Regions.Keys) {
            if (renderRegionsByCoordinate.TryGetValue(vehicleRegion, out var region))
                region.ClearDynamicContents();
        }

        vehiclePoseResolver.Clear();
        vehiclePoses.Clear();
        vehicleRenderer.ClearInstances();
        ClearVehicleInstances();
    }

    private void ClearVehicleInstances()
    {
        foreach (var (_, meshes) in resources.VehicleGraphicsResources)
        foreach (var resources1 in meshes)
            resources1.ClearInstances();
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

    private void DisposeRideEntranceModelResources()
    {
        foreach (var modelResources in resources.RideEntranceModelResources)
            modelResources.Dispose();

        resources.RideEntranceModelResources.Clear();
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        try {
            parkLoadTask?.GetAwaiter().GetResult();
            CompleteVehicleModelLoad(upload: false);
        }
        catch (Exception ex) {
            Log.Error(ex, "Park loading failed during shutdown");
        }

        parkLoadTask = null;
        pendingParkLoadResult = null;
        ResetVehicleState();
        vehicleRenderer.Clear();

        simulation.Dispose();
    }

    private sealed record ParkLoadResult(
        string ParkPath, int Width, int Height, TerrainMeshData Terrain, WaterMeshData Water, PathMeshData Paths,
        PathNavigation Navigation, PathAccessoryBatch[] PathAccessories, TrackMeshData Tracks, RideSupportMeshData RideSupports,
        RideEntranceExitMeshData RideEntrances, RideEntranceModelBatch[] RideEntranceModels, TreeSceneryBatch[] Trees,
        RCTTrackElementData[] TrackElements, RCTRideEntranceData[] RideEntranceData,
        TrackCenterlinePiece[] Centerlines, RCTPaletteEntry[] PaletteData, VehicleModel[] Vehicles);
}
