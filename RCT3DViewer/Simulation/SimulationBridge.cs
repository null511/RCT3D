using RCT3D.Interop;
using RCT3D.Interop.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace RCT3D.Simulation;

public sealed class SimulationBridge : IDisposable
{
    private readonly BlockingCollection<Action<LibOpenRCT2>> requests = new();
    private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock sync = new();
    private readonly Thread worker;

    private bool isDisposed;
    private Exception? shutdownException;
    private Exception? simulationFailure;
    private bool parkLoaded;
    private bool isPaused;
    private float gameTimeOfDayHours;
    private VehicleSnapshotPair? vehicleSnapshots;
    private RCTVehiclePathSample[] vehiclePathSamples = [];
    private PeepSnapshot? peepSnapshot;
    private int activePeepEntityId = -1;

    public VehicleSnapshotPair? VehicleSnapshots => Volatile.Read(ref vehicleSnapshots);
    public ReadOnlyMemory<RCTVehiclePathSample> VehiclePathSamples => Volatile.Read(ref vehiclePathSamples);
    public PeepSnapshot? Peep => Volatile.Read(ref peepSnapshot);
    public float? GameTimeOfDayHours => Volatile.Read(ref parkLoaded) ? Volatile.Read(ref gameTimeOfDayHours) : null;
    public bool? IsPaused => Volatile.Read(ref parkLoaded) ? Volatile.Read(ref isPaused) : null;
    public Exception? SimulationFailure => Volatile.Read(ref simulationFailure);


    public SimulationBridge(string libraryPath, string openRCT2Path, string rct2Path, string rct1Path)
    {
        worker = new Thread(() => WorkerLoop(libraryPath, openRCT2Path, rct2Path, rct1Path)) {
            Name = "OpenRCT2 worker",
            IsBackground = true,
        };

        worker.Start();

        try {
            started.Task.GetAwaiter().GetResult();
        }
        catch {
            worker.Join();
            throw;
        }
    }

