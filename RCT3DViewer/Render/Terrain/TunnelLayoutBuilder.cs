namespace RCT3D.Render.Terrain;

internal enum TerrainSide : byte
{
    West,
    North,
    East,
    South,
}

internal readonly record struct TunnelSpan(
    int TileX,
    int TileY,
    float FloorHeight,
    float RoofHeight,
    byte ConnectedSideMask);

internal readonly record struct TunnelOpening(
    int TileX,
    int TileY,
    TerrainSide Side,
    float FloorHeight,
    float RoofHeight);

internal readonly record struct TunnelStepSkirt(float BottomHeight, float TopHeight, bool FacesCurrentTile);

internal sealed class TunnelLayout(TunnelSpan[] spans, TunnelOpening[] openings)
{
    private readonly Dictionary<(int TileX, int TileY), TunnelSpan[]> spansByTile = spans
        .GroupBy(span => (span.TileX, span.TileY))
        .ToDictionary(group => group.Key, group => group
            .GroupBy(span => (span.FloorHeight, span.RoofHeight))
            .Select(level => new TunnelSpan(level.First().TileX, level.First().TileY,
                level.Key.FloorHeight, level.Key.RoofHeight,
                level.Aggregate((byte)0, (mask, span) => (byte)(mask | span.ConnectedSideMask))))
            .ToArray());

    private readonly Dictionary<(int TileX, int TileY, TerrainSide Side), TunnelOpening> openingsBySide = openings
        .GroupBy(opening => (opening.TileX, opening.TileY, opening.Side))
        .ToDictionary(group => group.Key, group => group.First());

    public TunnelSpan[] Spans { get; } = spans;
    public TunnelOpening[] Openings { get; } = openings;

    public IReadOnlyList<TunnelSpan> GetSpans(int tileX, int tileY) =>
        spansByTile.TryGetValue((tileX, tileY), out var tileSpans) ? tileSpans : Array.Empty<TunnelSpan>();

    public IEnumerable<TunnelStepSkirt> GetStepSkirts(int tileX, int tileY, TerrainSide side)
    {
        var neighbor = TunnelLayoutBuilder.GetNeighbor(tileX, tileY, side);
        var opposite = ((int)side + 2) & 3;
        var current = GetSpans(tileX, tileY)
            .Where(span => (span.ConnectedSideMask & (1 << (int)side)) != 0).ToArray();
        var other = GetSpans(neighbor.TileX, neighbor.TileY)
            .Where(span => (span.ConnectedSideMask & (1 << opposite)) != 0).ToArray();
        var connectedCurrent = current.Where(span => other.Any(candidate => InteriorsOverlap(span, candidate))).ToArray();
        var connectedOther = other.Where(span => current.Any(candidate => InteriorsOverlap(span, candidate))).ToArray();
        var heights = connectedCurrent.Concat(connectedOther)
            .SelectMany(span => new[] { span.FloorHeight, span.RoofHeight - TunnelProfile.WallThickness })
            .Distinct().Order().ToArray();
        TunnelStepSkirt? pending = null;
        for (var index = 0; index + 1 < heights.Length; index++) {
            var bottom = heights[index];
            var top = heights[index + 1];

            var currentOpen = connectedCurrent.Any(span => span.FloorHeight <= bottom
                && span.RoofHeight - TunnelProfile.WallThickness >= top);

            var otherOpen = connectedOther.Any(span => span.FloorHeight <= bottom
                && span.RoofHeight - TunnelProfile.WallThickness >= top);

            if (currentOpen == otherOpen) {
                if (pending is { } finished) yield return finished;
                pending = null;
                continue;
            }

            if (pending is { } previous && previous.FacesCurrentTile == currentOpen && previous.TopHeight == bottom)
                pending = previous with { TopHeight = top };
            else {
                if (pending is { } finished) yield return finished;
                pending = new TunnelStepSkirt(bottom, top, currentOpen);
            }
        }
        if (pending is { } last) yield return last;
    }

