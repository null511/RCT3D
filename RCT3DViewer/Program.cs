using RCT3D.Interop;
using RCT3D.Render;

namespace RCT3D;

internal class Program
{
    private static int Main(string[] args)
    {
        using var renderer = new Renderer();
        renderer.LibraryPath = Environment.GetEnvironmentVariable("RCT3D_OPENRCT2_DLL") ?? LibOpenRCT2.DefaultLibraryPath;
        renderer.OpenRCT2Path = Environment.GetEnvironmentVariable("RCT3D_OPENRCT2_DATA");
        renderer.Rct2Path = Environment.GetEnvironmentVariable("RCT3D_RCT2_PATH");
        renderer.Rct1Path = Environment.GetEnvironmentVariable("RCT3D_RCT1_PATH");
        renderer.StartupParkPath = Environment.GetEnvironmentVariable("RCT3D_PARK");

        try {
            renderer.Run();
            return 0;
        }
        catch {
            return 1;
        }
    }
}