    public Task<ParkSnapshot> LoadParkAsync(string savePath, Action<string> reportProgress)
    {
        var completion = new TaskCompletionSource<ParkSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (sync) {
            ObjectDisposedException.ThrowIf(isDisposed, this);

            requests.Add(openRCT2 => {
                try {
                    reportProgress("Loading park file...");
                    Volatile.Write(ref parkLoaded, false);
                    Volatile.Write(ref vehicleSnapshots, null);
                    Volatile.Write(ref vehiclePathSamples, []);
                    Volatile.Write(ref peepSnapshot, null);
                    Volatile.Write(ref activePeepEntityId, -1);
                    Volatile.Write(ref simulationFailure, null);
                    openRCT2.LoadPark(savePath);

                    reportProgress("Reading park surface data...");
                    var (width, height) = openRCT2.GetDimensions();
                    if (width <= 0 || height <= 0 || (long)width * height > int.MaxValue)
                        throw new InvalidOperationException($"OpenRCT2 returned invalid map dimensions {width}x{height}.");

                    var surfaceData = new RCTTileSurfaceData[checked(width * height)];
                    openRCT2.CopySurfaceData(surfaceData);

                    reportProgress("Reading path data...");
                    var pathCount = openRCT2.GetPathCount();
                    if (pathCount > int.MaxValue)
                        throw new InvalidOperationException($"OpenRCT2 returned too many path elements: {pathCount}.");

                    var pathData = new RCTTilePathData[(int)pathCount];
                    openRCT2.CopyPathData(pathData);

                    reportProgress("Reading tree scenery data...");
                    var treeCount = openRCT2.GetTreeCount();
                    if (treeCount > int.MaxValue)
                        throw new InvalidOperationException($"OpenRCT2 returned too many tree scenery elements: {treeCount}.");

                    var treeData = new RCTTreeSceneryData[(int)treeCount];
                    openRCT2.CopyTreeData(treeData);

                    reportProgress("Reading scenery and entrance clearance data...");
                    var obstacleCount = openRCT2.GetObstacleCount();
                    if (obstacleCount > int.MaxValue)
                        throw new InvalidOperationException($"OpenRCT2 returned too many obstacle elements: {obstacleCount}.");

                    var obstacleData = new RCTTileObstacleData[(int)obstacleCount];
                    openRCT2.CopyObstacleData(obstacleData);

                    reportProgress("Reading ride track and palette data...");
                    var trackCount = openRCT2.GetTrackCount();
                    if (trackCount > int.MaxValue)
                        throw new InvalidOperationException($"OpenRCT2 returned too many monorail track elements: {trackCount}.");

                    var trackData = new RCTTrackElementData[(int)trackCount];
                    openRCT2.CopyTrackData(trackData);

                    reportProgress("Reading ride entrance and exit tiles...");
                    var rideEntranceCount = openRCT2.GetRideEntranceCount();
                    if (rideEntranceCount > int.MaxValue)
                        throw new InvalidOperationException($"OpenRCT2 returned too many ride entrance and exit tiles: {rideEntranceCount}.");

                    var rideEntranceData = new RCTRideEntranceData[(int)rideEntranceCount];
                    openRCT2.CopyRideEntranceData(rideEntranceData);

                    var paletteData = new RCTPaletteEntry[256];
                    openRCT2.CopyPaletteData(paletteData);

                    Volatile.Write(ref isPaused, openRCT2.GetPauseState());
                    Volatile.Write(ref gameTimeOfDayHours, openRCT2.GetTimeOfDayHours());
                    Volatile.Write(ref vehicleSnapshots, new VehicleSnapshotPair(null, ReadVehicleSnapshot(openRCT2)));
                    reportProgress("Reading native vehicle paths...");
                    Volatile.Write(ref vehiclePathSamples, openRCT2.ReadVehiclePathSamples());
                    Volatile.Write(ref parkLoaded, true);
                    completion.TrySetResult(new ParkSnapshot(width, height, surfaceData, pathData, treeData, trackData, rideEntranceData, paletteData, obstacleData));
                }
                catch (Exception ex) {
                    completion.TrySetException(ex);
                }
            });
        }

        return completion.Task;
    }

    public Task<ushort> CreatePeepAsync(int x, int y, int z)
    {
        var completion = new TaskCompletionSource<ushort>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (sync) {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (!Volatile.Read(ref parkLoaded)) {
                completion.TrySetException(new InvalidOperationException("No park is loaded."));
                return completion.Task;
            }

            requests.Add(openRCT2 => {
                try {
                    var entityId = openRCT2.CreatePeep(x, y, z);
                    Volatile.Write(ref activePeepEntityId, entityId);
                    Volatile.Write(ref peepSnapshot, ToPeepSnapshot(openRCT2.GetPeepData(entityId)));
                    completion.TrySetResult(entityId);
                }
                catch (Exception ex) {
                    completion.TrySetException(ex);
                }
            });
        }

        return completion.Task;
    }

    public void MovePeep(ushort entityId, int x, int y, int z, byte direction)
    {
        lock (sync) {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (!Volatile.Read(ref parkLoaded) || Volatile.Read(ref activePeepEntityId) != entityId) return;

            requests.Add(openRCT2 => {
                try {
                    openRCT2.MovePeep(entityId, x, y, z, direction);
                    Volatile.Write(ref peepSnapshot, ToPeepSnapshot(openRCT2.GetPeepData(entityId)));
                }
                catch (Exception ex) {
                    Volatile.Write(ref simulationFailure, ex);
                }
            });
        }
    }

