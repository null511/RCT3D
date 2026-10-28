using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3D.Render.Rides.Tracks;

internal sealed class TrackMeshBuilder
{
    private readonly Dictionary<RCTRideType, TrackRideConfiguration> rideConfigurations;
    private readonly TrackPieceBuilderCatalog pieceBuilders = TrackPieceBuilderCatalog.Shared;


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
        ReadOnlySpan<RCTRideEntranceData> entrances,
        ReadOnlySpan<RCTTileSurfaceData> surfaces,
        int mapWidth,
        int mapHeight,
        ReadOnlySpan<RCTPaletteEntry> palette)
    {
        if (mapWidth < 0 || mapHeight < 0)
            throw new ArgumentOutOfRangeException(nameof(mapWidth));

        if (surfaces.Length < checked(mapWidth * mapHeight))
            throw new InvalidDataException("OpenRCT2 returned fewer terrain surfaces than the map dimensions require.");

        var entranceSideOpenings = BuildEntranceSideOpenings(trackElements, entrances);
        var pieces = new Dictionary<TrackPieceKey, List<RCTTrackElementData>>();

        foreach (var track in trackElements) {
            if ((uint)track.TileX >= (uint)mapWidth || (uint)track.TileY >= (uint)mapHeight)
                throw new InvalidDataException($"OpenRCT2 returned a track element outside the {mapWidth}x{mapHeight} map.");

            if (track.Direction > 3)
                throw new InvalidDataException($"OpenRCT2 returned an invalid track direction at ({track.TileX}, {track.TileY}).");

            if (track.SequenceCount == 0 || track.Sequence >= track.SequenceCount)
                throw new InvalidDataException($"OpenRCT2 returned an invalid track sequence at ({track.TileX}, {track.TileY}).");

            if (!rideConfigurations.TryGetValue(track.RideType, out _)
                || !pieceBuilders.TryResolve(track.Part, out _)) continue;

            var offset = RotateSequenceOffset(track.SequenceOffsetX, track.SequenceOffsetY, track.Direction);

            var key = new TrackPieceKey(
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

        var geometry = new TrackMeshGeometry();
        var supportGeometry = new RideSupportMeshGeometry();
        var rideCenterlines = new List<TrackCenterlinePiece>();
        var piecesByRegion = new Dictionary<RenderRegionCoordinate, List<(TrackPieceKey Key, TrackRideConfiguration Configuration, List<RCTTrackElementData> Elements)>>();
        
        foreach (var (key, pieceElements) in pieces) {
            pieceElements.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
            var configuration = rideConfigurations[key.RideType];
            pieceBuilders.TryResolve(key.TrackType, out var pieceBuilder);

            if (pieceBuilder.RequiresCompletePiece)
                ValidatePieceSequences(key, pieceElements);

            var region = new RenderRegionCoordinate(
                Math.Clamp(key.OriginTileX / RenderRegionLayout.RegionSizeInTiles, 0, Math.Max(0, RenderRegionLayout.GetRegionCount(mapWidth) - 1)),
                Math.Clamp(key.OriginTileY / RenderRegionLayout.RegionSizeInTiles, 0, Math.Max(0, RenderRegionLayout.GetRegionCount(mapHeight) - 1)));
            
            if (!piecesByRegion.TryGetValue(region, out var regionPieces)) {
                regionPieces = new List<(TrackPieceKey, TrackRideConfiguration, List<RCTTrackElementData>)>();
                piecesByRegion.Add(region, regionPieces);
            }

            regionPieces.Add((key, configuration, pieceElements));
        }

        foreach (var region in RenderRegionLayout.Enumerate(mapWidth, mapHeight)) {
            if (!piecesByRegion.TryGetValue(region, out var regionPieces)) continue;

            geometry.BeginRegion(region);
            supportGeometry.BeginRegion(region);

            foreach (var (key, configuration, piece) in regionPieces) {
                var track = piece[0];
                pieceBuilders.TryResolve(track.Part, out var pieceBuilder);
                var pieceSpan = CollectionsMarshal.AsSpan(piece);
                var hasSupportPlacements = configuration.HasSupportPlacements;
                var captureCenterline = hasSupportPlacements || configuration.SmoothMovement;
                geometry.SetVerticalOffset(configuration.CenterlineHeightOffset);
                if (captureCenterline)
                    geometry.BeginCenterlineCapture();

                try {
                    pieceBuilder.AddPiece(pieceSpan, geometry, configuration.PartGenerators, palette);
                }
                finally {
                    geometry.SetVerticalOffset(0.0f);
                }

                foreach (ref readonly var stationTrack in pieceSpan) {
                    if (!stationTrack.Part.IsStationPlatform()) continue;

                    configuration.StationBuilder?.AddStationTile(
                        stationTrack.Part,
                        stationTrack.TileX,
                        stationTrack.TileY,
                        stationTrack.Direction,
                        stationTrack.BaseHeight,
                        geometry,
                        GetPaletteColor(stationTrack.SupportPaletteIndex, palette),
                        entranceSideOpenings.GetValueOrDefault((
                            stationTrack.RideId,
                            stationTrack.TileX,
                            stationTrack.TileY,
                            stationTrack.BaseHeight)));
                }

                var centerlines = captureCenterline ? geometry.EndCenterlineCapture() : [];
                if (configuration.SmoothMovement && centerlines.Length > 0) {
                    var sequences = new TrackCenterlineSequence[centerlines.Length];
                    for (var i = 0; i < centerlines.Length; i++) {
                        var sequenceTrack = pieceSpan[0];
                        foreach (ref readonly var candidate in pieceSpan) {
                            if (candidate.Sequence != centerlines[i].Sequence) continue;
                            sequenceTrack = candidate;
                            break;
                        }
                        sequences[i] = new TrackCenterlineSequence(
                            sequenceTrack.TileX, sequenceTrack.TileY, sequenceTrack.BaseHeight, centerlines[i]);
                    }

                    rideCenterlines.Add(new TrackCenterlinePiece(
                        key.RideId,
                        track.RideType,
                        track.Part,
                        key.OriginTileX,
                        key.OriginTileY,
                        key.Direction,
                        key.OriginBaseHeight,
                        sequences));
                }

                if (hasSupportPlacements && configuration.SupportMeshBuilder is { } supportMeshBuilder) {
                    if (centerlines.Length == 0)
                        throw new InvalidDataException($"Track piece {track.Part} did not expose a centerline for support placement.");

                    var placements = new List<TrackSupportPlacement>();

                    foreach (ref readonly var supportTrack in pieceSpan) {
                        if (supportMeshBuilder.PlacementMode == TrackSupportPlacementMode.TrackBased) {
                            placements.Clear();
                            foreach (var progress in TrackBasedSupportProgressMap.GetProgresses(supportTrack))
                                placements.Add(TrackSupportPlacement.OnTrack(trackProgress: progress));
                        }
                        else {
                            configuration.PopulateSupportPlacements(supportTrack, placements);
                        }

                        var trackHeightOffset = configuration.SupportTrackOffset;

                        var supportColor = GetPaletteColor(supportTrack.SupportPaletteIndex, palette);
                        foreach (var placement in placements) {
                            var anchor = ResolveSupportAnchor(
                                supportTrack, placement, supportMeshBuilder.PlacementMode, centerlines,
                                mapWidth, mapHeight);

                            var surfaceIndex = checked(anchor.SurfaceTileY * mapWidth + anchor.SurfaceTileX);
                            var surface = surfaces[surfaceIndex];
                            var trackBaseHeight = supportTrack.BaseHeight * WorldUnits.HeightPerBaseUnit;
                            if (IsSurfaceLevelWithTrackBase(surface, trackBaseHeight)) continue;

                            supportMeshBuilder.AddSupport(supportTrack, placement, anchor, supportGeometry, surface, supportColor, trackHeightOffset);
                        }
                    }
                }
            }

            geometry.EndRegion();
            supportGeometry.EndRegion();
        }

        return new TrackMeshBuildData(geometry.Build(), supportGeometry.Build(), rideCenterlines.ToArray());
    }

    private static TrackSupportAnchor ResolveSupportAnchor(
        in RCTTrackElementData track,
        TrackSupportPlacement placement,
        TrackSupportPlacementMode placementMode,
        ReadOnlySpan<TrackCenterlinePath> centerlines,
        int mapWidth,
        int mapHeight)
    {
        Vector3 position;
        Vector3 tangent;

        if (placementMode == TrackSupportPlacementMode.TrackBased) {
            var centerline = GetCenterlineForSequence(track.Sequence, centerlines);
            (position, tangent) = FindCenterlinePointAtProgress(centerline.Points, placement.TrackProgress);
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

    private static bool IsSurfaceLevelWithTrackBase(RCTTileSurfaceData surface, float trackBaseHeight)
    {
        var baseHeight = surface.BaseZ * WorldUnits.HeightPerBaseUnit;
        return baseHeight + surface.NorthCornerHeight * 1.5f == trackBaseHeight
            && baseHeight + surface.EastCornerHeight * 1.5f == trackBaseHeight
            && baseHeight + surface.SouthCornerHeight * 1.5f == trackBaseHeight
            && baseHeight + surface.WestCornerHeight * 1.5f == trackBaseHeight;
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

    private static Dictionary<(ushort RideId, int TileX, int TileY, byte BaseHeight), StationSideOpenings>
        BuildEntranceSideOpenings(
            ReadOnlySpan<RCTTrackElementData> trackElements,
            ReadOnlySpan<RCTRideEntranceData> entrances)
    {
        var tracksByTile = new Dictionary<(int X, int Y), List<RCTTrackElementData>>();
        foreach (ref readonly var track in trackElements) {
            if (!tracksByTile.TryGetValue((track.TileX, track.TileY), out var tileTracks)) {
                tileTracks = new List<RCTTrackElementData>();
                tracksByTile.Add((track.TileX, track.TileY), tileTracks);
            }
            tileTracks.Add(track);
        }

        var openings = new Dictionary<(ushort RideId, int TileX, int TileY, byte BaseHeight), StationSideOpenings>();
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
            if (!tracksByTile.TryGetValue((entrance.TileX + offsetX, entrance.TileY + offsetY), out var tileTracks))
                continue;

            foreach (var track in tileTracks) {
                if (track.RideId != entrance.RideId
                    || track.BaseHeight != entrance.BaseHeight
                    || !track.Part.IsStationPlatform())
                    continue;

                // OpenRCT2 uses (entrance direction - reverse track direction) & 3
                // to identify the entrance side; relative directions 1 and 3 are
                // the two lateral sides of the track.
                var relativeDirection = (entrance.Direction - ((track.Direction + 2) & 3)) & 3;
                var sideOpening = relativeDirection switch {
                    1 => StationSideOpenings.PositiveSide,
                    3 => StationSideOpenings.NegativeSide,
                    _ => StationSideOpenings.None
                };
                if (sideOpening == StationSideOpenings.None)
                    continue;

                var key = (track.RideId, track.TileX, track.TileY, track.BaseHeight);
                openings[key] = openings.GetValueOrDefault(key) | sideOpening;
            }
        }

        return openings;
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
