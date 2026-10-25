using RCT3D.Interop;
using RCT3D.Render.Terrain;
using System.Numerics;

namespace RCT3D.Render.Paths.Meshing;

internal abstract class PathTypeGeometryBuilder(PathMaterialLibrary materials)
{
    //public const float TileSize = 3.0f;
    //public const float BaseHeightScale = 0.75f;
    private const float SurfaceBias = 0.008f;
    private const float RailHeight = 0.62f;
    private const float RailThickness = 0.045f;
    private const float RailPostHalfWidth = 0.055f;
    public const float SupportHalfWidth = 0.075f;
    private const float FullSupportSectionHeight = 3.0f;
    public const float CornerInset = WorldUnits.TileSize * 4.0f / 32.0f;
    public const float SupportBaseHalfWidth = 0.24f;
    public const float SupportBaseHeight = 0.18f;
    private const float ElevatedPlatformThickness = 0.18f;
    private const float ElevatedPlatformTopOffset = 0.002f;
    private const float CornerHeightScale = 1.5f;
    protected const float HeightEpsilon = 0.0001f;

    public const byte DirectionWest = 0;
    public const byte DirectionNorth = 1;
    public const byte DirectionEast = 2;
    public const byte DirectionSouth = 3;

    private static readonly byte[] expectedSurfaceSlopeByPathDirection = [12, 9, 3, 6];

    protected PathMaterialLibrary Materials {get;} = materials;


    public void AddPath(RCTTilePathData path, RCTTileSurfaceData surface, PathMeshGeometry geometry, PathSupportStationRegistry supportStations)
    {
        AddSurface(path, geometry);

        if (Materials.HasVisibleRailings(path.RailingsIdHash) && (path.IsQueue || path.IsSloped || path.BaseZ != surface.BaseZ || surface.SlopeFlags != 0)) {
            AddRailings(path, geometry);
        }

        if (ShouldDrawSupports(path, surface)) {
            AddElevatedPlatformSlab(path, geometry);
            AddSupports(path, surface, Materials.GetRailingLayer(path.RailingsIdHash), geometry, supportStations);
        }
    }

    private void AddRailings(RCTTilePathData path, PathMeshGeometry geometry)
    {
        var west = path.TileX * WorldUnits.TileSize;
        var east = (path.TileX + 1) * WorldUnits.TileSize;
        var south = path.TileY * WorldUnits.TileSize;
        var north = (path.TileY + 1) * WorldUnits.TileSize;
        var baseHeight = path.BaseZ * WorldUnits.HeightPerBaseUnit;
        var material = Materials.GetRailingLayer(path.RailingsIdHash);

        if (path.IsSloped) {
            if (path.SlopeDirection is DirectionWest or DirectionEast) {
                if ((path.Edges & (1 << DirectionNorth)) == 0)
                    AddRailingEdge(path, DirectionNorth, new Vector3(west, 0, north), new Vector3(east, 0, north), baseHeight, material, geometry);
                
                if ((path.Edges & (1 << DirectionSouth)) == 0)
                    AddRailingEdge(path, DirectionSouth, new Vector3(west, 0, south), new Vector3(east, 0, south), baseHeight, material, geometry);
            }
            else {
                if ((path.Edges & (1 << DirectionWest)) == 0)
                    AddRailingEdge(path, DirectionWest, new Vector3(west, 0, south), new Vector3(west, 0, north), baseHeight, material, geometry);

                if ((path.Edges & (1 << DirectionEast)) == 0)
                    AddRailingEdge(path, DirectionEast, new Vector3(east, 0, south), new Vector3(east, 0, north), baseHeight, material, geometry);
            }

            return;
        }

        for (byte direction = 0; direction < 4; direction++) {
            if ((path.Edges & (1 << direction)) != 0) continue;

            switch (direction) {
                case DirectionWest:
                    AddRailingEdge(path, direction, new Vector3(west, 0, south), new Vector3(west, 0, north), baseHeight, material, geometry);
                    break;
                case DirectionNorth:
                    AddRailingEdge(path, direction, new Vector3(west, 0, north), new Vector3(east, 0, north), baseHeight, material, geometry);
                    break;
                case DirectionEast:
                    AddRailingEdge(path, direction, new Vector3(east, 0, south), new Vector3(east, 0, north), baseHeight, material, geometry);
                    break;
                case DirectionSouth:
                    AddRailingEdge(path, direction, new Vector3(west, 0, south), new Vector3(east, 0, south), baseHeight, material, geometry);
                    break;
            }
        }
    }

