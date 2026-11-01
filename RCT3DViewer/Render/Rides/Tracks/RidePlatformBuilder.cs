using RCT3D.Interop.Models;
using System.Numerics;
using Serilog;

namespace RCT3D.Render.Rides.Tracks;

[Flags]
internal enum PlatformSideOpenings : byte
{
    None = 0,
    West = 1,
    North = 2,
    East = 4,
    South = 8,
}

internal sealed class RidePlatformBuilder
{
    public readonly record struct PlatformTile(
        RCTTrackElementData Track,
        RidePlatformSurfaceType SurfaceType,
        RidePlatformRailingType RailingType,
        PlatformSideOpenings Openings,
        bool HasWestSide,
        bool HasNorthSide,
        bool HasEastSide,
        bool HasSouthSide);

    private const float SurfaceHalfHeight = 0.16f;
    private static readonly TrackRgba8 WoodSurfaceColor = new(99, 75, 45, 255);
    private static readonly TrackRailSpecularData WoodSurfaceSpecular = new(225, 0);
    private static readonly TrackRgba8 MetalSurfaceColor = new(90, 90, 90, 255);
    private static readonly TrackRailSpecularData MetalSurfaceSpecular = new(100, 25);
    private static readonly TrackRgba8 FiberSurfaceColor = new(115, 115, 115, 255);
    private static readonly TrackRailSpecularData FiberSurfaceSpecular = new(190, 0);
    private const float RailingHeight = 0.62f;
    private const float RailingHalfWidth = 0.035f;

    private const float ConcretePostRadius = 0.08f;
    private const float ConcretePostCapRadius = 0.02f;
    private const float ConcretePostHeight = 0.38f;
    private const int ConcretePostFaceCount = 6;
    private static readonly TrackRgba8 ConcretePostColor = new(105, 105, 105, 255);
    private static readonly TrackRailSpecularData ConcretePostSpecular = new(245, 0);

    private static readonly TrackRgba8 FencedRailingColor = new(110, 82, 45, 255);
    private static readonly TrackRailSpecularData FencedRailingSpecular = new(220, 0);


    private readonly record struct PlatformPieceKey(
        ushort RideId,
        RCTRideType RideType,
        RCTTrackElementType TrackType,
        int OriginTileX,
        int OriginTileY,
        byte Direction,
        int OriginBaseHeight);

