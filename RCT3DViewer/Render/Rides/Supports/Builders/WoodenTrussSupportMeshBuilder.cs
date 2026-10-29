using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Supports.Builders;

internal sealed class WoodenTrussSupportMeshBuilder : IRideSupportMeshBuilder
{
    private const float BeamHalfWidth = 0.08f;

    private static readonly TrackRailSpecularData supportSpecular = new(220, 0);

    private static readonly HashSet<RCTTrackElementType> loopTrackTypes = Enum.GetValues<RCTTrackElementType>()
        .Where(trackType => trackType.ToString().Contains("Loop", StringComparison.Ordinal))
        .ToHashSet();

    public TrackSupportPlacementMode PlacementMode => TrackSupportPlacementMode.TileBased;
    public bool HasDefaultSupportPlacements => true;


    public void GetDefaultSupportPlacements(RCTTrackElementData track, List<TrackSupportPlacement> placements)
    {
        placements.Clear();
        if (loopTrackTypes.Contains(track.Part)) return;

        placements.Add(TrackSupportPlacement.AtTile());
    }

    public void AddSupport(
        RCTTrackElementData track,
        TrackSupportPlacement placement,
        TrackSupportAnchor anchor,
        RideSupportMeshGeometry geometry,
        RCTTileSurfaceData surface,
        TrackRgba8 color,
        float trackHeightOffset)
    {
        var tileX = track.TileX * WorldUnits.TileSize;
        var tileZ = track.TileY * WorldUnits.TileSize;

        var west = tileX + BeamHalfWidth;
        var east = tileX + WorldUnits.TileSize - BeamHalfWidth;
        var south = tileZ + BeamHalfWidth;
        var north = tileZ + WorldUnits.TileSize - BeamHalfWidth;

        var trackBaseHeight = track.BaseHeight * WorldUnits.HeightPerBaseUnit;
        var southWestHeight = GetCornerHeight(surface, surface.SouthCornerHeight);
        var southEastHeight = GetCornerHeight(surface, surface.EastCornerHeight);
        var northEastHeight = GetCornerHeight(surface, surface.NorthCornerHeight);
        var northWestHeight = GetCornerHeight(surface, surface.WestCornerHeight);

        if (southWestHeight == trackBaseHeight
            && southEastHeight == trackBaseHeight
            && northEastHeight == trackBaseHeight
            && northWestHeight == trackBaseHeight)
            return;

        var topHeight = trackBaseHeight + placement.HeightExtra * WorldUnits.HeightPerBaseUnit / 8.0f;

        var terrainBaseGameUnits = Math.Min(
            Math.Min(
                GetCornerHeightInBaseUnits(surface, surface.SouthCornerHeight),
                GetCornerHeightInBaseUnits(surface, surface.EastCornerHeight)),
            Math.Min(
                GetCornerHeightInBaseUnits(surface, surface.NorthCornerHeight),
                GetCornerHeightInBaseUnits(surface, surface.WestCornerHeight)));

        var segmentBaseHeight = anchor.SupportBaseHeight ?? terrainBaseGameUnits * WorldUnits.HeightPerBaseUnit;

        segmentBaseHeight = MathF.Ceiling(segmentBaseHeight / (WorldUnits.HeightPerBaseUnit * 2f)) * (WorldUnits.HeightPerBaseUnit * 2f);

        var segmentBaseGameUnits = anchor.SupportBaseHeight.HasValue
            ? segmentBaseHeight / WorldUnits.HeightPerBaseUnit
            : terrainBaseGameUnits;

        var wholeSegmentUnits = (int)MathF.Floor((topHeight - segmentBaseHeight) / (2f*WorldUnits.HeightPerBaseUnit));

        // alignment
        if (wholeSegmentUnits >= 1 && segmentBaseGameUnits % 4 >= 2) {
            AddShortBoxSegment(geometry, west, east, south, north, segmentBaseHeight, track.Direction, color);
            segmentBaseHeight += 2f * WorldUnits.HeightPerBaseUnit;
            wholeSegmentUnits -= 1;
        }

        while (wholeSegmentUnits >= 2) {
            AddTallBoxSegment(geometry, west, east, south, north, segmentBaseHeight, track.Direction, color);
            segmentBaseHeight += 4f * WorldUnits.HeightPerBaseUnit;
            wholeSegmentUnits -= 2;
        }

        if (wholeSegmentUnits >= 1) {
            AddShortBoxSegment(geometry, west, east, south, north, segmentBaseHeight, track.Direction, color);
        }
    }