    protected abstract void AddSupports(
        RCTTilePathData path, RCTTileSurfaceData surface, uint material, PathMeshGeometry geometry,
        PathSupportStationRegistry supportStations);

    protected void AddSurface(RCTTilePathData path, PathMeshGeometry geometry)
    {
        var west = path.TileX * WorldUnits.TileSize;
        var east = (path.TileX + 1) * WorldUnits.TileSize;
        var south = path.TileY * WorldUnits.TileSize;
        var north = (path.TileY + 1) * WorldUnits.TileSize;

        var baseHeight = path.BaseZ * WorldUnits.HeightPerBaseUnit;

        var southWest = new Vector3(west, GetPathCornerHeight(path, true, false, baseHeight) + SurfaceBias, south);
        var southEast = new Vector3(east, GetPathCornerHeight(path, false, false, baseHeight) + SurfaceBias, south);
        var northEast = new Vector3(east, GetPathCornerHeight(path, false, true, baseHeight) + SurfaceBias, north);
        var northWest = new Vector3(west, GetPathCornerHeight(path, true, true, baseHeight) + SurfaceBias, north);

        var material = Materials.GetLayer(path.SurfaceIdHash);

        geometry.AddTriangle(northEast, new Vector2(1, 1), southEast, new Vector2(1, 0), northWest, new Vector2(0, 1), material);
        geometry.AddTriangle(southEast, new Vector2(1, 0), southWest, new Vector2(0, 0), northWest, new Vector2(0, 1), material);
    }

    protected abstract void AddElevatedPlatformSlab(RCTTilePathData path, PathMeshGeometry geometry);

    protected void BuildElevatedPlatformSlab(RCTTilePathData path, PathMeshGeometry geometry, uint material)
    {
        var west = path.TileX * WorldUnits.TileSize;
        var east = (path.TileX + 1) * WorldUnits.TileSize;
        var south = path.TileY * WorldUnits.TileSize;
        var north = (path.TileY + 1) * WorldUnits.TileSize;

        var baseHeight = path.BaseZ * WorldUnits.HeightPerBaseUnit;
        var topOffset = ElevatedPlatformTopOffset;

        var topSouthWest = new Vector3(west, GetPathCornerHeight(path, true, false, baseHeight) + topOffset, south);
        var topSouthEast = new Vector3(east, GetPathCornerHeight(path, false, false, baseHeight) + topOffset, south);
        var topNorthEast = new Vector3(east, GetPathCornerHeight(path, false, true, baseHeight) + topOffset, north);
        var topNorthWest = new Vector3(west, GetPathCornerHeight(path, true, true, baseHeight) + topOffset, north);

        var bottomSouthWest = topSouthWest - Vector3.UnitY * ElevatedPlatformThickness;
        var bottomSouthEast = topSouthEast - Vector3.UnitY * ElevatedPlatformThickness;
        var bottomNorthEast = topNorthEast - Vector3.UnitY * ElevatedPlatformThickness;
        var bottomNorthWest = topNorthWest - Vector3.UnitY * ElevatedPlatformThickness;

        var center = (topSouthWest + topSouthEast + topNorthEast + topNorthWest
            + bottomSouthWest + bottomSouthEast + bottomNorthEast + bottomNorthWest) / 8.0f;

        geometry.AddPrismFace(topSouthWest, topSouthEast, topNorthEast, topNorthWest, center, material);
        geometry.AddPrismFace(bottomSouthWest, bottomNorthWest, bottomNorthEast, bottomSouthEast, center, material);
        geometry.AddPrismFace(topSouthWest, topNorthWest, bottomNorthWest, bottomSouthWest, center, material);
        geometry.AddPrismFace(topSouthEast, bottomSouthEast, bottomNorthEast, topNorthEast, center, material);
        geometry.AddPrismFace(topSouthWest, bottomSouthWest, bottomSouthEast, topSouthEast, center, material);
        geometry.AddPrismFace(topNorthWest, topNorthEast, bottomNorthEast, bottomNorthWest, center, material);
    }

