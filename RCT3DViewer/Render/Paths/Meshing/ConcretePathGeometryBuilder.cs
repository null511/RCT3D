using RCT3DViewer.Interop;
using RCT3DViewer.Render.Terrain;
using System.Numerics;

namespace RCT3DViewer.Render.Paths.Meshing;

internal sealed class ConcretePathGeometryBuilder(PathMaterialLibrary materials) : PathTypeGeometryBuilder(materials)
{
    protected override void AddRailingEdge(RCTTilePathData path, byte direction, Vector3 start, Vector3 end, float baseHeight, uint material, PathMeshGeometry geometry)
    {
        BuildRailingEdge(path, direction, start, end, baseHeight, material, 12, geometry);
    }

    protected override void AddSupports(RCTTilePathData path, RCTTileSurfaceData surface, uint material, PathMeshGeometry geometry, PathSupportStationRegistry supportStations)
    {
        var tileSize = TileSize;
        var x = (path.TileX + 0.5f) * tileSize;
        var z = (path.TileY + 0.5f) * tileSize;

        var bottom = GetSurfaceHeight(surface, path.TileX, path.TileY, x, z);
        var top = GetPathHeight(path, x, z);

        if (top <= bottom + HeightEpsilon) return;

        var baseTop = MathF.Min(bottom + SupportBaseHeight, top);
        AddBox(new Vector3(x, bottom, z), SupportBaseHalfWidth, baseTop, material, geometry);

        if (top > baseTop + HeightEpsilon)
            AddBox(new Vector3(x, baseTop, z), SupportHalfWidth, top, material, geometry);
    }

    protected override void AddElevatedPlatformSlab(RCTTilePathData path, PathMeshGeometry geometry)
    {
        var material = Materials.GetLayer(TerrainMaterialLibrary.HashObjectId("rct2.footpath_railings.wood"));

        BuildElevatedPlatformSlab(path, geometry, material);
    }
}
