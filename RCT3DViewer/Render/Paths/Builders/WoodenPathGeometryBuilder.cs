using RCT3D.Interop.Models;
using RCT3D.Render.Paths.Meshing;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Terrain;
using System.Numerics;

namespace RCT3D.Render.Paths.Builders;

internal sealed class WoodenPathGeometryBuilder(PathMaterialLibrary materials) : PathTypeGeometryBuilder(materials)
{
    private const float BeamHalfWidth = 0.08f;

    protected override bool CanBuildAboveObstacles => true;

    protected override TrackRgba8 GetRailingColor(RCTTilePathData path) => new(148, 101, 55, 255);

    protected override float GetSupportTopHeight(RCTTilePathData path) =>
        path.BaseZ * WorldUnits.HeightPerBaseUnit + ElevatedPlatformTopOffset - ElevatedPlatformThickness
        + (path.IsSloped ? 2f * WorldUnits.HeightPerBaseUnit * (1f - BeamHalfWidth / WorldUnits.TileSize) : 0f);

    protected override void AddSupports(
        RCTTilePathData path, RCTTileSurfaceData surface, PathRailingMeshGeometry geometry,
        PathSupportStationRegistry supportStations, float? supportBaseHeight)
    {
        var terrainBottom = surface.BaseZ * WorldUnits.HeightPerBaseUnit
            + Math.Min(Math.Min(surface.SouthCornerHeight, surface.EastCornerHeight),
                Math.Min(surface.NorthCornerHeight, surface.WestCornerHeight)) * WorldUnits.TerrainCornerHeightScale;

        var shortHeight = 2f * WorldUnits.HeightPerBaseUnit;
        var bottom = MathF.Ceiling((supportBaseHeight ?? terrainBottom) / shortHeight) * shortHeight;
        var top = path.BaseZ * WorldUnits.HeightPerBaseUnit + ElevatedPlatformTopOffset - ElevatedPlatformThickness;
        if (bottom >= top + (path.IsSloped ? shortHeight : 0f)) return;

        var wholeSegments = (int)MathF.Floor((top - bottom) / shortHeight);
        if (wholeSegments >= 1 && bottom / WorldUnits.HeightPerBaseUnit % 4 >= 2) {
            AddBoxSegment(path, bottom, bottom + shortHeight, false, geometry);
            bottom += shortHeight;
            wholeSegments--;
        }

        while (wholeSegments >= 2) {
            AddBoxSegment(path, bottom, bottom + 2f * shortHeight, true, geometry);
            bottom += 2f * shortHeight;
            wholeSegments -= 2;
        }

        if (wholeSegments >= 1) {
            AddBoxSegment(path, bottom, bottom + shortHeight, false, geometry);
            bottom += shortHeight;
        }

        if (bottom < top + (path.IsSloped ? shortHeight : 0f) - HeightEpsilon)
            AddBoxSegment(path, bottom, top + (path.IsSloped ? shortHeight : 0f), false, geometry);
    }

    protected override void AddElevatedPlatformSlab(RCTTilePathData path, PathMeshGeometry geometry)
    {
        var material = Materials.GetRailingLayer(TerrainMaterialLibrary.HashObjectId("rct2.footpath_railings.wood"));

        BuildElevatedPlatformSlab(path, geometry, material);
    }

    protected override void AddRailingEdge(RCTTilePathData path, byte direction, Vector3 start, Vector3 end, float baseHeight, PathRailingMeshGeometry geometry)
    {
        BuildRailingEdge(path, direction, start, end, baseHeight, 3, geometry);
    }

    private void AddBoxSegment(
        RCTTilePathData path, float bottom, float top, bool tall, PathRailingMeshGeometry geometry)
    {
        var west = path.TileX * WorldUnits.TileSize + BeamHalfWidth;
        var east = (path.TileX + 1) * WorldUnits.TileSize - BeamHalfWidth;
        var south = path.TileY * WorldUnits.TileSize + BeamHalfWidth;
        var north = (path.TileY + 1) * WorldUnits.TileSize - BeamHalfWidth;

        Span<Vector3> corners = stackalloc Vector3[4] {
            new(west, 0f, south), new(east, 0f, south),
            new(east, 0f, north), new(west, 0f, north)
        };

        for (var index = 0; index < corners.Length; index++) {
            ref var corner = ref corners[index];
            corner.Y = MathF.Min(top, GetPathHeight(path, corner.X, corner.Z)
                + ElevatedPlatformTopOffset - ElevatedPlatformThickness);
            if (corner.Y > bottom + HeightEpsilon)
                AddBeam(new Vector3(corner.X, bottom, corner.Z), corner, BeamHalfWidth, 0, geometry);
            corner.Y -= BeamHalfWidth;
        }

        for (var index = 0; index < corners.Length; index++) {
            var start = corners[index];
            var end = corners[(index + 1) % corners.Length];
            if (MathF.Min(start.Y, end.Y) - BeamHalfWidth >= bottom)
                AddBeam(start, end, BeamHalfWidth, 0, geometry);
        }

        if (path.SupportOrientation == RCTPathSupportOrientation.NorthWestSouthEast) {
            AddBracePair(corners[0], corners[3], bottom, tall, geometry);
            AddBracePair(corners[1], corners[2], bottom, tall, geometry);
        }
        else {
            AddBracePair(corners[0], corners[1], bottom, tall, geometry);
            AddBracePair(corners[3], corners[2], bottom, tall, geometry);
        }
    }

    private static void AddBracePair(Vector3 start, Vector3 end, float bottom, bool tall, PathRailingMeshGeometry geometry)
    {
        if (MathF.Min(start.Y, end.Y) <= bottom + BeamHalfWidth + HeightEpsilon) return;

        var lowerStart = new Vector3(start.X, bottom + BeamHalfWidth, start.Z);
        var lowerEnd = new Vector3(end.X, bottom + BeamHalfWidth, end.Z);
        if (tall) {
            AddBeam(lowerStart, end, BeamHalfWidth, 0, geometry);
            AddBeam(start, lowerEnd, BeamHalfWidth, 0, geometry);
        }
        else {
            var upperCenter = (start + end) * 0.5f;
            AddBeam(lowerStart, upperCenter, BeamHalfWidth, 0, geometry);
            AddBeam(upperCenter, lowerEnd, BeamHalfWidth, 0, geometry);
        }
    }
}