    protected static List<(float Bottom, float Top)> GetSupportColumnSections(
        RCTTilePathData path, RCTTileSurfaceData surface, Vector2 position)
    {
        var sections = new List<(float Bottom, float Top)>();
        var bottom = GetSurfaceHeight(surface, path.TileX, path.TileY, position.X, position.Y);
        var top = GetPathHeight(path, position.X, position.Y);

        while (top > bottom + HeightEpsilon) {
            var sectionTop = MathF.Min(bottom + FullSupportSectionHeight, top);
            sections.Add((bottom, sectionTop));
            bottom = sectionTop;
        }

        return sections;
    }

    protected static void AddSupportColumn(Vector2 position, List<(float Bottom, float Top)> sections, uint material, PathMeshGeometry geometry)
    {
        foreach (var section in sections)
            AddVerticalBeam(new Vector3(position.X, section.Bottom, position.Y), SupportHalfWidth, section.Bottom, section.Top, material, geometry);
    }

    protected static void AddDiagonalBraces(
        Vector2 start, (float Bottom, float Top) startSection,
        Vector2 end, (float Bottom, float Top) endSection,
        uint material, PathMeshGeometry geometry)
    {
        AddBeam(new Vector3(start.X, startSection.Bottom, start.Y), new Vector3(end.X, endSection.Top, end.Y),
            SupportHalfWidth, material, geometry);

        AddBeam(new Vector3(start.X, startSection.Top, start.Y), new Vector3(end.X, endSection.Bottom, end.Y),
            SupportHalfWidth, material, geometry);
    }

    protected abstract void AddRailingEdge(RCTTilePathData path, byte direction, Vector3 start, Vector3 end, float baseHeight, uint material, PathMeshGeometry geometry);

    protected static void BuildRailingEdge(RCTTilePathData path, byte direction, Vector3 start, Vector3 end, float baseHeight, uint material, int postCount, PathMeshGeometry geometry)
    {
        var tileSize = WorldUnits.TileSize;
        var startHeight = GetPathCornerHeight(path, start.X == path.TileX * tileSize, start.Z == (path.TileY + 1) * tileSize, baseHeight);
        var endHeight = GetPathCornerHeight(path, end.X == path.TileX * tileSize, end.Z == (path.TileY + 1) * tileSize, baseHeight);
        start.Y = startHeight;
        end.Y = endHeight;

        var startCorner = CornerBitForEdge(direction, atStart: true);
        var endCorner = CornerBitForEdge(direction, atStart: false);
        var startConnectedDiagonally = (path.Corners & (1 << startCorner)) != 0;
        var endConnectedDiagonally = (path.Corners & (1 << endCorner)) != 0;
        var edge = end - start;
        var edgeLength = edge.Length();
        if (edgeLength <= 2 * CornerInset) return;

        var originalStartHeight = startHeight;
        var originalEndHeight = endHeight;
        var edgeDirection = Vector3.Normalize(edge);

        if (startConnectedDiagonally) {
            start += edgeDirection * CornerInset;
            startHeight = originalStartHeight + (originalEndHeight - originalStartHeight) * (CornerInset / edgeLength);
            start.Y = startHeight;
        }

        if (endConnectedDiagonally) {
            end -= edgeDirection * CornerInset;
            endHeight = originalStartHeight + (originalEndHeight - originalStartHeight) * (1.0f - CornerInset / edgeLength);
            end.Y = endHeight;
        }

        for (var index = 0; index < postCount; index++) {
            var ratio = postCount == 1 ? 0.5f : (float)index / (postCount - 1);
            var post = Vector3.Lerp(start, end, ratio);
            AddVerticalBeam(post, RailPostHalfWidth, post.Y, post.Y + RailHeight, material, geometry);
        }

        AddRailBeam(start, end, RailHeight, material, geometry);
    }

