using RCT3D.Render.Paths;
using RCT3D.Render.Paths.Meshing;
using RCT3D.Render.Terrain;
using RCT3D.Simulation;
using RCT3D.Interop;
using System.Numerics;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Entrances;

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
    private readonly TrackMeshBuilder trackMeshBuilder = new();
    private readonly RideEntranceExitMeshBuilder rideEntranceExitMeshBuilder = new();

    private Task<ParkLoadResult>? parkLoadTask;
    private ParkLoadResult? pendingParkLoadResult;
    private string parkLoadProgress = string.Empty;
    private int loadedMapWidth;
    private int loadedMapHeight;
    private Dictionary<(int TileX, int TileY), RCTTrackElementData[]> trackElementsByTile = new();
    private bool isDisposed;

    public TerrainMeshData? TerrainMesh { get; private set; }
    public PathMeshData? PathMesh { get; private set; }
    public WaterMeshData? WaterMesh { get; private set; }
    public TrackMeshData? TrackMesh { get; private set; }
    public RideSupportMeshData? RideSupportMesh { get; private set; }
    public RideEntranceExitMeshData? RideEntranceExitMesh { get; private set; }
    public float? GameTimeOfDayHours => simulation.GameTimeOfDayHours;
    public bool? IsPaused => simulation.IsPaused;

    public void TogglePause() => simulation.TogglePause();

    public IReadOnlyList<RCTTrackElementData> GetTrackElementsAtTile(int tileX, int tileY) =>
        trackElementsByTile.TryGetValue((tileX, tileY), out var elements) ? elements : Array.Empty<RCTTrackElementData>();


    public void LoadPark(string parkPath, float terrainSkirtFloor)
    {
        if (ui.IsParkLoading || parkLoadTask is not null || pendingParkLoadResult is not null) return;

        ui.BeginParkLoad(parkPath);
        Volatile.Write(ref parkLoadProgress, "Loading park file...");
        parkLoadTask = BuildParkMeshesAsync(parkPath, terrainSkirtFloor);
    }

    private async Task<ParkLoadResult> BuildParkMeshesAsync(string parkPath, float terrainSkirtFloor)
    {
        try {
            var snapshot = await simulation.LoadParkAsync(parkPath, progress => Volatile.Write(ref parkLoadProgress, progress));
            Volatile.Write(ref parkLoadProgress, "Generating terrain, water, path, accessory, track, support, and station meshes...");

            var meshData = await Task.Run(() => (
                Terrain: terrainMeshBuilder.Build(snapshot.SurfaceData, snapshot.Width, snapshot.Height, terrainSkirtFloor),
                Paths: pathMeshBuilder.Build(snapshot.PathData, snapshot.SurfaceData, snapshot.Width, snapshot.Height),
                PathAccessories: pathAccessoryMeshBuilder.Build(snapshot.PathData, snapshot.Width, snapshot.Height),
                Tracks: trackMeshBuilder.Build(snapshot.TrackData, snapshot.SurfaceData, snapshot.Width, snapshot.Height, snapshot.PaletteData),
                RideEntrances: rideEntranceExitMeshBuilder.Build(snapshot.RideEntranceData, snapshot.Width, snapshot.Height)));

            return new ParkLoadResult(
                parkPath, snapshot.Width, snapshot.Height,
                meshData.Terrain.Terrain, meshData.Terrain.Water, meshData.Paths, meshData.PathAccessories,
                meshData.Tracks.Tracks, meshData.Tracks.RideSupports, meshData.RideEntrances, snapshot.TrackData);
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
                ui.SetParkLoadStatus("Uploading terrain, water, path, accessory, track, support, and station meshes...");
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

                foreach (var batch in result.PathAccessories) {
                    var accessoryResources = new ModelGraphicsResources(resources.DeviceContext!, resources.DeferredTargets!.GBufferRenderPass,
                        resources.UniformBufferResources!.DescriptorSetLayout, batch.Material);

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

                TerrainMesh = result.Terrain;
                PathMesh = result.Paths;
                WaterMesh = result.Water;
                TrackMesh = result.Tracks;
                RideSupportMesh = result.RideSupports;
                RideEntranceExitMesh = result.RideEntrances;
                trackElementsByTile = result.TrackElements
                    .GroupBy(track => (track.TileX, track.TileY))
                    .ToDictionary(group => group.Key, group => group.ToArray());
                loadedMapWidth = result.Width;
                loadedMapHeight = result.Height;

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
        trackElementsByTile.Clear();
        loadedMapWidth = 0;
        loadedMapHeight = 0;
        frameRenderer.InvalidateHistory();
        pendingParkLoadResult = null;
        parkLoadTask = null;

        try {
            if (resources.Vk is not null && resources.LogicalDevice.Handle != 0) {
                if (resources.Vk.DeviceWaitIdle(resources.LogicalDevice) != Silk.NET.Vulkan.Result.Success)
                    throw new InvalidOperationException("Could not wait for the Vulkan device before clearing park meshes.");

                DisposePathAccessoryResources();
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

    private void UpdateVehicleCubeInstances(uint currentFrame)
    {
        var vehicleResources = resources.VehicleCubeGraphicsResources;
        if (vehicleResources.Length == 0) return;

        var snapshots = parkLoadTask is null && pendingParkLoadResult is null
            ? simulation.VehicleSnapshots : null;

        var instances = VehicleCubeRenderer.BuildInstances(snapshots, loadedMapWidth, loadedMapHeight);
        vehicleResources[(int)currentFrame].UploadInstances(instances);
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
            Console.Error.WriteLine($"Failed to shut down OpenRCT2: {ex.Message}");
        }
    }

    private sealed record ParkLoadResult(
        string ParkPath, int Width, int Height, TerrainMeshData Terrain, WaterMeshData Water, PathMeshData Paths,
        PathAccessoryBatch[] PathAccessories, TrackMeshData Tracks, RideSupportMeshData RideSupports,
        RideEntranceExitMeshData RideEntrances, RCTTrackElementData[] TrackElements);
}
