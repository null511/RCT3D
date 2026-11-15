using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RCT3D.Configuration;
using RCT3D.Interop;
using RCT3D.Render;
using RCT3D.Render.Park;
using Serilog;
using Silk.NET.Maths;
using Silk.NET.Windowing;


Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

try {
    var configBuilder = new ConfigurationBuilder()
        .AddEnvironmentVariables("RCT3D_");

    var config = configBuilder.Build();
    var services = new ServiceCollection();

    services.AddLogging(options => options.AddSerilog());

    services.AddSingleton<IConfiguration>(config);
    services.AddSingleton<GraphicsOptionsStore>();
    services.AddSingleton<GamePathSettingsStore>();
    services.AddSingleton<GamePathConfiguration>();
    services.AddSingleton<ViewerCommandQueue>();
    services.AddSingleton<ViewerUiController>();
    services.AddSingleton<MenuScreen>();
    services.AddSingleton<VulkanRenderResources>();
    services.AddSingleton<ViewerInputController>();
    services.AddSingleton<FrameCoordinator>();
    services.AddSingleton<ParkScreenFactory>();
    services.AddSingleton<Renderer>();

    services.AddSingleton(new ViewerStartupOptions(config["OPENRCT2_DLL"] ?? LibOpenRCT2.DefaultLibraryPath, config["PARK"]));

    services.AddSingleton<IWindow>(_ => {
        var options = WindowOptions.DefaultVulkan;
        options.Size = new Vector2D<int>(1280, 720);
        options.Title = "RCT3D Viewer";
        return Window.Create(options);
    });

    using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    
    var renderer = provider.GetRequiredService<Renderer>();
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
