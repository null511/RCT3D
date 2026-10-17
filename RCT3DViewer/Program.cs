namespace RCT3DViewer;

internal class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--smoke-test-openrct2")
        {
            return RunOpenRCT2SmokeTest(args);
        }

        using var renderer = new Render.Renderer();
        renderer.Run();
        return 0;
    }

    private static int RunOpenRCT2SmokeTest(string[] args)
    {
        var parkPath = args.Length > 1
            ? args[1]
            : Environment.GetEnvironmentVariable("RCT3DVIEWER_PARK")
                ?? @"C:\Users\null5\Documents\OpenRCT2\save\Mothball Mountain.park";
        var rct2Path = Environment.GetEnvironmentVariable("RCT3DVIEWER_RCT2_DATA")
            ?? @"Z:\SteamLibrary\steamapps\common\Rollercoaster Tycoon 2";
        var openRCT2Path = Environment.GetEnvironmentVariable("RCT3DVIEWER_OPENRCT2_DATA")
            ?? Path.Combine(AppContext.BaseDirectory, "openrct2-data");
        var libraryPath = Environment.GetEnvironmentVariable("RCT3DVIEWER_OPENRCT2_DLL")
            ?? Interop.LibOpenRCT2.DefaultLibraryPath;

        try
        {
            using var simulation = new Simulation.SimulationBridge(libraryPath, openRCT2Path, rct2Path);
            simulation.LoadPark(parkPath);
            var (width, height) = simulation.GetDimensions();
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException($"Invalid map dimensions {width}x{height}.");

            var surfaces = new Interop.RCTTileSurfaceData[checked(width * height)];
            simulation.FillSurfaceData(surfaces);
            Console.WriteLine($"OpenRCT2 adapter smoke test passed: {width}x{height}, {surfaces.Length} terrain tiles, park={parkPath}");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"OpenRCT2 adapter smoke test failed: {error}");
            return 1;
        }
    }
}
