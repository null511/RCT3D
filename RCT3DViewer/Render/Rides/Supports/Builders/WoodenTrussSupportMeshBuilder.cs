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
        if (loopTrackTypes.Contains(track.TrackType)) return;

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

        var segmentBaseHeight = terrainBaseGameUnits * WorldUnits.HeightPerBaseUnit;
        var wholeSegmentUnits = (int)MathF.Floor((topHeight - segmentBaseHeight) / (2f*WorldUnits.HeightPerBaseUnit));

        // alignment
        if (wholeSegmentUnits >= 1 && terrainBaseGameUnits % 4 >= 2) {
            AddBoxSegment(geometry, west, east, south, north, segmentBaseHeight, 2f * WorldUnits.HeightPerBaseUnit, color);
            segmentBaseHeight += 2f * WorldUnits.HeightPerBaseUnit;
            wholeSegmentUnits -= 1;
        }

        while (wholeSegmentUnits >= 2) {
            AddBoxSegment(geometry, west, east, south, north, segmentBaseHeight, 4f * WorldUnits.HeightPerBaseUnit, color);
            segmentBaseHeight += 4f * WorldUnits.HeightPerBaseUnit;
            wholeSegmentUnits -= 2;
        }

        if (wholeSegmentUnits >= 1) {
            AddBoxSegment(geometry, west, east, south, north, segmentBaseHeight, 2f * WorldUnits.HeightPerBaseUnit, color);
        }
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