    public Dictionary<RenderRegionCoordinate, List<PlatformTile>> BuildTilesByRegion(
        ReadOnlySpan<RCTTrackElementData> trackElements,
        ReadOnlySpan<RCTRideEntranceData> entrances,
        IReadOnlyDictionary<RCTRideType, TrackRideConfiguration> rideConfigurations,
        int mapWidth,
        int mapHeight)
    {
        var pieces = new Dictionary<PlatformPieceKey, List<RCTTrackElementData>>();
        var entranceOpenings = BuildEntranceEdgeOpenings(trackElements, entrances);

        foreach (ref readonly var track in trackElements) {
            if (track.IsRidePlatformBase == 0) continue;

            if (track.IsRidePlatformBase != 1)
                throw new InvalidDataException("OpenRCT2 returned an invalid ride platform marker.");

            if ((uint)track.TileX >= (uint)mapWidth || (uint)track.TileY >= (uint)mapHeight)
                throw new InvalidDataException($"OpenRCT2 returned a ride platform outside the {mapWidth}x{mapHeight} map.");

            if (track.Direction > 3)
                throw new InvalidDataException($"OpenRCT2 returned an invalid ride platform direction at ({track.TileX}, {track.TileY}).");

            if (track.SequenceCount == 0 || track.Sequence >= track.SequenceCount)
                throw new InvalidDataException($"OpenRCT2 returned an invalid ride platform sequence at ({track.TileX}, {track.TileY}).");

            var offset = RotateSequenceOffset(track.SequenceOffsetX, track.SequenceOffsetY, track.Direction);

            var key = new PlatformPieceKey(
                track.RideId,
                track.RideType,
                track.Part,
                track.TileX - offset.X,
                track.TileY - offset.Y,
                track.Direction,
                track.BaseHeight - track.SequenceOffsetZ);

            if (!pieces.TryGetValue(key, out var pieceElements)) {
                pieceElements = new List<RCTTrackElementData>(track.SequenceCount);
                pieces.Add(key, pieceElements);
            }

            pieceElements.Add(track);
        }

        var tilesByRegion = new Dictionary<RenderRegionCoordinate, List<PlatformTile>>();

        foreach (var (key, pieceElements) in pieces) {
            if (!HasCompletePieceSequences(pieceElements))
                Log.Warning(
                    "Ride platform piece {TrackType} for ride {RideId} at ({TileX}, {TileY}) has incomplete sequence data; processing {RetainedSequenceCount} retained tiles",
                    key.TrackType, key.RideId, key.OriginTileX, key.OriginTileY, pieceElements.Count);

            var platformCoordinates = pieceElements
                .Select(track => (track.TileX, track.TileY))
                .ToHashSet();

            foreach (var track in pieceElements) {
                var platformTile = new PlatformTile(
                    track,
                    rideConfigurations.TryGetValue(track.RideType, out var configuration)
                        ? configuration.PlatformSurface
                        : RidePlatformSurfaceType.Wood,
                    rideConfigurations.TryGetValue(track.RideType, out configuration)
                        ? configuration.PlatformRailing
                        : RidePlatformRailingType.Chained,
                    entranceOpenings.GetValueOrDefault((track.RideId, track.TileX, track.TileY, track.BaseHeight)),
                    !platformCoordinates.Contains((track.TileX - 1, track.TileY)),
                    !platformCoordinates.Contains((track.TileX, track.TileY + 1)),
                    !platformCoordinates.Contains((track.TileX + 1, track.TileY)),
                    !platformCoordinates.Contains((track.TileX, track.TileY - 1)));
                var region = new RenderRegionCoordinate(
                    Math.Clamp(track.TileX / RenderRegionLayout.RegionSizeInTiles, 0, Math.Max(0, RenderRegionLayout.GetRegionCount(mapWidth) - 1)),
                    Math.Clamp(track.TileY / RenderRegionLayout.RegionSizeInTiles, 0, Math.Max(0, RenderRegionLayout.GetRegionCount(mapHeight) - 1)));

                if (!tilesByRegion.TryGetValue(region, out var regionTiles)) {
                    regionTiles = new List<PlatformTile>();
                    tilesByRegion.Add(region, regionTiles);
                }

                regionTiles.Add(platformTile);
            }
        }

        return tilesByRegion;
    }