    public Task EnterRideAsync(ushort entityId, ushort rideId, byte stationIndex)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (sync) {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (!Volatile.Read(ref parkLoaded) || Volatile.Read(ref activePeepEntityId) != entityId) {
                completion.TrySetException(new InvalidOperationException("The Peep Mode guest is not active."));
                return completion.Task;
            }

            requests.Add(openRCT2 => {
                try {
                    openRCT2.EnterRide(entityId, rideId, stationIndex);
                    Volatile.Write(ref peepSnapshot, ToPeepSnapshot(openRCT2.GetPeepData(entityId)));
                    completion.TrySetResult();
                }
                catch (Exception ex) {
                    completion.TrySetException(ex);
                }
            });
        }

        return completion.Task;
    }

    public Task TakeControlPeepAsync(ushort entityId, int x, int y, int z, byte direction)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (sync) {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (!Volatile.Read(ref parkLoaded) || Volatile.Read(ref activePeepEntityId) != entityId) {
                completion.TrySetException(new InvalidOperationException("The Peep Mode guest is not active."));
                return completion.Task;
            }

            requests.Add(openRCT2 => {
                try {
                    openRCT2.TakeControlPeep(entityId, x, y, z, direction);
                    Volatile.Write(ref peepSnapshot, ToPeepSnapshot(openRCT2.GetPeepData(entityId)));
                    completion.TrySetResult();
                }
                catch (Exception ex) {
                    completion.TrySetException(ex);
                }
            });
        }

        return completion.Task;
    }

    public void ReleasePeep(ushort entityId)
    {
        lock (sync) {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (Volatile.Read(ref activePeepEntityId) != entityId) return;

            requests.Add(openRCT2 => {
                try {
                    openRCT2.ReleasePeep(entityId);
                    Volatile.Write(ref activePeepEntityId, -1);
                    Volatile.Write(ref peepSnapshot, null);
                }
                catch (Exception ex) {
                    Volatile.Write(ref simulationFailure, ex);
                }
            });
        }
    }

    public void RemovePeep(ushort entityId)
    {
        lock (sync) {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (Volatile.Read(ref activePeepEntityId) != entityId) return;

            requests.Add(openRCT2 => {
                try {
                    openRCT2.RemovePeep(entityId);
                    Volatile.Write(ref activePeepEntityId, -1);
                    Volatile.Write(ref peepSnapshot, null);
                }
                catch (Exception ex) {
                    Volatile.Write(ref simulationFailure, ex);
                }
            });
        }
    }

    public void TogglePause()
    {
        lock (sync) {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (!Volatile.Read(ref parkLoaded)) return;

            requests.Add(openRCT2 => {
                try {
                    Volatile.Write(ref isPaused, openRCT2.TogglePause());
                }
                catch (Exception ex) {
                    Volatile.Write(ref simulationFailure, ex);
                }
            });
        }
    }

    private void WorkerLoop(string libraryPath, string openRCT2Path, string rct2Path, string rct1Path)
    {
        LibOpenRCT2? openRCT2 = null;

        try {
            openRCT2 = new LibOpenRCT2(libraryPath, openRCT2Path, rct2Path, rct1Path);

            started.TrySetResult();
            var updateInterval = Math.Max(1L, Stopwatch.Frequency / 40);
            long nextUpdate = 0;
            while (true) {
                var timeoutMilliseconds = Timeout.Infinite;
                if (Volatile.Read(ref parkLoaded)) {
                    var remaining = nextUpdate - Stopwatch.GetTimestamp();
                    timeoutMilliseconds = remaining <= 0
                        ? 0
                        : Math.Clamp((int)Math.Ceiling(remaining * 1000.0 / Stopwatch.Frequency), 1, 25);
                }

                if (requests.TryTake(out var request, timeoutMilliseconds))
                    request(openRCT2);
                else if (requests.IsCompleted)
                    break;

                if (!Volatile.Read(ref parkLoaded)) {
                    nextUpdate = 0;
                    continue;
                }

                var now = Stopwatch.GetTimestamp();
                if (nextUpdate == 0) {
                    nextUpdate = now + updateInterval;
                }
                else if (now >= nextUpdate) {
                    try {
                        openRCT2.AdvanceSimulation();
                        Volatile.Write(ref gameTimeOfDayHours, openRCT2.GetTimeOfDayHours());
                        PublishVehicleSnapshot(openRCT2);
                        PublishPeepSnapshot(openRCT2);
                        nextUpdate += updateInterval;
                        if (now - nextUpdate > updateInterval * 4)
                            nextUpdate = now + updateInterval;
                    }
                    catch (Exception ex) {
                        Volatile.Write(ref simulationFailure, ex);
                        Volatile.Write(ref vehicleSnapshots, null);
                        Volatile.Write(ref parkLoaded, false);
                        nextUpdate = 0;
                    }
                }
            }
        }
        catch (Exception ex) {
            started.TrySetException(ex);
        }
        finally {
            try {
                openRCT2?.Dispose();
            }
            catch (Exception ex) {
                shutdownException = ex;
            }
        }
    }

    private void PublishVehicleSnapshot(LibOpenRCT2 openRCT2)
    {
        var previous = Volatile.Read(ref vehicleSnapshots)?.Current;
        var current = ReadVehicleSnapshot(openRCT2);
        Volatile.Write(ref vehicleSnapshots, new VehicleSnapshotPair(previous, current));
    }

    private void PublishPeepSnapshot(LibOpenRCT2 openRCT2)
    {
        var entityId = Volatile.Read(ref activePeepEntityId);
        if (entityId < 0) return;

        try {
            Volatile.Write(ref peepSnapshot, ToPeepSnapshot(openRCT2.GetPeepData((ushort)entityId)));
        }
        catch (Exception ex) {
            Volatile.Write(ref simulationFailure, ex);
            Volatile.Write(ref peepSnapshot, null);
            Volatile.Write(ref activePeepEntityId, -1);
        }
    }

    private static PeepSnapshot ToPeepSnapshot(RCTPeepData data) => new(
        data.EntityId, data.X, data.Y, data.Z, data.State, data.RideId,
        data.StationIndex, data.TrainIndex, data.CarIndex, data.SeatIndex, data.TrainHeadEntityId, data.VehicleEntityId);

    private static VehicleSnapshot ReadVehicleSnapshot(LibOpenRCT2 openRCT2)
    {
        var count = openRCT2.GetVehicleCount();
        if (count > int.MaxValue)
            throw new InvalidOperationException($"OpenRCT2 returned too many positioned ride vehicles: {count}.");

        var vehicles = new RCTVehicleData[(int)count];
        openRCT2.CopyVehicleData(vehicles);
        return new VehicleSnapshot(Stopwatch.GetTimestamp(), vehicles);
    }

    public void Dispose()
    {
        lock (sync) {
            if (isDisposed) return;

            isDisposed = true;
            requests.CompleteAdding();
        }

        worker.Join();
        requests.Dispose();
        if (shutdownException is not null)
            throw new InvalidOperationException("Failed to shut down OpenRCT2 on its worker thread.", shutdownException);
    }
}

public sealed record ParkSnapshot(
    int Width,
    int Height,
    RCTTileSurfaceData[] SurfaceData,
    RCTTilePathData[] PathData,
    RCTTreeSceneryData[] TreeData,
    RCTTrackElementData[] TrackData,
    RCTRideEntranceData[] RideEntranceData,
    RCTPaletteEntry[] PaletteData,
    RCTTileObstacleData[] ObstacleData);

public sealed record VehicleSnapshot(long Timestamp, ReadOnlyMemory<RCTVehicleData> Vehicles);

public sealed record VehicleSnapshotPair(VehicleSnapshot? Previous, VehicleSnapshot Current);

public sealed record PeepSnapshot(
    ushort EntityId,
    int X,
    int Y,
    int Z,
    byte State,
    ushort RideId,
    byte StationIndex,
    byte TrainIndex,
    byte CarIndex,
    byte SeatIndex,
    ushort TrainHeadEntityId,
    ushort VehicleEntityId);
