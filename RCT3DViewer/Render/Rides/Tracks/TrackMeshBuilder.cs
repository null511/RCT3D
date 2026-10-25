using System.Runtime.InteropServices;
using System.Numerics;
using RCT3D.Interop;
using RCT3D.Render.Rides.Supports;

namespace RCT3D.Render.Rides.Tracks;

internal sealed class TrackMeshBuilder
{
    private readonly Dictionary<RCTRideType, TrackRideConfiguration> rideConfigurations;

    public TrackMeshBuilder(IEnumerable<TrackRideConfiguration>? rideConfigurations = null)
    {
        this.rideConfigurations = (rideConfigurations ?? TrackRideConfigurations.All)
            .ToDictionary(configuration => configuration.RideType);
    }

    private readonly record struct TrackPieceKey(
        ushort RideId,
        RCTRideType RideType,
        RCTTrackElementType TrackType,
        int OriginTileX,
        int OriginTileY,
        byte Direction,
        int OriginBaseHeight);

    public TrackMeshBuildData Build(
        ReadOnlySpan<RCTTrackElementData> trackElements,
        ReadOnlySpan<RCTTileSurfaceData> surfaces,
        int mapWidth,
        int mapHeight,
        ReadOnlySpan<RCTPaletteEntry> palette)
    {
        if (mapWidth < 0 || mapHeight < 0)
            throw new ArgumentOutOfRangeException(nameof(mapWidth));

        if (surfaces.Length < checked(mapWidth * mapHeight))
            throw new InvalidDataException("OpenRCT2 returned fewer terrain surfaces than the map dimensions require.");

        var pieces = new Dictionary<TrackPieceKey, List<RCTTrackElementData>>();

        foreach (var track in trackElements) {
            if ((uint)track.TileX >= (uint)mapWidth || (uint)track.TileY >= (uint)mapHeight)
                throw new InvalidDataException($"OpenRCT2 returned a track element outside the {mapWidth}x{mapHeight} map.");

            if (track.Direction > 3)
                throw new InvalidDataException($"OpenRCT2 returned an invalid track direction at ({track.TileX}, {track.TileY}).");

            if (track.SequenceCount == 0 || track.Sequence >= track.SequenceCount)
                throw new InvalidDataException($"OpenRCT2 returned an invalid track sequence at ({track.TileX}, {track.TileY}).");

            if (!rideConfigurations.TryGetValue(track.RideType, out var configuration)
                || !configuration.TryResolvePiece(track.TrackType, out _)) continue;

            var offset = RotateSequenceOffset(track.SequenceOffsetX, track.SequenceOffsetY, track.Direction);

            var key = new TrackPieceKey(
                track.RideId,
                track.RideType,
                track.TrackType,
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

        var geometry = new TrackMeshGeometry();
        var supportGeometry = new RideSupportMeshGeometry();
        var piecesByRegion = new Dictionary<RenderRegionCoordinate, List<(TrackRideConfiguration Configuration, List<RCTTrackElementData> Elements)>>();
        
        foreach (var (key, pieceElements) in pieces) {
            pieceElements.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
            var configuration = rideConfigurations[key.RideType];
            configuration.TryResolvePiece(key.TrackType, out var pieceBuilder);

            if (pieceBuilder.RequiresCompletePiece)
                ValidatePieceSequences(key, pieceElements);

            var region = new RenderRegionCoordinate(
                Math.Clamp(key.OriginTileX / RenderRegionLayout.RegionSizeInTiles, 0, Math.Max(0, RenderRegionLayout.GetRegionCount(mapWidth) - 1)),
                Math.Clamp(key.OriginTileY / RenderRegionLayout.RegionSizeInTiles, 0, Math.Max(0, RenderRegionLayout.GetRegionCount(mapHeight) - 1)));
            
            if (!piecesByRegion.TryGetValue(region, out var regionPieces)) {
                regionPieces = new List<(TrackRideConfiguration, List<RCTTrackElementData>)>();
                piecesByRegion.Add(region, regionPieces);
            }

            regionPieces.Add((configuration, pieceElements));
        }

        foreach (var region in RenderRegionLayout.Enumerate(mapWidth, mapHeight)) {
            if (!piecesByRegion.TryGetValue(region, out var regionPieces)) continue;

            geometry.BeginRegion(region);
            supportGeometry.BeginRegion(region);

            foreach (var (configuration, piece) in regionPieces) {
                var track = piece[0];
                configuration.TryResolvePiece(track.TrackType, out var pieceBuilder);
                var pieceSpan = CollectionsMarshal.AsSpan(piece);
                var hasSupportPlacements = configuration.SupportMeshBuilder is not null
                    && configuration.GetSupportPlacements is not null;
                geometry.SetVerticalOffset(configuration.CenterlineHeightOffset);
                if (hasSupportPlacements)
                    geometry.BeginCenterlineCapture();

                try {
                    pieceBuilder.AddPiece(pieceSpan, geometry, configuration.PartGenerators, palette);
                }
                finally {
                    geometry.SetVerticalOffset(0.0f);
                }

                if (hasSupportPlacements
                    && configuration.SupportMeshBuilder is { } supportMeshBuilder
                    && configuration.GetSupportPlacements is { } getSupportPlacements) {
                    var centerlines = geometry.EndCenterlineCapture();
                    if (centerlines.Length == 0)
                        throw new InvalidDataException($"Track piece {track.TrackType} did not expose a centerline for support placement.");

                    var placements = new List<TrackSupportPlacement>();
                    foreach (ref readonly var supportTrack in pieceSpan) {
                        getSupportPlacements(supportTrack, placements);
                        var supportColor = GetPaletteColor(supportTrack.SupportPaletteIndex, palette);
                        foreach (var placement in placements) {
                            var anchor = ResolveSupportAnchor(
                                supportTrack, placement, supportMeshBuilder.PlacementMode, centerlines,
                                configuration.SupportTrackOffset, mapWidth, mapHeight);
                            var surfaceIndex = checked(anchor.SurfaceTileY * mapWidth + anchor.SurfaceTileX);
                            var surface = surfaces[surfaceIndex];
                            supportMeshBuilder.AddSupport(
                                supportTrack, placement, anchor, supportGeometry, surface, supportColor, configuration.SupportTrackOffset);
                        }
                    }
                }
            }

            geometry.EndRegion();
            supportGeometry.EndRegion();
        }

        return new TrackMeshBuildData(geometry.Build(), supportGeometry.Build());
    }

    private static TrackSupportAnchor ResolveSupportAnchor(
        in RCTTrackElementData track,
        TrackSupportPlacement placement,
        TrackSupportPlacementMode placementMode,
        ReadOnlySpan<TrackCenterlinePath> centerlines,
        float supportTrackOffset,
        int mapWidth,
        int mapHeight)
    {
        Vector3 position;
        Vector3 tangent;

        if (placementMode == TrackSupportPlacementMode.TrackBased) {
            var centerline = GetCenterlineForSequence(track.Sequence, centerlines);
            if (float.IsFinite(placement.TrackProgress)) {
                (position, tangent) = FindCenterlinePointAtProgress(centerline.Points, placement.TrackProgress);
            }
            else {
                var target = TrackCenterlineGeometry.GetTileCenter(track);
                target.Y = track.BaseHeight * WorldUnits.HeightPerBaseUnit
                    + placement.HeightExtra * WorldUnits.HeightPerBaseUnit / 8.0f
                    + supportTrackOffset;
                (position, tangent) = FindClosestCenterlinePointInTile(centerline.Points, track.TileX, track.TileY, target);
            }

            if (placement.TrackLateralOffset != 0.0f) {
                var lateral = Vector3.Cross(Vector3.UnitY, tangent);
                if (lateral.LengthSquared() <= float.Epsilon)
                    lateral = Vector3.Cross(Vector3.UnitY, TrackCenterlineGeometry.GetTrackDirection(track.Direction));
                position += Vector3.Normalize(lateral) * placement.TrackLateralOffset;
            }
        }
        else {
            position = new Vector3(
                (track.TileX + placement.TileOffsetX) * WorldUnits.TileSize,
                0.0f,
                (track.TileY + placement.TileOffsetZ) * WorldUnits.TileSize);
            tangent = TrackCenterlineGeometry.GetTrackDirection(track.Direction);
        }

        var surfaceTileX = Math.Clamp((int)MathF.Floor(position.X / WorldUnits.TileSize), 0, mapWidth - 1);
        var surfaceTileY = Math.Clamp((int)MathF.Floor(position.Z / WorldUnits.TileSize), 0, mapHeight - 1);
        return new TrackSupportAnchor(position, tangent, surfaceTileX, surfaceTileY);
    }

    private static TrackCenterlinePath GetCenterlineForSequence(byte sequence, ReadOnlySpan<TrackCenterlinePath> centerlines)
    {
        if (centerlines.Length == 1)
            return centerlines[0];

        foreach (ref readonly var centerline in centerlines) {
            if (centerline.Sequence == sequence)
                return centerline;
        }

        throw new InvalidDataException($"No rendered track centerline was captured for sequence {sequence}.");
    }

    private static (Vector3 Position, Vector3 Tangent) FindClosestCenterlinePointInTile(
        ReadOnlySpan<Vector3> points,
        int tileX,
        int tileY,
        Vector3 target)
    {
        var minX = tileX * WorldUnits.TileSize;
        var minZ = tileY * WorldUnits.TileSize;
        var maxX = minX + WorldUnits.TileSize;
        var maxZ = minZ + WorldUnits.TileSize;
        var bestDistance = float.PositiveInfinity;
        var bestPosition = default(Vector3);
        var bestTangent = Vector3.Zero;

        for (var i = 0; i < points.Length - 1; i++) {
            var start = points[i];
            var end = points[i + 1];
            if (!ClipSegmentToTile(start, end, minX, minZ, maxX, maxZ, out var minT, out var maxT)) continue;

            var segment = end - start;
            var lengthSquared = segment.LengthSquared();
            var projectedT = lengthSquared > float.Epsilon
                ? Vector3.Dot(target - start, segment) / lengthSquared
                : minT;
            var t = Math.Clamp(projectedT, minT, maxT);
            var position = Vector3.Lerp(start, end, t);
            var distance = Vector3.DistanceSquared(position, target);
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            bestPosition = position;
            bestTangent = lengthSquared > float.Epsilon ? Vector3.Normalize(segment) : Vector3.UnitY;
        }

        if (float.IsPositiveInfinity(bestDistance))
            throw new InvalidDataException($"The rendered track centerline does not pass through support tile ({tileX}, {tileY}).");

        return (bestPosition, bestTangent);
    }

    private static (Vector3 Position, Vector3 Tangent) FindCenterlinePointAtProgress(
        ReadOnlySpan<Vector3> points,
        float progress)
    {
        if (points.Length < 2 || !float.IsFinite(progress) || progress < 0.0f || progress > 1.0f)
            throw new InvalidDataException("A track-based support placement has invalid centerline progress.");

        var totalLength = 0.0f;
        for (var i = 0; i < points.Length - 1; i++)
            totalLength += Vector3.Distance(points[i], points[i + 1]);

        if (totalLength <= float.Epsilon)
            throw new InvalidDataException("The rendered track centerline has no length for support placement.");

        var targetDistance = totalLength * progress;
        var traversedDistance = 0.0f;
        for (var i = 0; i < points.Length - 1; i++) {
            var segment = points[i + 1] - points[i];
            var segmentLength = segment.Length();
            if (segmentLength <= float.Epsilon) continue;

            if (targetDistance <= traversedDistance + segmentLength || i == points.Length - 2) {
                var segmentProgress = Math.Clamp((targetDistance - traversedDistance) / segmentLength, 0.0f, 1.0f);
                return (Vector3.Lerp(points[i], points[i + 1], segmentProgress), segment / segmentLength);
            }

            traversedDistance += segmentLength;
        }

        throw new InvalidDataException("Could not resolve a point on the rendered track centerline.");
    }

    private static bool ClipSegmentToTile(
        Vector3 start,
        Vector3 end,
        float minX,
        float minZ,
        float maxX,
        float maxZ,
        out float minT,
        out float maxT)
    {
        minT = 0.0f;
        maxT = 1.0f;
        var deltaX = end.X - start.X;
        var deltaZ = end.Z - start.Z;

        return ClipAxis(-deltaX, start.X - minX, ref minT, ref maxT)
            && ClipAxis(deltaX, maxX - start.X, ref minT, ref maxT)
            && ClipAxis(-deltaZ, start.Z - minZ, ref minT, ref maxT)
            && ClipAxis(deltaZ, maxZ - start.Z, ref minT, ref maxT);
    }

    private static bool ClipAxis(float p, float q, ref float minT, ref float maxT)
    {
        if (MathF.Abs(p) <= float.Epsilon)
            return q >= 0.0f;

        var t = q / p;
        if (p < 0.0f) {
            if (t > maxT) return false;
            minT = Math.Max(minT, t);
        }
        else {
            if (t < minT) return false;
            maxT = Math.Min(maxT, t);
        }

        return minT <= maxT;
    }

    private static (int X, int Y) RotateSequenceOffset(int x, int y, byte direction) => direction switch {
        0 => (x, y),
        1 => (y, -x),
        2 => (-x, -y),
        3 => (-y, x),
        _ => throw new InvalidDataException($"OpenRCT2 returned invalid track direction {direction}.")
    };

    private static TrackRgba8 GetPaletteColor(byte paletteIndex, ReadOnlySpan<RCTPaletteEntry> palette)
    {
        if (paletteIndex >= palette.Length)
            throw new InvalidDataException($"OpenRCT2 returned invalid support palette index {paletteIndex}.");

        var entry = palette[paletteIndex];
        return new TrackRgba8(entry.Red, entry.Green, entry.Blue, entry.Alpha);
    }

    private static void ValidatePieceSequences(TrackPieceKey key, List<RCTTrackElementData> elements)
    {
        var expectedCount = elements[0].SequenceCount;
        if (elements.Count != expectedCount || elements.Any(element => element.SequenceCount != expectedCount))
            throw new InvalidDataException(
                $"Track piece {key.TrackType} for ride {key.RideId} at ({key.OriginTileX}, {key.OriginTileY}) has incomplete sequence data.");

        for (var sequence = 0; sequence < expectedCount; sequence++) {
            if (elements[sequence].Sequence != sequence)
                throw new InvalidDataException(
                    $"Track piece {key.TrackType} for ride {key.RideId} at ({key.OriginTileX}, {key.OriginTileY}) has duplicate or missing sequences.");
        }
    }
}