    public void AddTile(in PlatformTile platformTile, TrackMeshGeometry geometry, RCTTileSurfaceData surface)
    {
        var track = platformTile.Track;
        var (surfaceColor, surfaceSpecular) = platformTile.SurfaceType switch {
            RidePlatformSurfaceType.Wood => (WoodSurfaceColor, WoodSurfaceSpecular),
            RidePlatformSurfaceType.Metal => (MetalSurfaceColor, MetalSurfaceSpecular),
            RidePlatformSurfaceType.Fiber => (FiberSurfaceColor, FiberSurfaceSpecular),
            _ => throw new InvalidDataException($"Invalid ride platform surface type {platformTile.SurfaceType}.")
        };

        var minX = track.TileX * WorldUnits.TileSize;
        var minZ = track.TileY * WorldUnits.TileSize;
        var maxX = minX + WorldUnits.TileSize;
        var maxZ = minZ + WorldUnits.TileSize;

        var topY = track.BaseHeight * WorldUnits.HeightPerBaseUnit + SurfaceHalfHeight;
        var terrainBaseY = surface.BaseZ * WorldUnits.HeightPerBaseUnit;

        var southWestBottomY = MathF.Min(topY, terrainBaseY + surface.SouthCornerHeight * WorldUnits.TerrainCornerHeightScale);
        var northWestBottomY = MathF.Min(topY, terrainBaseY + surface.WestCornerHeight * WorldUnits.TerrainCornerHeightScale);
        var northEastBottomY = MathF.Min(topY, terrainBaseY + surface.NorthCornerHeight * WorldUnits.TerrainCornerHeightScale);
        var southEastBottomY = MathF.Min(topY, terrainBaseY + surface.EastCornerHeight * WorldUnits.TerrainCornerHeightScale);

        var southWest = new Vector3(minX, topY, minZ);
        var northWest = new Vector3(minX, topY, maxZ);
        var northEast = new Vector3(maxX, topY, maxZ);
        var southEast = new Vector3(maxX, topY, minZ);

        var southWestBottom = new Vector3(minX, southWestBottomY, minZ);
        var northWestBottom = new Vector3(minX, northWestBottomY, maxZ);
        var northEastBottom = new Vector3(maxX, northEastBottomY, maxZ);
        var southEastBottom = new Vector3(maxX, southEastBottomY, minZ);

        // top
        AddQuad(geometry, southWest, northWest, northEast, southEast, Vector3.UnitY, surfaceColor, surfaceSpecular);

        if (platformTile.HasWestSide) {
            AddQuad(geometry, southWest, southWestBottom, northWestBottom, northWest, -Vector3.UnitX, surfaceColor, surfaceSpecular);

            if ((platformTile.Openings & PlatformSideOpenings.West) == 0)
                AddRailing(geometry, new Vector3(minX, topY, minZ), new Vector3(minX, topY, maxZ), Vector3.UnitX, platformTile.RailingType);
        }

        if (platformTile.HasNorthSide) {
            AddQuad(geometry, northWest, northWestBottom, northEastBottom, northEast, Vector3.UnitZ, surfaceColor, surfaceSpecular);

            if ((platformTile.Openings & PlatformSideOpenings.North) == 0)
                AddRailing(geometry, new Vector3(minX, topY, maxZ), new Vector3(maxX, topY, maxZ), -Vector3.UnitZ, platformTile.RailingType);
        }

        if (platformTile.HasEastSide) {
            AddQuad(geometry, southEast, northEast, northEastBottom, southEastBottom, Vector3.UnitX, surfaceColor, surfaceSpecular);

            if ((platformTile.Openings & PlatformSideOpenings.East) == 0)
                AddRailing(geometry, new Vector3(maxX, topY, maxZ), new Vector3(maxX, topY, minZ), -Vector3.UnitX, platformTile.RailingType);
        }

        if (platformTile.HasSouthSide) {
            AddQuad(geometry, southWest, southEast, southEastBottom, southWestBottom, -Vector3.UnitZ, surfaceColor, surfaceSpecular);

            if ((platformTile.Openings & PlatformSideOpenings.South) == 0)
                AddRailing(geometry, new Vector3(maxX, topY, minZ), new Vector3(minX, topY, minZ), Vector3.UnitZ, platformTile.RailingType);
        }
    }

    private static void AddRailing(TrackMeshGeometry geometry, Vector3 start, Vector3 end, Vector3 axis, RidePlatformRailingType type)
    {
        var direction = Vector3.Normalize(end - start);
        
        if (type == RidePlatformRailingType.Fenced) {
            var length = Vector3.Distance(start, end);
            var postCount = 8;

            for (var i = 0; i <= postCount; i++) {
                var postBase = start + direction * (length * i / postCount);
                AddBeam(geometry, postBase, postBase + Vector3.UnitY * RailingHeight, RailingHalfWidth, FencedRailingColor, FencedRailingSpecular);
            }

            var railBottom = start + Vector3.UnitY * 0.12f;
            var railTop = start + Vector3.UnitY * RailingHeight;
            AddBeam(geometry, railBottom, railBottom + direction * length, RailingHalfWidth, FencedRailingColor, FencedRailingSpecular);
            AddBeam(geometry, railTop, railTop + direction * length, RailingHalfWidth, FencedRailingColor, FencedRailingSpecular);

            for (var i = 1; i < postCount * 2; i++) {
                var picketBase = start + direction * (length * i / (postCount * 2)) + Vector3.UnitY * 0.12f;
                AddBeam(geometry, picketBase, picketBase + Vector3.UnitY * (RailingHeight - 0.12f), RailingHalfWidth * 0.5f, FencedRailingColor, FencedRailingSpecular);
            }
        }
        else {
            var beam_length = 3f - 0.2f;

            for (var i = 0; i <= 2; i++) {
                var postBase = start + direction * (beam_length * (i / 2f) + 0.1f);
                postBase += ConcretePostRadius * axis;

                AddConcretePost(geometry, postBase);
            }

            // TODO: add flat planes for chain
        }
    }

