using Microsoft.Extensions.Configuration;
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
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddIniFile("config.ini", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables("RCT3D_");

            IConfiguration config = builder.Build();

            using var renderer = new Renderer();
            renderer.LibraryPath = config["OPENRCT2_DLL"] ?? LibOpenRCT2.DefaultLibraryPath;
            renderer.StartupPark = config["PARK"];

            renderer.OpenRCT2Path = config["OPENRCT2_DATA"];
            renderer.Rct2Path = config["RCT2_PATH"];
            renderer.Rct1Path = config["RCT1_PATH"];

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
