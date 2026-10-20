using RCT3DViewer.Render.Paths.Meshing;
using RCT3DViewer.Render.Terrain;
using RCT3DViewer.Render.Paths;
using RCT3DViewer.Simulation;
using System.Numerics;

namespace RCT3DViewer.Render;

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

    private Task<ParkLoadResult>? parkLoadTask;
    private ParkLoadResult? pendingParkLoadResult;
    private string parkLoadProgress = string.Empty;
    private int loadedMapWidth;
    private int loadedMapHeight;
    private bool isDisposed;

    public TerrainMeshData? TerrainMesh { get; private set; }
    public PathMeshData? PathMesh { get; private set; }
    public WaterMeshData? WaterMesh { get; private set; }
    public float? GameTimeOfDayHours => simulation.GameTimeOfDayHours;


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
            Volatile.Write(ref parkLoadProgress, "Generating terrain, water, path, and accessory meshes...");

            var meshData = await Task.Run(() => (
                Terrain: terrainMeshBuilder.Build(snapshot.SurfaceData, snapshot.Width, snapshot.Height, terrainSkirtFloor),
                Paths: pathMeshBuilder.Build(snapshot.PathData, snapshot.SurfaceData, snapshot.Width, snapshot.Height),
                PathAccessories: pathAccessoryMeshBuilder.Build(snapshot.PathData, snapshot.Width, snapshot.Height)));

            return new ParkLoadResult(
                parkPath, snapshot.Width, snapshot.Height,
                meshData.Terrain.Terrain, meshData.Terrain.Water, meshData.Paths, meshData.PathAccessories);
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
                ui.SetParkLoadStatus("Uploading terrain, water, path, and accessory meshes...");
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
                DisposePathAccessoryResources();

                foreach (var batch in result.PathAccessories) {
                    var accessoryResources = new ModelGraphicsResources(resources.DeviceContext!, resources.DeferredTargets!.GBufferRenderPass,
                        resources.UniformBufferResources!.DescriptorSetLayout, batch.Material, deferred: true);

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
        PathAccessoryBatch[] PathAccessories);
}