    private static void AddBeam(
        TrackMeshGeometry geometry,
        Vector3 start,
        Vector3 end,
        float halfWidth,
        TrackRgba8 color,
        TrackRailSpecularData specular)
    {
        var axis = Vector3.Normalize(end - start);
        var side = MathF.Abs(Vector3.Dot(axis, Vector3.UnitY)) > 0.99f
            ? Vector3.UnitX
            : Vector3.Normalize(Vector3.Cross(Vector3.UnitY, axis));

        var up = Vector3.Normalize(Vector3.Cross(axis, side));
        var sideOffset = side * halfWidth;
        var upOffset = up * halfWidth;
        var a = start - sideOffset - upOffset;
        var b = start + sideOffset - upOffset;
        var c = end + sideOffset - upOffset;
        var d = end - sideOffset - upOffset;
        var e = start - sideOffset + upOffset;
        var f = start + sideOffset + upOffset;
        var g = end + sideOffset + upOffset;
        var h = end - sideOffset + upOffset;

        AddQuad(geometry, a, b, c, d, -up, color, specular);
        AddQuad(geometry, e, h, g, f, up, color, specular);
        AddQuad(geometry, a, e, f, b, -axis, color, specular);
        AddQuad(geometry, d, c, g, h, axis, color, specular);
        AddQuad(geometry, a, d, h, e, -side, color, specular);
        AddQuad(geometry, b, f, g, c, side, color, specular);
    }