    private static void AddTallBoxSegment(
        RideSupportMeshGeometry geometry,
        float west,
        float east,
        float south,
        float north,
        float baseHeight,
        byte trackDirection,
        TrackRgba8 color)
    {
        AddBoxSegment(geometry, west, east, south, north, baseHeight, 4f * WorldUnits.HeightPerBaseUnit, color);

        var trackVector = TrackCenterlineGeometry.GetDirection(trackDirection);
        var leftDirection = Vector3.Normalize(Vector3.Cross(trackVector, Vector3.UnitY));
        var topHeight = baseHeight + 4f * WorldUnits.HeightPerBaseUnit;
        var center = new Vector3((west + east) * 0.5f, -BeamHalfWidth, (south + north) * 0.5f);
        var halfExtent = (east - west) * 0.5f;
        var leftOffset = leftDirection * halfExtent;

        AddDiagonalBracePair(
            geometry, center, trackVector, leftOffset, leftDirection, halfExtent,
            baseHeight, topHeight, color);

        AddDiagonalBracePair(
            geometry, center, trackVector, -leftOffset, leftDirection, halfExtent,
            baseHeight, topHeight, color);
    }

    private static void AddDiagonalBracePair(
        RideSupportMeshGeometry geometry,
        Vector3 center,
        Vector3 trackDirection,
        Vector3 sideOffset,
        Vector3 widthDirection,
        float halfLength,
        float baseHeight,
        float topHeight,
        TrackRgba8 color)
    {
        var lowerStart = center - trackDirection * halfLength + sideOffset;
        var upperEnd = center + trackDirection * halfLength + sideOffset;
        lowerStart.Y = baseHeight + center.Y;
        upperEnd.Y = topHeight + center.Y;
        geometry.AddBeam(lowerStart, upperEnd, widthDirection, BeamHalfWidth, BeamHalfWidth, color, supportSpecular);

        var upperStart = center - trackDirection * halfLength + sideOffset;
        var lowerEnd = center + trackDirection * halfLength + sideOffset;
        upperStart.Y = topHeight + center.Y;
        lowerEnd.Y = baseHeight + center.Y;
        geometry.AddBeam(upperStart, lowerEnd, widthDirection, BeamHalfWidth, BeamHalfWidth, color, supportSpecular);
    }

    private static void AddShortBoxSegment(
        RideSupportMeshGeometry geometry,
        float west,
        float east,
        float south,
        float north,
        float baseHeight,
        byte trackDirection,
        TrackRgba8 color)
    {
        AddBoxSegment(geometry, west, east, south, north, baseHeight, 2f * WorldUnits.HeightPerBaseUnit, color);

        // TODO: add short cross-section
        var trackVector = TrackCenterlineGeometry.GetDirection(trackDirection);
        var leftDirection = Vector3.Normalize(Vector3.Cross(trackVector, Vector3.UnitY));
        var topHeight = baseHeight + 2f * WorldUnits.HeightPerBaseUnit;
        var center = new Vector3((west + east) * 0.5f, -BeamHalfWidth, (south + north) * 0.5f);
        var halfExtent = (east - west) * 0.5f;
        var leftOffset = leftDirection * halfExtent;

        AddHalfDiagonalBracePair(
            geometry, center, trackVector, leftOffset, leftDirection, halfExtent,
            baseHeight, topHeight, color);

        AddHalfDiagonalBracePair(
            geometry, center, trackVector, -leftOffset, leftDirection, halfExtent,
            baseHeight, topHeight, color);
    }

