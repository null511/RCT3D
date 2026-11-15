namespace RCT3D.Configuration;

internal sealed record ViewerStartupOptions(string LibraryPath, string? StartupPark, float TerrainSkirtFloor = -24.0f);