    private static int CornerBitForEdge(byte direction, bool atStart)
    {
        return (direction, atStart) switch {
            (DirectionWest, true) => 3,
            (DirectionWest, false) => 0,
            (DirectionNorth, true) => 0,
            (DirectionNorth, false) => 1,
            (DirectionEast, true) => 2,
            (DirectionEast, false) => 1,
            (DirectionSouth, true) => 3,
            (DirectionSouth, false) => 2,
            _ => 0,
        };
    }

    private static void AddRailBeam(Vector3 start, Vector3 end, float height, uint material, PathMeshGeometry geometry)
    {
        start.Y += height;
        end.Y += height;
        AddBeam(start, end, RailThickness, material, geometry);
    }

    protected static void AddBeam(Vector3 start, Vector3 end, float halfWidth, uint material, PathMeshGeometry geometry)
    {
        var axis = end - start;
        if (axis.LengthSquared() < 0.0001f) {
            var center = start;
            start = center - Vector3.UnitY * (RailHeight * 0.5f);
            end = center + Vector3.UnitY * (RailHeight * 0.5f);
            axis = end - start;
        }

        axis = Vector3.Normalize(axis);
        var side = Vector3.Cross(Vector3.UnitY, axis);
        side = side.LengthSquared() < 0.0001f ? Vector3.UnitX : Vector3.Normalize(side);
        var up = Vector3.Normalize(Vector3.Cross(axis, side));

        Span<Vector3> crossSection = stackalloc Vector3[4] {
            -side * halfWidth - up * halfWidth,
            side * halfWidth - up * halfWidth,
            side * halfWidth + up * halfWidth,
            -side * halfWidth + up * halfWidth,
        };

        Span<Vector3> points = stackalloc Vector3[8];
        for (var i = 0; i < 4; i++) {
            points[i] = start + crossSection[i];
            points[i + 4] = end + crossSection[i];
        }

        var centerPoint = (start + end) * 0.5f;
        geometry.AddPrismFace(points[0], points[1], points[5], points[4], centerPoint, material);
        geometry.AddPrismFace(points[1], points[2], points[6], points[5], centerPoint, material);
        geometry.AddPrismFace(points[2], points[3], points[7], points[6], centerPoint, material);
        geometry.AddPrismFace(points[3], points[0], points[4], points[7], centerPoint, material);
        geometry.AddPrismFace(points[0], points[3], points[2], points[1], centerPoint, material);
        geometry.AddPrismFace(points[4], points[5], points[6], points[7], centerPoint, material);
    }

    private static void AddVerticalBeam(Vector3 center, float halfWidth, float bottom, float top, uint material, PathMeshGeometry geometry)
    {
        AddBox(new Vector3(center.X, bottom, center.Z), halfWidth, top, material, geometry);
    }

    protected static void AddBox(Vector3 centerAtBottom, float halfWidth, float top, uint material, PathMeshGeometry geometry)
    {
        var bottom = centerAtBottom.Y;
        if (top <= bottom + HeightEpsilon) return;

        var minX = centerAtBottom.X - halfWidth;
        var maxX = centerAtBottom.X + halfWidth;
        var minZ = centerAtBottom.Z - halfWidth;
        var maxZ = centerAtBottom.Z + halfWidth;
        var low = new Vector3(minX, bottom, minZ);
        var high = new Vector3(maxX, top, maxZ);
        var center = (low + high) * 0.5f;

        geometry.AddPrismFace(new(minX, bottom, minZ), new(maxX, bottom, minZ), new(maxX, top, minZ), new(minX, top, minZ), center, material);
        geometry.AddPrismFace(new(maxX, bottom, minZ), new(maxX, bottom, maxZ), new(maxX, top, maxZ), new(maxX, top, minZ), center, material);
        geometry.AddPrismFace(new(maxX, bottom, maxZ), new(minX, bottom, maxZ), new(minX, top, maxZ), new(maxX, top, maxZ), center, material);
        geometry.AddPrismFace(new(minX, bottom, maxZ), new(minX, bottom, minZ), new(minX, top, minZ), new(minX, top, maxZ), center, material);
        geometry.AddPrismFace(new(minX, bottom, minZ), new(minX, bottom, maxZ), new(maxX, bottom, maxZ), new(maxX, bottom, minZ), center, material);
        geometry.AddPrismFace(new(minX, top, minZ), new(maxX, top, minZ), new(maxX, top, maxZ), new(minX, top, maxZ), center, material);
    }