    private static void AddHalfDiagonalBracePair(
        RideSupportMeshGeometry geometry,
        Vector3 center,
        Vector3 trackDirection,
        Vector3 sideOffset,
        Vector3 widthDirection,
        float halfLength,
        float baseHeight,
        float topHeight,
        TrackRgba8 color)
    {
        var lowerStart = center - trackDirection * halfLength + sideOffset;
        lowerStart.Y = baseHeight + center.Y;

        var upperCenter = center + sideOffset;
        upperCenter.Y = topHeight + center.Y;

        var lowerEnd = center + trackDirection * halfLength + sideOffset;
        lowerEnd.Y = baseHeight + center.Y;

        geometry.AddBeam(lowerStart, upperCenter, widthDirection, BeamHalfWidth, BeamHalfWidth, color, supportSpecular);
        geometry.AddBeam(upperCenter, lowerEnd, widthDirection, BeamHalfWidth, BeamHalfWidth, color, supportSpecular);
    }

    private static void AddBoxSegment(
        RideSupportMeshGeometry geometry,
        float west,
        float east,
        float south,
        float north,
        float baseHeight,
        float segmentHeight,
        TrackRgba8 color)
    {
        var topHeight = baseHeight + segmentHeight;
        var southWest = new Vector3(west, 0.0f, south);
        var southEast = new Vector3(east, 0.0f, south);
        var northEast = new Vector3(east, 0.0f, north);
        var northWest = new Vector3(west, 0.0f, north);

        AddVerticalBeam(geometry, southWest, baseHeight, topHeight, color);
        AddVerticalBeam(geometry, southEast, baseHeight, topHeight, color);
        AddVerticalBeam(geometry, northEast, baseHeight, topHeight, color);
        AddVerticalBeam(geometry, northWest, baseHeight, topHeight, color);

        AddTopFrame(
            geometry, southWest, southEast, northEast, northWest,
            topHeight - BeamHalfWidth, BeamHalfWidth, color);
    }

    private static void AddVerticalBeam(
        RideSupportMeshGeometry geometry,
        Vector3 position,
        float baseHeight,
        float topHeight,
        TrackRgba8 color)
    {
        var start = new Vector3(position.X, baseHeight, position.Z);
        var end = new Vector3(position.X, topHeight, position.Z);
        geometry.AddBeam(start, end, Vector3.UnitX, BeamHalfWidth, BeamHalfWidth, color, supportSpecular);
    }

    private static float GetCornerHeight(RCTTileSurfaceData surface, byte cornerHeight) =>
        surface.BaseZ * WorldUnits.HeightPerBaseUnit + cornerHeight * 1.5f;

    private static int GetCornerHeightInBaseUnits(RCTTileSurfaceData surface, byte cornerHeight) =>
        surface.BaseZ + cornerHeight * 2;

    private static void AddTopFrame(
        RideSupportMeshGeometry geometry,
        Vector3 southWest,
        Vector3 southEast,
        Vector3 northEast,
        Vector3 northWest,
        float centerHeight,
        float verticalHalfHeight,
        TrackRgba8 color)
    {
        AddTopBeam(geometry, southWest, southEast, centerHeight, verticalHalfHeight, color);
        AddTopBeam(geometry, southEast, northEast, centerHeight, verticalHalfHeight, color);
        AddTopBeam(geometry, northEast, northWest, centerHeight, verticalHalfHeight, color);
        AddTopBeam(geometry, northWest, southWest, centerHeight, verticalHalfHeight, color);
    }

    private static void AddTopBeam(
        RideSupportMeshGeometry geometry,
        Vector3 start,
        Vector3 end,
        float centerHeight,
        float verticalHalfHeight,
        TrackRgba8 color)
    {
        start.Y = centerHeight;
        end.Y = centerHeight;
        geometry.AddBeam(start, end, Vector3.UnitY, verticalHalfHeight, BeamHalfWidth, color, supportSpecular);
    }
}
