using RCT3D.Interop.Models;
using RCT3D.Render.Paths.Meshing;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Terrain;
using System.Numerics;

namespace RCT3D.Render.Paths.Builders;

internal abstract class BambooPathGeometryBuilder(PathMaterialLibrary materials, string objectId, TrackRgba8 railingColor) : PathTypeGeometryBuilder(materials)
{
    private readonly TrackRgba8 _railingColor = railingColor;
    private const int RailingRadialSegments = 5;
    private const int SupportRadialSegments = 6;

    private const float RailHeight = 0.62f;
    private const float RailRadius = 0.045f;
    private const float PostRadius = 0.055f;

    protected override float ElevatedPlatformThickness => 0.06f;

    protected override bool DrawSurfaceOverPlatform => false;

    protected override float GetSupportTopHeight(RCTTilePathData path)
    {
        GetSupportPositions(path, out var first, out var second);
        return MathF.Max(GetPathHeight(path, first.X, first.Y), GetPathHeight(path, second.X, second.Y))
            - ElevatedPlatformThickness;
    }


    protected override uint GetRailingMaterial(RCTTilePathData path) => Materials.GetRailingLayer(TerrainMaterialLibrary.HashObjectId(objectId));

    protected override TrackRgba8 GetRailingColor(RCTTilePathData path) => _railingColor;

    protected override void AddRailingEdge(RCTTilePathData path, byte direction, Vector3 start, Vector3 end, float baseHeight, PathRailingMeshGeometry geometry)
    {
        if (!PrepareRailingEdge(path, direction, ref start, ref end, baseHeight)) return;

        var bottomOffset = Vector3.UnitY * ElevatedPlatformThickness;
        var topOffset = Vector3.UnitY * RailHeight;

        for (var index = 1; index < 6; index += 2) {
            var post = Vector3.Lerp(start, end, index / 6f);
            RoundPoleMeshBuilder.Build(post - bottomOffset, post + topOffset, PostRadius, RailingRadialSegments, 0, geometry);
        }

        RoundPoleMeshBuilder.Build(start + topOffset, end + topOffset, RailRadius, RailingRadialSegments, 0, geometry);
        RoundPoleMeshBuilder.Build(start + topOffset / 3.0f, end + topOffset / 3.0f, RailRadius, RailingRadialSegments, 0, geometry);
    }

    protected override void AddSupports(
        RCTTilePathData path, RCTTileSurfaceData surface, PathRailingMeshGeometry geometry,
        PathSupportStationRegistry supportStations, float? supportBaseHeight)
    {
        GetSupportPositions(path, out var first, out var second);
        AddSupportPole(path, surface, first, geometry, supportStations);
        AddSupportPole(path, surface, second, geometry, supportStations);
    }

    private static void GetSupportPositions(RCTTilePathData path, out Vector2 first, out Vector2 second)
    {
        var center = new Vector2((path.TileX + 0.5f) * WorldUnits.TileSize, (path.TileY + 0.5f) * WorldUnits.TileSize);

        const float SupportOffset = 0.4f * WorldUnits.TileSize;

        var offset = path.SupportOrientation == RCTPathSupportOrientation.NorthWestSouthEast
            ? new Vector2(SupportOffset, 0f)
            : new Vector2(0f, SupportOffset);

        first = center - offset;
        second = center + offset;
    }

    protected override void AddElevatedPlatformSlab(RCTTilePathData path, PathMeshGeometry geometry)
    {
        BuildElevatedPlatformSlab(path, geometry, GetRailingMaterial(path));
    }

    private void AddSupportPole(
        RCTTilePathData path, RCTTileSurfaceData surface, Vector2 position,
        PathRailingMeshGeometry geometry, PathSupportStationRegistry supportStations)
    {
        var bottom = GetSurfaceHeight(surface, path.TileX, path.TileY, position.X, position.Y);
        var top = GetPathHeight(path, position.X, position.Y) - ElevatedPlatformThickness;
        if (top <= bottom + HeightEpsilon) return;

        var start = new Vector3(position.X, bottom, position.Y);
        var end = new Vector3(position.X, top, position.Y);

        if (supportStations.TryAddPole(start, end, geometry.Color))
            RoundPoleMeshBuilder.Build(start, end, SupportHalfWidth, SupportRadialSegments, 0, geometry);
    }
}