    private static void AddConcretePost(
        TrackMeshGeometry geometry,
        Vector3 bottomCenter)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ConcretePostFaceCount, 3);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ConcretePostRadius, 0.0f);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ConcretePostCapRadius, 0.0f);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ConcretePostCapRadius, ConcretePostRadius);

        var topCenter = bottomCenter + Vector3.UnitY * ConcretePostHeight;
        var cylinderTop = topCenter - Vector3.UnitY * ConcretePostCapRadius;
        var wallBottom = new uint[ConcretePostFaceCount];
        var wallTop = new uint[ConcretePostFaceCount];
        var capBottom = new uint[ConcretePostFaceCount];
        var capTop = new uint[ConcretePostFaceCount];

        for (var face = 0; face < ConcretePostFaceCount; face++) {
            var angle = 2.0f * MathF.PI * face / ConcretePostFaceCount;
            var radial = new Vector3(MathF.Cos(angle), 0.0f, MathF.Sin(angle));

            wallBottom[face] = geometry.AddIndexedVertex(bottomCenter + radial * ConcretePostRadius, radial, ConcretePostColor, ConcretePostSpecular);
            wallTop[face] = geometry.AddIndexedVertex(cylinderTop + radial * ConcretePostRadius, radial, ConcretePostColor, ConcretePostSpecular);
            capBottom[face] = geometry.AddIndexedVertex(cylinderTop + radial * ConcretePostRadius, Vector3.Normalize(radial + Vector3.UnitY), ConcretePostColor, ConcretePostSpecular);
            capTop[face] = geometry.AddIndexedVertex(topCenter + radial * ConcretePostCapRadius, Vector3.UnitY, ConcretePostColor, ConcretePostSpecular);
        }

        for (var face = 0; face < ConcretePostFaceCount; face++) {
            var next = (face + 1) % ConcretePostFaceCount;
            AddPostQuad(geometry, wallBottom[face], wallBottom[next], wallTop[next], wallTop[face]);
            AddPostQuad(geometry, capBottom[face], capBottom[next], capTop[next], capTop[face]);
        }

        var topCenterVertex = geometry.AddIndexedVertex(topCenter, Vector3.UnitY, ConcretePostColor, ConcretePostSpecular);

        for (var face = 0; face < ConcretePostFaceCount; face++) {
            var next = (face + 1) % ConcretePostFaceCount;
            geometry.AddIndexedTriangle(topCenterVertex, capTop[face], capTop[next]);
        }
    }

    private static void AddPostQuad(TrackMeshGeometry geometry, uint a, uint b, uint c, uint d)
    {
        geometry.AddIndexedTriangle(a, b, c);
        geometry.AddIndexedTriangle(a, c, d);
    }

    private static Dictionary<(ushort RideId, int TileX, int TileY, byte BaseHeight), PlatformSideOpenings>
        BuildEntranceEdgeOpenings(
            ReadOnlySpan<RCTTrackElementData> trackElements,
            ReadOnlySpan<RCTRideEntranceData> entrances)
    {
        var platformTracksByTile = new Dictionary<(int X, int Y), List<RCTTrackElementData>>();
        foreach (ref readonly var track in trackElements) {
            if (track.IsRidePlatformBase != 1) continue;
            if (!platformTracksByTile.TryGetValue((track.TileX, track.TileY), out var tileTracks)) {
                tileTracks = new List<RCTTrackElementData>();
                platformTracksByTile.Add((track.TileX, track.TileY), tileTracks);
            }
            tileTracks.Add(track);
        }

        var openings = new Dictionary<(ushort RideId, int TileX, int TileY, byte BaseHeight), PlatformSideOpenings>();
        foreach (ref readonly var entrance in entrances) {
            if (entrance.Direction > 3)
                throw new InvalidDataException($"OpenRCT2 returned an invalid ride entrance direction {entrance.Direction}.");

            var (offsetX, offsetY) = entrance.Direction switch {
                0 => (-1, 0),
                1 => (0, 1),
                2 => (1, 0),
                3 => (0, -1),
                _ => throw new InvalidDataException($"OpenRCT2 returned invalid ride entrance direction {entrance.Direction}.")
            };
            if (!platformTracksByTile.TryGetValue((entrance.TileX + offsetX, entrance.TileY + offsetY), out var tileTracks))
                continue;

            var opening = (offsetX, offsetY) switch {
                (-1, 0) => PlatformSideOpenings.East,
                (0, 1) => PlatformSideOpenings.South,
                (1, 0) => PlatformSideOpenings.West,
                (0, -1) => PlatformSideOpenings.North,
                _ => PlatformSideOpenings.None
            };
            foreach (var track in tileTracks) {
                if (track.RideId != entrance.RideId || track.BaseHeight != entrance.BaseHeight)
                    continue;

                var key = (track.RideId, track.TileX, track.TileY, track.BaseHeight);
                openings[key] = openings.GetValueOrDefault(key) | opening;
            }
        }

        return openings;
    }

    private static void AddQuad(
        TrackMeshGeometry geometry,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        Vector3 normal,
        TrackRgba8 color,
        TrackRailSpecularData specular)
    {
        var first = geometry.AddIndexedVertex(a, normal, color, specular);
        var second = geometry.AddIndexedVertex(b, normal, color, specular);
        var third = geometry.AddIndexedVertex(c, normal, color, specular);
        var fourth = geometry.AddIndexedVertex(d, normal, color, specular);

        geometry.AddIndexedTriangle(first, third, second);
        geometry.AddIndexedTriangle(first, fourth, third);
    }

    private static (int X, int Y) RotateSequenceOffset(int x, int y, byte direction) => direction switch {
        0 => (x, y),
        1 => (y, -x),
        2 => (-x, -y),
        3 => (-y, x),
        _ => throw new InvalidDataException($"OpenRCT2 returned invalid track direction {direction}.")
    };

    private static bool HasCompletePieceSequences(List<RCTTrackElementData> elements)
    {
        var expectedCount = elements[0].SequenceCount;
        if (elements.Count != expectedCount || elements.Any(element => element.SequenceCount != expectedCount))
            return false;

        var sequences = new bool[expectedCount];
        foreach (var element in elements) {
            if (sequences[element.Sequence])
                return false;

            sequences[element.Sequence] = true;
        }

        return sequences.All(sequence => sequence);
    }
}
