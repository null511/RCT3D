using RCT3D.Interop.Models;
using RCT3D.Render.Terrain;
using System.Numerics;

namespace RCT3D.Render;

internal sealed class MapOccupancyMap
{
    private readonly RCTTileSurfaceData[] terrainSurfaces;
    private readonly Dictionary<(int TileX, int TileY), List<OccupiedObject>> occupiedByTile = new();
    private readonly Dictionary<RCTTrackElementData, Vector3[][]> trackCenterlines = new();

    public int Width { get; }
    public int Height { get; }
    public IEnumerable<RouteTileOccupancy> RouteTiles => EnumerateRouteTiles();

    public MapOccupancyMap(ReadOnlySpan<RCTTileSurfaceData> surfaces, int width, int height)
    {
        if (width <= 0 || height <= 0 || surfaces.Length < checked(width * height))
            throw new ArgumentOutOfRangeException(nameof(width));

        Width = width;
        Height = height;
        terrainSurfaces = surfaces[..checked(width * height)].ToArray();
    }

    public void AddPath(in RCTTilePathData path)
    {
        var baseHeight = path.BaseZ * WorldUnits.HeightPerBaseUnit;
        var topHeight = path.ClearanceHeight * WorldUnits.HeightPerBaseUnit;
        Add(path.TileX, path.TileY, baseHeight, topHeight, null, true, false, 0, 0, path.Edges, 0);
    }

    public void AddTrack(in RCTTrackElementData track)
    {
        var bottomHeight = track.BaseHeight * WorldUnits.HeightPerBaseUnit;
        var topHeight = track.ClearanceHeight * WorldUnits.HeightPerBaseUnit;
        Add(track.TileX, track.TileY, bottomHeight, topHeight, track, true, true, track.Direction,
            track.TunnelSideMask, track.ConnectionSideMask, track.RideId);
    }

    public float GetTerrainHeight(int tileX, int tileY, float tileU, float tileV)
    {
        if ((uint)tileX >= (uint)Width || (uint)tileY >= (uint)Height)
            throw new ArgumentOutOfRangeException(nameof(tileX));

        return TerrainSurfaceSampler.GetHeight(terrainSurfaces[tileY * Width + tileX], tileU, tileV);
    }

    public bool IsTerrainAbove(int tileX, int tileY, float tileU, float tileV, float height) =>
        GetTerrainHeight(tileX, tileY, tileU, tileV) > height;

    public void AddTrackCenterlines(RCTTrackElementData track, Vector3[][] paths) =>
        trackCenterlines[track] = paths;

    public IEnumerable<Vector3> GetTrackEdgeCrossings(RouteTileOccupancy route, TerrainSide side)
    {
        if (route.TrackElement is not { } track || !trackCenterlines.TryGetValue(track, out var paths))
            yield break;

        var xAxis = side is TerrainSide.West or TerrainSide.East;
        var edge = (xAxis ? route.TileX + (side == TerrainSide.East ? 1 : 0)
            : route.TileY + (side == TerrainSide.North ? 1 : 0)) * WorldUnits.TileSize;
        var start = (xAxis ? route.TileY : route.TileX) * WorldUnits.TileSize;
        foreach (var points in paths)
        for (var index = 1; index < points.Length; index++) {
            var first = points[index - 1];
            var second = points[index];
            var firstAxis = xAxis ? first.X : first.Z;
            var delta = (xAxis ? second.X : second.Z) - firstAxis;
            if (MathF.Abs(delta) < 0.00001f) continue;
            var amount = (edge - firstAxis) / delta;
            if (amount < -0.00001f || amount > 1.00001f) continue;
            var crossing = Vector3.Lerp(first, second, Math.Clamp(amount, 0.0f, 1.0f));
            var along = xAxis ? crossing.Z : crossing.X;
            if (along > start && along < start + WorldUnits.TileSize
                && crossing.Y >= route.FloorHeight && crossing.Y <= route.ClearanceHeight)
                yield return crossing;
        }
    }

    public bool TryGetHighestIntersection(
        int tileX,
        int tileY,
        float supportBottom,
        float supportTop,
        RCTTrackElementData? hostTrack,
        out float highestObjectTop)
    {
        highestObjectTop = float.NegativeInfinity;
        if (!occupiedByTile.TryGetValue((tileX, tileY), out var occupiedObjects))
            return false;

        foreach (var occupiedObject in occupiedObjects) {
            if (hostTrack.HasValue && occupiedObject.TrackElement is { } track && track.Equals(hostTrack.Value))
                continue;

            if (occupiedObject.TopHeight < supportBottom || occupiedObject.BottomHeight > supportTop)
                continue;

            highestObjectTop = MathF.Max(highestObjectTop, occupiedObject.TopHeight);
        }

        return !float.IsNegativeInfinity(highestObjectTop);
    }

    private void Add(
        int tileX,
        int tileY,
        float bottomHeight,
        float topHeight,
        RCTTrackElementData? trackElement,
        bool isRoute,
        bool isTrack,
        byte direction,
        byte tunnelSideMask,
        byte connectionSideMask,
        ushort rideId)
    {
        if ((uint)tileX >= (uint)Width || (uint)tileY >= (uint)Height)
            throw new ArgumentOutOfRangeException(nameof(tileX));
        if (!float.IsFinite(bottomHeight) || !float.IsFinite(topHeight) || topHeight < bottomHeight)
            throw new ArgumentOutOfRangeException(nameof(topHeight));

        if (!occupiedByTile.TryGetValue((tileX, tileY), out var occupiedObjects)) {
            occupiedObjects = new List<OccupiedObject>();
            occupiedByTile.Add((tileX, tileY), occupiedObjects);
        }

        occupiedObjects.Add(new OccupiedObject(
            bottomHeight, topHeight, trackElement, isRoute, isTrack, direction, tunnelSideMask, connectionSideMask, rideId));
    }

    private IEnumerable<RouteTileOccupancy> EnumerateRouteTiles()
    {
        var routeId = 0;
        foreach (var (tile, objects) in occupiedByTile)
        foreach (var item in objects) {
            if (!item.IsRoute)
                continue;

            yield return new RouteTileOccupancy(
                routeId++, tile.TileX, tile.TileY, item.BottomHeight, item.TopHeight,
                item.IsTrack, item.Direction, item.TunnelSideMask, item.ConnectionSideMask, item.RideId, item.TrackElement);
        }
    }

    private readonly record struct OccupiedObject(
        float BottomHeight,
        float TopHeight,
        RCTTrackElementData? TrackElement,
        bool IsRoute,
        bool IsTrack,
        byte Direction,
        byte TunnelSideMask,
        byte ConnectionSideMask,
        ushort RideId);
}

internal readonly record struct RouteTileOccupancy(
    int RouteId,
    int TileX,
    int TileY,
    float FloorHeight,
    float ClearanceHeight,
    bool IsTrack,
    byte Direction,
    byte TunnelSideMask,
    byte ConnectionSideMask,
    ushort RideId,
    RCTTrackElementData? TrackElement);
