using System.Numerics;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Terrain;

namespace RCT3D.Render;

internal static class RenderRegionLayout
{
    public const int RegionSizeInTiles = 8;

    public static int GetRegionCount(int tileCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tileCount);
        return tileCount == 0 ? 0 : 1 + (tileCount - 1) / RegionSizeInTiles;
    }

    public static IEnumerable<RenderRegionCoordinate> Enumerate(int mapWidth, int mapHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(mapWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(mapHeight);

        var regionCountX = GetRegionCount(mapWidth);
        var regionCountY = GetRegionCount(mapHeight);
        for (var regionY = 0; regionY < regionCountY; regionY++)
        for (var regionX = 0; regionX < regionCountX; regionX++)
            yield return new RenderRegionCoordinate(regionX, regionY);
    }

    public static RenderRegionTileBounds GetTileBounds(RenderRegionCoordinate region, int mapWidth, int mapHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(mapWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(mapHeight);

        if ((uint)region.X >= (uint)GetRegionCount(mapWidth) || (uint)region.Y >= (uint)GetRegionCount(mapHeight))
            throw new ArgumentOutOfRangeException(nameof(region));

        var tileX = region.X * RegionSizeInTiles;
        var tileY = region.Y * RegionSizeInTiles;

        return new RenderRegionTileBounds(
            tileX,
            tileY,
            Math.Min(RegionSizeInTiles, mapWidth - tileX),
            Math.Min(RegionSizeInTiles, mapHeight - tileY));
    }
}

internal readonly record struct RenderRegionCoordinate(int X, int Y);

internal readonly record struct RenderRegionTileBounds(int TileX, int TileY, int Width, int Height);

internal readonly record struct AxisAlignedBounds
{
    public Vector3 Min { get; }
    public Vector3 Max { get; }
    public Vector3 Center => Min + (Max - Min) * 0.5f;

    public AxisAlignedBounds(Vector3 min, Vector3 max)
    {
        if (min.X > max.X || min.Y > max.Y || min.Z > max.Z)
            throw new ArgumentException("Bounds minimum must not exceed bounds maximum.");

        Min = min;
        Max = max;
    }

    public AxisAlignedBounds Encapsulate(Vector3 point) => new(Vector3.Min(Min, point), Vector3.Max(Max, point));

    public AxisAlignedBounds Encapsulate(AxisAlignedBounds other) =>
        new(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));
}

internal readonly record struct RenderRegionIndexRange(
    RenderRegionCoordinate Region,
    AxisAlignedBounds Bounds,
    uint FirstIndex,
    uint IndexCount)
{
    public bool IsEmpty => IndexCount == 0;
}

internal static class RenderRegionMeshRanges
{
    public static RenderRegionIndexRange Create(
        RenderRegionCoordinate region,
        List<Vector3> vertices,
        List<uint> indices,
        int firstVertex,
        int firstIndex)
    {
        if (firstVertex < 0 || firstVertex >= vertices.Count)
            throw new ArgumentOutOfRangeException(nameof(firstVertex));
        if (firstIndex < 0 || firstIndex >= indices.Count)
            throw new ArgumentOutOfRangeException(nameof(firstIndex));

        var bounds = new AxisAlignedBounds(vertices[firstVertex], vertices[firstVertex]);
        for (var i = firstVertex + 1; i < vertices.Count; i++)
            bounds = bounds.Encapsulate(vertices[i]);

        return new RenderRegionIndexRange(
            region,
            bounds,
            checked((uint)firstIndex),
            checked((uint)(indices.Count - firstIndex)));
    }

    public static RenderRegionIndexRange Create(
        RenderRegionCoordinate region,
        List<TerrainVertex> vertices,
        List<uint> indices,
        int firstVertex,
        int firstIndex)
    {
        if (firstVertex < 0 || firstVertex >= vertices.Count)
            throw new ArgumentOutOfRangeException(nameof(firstVertex));
        if (firstIndex < 0 || firstIndex >= indices.Count)
            throw new ArgumentOutOfRangeException(nameof(firstIndex));

        var bounds = new AxisAlignedBounds(vertices[firstVertex].Position, vertices[firstVertex].Position);
        for (var i = firstVertex + 1; i < vertices.Count; i++)
            bounds = bounds.Encapsulate(vertices[i].Position);

        return new RenderRegionIndexRange(
            region,
            bounds,
            checked((uint)firstIndex),
            checked((uint)(indices.Count - firstIndex)));
    }

    public static RenderRegionIndexRange Create(
        RenderRegionCoordinate region,
        List<TrackVertex> vertices,
        List<uint> indices,
        int firstVertex,
        int firstIndex)
    {
        if (firstVertex < 0 || firstVertex >= vertices.Count)
            throw new ArgumentOutOfRangeException(nameof(firstVertex));
        if (firstIndex < 0 || firstIndex >= indices.Count)
            throw new ArgumentOutOfRangeException(nameof(firstIndex));

        var bounds = new AxisAlignedBounds(vertices[firstVertex].Position, vertices[firstVertex].Position);
        for (var i = firstVertex + 1; i < vertices.Count; i++)
            bounds = bounds.Encapsulate(vertices[i].Position);

        return new RenderRegionIndexRange(
            region,
            bounds,
            checked((uint)firstIndex),
            checked((uint)(indices.Count - firstIndex)));
    }
}
