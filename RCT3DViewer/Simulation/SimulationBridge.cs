using RCT3DViewer.Interop;
using System.Collections.Concurrent;

namespace RCT3DViewer.Simulation;

public sealed class SimulationBridge : IDisposable
{
    private readonly BlockingCollection<Action<LibOpenRCT2>> _requests = new();
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _sync = new();
    private readonly Thread _worker;

    private bool _disposed;
    private Exception? _shutdownException;


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
            foreach (var request in _requests.GetConsumingEnumerable())
                request(openRCT2);
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
