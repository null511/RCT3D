using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RCT3D.Configuration;
using RCT3D.Render;
using Serilog;


Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

try {
    var configBuilder = new ConfigurationBuilder()
        .AddEnvironmentVariables("RCT3D_");

    var config = configBuilder.Build();

    var services = new ServiceCollection();
    services.AddSingleton<IConfiguration>(config);
    services.AddSingleton<GraphicsOptionsStore>();
    services.AddSingleton<ViewerUiController>();
    services.AddSingleton<Renderer>();

    services.AddTransient<MenuScreen>();

    services.AddLogging(options => options.AddSerilog());

    using var provider = services.BuildServiceProvider();
    using var renderer = provider.GetRequiredService<Renderer>();

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
