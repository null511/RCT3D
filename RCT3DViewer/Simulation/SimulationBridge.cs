using RCT3DViewer.Interop;

namespace RCT3DViewer.Simulation;

public sealed class SimulationBridge : IDisposable
{
    private readonly LibOpenRCT2 _openRCT2;

    public SimulationBridge(string libraryPath, string openRCT2Path, string rct2Path)
    {
        _openRCT2 = new LibOpenRCT2(libraryPath, openRCT2Path, rct2Path);
    }

    public void LoadPark(string savePath) => _openRCT2.LoadPark(savePath);

    public (int Width, int Height) GetDimensions() => _openRCT2.GetDimensions();

    public void FillSurfaceData(Span<RCTTileSurfaceData> output) => _openRCT2.CopySurfaceData(output);

    public void Dispose() => _openRCT2.Dispose();
}