    private static bool InteriorsOverlap(TunnelSpan first, TunnelSpan second) =>
        first.FloorHeight < second.RoofHeight - TunnelProfile.WallThickness
        && second.FloorHeight < first.RoofHeight - TunnelProfile.WallThickness;

    public bool HasTunnelConnection(int tileX, int tileY, TerrainSide side, float floorHeight, float roofHeight)
    {
        var neighbor = TunnelLayoutBuilder.GetNeighbor(tileX, tileY, side);

        return GetSpans(tileX, tileY).Any(span =>
            (span.ConnectedSideMask & (1 << (int)side)) != 0
            && span.FloorHeight < roofHeight && floorHeight < span.RoofHeight)
            && GetSpans(neighbor.TileX, neighbor.TileY).Any(span =>
                (span.ConnectedSideMask & (1 << (((int)side + 2) & 3))) != 0
                && span.FloorHeight < roofHeight && floorHeight < span.RoofHeight);
    }

    public bool TryGetOpening(int tileX, int tileY, TerrainSide side, out TunnelOpening opening) =>
        openingsBySide.TryGetValue((tileX, tileY, side), out opening);

    public bool HasOutsetCorner(TunnelSpan span, TerrainSide wallSide, TerrainSide endSide)
    {
        if (!HasTunnelConnection(span.TileX, span.TileY, endSide, span.FloorHeight, span.RoofHeight))
            return false;

        var neighbor = TunnelLayoutBuilder.GetNeighbor(span.TileX, span.TileY, endSide);
        var diagonal = TunnelLayoutBuilder.GetNeighbor(neighbor.TileX, neighbor.TileY, wallSide);
        var oppositeEnd = (TerrainSide)(((int)endSide + 2) & 3);
        var oppositeWall = (TerrainSide)(((int)wallSide + 2) & 3);
        return GetSpans(neighbor.TileX, neighbor.TileY).Any(candidate =>
                candidate.FloorHeight <= span.FloorHeight && candidate.RoofHeight >= span.RoofHeight
                && (candidate.ConnectedSideMask & (1 << (int)oppositeEnd)) != 0
                && (candidate.ConnectedSideMask & (1 << (int)wallSide)) != 0)
            && GetSpans(diagonal.TileX, diagonal.TileY).Any(candidate =>
                candidate.FloorHeight <= span.FloorHeight && candidate.RoofHeight >= span.RoofHeight
                && (candidate.ConnectedSideMask & (1 << (int)oppositeWall)) != 0
                && !HasTunnelConnection(diagonal.TileX, diagonal.TileY, oppositeEnd,
                    span.FloorHeight, span.RoofHeight))
            && !HasOpening(diagonal.TileX, diagonal.TileY, oppositeEnd);
    }

    public bool HasOpening(int tileX, int tileY, TerrainSide side) =>
        openingsBySide.ContainsKey((tileX, tileY, side));
}

