using RCT3D.Interop;
using RCT3D.Render;
using Serilog;

namespace RCT3D;

internal class Program
{
    private static int Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        try {
            using var renderer = new Renderer();
            renderer.LibraryPath = Environment.GetEnvironmentVariable("RCT3D_OPENRCT2_DLL") ?? LibOpenRCT2.DefaultLibraryPath;
            renderer.OpenRCT2Path = Environment.GetEnvironmentVariable("RCT3D_OPENRCT2_DATA");
            renderer.Rct2Path = Environment.GetEnvironmentVariable("RCT3D_RCT2_PATH");
            renderer.Rct1Path = Environment.GetEnvironmentVariable("RCT3D_RCT1_PATH");
            renderer.StartupParkPath = Environment.GetEnvironmentVariable("RCT3D_PARK");

            renderer.Run();
            return 0;
        }
        catch (Exception ex) {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally {
            Log.CloseAndFlush();
        }
    }
}
