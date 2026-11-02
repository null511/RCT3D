using RCT3D.Interop.Models;
using System.Numerics;

namespace RCT3D.Render.Rides.Tracks.Builders;

internal sealed class DefaultTrackStationBuilder : ITrackStationBuilder
{
    private const float MetalPlatformThickness = 0.18f;
    private static readonly TrackRailSpecularData MetalPlatformSpecular = new(80, 20);
    private static readonly TrackRgba8 MetalPlatformColor = new(20, 20, 20, 255);

    private const float WoodPlatformWidth = 0.64f;
    private const float WoodPlatformThickness = 0.10f;
    private static readonly TrackRailSpecularData WoodPlatformSpecular = new(220, 0);
    private static readonly TrackRgba8 WoodPlatformColor = new(128, 104, 55, 255);

    private const float RailingBeamHeight = 0.48f;
    private const float RailingBeamHalfSize = 0.03f;

    private const float RailingPostHeight = 0.56f;
    private const float RailingPostHalfSize = 0.03f;


    public void AddStationTile(
        RCTTrackElementType trackType,
        int tileX,
        int tileY,
        byte direction,
        byte baseHeight,
        TrackMeshGeometry geometry,
        TrackRgba8 supportColor,
        StationSideOpenings sideOpenings)
    {
        if (!trackType.IsStationPlatform()) return;

        var forward = TrackCenterlineGeometry.GetDirection(direction);
        var side = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
        var baseY = baseHeight * WorldUnits.HeightPerBaseUnit;
        var halfTile = WorldUnits.TileSize * 0.5f;

        var center = new Vector3(
            (tileX + 0.5f) * WorldUnits.TileSize,
            baseY,
            (tileY + 0.5f) * WorldUnits.TileSize);

        // metal base
        AddBox(
            geometry,
            center + Vector3.UnitY * (MetalPlatformThickness * 0.5f),
            forward,
            side,
            halfTile,
            halfTile,
            MetalPlatformThickness * 0.5f,
            MetalPlatformColor,
            MetalPlatformSpecular);

        // wood left
        var leftWoodCenter = center
            - side * (halfTile - WoodPlatformWidth*0.5f)
            + Vector3.UnitY * (MetalPlatformThickness + WoodPlatformThickness * 0.5f);

        AddBox(
            geometry,
            leftWoodCenter,
            forward,
            side,
            halfTile,
            WoodPlatformWidth * 0.5f,
            WoodPlatformThickness * 0.5f,
            WoodPlatformColor,
            WoodPlatformSpecular);

        // wood right
        var rightWoodCenter = center
                             + side * (halfTile - WoodPlatformWidth*0.5f)
                             + Vector3.UnitY * (MetalPlatformThickness + WoodPlatformThickness * 0.5f);

        AddBox(
            geometry,
            rightWoodCenter,
            forward,
            side,
            halfTile,
            WoodPlatformWidth * 0.5f,
            WoodPlatformThickness * 0.5f,
            WoodPlatformColor,
            WoodPlatformSpecular);

        // railings
        if ((sideOpenings & StationSideOpenings.NegativeSide) == 0)
            AddSideRailing(geometry, center, forward, side, -halfTile + RailingBeamHalfSize, halfTile, supportColor);

        if ((sideOpenings & StationSideOpenings.PositiveSide) == 0)
            AddSideRailing(geometry, center, forward, side, halfTile - RailingBeamHalfSize, halfTile, supportColor);
    }

    private static void AddSideRailing(
        TrackMeshGeometry geometry,
        Vector3 tileCenter,
        Vector3 forward,
        Vector3 side,
        float sideOffset,
        float halfTile,
        TrackRgba8 color)
    {
        var railCenter = tileCenter
            + side * sideOffset
            + Vector3.UnitY * (MetalPlatformThickness + WoodPlatformThickness + RailingBeamHeight);

        // top rail
        AddBox(
            geometry,
            railCenter,
            forward,
            side,
            halfTile,
            RailingBeamHalfSize,
            RailingBeamHalfSize,
            color,
            WoodPlatformSpecular);

        //foreach (var along in new[] { -halfTile, 0.0f, halfTile }) {
        const int beamCount = 10;
        for (var i = 0; i <= beamCount; i++) {
            var along = (i / (float)beamCount - 0.5f) * halfTile*2f;
            
            var postCenter = tileCenter + forward * along + side * sideOffset
                + Vector3.UnitY * (MetalPlatformThickness + WoodPlatformThickness + RailingPostHeight * 0.5f);

            AddBox(
                geometry,
                postCenter,
                forward,
                side,
                RailingPostHalfSize,
                RailingPostHalfSize,
                RailingPostHeight * 0.5f,
                color,
                WoodPlatformSpecular);
        }
    }

    private static void AddBox(
        TrackMeshGeometry geometry,
        Vector3 center,
        Vector3 forward,
        Vector3 side,
        float halfLength,
        float halfWidth,
        float halfHeight,
        TrackRgba8 color,
        TrackRailSpecularData specular)
    {
        var along = forward * halfLength;
        var across = side * halfWidth;
        var up = Vector3.UnitY * halfHeight;

        AddFace(geometry,
            center + along - across - up, center + along + across - up,
            center + along + across + up, center + along - across + up,
            forward, color, specular);

        AddFace(geometry,
            center - along - across - up, center - along - across + up,
            center - along + across + up, center - along + across - up,
            -forward, color, specular);

        AddFace(geometry,
            center - along + across - up, center - along + across + up,
            center + along + across + up, center + along + across - up,
            side, color, specular);

        AddFace(geometry,
            center - along - across - up, center + along - across - up,
            center + along - across + up, center - along - across + up,
            -side, color, specular);

        AddFace(geometry,
            center - along - across + up, center + along - across + up,
            center + along + across + up, center - along + across + up,
            Vector3.UnitY, color, specular);

        AddFace(geometry,
            center - along - across - up, center - along + across - up,
            center + along + across - up, center + along - across - up,
            -Vector3.UnitY, color, specular);
    }

    private static void AddFace(
        TrackMeshGeometry geometry,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        Vector3 normal,
        TrackRgba8 color,
        TrackRailSpecularData specular)
    {
        var aIndex = geometry.AddIndexedVertex(a, normal, color, specular);
        var bIndex = geometry.AddIndexedVertex(b, normal, color, specular);
        var cIndex = geometry.AddIndexedVertex(c, normal, color, specular);
        var dIndex = geometry.AddIndexedVertex(d, normal, color, specular);

        AddTriangle(geometry, aIndex, bIndex, cIndex, b - a, c - a, normal);
        AddTriangle(geometry, aIndex, cIndex, dIndex, c - a, d - a, normal);
    }

    private static void AddTriangle(
        TrackMeshGeometry geometry,
        uint a,
        uint b,
        uint c,
        Vector3 edgeAB,
        Vector3 edgeAC,
        Vector3 normal)
    {
        // The camera mirrors X, while its Vulkan Y-flip compensates for the viewport's
        // screen-space Y direction; together this makes front-face winding oppose the
        // outward object-space normals for the track pipeline's CCW front-face setting.
        if (Vector3.Dot(Vector3.Cross(edgeAB, edgeAC), normal) > 0.0f) (b, c) = (c, b);

        geometry.AddIndexedTriangle(a, b, c);
    }
}
