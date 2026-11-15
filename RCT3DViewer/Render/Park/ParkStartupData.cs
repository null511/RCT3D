using RCT3D.Configuration;
using RCT3D.Render.Paths;
using RCT3D.Render.Paths.Meshing;
using RCT3D.Render.Scenery;
using RCT3D.Render.Terrain;
using RCT3D.Simulation;

namespace RCT3D.Render.Park;

internal sealed record ParkStartupData(
    SimulationBridge Simulation,
    TerrainMaterialLibrary TerrainMaterials,
    PathMaterialLibrary PathMaterials,
    PathAccessoryAssetLibrary PathAccessoryAssets,
    TreeSceneryAssetLibrary TreeSceneryAssets,
    GraphicsOptionsSettings GraphicsOptions) : IDisposable
{
    public void Dispose() => Simulation.Dispose();
}