internal sealed class TunnelLayoutBuilder
{
    public TunnelLayout Build(MapOccupancyMap occupancy, float terrainSkirtFloor)
    {
        var routes = occupancy.RouteTiles.ToArray();
        var routesByTile = routes.GroupBy(route => (route.TileX, route.TileY))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var undergroundRouteIds = new HashSet<int>();

        foreach (var route in routes) {
            var roofHeight = GetRoofHeight(route);
            if (occupancy.GetTerrainHeight(route.TileX, route.TileY, 0.5f, 0.5f) >= roofHeight)
                undergroundRouteIds.Add(route.RouteId);
        }

        var spans = new List<TunnelSpan>();
        var openings = new List<TunnelOpening>();

        foreach (var route in routes.Where(route => undergroundRouteIds.Contains(route.RouteId))) {
            var candidateSideMask = GetCandidateSideMask(route);
            byte connectedSideMask = 0;

            foreach (var side in Enum.GetValues<TerrainSide>()) {
                var neighbor = GetNeighbor(route.TileX, route.TileY, side);

                if (!IsInsideMap(neighbor, occupancy)) {
                    if (IsPortalSideAllowed(route, candidateSideMask, side)
                        && IsTerrainSideWall(occupancy, route, side, terrainSkirtFloor)
                        && (!route.IsTrack || HasTrackPortalCrossing(occupancy, route, side, [], terrainSkirtFloor)))
                        openings.Add(new TunnelOpening(route.TileX, route.TileY, side,
                            route.FloorHeight, GetRoofHeight(route)));

                    continue;
                }

                if (!routesByTile.TryGetValue(neighbor, out var neighborRoutes))
                    continue;

                var connectedRoutes = GetConnectedRoutes(route, side, neighborRoutes).ToArray();
                if (connectedRoutes.Length == 0)
                    continue;

                if (connectedRoutes.Any(candidate => undergroundRouteIds.Contains(candidate.RouteId))) {
                    connectedSideMask |= (byte)(1 << (int)side);
                    continue;
                }

                if (IsTerrainSideWall(occupancy, route, side, terrainSkirtFloor)
                    && (!route.IsTrack || HasTrackPortalCrossing(occupancy, route, side,
                        connectedRoutes, terrainSkirtFloor)))
                    openings.Add(new TunnelOpening(route.TileX, route.TileY, side,
                        route.FloorHeight, GetRoofHeight(route)));
            }

            spans.Add(new TunnelSpan(route.TileX, route.TileY, route.FloorHeight,
                GetRoofHeight(route), connectedSideMask));
        }

        return new TunnelLayout(spans.ToArray(), openings.Distinct().ToArray());
    }

    private static float GetRoofHeight(RouteTileOccupancy route) =>
        MathF.Max(route.ClearanceHeight, route.FloorHeight + WorldUnits.HeightPerBaseUnit * 2.0f);

    private static bool HasTrackPortalCrossing(MapOccupancyMap occupancy, RouteTileOccupancy route,
        TerrainSide side, IReadOnlyList<RouteTileOccupancy> neighbors, float terrainSkirtFloor)
    {
        var neighbor = GetNeighbor(route.TileX, route.TileY, side);
        var insideMap = IsInsideMap(neighbor, occupancy);
        foreach (var crossing in occupancy.GetTrackEdgeCrossings(route, side)) {
            var u = Math.Clamp(crossing.X / WorldUnits.TileSize - route.TileX, 0.0f, 1.0f);
            var v = Math.Clamp(crossing.Z / WorldUnits.TileSize - route.TileY, 0.0f, 1.0f);
            var currentHeight = occupancy.GetTerrainHeight(route.TileX, route.TileY, u, v);
            var otherHeight = insideMap
                ? occupancy.GetTerrainHeight(neighbor.TileX, neighbor.TileY,
                    Math.Clamp(crossing.X / WorldUnits.TileSize - neighbor.TileX, 0.0f, 1.0f),
                    Math.Clamp(crossing.Z / WorldUnits.TileSize - neighbor.TileY, 0.0f, 1.0f))
                : terrainSkirtFloor;
            if (currentHeight < GetRoofHeight(route) || otherHeight >= GetRoofHeight(route))
                continue;
            if (!insideMap || neighbors.Any(candidate => candidate.IsTrack && candidate.RideId == route.RideId
                && otherHeight < GetRoofHeight(candidate)
                && occupancy.GetTrackEdgeCrossings(candidate, Opposite(side)).Any(other =>
                    System.Numerics.Vector3.DistanceSquared(crossing, other) <= 0.0001f)))
                return true;
        }
        return false;
    }

    private static byte GetCandidateSideMask(RouteTileOccupancy route)
    {
        if (!route.IsTrack || route.TunnelSideMask == 0 || route.Direction > 3)
            return 0;

        byte mask = 0;
        if ((route.TunnelSideMask & 1) != 0)
            mask |= (byte)(1 << route.Direction);

        if ((route.TunnelSideMask & 2) != 0)
            mask |= (byte)(1 << ((route.Direction + 1) & 3));

        return mask;
    }

    private static bool IntervalsOverlap(float firstBottom, float firstTop, float secondBottom, float secondTop) =>
        firstBottom < secondTop && secondBottom < firstTop;