    private static bool ShouldDrawSupports(RCTTilePathData path, RCTTileSurfaceData surface)
    {
        if (surface.BaseZ < path.BaseZ) return true;
        if (surface.BaseZ > path.BaseZ) return false;
        if (!path.IsSloped) return surface.SlopeFlags != 0;
        
        return path.SlopeDirection <= DirectionSouth && surface.SlopeFlags != expectedSurfaceSlopeByPathDirection[path.SlopeDirection];
    }

    protected static float GetPathHeight(RCTTilePathData path, float worldX, float worldZ)
    {
        var baseHeight = path.BaseZ * WorldUnits.HeightPerBaseUnit;

        return InterpolateTileHeight(
            (worldX - path.TileX * WorldUnits.TileSize) / WorldUnits.TileSize,
            (worldZ - path.TileY * WorldUnits.TileSize) / WorldUnits.TileSize,
            GetPathCornerHeight(path, false, true, baseHeight),
            GetPathCornerHeight(path, false, false, baseHeight),
            GetPathCornerHeight(path, true, false, baseHeight),
            GetPathCornerHeight(path, true, true, baseHeight));
    }

    private static float GetPathCornerHeight(RCTTilePathData path, bool west, bool north, float baseHeight)
    {
        if (!path.IsSloped) return baseHeight;

        var uphill = path.SlopeDirection switch {
            DirectionWest => west,
            DirectionNorth => north,
            DirectionEast => !west,
            DirectionSouth => !north,
            _ => false,
        };

        return baseHeight + (uphill ? 2.0f * WorldUnits.HeightPerBaseUnit : 0.0f);
    }

    protected static float GetSurfaceHeight(RCTTileSurfaceData surface, int tileX, int tileY, float worldX, float worldZ)
    {
        var baseHeight = surface.BaseZ * WorldUnits.HeightPerBaseUnit;

        return InterpolateTileHeight(
            (worldX - tileX * WorldUnits.TileSize) / WorldUnits.TileSize,
            (worldZ - tileY * WorldUnits.TileSize) / WorldUnits.TileSize,
            baseHeight + surface.NorthCornerHeight * CornerHeightScale,
            baseHeight + surface.EastCornerHeight * CornerHeightScale,
            baseHeight + surface.SouthCornerHeight * CornerHeightScale,
            baseHeight + surface.WestCornerHeight * CornerHeightScale);
    }

    private static float InterpolateTileHeight(float x, float z, float northEast, float southEast, float southWest, float northWest)
    {
        if (x + z >= 1.0f) {
            return northEast * (x + z - 1.0f) + southEast * (1.0f - z) + northWest * (1.0f - x);
        }

        return southEast * x + southWest * (1.0f - x - z) + northWest * z;
    }
}

internal static class PathGeometryBuilderResolver
{
    private static readonly uint ConcreteHash = TerrainMaterialLibrary.HashObjectId("rct2.footpath_railings.concrete");
    private static readonly uint ConcreteGreenHash = TerrainMaterialLibrary.HashObjectId("rct2.footpath_railings.concrete_green");
    //private static readonly uint SpaceHash = TerrainMaterialLibrary.HashObjectId("rct2.footpath_railings.space");
    //private static readonly uint BambooBlackHash = TerrainMaterialLibrary.HashObjectId("rct2.footpath_railings.bamboo_black");
    //private static readonly uint BambooBrownHash = TerrainMaterialLibrary.HashObjectId("rct2.footpath_railings.bamboo_brown");

    public static PathTypeGeometryBuilder Resolve(PathMaterialLibrary materials, uint railingsIdHash)
    {
        if (railingsIdHash == ConcreteHash || railingsIdHash == ConcreteGreenHash)
            return new ConcretePathGeometryBuilder(materials);

        //if (railingsIdHash == SpaceHash || railingsIdHash == BambooBlackHash || railingsIdHash == BambooBrownHash)
        //    return new SpaceBambooPathGeometryBuilder(materials);

        return new WoodenPathGeometryBuilder(materials);
    }
}
