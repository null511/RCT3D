using RCT3D.Interop;
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
    private float gameTimeOfDayHours;
    private VehicleSnapshotPair? vehicleSnapshots;

    public VehicleSnapshotPair? VehicleSnapshots => Volatile.Read(ref vehicleSnapshots);
    public float? GameTimeOfDayHours => Volatile.Read(ref parkLoaded) ? Volatile.Read(ref gameTimeOfDayHours) : null;
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

                    reportProgress("Reading ride track and palette data...");
                    var trackCount = openRCT2.GetTrackCount();
                    if (trackCount > int.MaxValue)
                        throw new InvalidOperationException($"OpenRCT2 returned too many monorail track elements: {trackCount}.");

                    var trackData = new RCTTrackElementData[(int)trackCount];
                    openRCT2.CopyTrackData(trackData);
                    var paletteData = new RCTPaletteEntry[256];
                    openRCT2.CopyPaletteData(paletteData);

                    Volatile.Write(ref gameTimeOfDayHours, openRCT2.GetTimeOfDayHours());
                    Volatile.Write(ref vehicleSnapshots, new VehicleSnapshotPair(null, ReadVehicleSnapshot(openRCT2)));
                    Volatile.Write(ref parkLoaded, true);
                    completion.TrySetResult(new ParkSnapshot(width, height, surfaceData, pathData, trackData, paletteData));
                }
                catch (Exception ex) {
                    completion.TrySetException(ex);
                }
            });
        }

        return completion.Task;
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
    RCTTrackElementData[] TrackData,
    RCTPaletteEntry[] PaletteData);

public sealed record VehicleSnapshot(long Timestamp, ReadOnlyMemory<RCTVehicleData> Vehicles);

public sealed record VehicleSnapshotPair(VehicleSnapshot? Previous, VehicleSnapshot Current);
