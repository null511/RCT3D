using RCT3DViewer.Interop;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace RCT3DViewer.Simulation;

public sealed class SimulationBridge : IDisposable
{
    private readonly BlockingCollection<Action<LibOpenRCT2>> _requests = new();
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _sync = new();
    private readonly Thread _worker;

    private bool _disposed;
    private Exception? _shutdownException;
    private Exception? _simulationFailure;
    private bool _parkLoaded;
    private float _gameTimeOfDayHours;
    private VehicleSnapshotPair? _vehicleSnapshots;

    public VehicleSnapshotPair? VehicleSnapshots => Volatile.Read(ref _vehicleSnapshots);
    public float? GameTimeOfDayHours => Volatile.Read(ref _parkLoaded) ? Volatile.Read(ref _gameTimeOfDayHours) : null;
    public Exception? SimulationFailure => Volatile.Read(ref _simulationFailure);


    public SimulationBridge(string libraryPath, string openRCT2Path, string rct2Path)
    {
        _worker = new Thread(() => WorkerLoop(libraryPath, openRCT2Path, rct2Path)) {
            Name = "OpenRCT2 worker",
            IsBackground = true,
        };

        _worker.Start();

        try {
            _started.Task.GetAwaiter().GetResult();
        }
        catch {
            _worker.Join();
            throw;
        }
    }

    public Task<ParkSnapshot> LoadParkAsync(string savePath, Action<string> reportProgress)
    {
        var completion = new TaskCompletionSource<ParkSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_sync) {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _requests.Add(openRCT2 => {
                try {
                    reportProgress("Loading park file...");
                    Volatile.Write(ref _parkLoaded, false);
                    Volatile.Write(ref _vehicleSnapshots, null);
                    Volatile.Write(ref _simulationFailure, null);
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
                    Volatile.Write(ref _gameTimeOfDayHours, openRCT2.GetTimeOfDayHours());
                    Volatile.Write(ref _vehicleSnapshots, new VehicleSnapshotPair(null, ReadVehicleSnapshot(openRCT2)));
                    Volatile.Write(ref _parkLoaded, true);
                    completion.TrySetResult(new ParkSnapshot(width, height, surfaceData, pathData));
                }
                catch (Exception ex) {
                    completion.TrySetException(ex);
                }
            });
        }

        return completion.Task;
    }

    private void WorkerLoop(string libraryPath, string openRCT2Path, string rct2Path)
    {
        LibOpenRCT2? openRCT2 = null;

        try {
            openRCT2 = new LibOpenRCT2(libraryPath, openRCT2Path, rct2Path);

            _started.TrySetResult();
            var updateInterval = Math.Max(1L, Stopwatch.Frequency / 40);
            long nextUpdate = 0;
            while (true) {
                var timeoutMilliseconds = Timeout.Infinite;
                if (Volatile.Read(ref _parkLoaded)) {
                    var remaining = nextUpdate - Stopwatch.GetTimestamp();
                    timeoutMilliseconds = remaining <= 0
                        ? 0
                        : Math.Clamp((int)Math.Ceiling(remaining * 1000.0 / Stopwatch.Frequency), 1, 25);
                }

                if (_requests.TryTake(out var request, timeoutMilliseconds))
                    request(openRCT2);
                else if (_requests.IsCompleted)
                    break;

                if (!Volatile.Read(ref _parkLoaded)) {
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
                        Volatile.Write(ref _gameTimeOfDayHours, openRCT2.GetTimeOfDayHours());
                        PublishVehicleSnapshot(openRCT2);
                        nextUpdate += updateInterval;
                        if (now - nextUpdate > updateInterval * 4)
                            nextUpdate = now + updateInterval;
                    }
                    catch (Exception ex) {
                        Volatile.Write(ref _simulationFailure, ex);
                        Volatile.Write(ref _vehicleSnapshots, null);
                        Volatile.Write(ref _parkLoaded, false);
                        nextUpdate = 0;
                    }
                }
            }
        }
        catch (Exception ex) {
            _started.TrySetException(ex);
        }
        finally {
            try {
                openRCT2?.Dispose();
            }
            catch (Exception ex) {
                _shutdownException = ex;
            }
        }
    }

    private void PublishVehicleSnapshot(LibOpenRCT2 openRCT2)
    {
        var previous = Volatile.Read(ref _vehicleSnapshots)?.Current;
        var current = ReadVehicleSnapshot(openRCT2);
        Volatile.Write(ref _vehicleSnapshots, new VehicleSnapshotPair(previous, current));
    }

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
        lock (_sync) {
            if (_disposed) return;

            _disposed = true;
            _requests.CompleteAdding();
        }

        _worker.Join();
        _requests.Dispose();
        if (_shutdownException is not null)
            throw new InvalidOperationException("Failed to shut down OpenRCT2 on its worker thread.", _shutdownException);
    }
}

public sealed record ParkSnapshot(int Width, int Height, RCTTileSurfaceData[] SurfaceData, RCTTilePathData[] PathData);

public sealed record VehicleSnapshot(long Timestamp, ReadOnlyMemory<RCTVehicleData> Vehicles);

public sealed record VehicleSnapshotPair(VehicleSnapshot? Previous, VehicleSnapshot Current);
