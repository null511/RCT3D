using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal class WoodenWildMouse : RideDefinition
{
    private static readonly TrackRgba8 railColor = new(200, 200, 200, 255);
    private static readonly TrackRailSpecularData railSpecular = new(180, 255);
    private static readonly TrackRailSpecularData paintSpecular = new(180, 0);


    public WoodenWildMouse() : base(RCTRideType.WoodenWildMouse)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.5f;

        SupportStyle = RideSupportStyle.WoodenTruss;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        const float halfHeight = 0.07f;
        const float halfWidth = 0.14f;
        const float halfWidthSides = 0.06f;

        const float railSeparation = 0.34f;
        const float centerY = -halfHeight;


        var part = new TrackPart {
            BraceInterval = 0.75f,
        };

        // left rail - top
        part.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(-railSeparation + halfWidthSides, centerY + halfHeight),
            new Vector2(-railSeparation - halfWidthSides, centerY + halfHeight),
        ], false) {
            Color = railColor,
            Specular = railSpecular,
        });

        // left rail - left
        part.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(-railSeparation - halfWidthSides, centerY + halfHeight),
            new Vector2(-railSeparation - halfWidthSides, centerY - halfHeight),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = paintSpecular,
        });

        // left rail - bottom
        part.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(-railSeparation - halfWidthSides, centerY - halfHeight),
            new Vector2(-railSeparation + halfWidthSides, centerY - halfHeight),
        ], false) {
            Color = railColor,
            Specular = railSpecular,
        });

        // left rail - right
        part.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(-railSeparation + halfWidthSides, centerY - halfHeight),
            new Vector2(-railSeparation + halfWidthSides, centerY + halfHeight),
        ], false) {
            Color = railColor,
            Specular = railSpecular,
        });

        // center rail
        part.AppendRailPart.Box(0f, centerY, halfWidth, halfHeight, shape => {
            shape.Color = railColor;
            shape.Specular = railSpecular;
        });

        // right rail - top
        part.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(railSeparation + halfWidthSides, centerY + halfHeight),
            new Vector2(railSeparation - halfWidthSides, centerY + halfHeight),
        ], false) {
            Color = railColor,
            Specular = railSpecular,
        });

        // right rail - left
        part.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(railSeparation - halfWidthSides, centerY + halfHeight),
            new Vector2(railSeparation - halfWidthSides, centerY - halfHeight),
        ], false) {
            Color = railColor,
            Specular = railSpecular,
        });

        // right rail - bottom
        part.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(railSeparation - halfWidthSides, centerY - halfHeight),
            new Vector2(railSeparation + halfWidthSides, centerY - halfHeight),
        ], false) {
            Color = railColor,
            Specular = railSpecular,
        });

        // right rail - right
        part.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(railSeparation + halfWidthSides, centerY - halfHeight),
            new Vector2(railSeparation + halfWidthSides, centerY + halfHeight),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = paintSpecular,
        });

        part.AppendBracePart.Cylinder(
            new Vector3(-railSeparation, -halfHeight, 0f),
            new Vector3( railSeparation, -halfHeight, 0f),
            0.06f, 4, shape => {
                shape.Color = railColor;
                shape.Specular = railSpecular;
            });

        return part;
    }
}
