using System.Numerics;
using RCT3D.Interop.Models;
using RCT3D.Render.Paths.Meshing;
using RCT3D.Render.Terrain;

namespace RCT3D.Render.Paths.Builders;

internal sealed class ConcretePathGeometryBuilder(PathMaterialLibrary materials) : PathTypeGeometryBuilder(materials)
{
    protected override void AddRailingEdge(RCTTilePathData path, byte direction, Vector3 start, Vector3 end, float baseHeight, PathRailingMeshGeometry geometry)
    {
        BuildRailingEdge(path, direction, start, end, baseHeight, 12, geometry);
    }

    protected override void AddSupports(RCTTilePathData path, RCTTileSurfaceData surface, PathRailingMeshGeometry geometry, PathSupportStationRegistry supportStations, float? supportBaseHeight)
    {
        var x = (path.TileX + 0.5f) * WorldUnits.TileSize;
        var z = (path.TileY + 0.5f) * WorldUnits.TileSize;

        var bottom = GetSurfaceHeight(surface, path.TileX, path.TileY, x, z);
        var top = GetPathHeight(path, x, z);

        if (top <= bottom + HeightEpsilon) return;

        var baseTop = MathF.Min(bottom + SupportBaseHeight, top);
        AddBox(new Vector3(x, bottom, z), SupportBaseHalfWidth, baseTop, 0, geometry);

        if (top > baseTop + HeightEpsilon)
            AddBox(new Vector3(x, baseTop, z), SupportHalfWidth, top, 0, geometry);
    }

    protected override void AddElevatedPlatformSlab(RCTTilePathData path, PathMeshGeometry geometry)
    {
        var material = Materials.GetRailingLayer(TerrainMaterialLibrary.HashObjectId("rct2.footpath_railings.wood"));

        BuildElevatedPlatformSlab(path, geometry, material);
    }
}
