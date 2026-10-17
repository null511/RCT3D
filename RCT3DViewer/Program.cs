using RCT3DViewer.Interop;

namespace RCT3DViewer;

internal class Program
{
    private const string DefaultParkPath = @"C:\Users\null5\Documents\OpenRCT2\save\Mothball Mountain.park";
    private const string DefaultRCT2Path = @"Z:\SteamLibrary\steamapps\common\Rollercoaster Tycoon 2";
    private const string DefaultOpenRCT2Path = "openrct2-data";


    private static int Main(string[] args)
    {
        // TODO: Add command line arguments to override the default paths for the park, RCT2, and OpenRCT2 data directories.

        using var renderer = new Render.Renderer();
        renderer.LibraryPath = Environment.GetEnvironmentVariable("RCT3DVIEWER_OPENRCT2_DLL") ?? LibOpenRCT2.DefaultLibraryPath;
        renderer.OpenRCT2Path = Environment.GetEnvironmentVariable("RCT3DVIEWER_OPENRCT2_DATA") ?? DefaultOpenRCT2Path;
        renderer.Rct2Path = Environment.GetEnvironmentVariable("RCT3DVIEWER_RCT2_DATA") ?? DefaultRCT2Path;
        renderer.ParkPath = Environment.GetEnvironmentVariable("RCT3DVIEWER_PARK") ?? DefaultParkPath;

        renderer.Run();
        return 0;
    }
}