    private static IEnumerable<RouteTileOccupancy> GetConnectedRoutes(
        RouteTileOccupancy route, TerrainSide side, IEnumerable<RouteTileOccupancy> neighbors)
    {
        if (route.IsTrack)
            return neighbors.Where(candidate => candidate.IsTrack && candidate.RideId == route.RideId
                && IntervalsOverlap(route.FloorHeight, GetRoofHeight(route),
                    candidate.FloorHeight, GetRoofHeight(candidate)));

        var sideBit = 1 << (int)side;
        var oppositeBit = 1 << (int)Opposite(side);
        return route.ConnectionSideMask is 0 || (route.ConnectionSideMask & sideBit) == 0
            ? []
            : neighbors.Where(candidate => !candidate.IsTrack && (candidate.ConnectionSideMask & oppositeBit) != 0
                && IntervalsOverlap(route.FloorHeight, GetRoofHeight(route),
                    candidate.FloorHeight, GetRoofHeight(candidate)));
    }

    private static bool IsInsideMap((int TileX, int TileY) tile, MapOccupancyMap occupancy) =>
        (uint)tile.TileX < (uint)occupancy.Width && (uint)tile.TileY < (uint)occupancy.Height;

    private static bool IsPortalSideAllowed(RouteTileOccupancy route, byte candidateMask, TerrainSide side) =>
        ((route.IsTrack ? candidateMask : route.ConnectionSideMask) & (1 << (int)side)) != 0;

    private static bool IsTerrainSideWall(MapOccupancyMap occupancy, RouteTileOccupancy route, TerrainSide side, float terrainSkirtFloor)
    {
        var neighbor = GetNeighbor(route.TileX, route.TileY, side);
        var (currentStart, currentEnd) = GetEdgeEndpointHeights(occupancy, route.TileX, route.TileY, side);

        var (otherStart, otherEnd) = IsInsideMap(neighbor, occupancy)
            ? GetEdgeEndpointHeights(occupancy, neighbor.TileX, neighbor.TileY, Opposite(side))
            : (terrainSkirtFloor, terrainSkirtFloor);

        return IsWallAtEndpoint(currentStart, otherStart, route)
            || IsWallAtEndpoint(currentEnd, otherEnd, route);
    }

    private static bool IsWallAtEndpoint(float currentHeight, float otherHeight, RouteTileOccupancy route) =>
        MathF.Abs(currentHeight - otherHeight) > 0.0001f
        && IntervalsOverlap(route.FloorHeight, GetRoofHeight(route),
            MathF.Min(currentHeight, otherHeight), MathF.Max(currentHeight, otherHeight));

    internal static (int TileX, int TileY) GetNeighbor(int tileX, int tileY, TerrainSide side) => side switch {
        TerrainSide.West => (tileX - 1, tileY),
        TerrainSide.North => (tileX, tileY + 1),
        TerrainSide.East => (tileX + 1, tileY),
        TerrainSide.South => (tileX, tileY - 1),
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };

    private static TerrainSide Opposite(TerrainSide side) => (TerrainSide)(((int)side + 2) & 3);

    private static (float Start, float End) GetEdgeEndpointHeights(
        MapOccupancyMap occupancy, int tileX, int tileY, TerrainSide side) => side switch {
        TerrainSide.West => (occupancy.GetTerrainHeight(tileX, tileY, 0.0f, 0.0f),
            occupancy.GetTerrainHeight(tileX, tileY, 0.0f, 1.0f)),
        TerrainSide.North => (occupancy.GetTerrainHeight(tileX, tileY, 0.0f, 1.0f),
            occupancy.GetTerrainHeight(tileX, tileY, 1.0f, 1.0f)),
        TerrainSide.East => (occupancy.GetTerrainHeight(tileX, tileY, 1.0f, 0.0f),
            occupancy.GetTerrainHeight(tileX, tileY, 1.0f, 1.0f)),
        TerrainSide.South => (occupancy.GetTerrainHeight(tileX, tileY, 0.0f, 0.0f),
            occupancy.GetTerrainHeight(tileX, tileY, 1.0f, 0.0f)),
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };
}
