using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal class WoodenWildMouse : RideDefinition
{
    public WoodenWildMouse() : base(RCTRideType.WoodenWildMouse)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.5f;

        SupportStyle = RideSupportStyle.WoodenTruss;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        var shape = new TrackPart();
        var railColor = new TrackRgba8(200, 200, 200, 255);
        var railSpecular = new TrackRailSpecularData(180, 255);

        var centerX = 0f;
        var centerY = 0f;
        var centerLeftX = -0.4f;
        var centerRightX = 0.4f;

        var halfHeight = 0.06f;
        var halfWidth = 0.12f;
        var sideHalfWidth = 0.04f;

        // left rail - top
        shape.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(centerLeftX + sideHalfWidth, centerY + halfHeight),
            new Vector2(centerLeftX - sideHalfWidth, centerY + halfHeight),
        ], false) {
            Color = railColor,
            Specular = railSpecular,
        });

        // left rail - left
        shape.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(centerLeftX - sideHalfWidth, centerY + halfHeight),
            new Vector2(centerLeftX - sideHalfWidth, centerY - halfHeight),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = railSpecular,
        });

        // left rail - bottom
        shape.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(centerLeftX - sideHalfWidth, centerY - halfHeight),
            new Vector2(centerLeftX + sideHalfWidth, centerY - halfHeight),
        ], false) {
            Color = railColor,
            Specular = railSpecular,
        });

        // left rail - right
        shape.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(centerLeftX + sideHalfWidth, centerY - halfHeight),
            new Vector2(centerLeftX + sideHalfWidth, centerY + halfHeight),
        ], false) {
            Color = railColor,
            Specular = railSpecular,
        });

        // center rail
        shape.AppendRailPart.Box(centerX, centerY, halfWidth, halfHeight, part => {
            part.Color = railColor;
            part.Specular = railSpecular;
        });

        // right rail - top
        shape.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(centerRightX + sideHalfWidth, centerY + halfHeight),
            new Vector2(centerRightX - sideHalfWidth, centerY + halfHeight),
        ], false) {
            Color = railColor,
            Specular = railSpecular,
        });

        // right rail - left
        shape.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(centerRightX - sideHalfWidth, centerY + halfHeight),
            new Vector2(centerRightX - sideHalfWidth, centerY - halfHeight),
        ], false) {
            Color = railColor,
            Specular = railSpecular,
        });

        // right rail - bottom
        shape.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(centerRightX - sideHalfWidth, centerY - halfHeight),
            new Vector2(centerRightX + sideHalfWidth, centerY - halfHeight),
        ], false) {
            Color = railColor,
            Specular = railSpecular,
        });

        // right rail - right
        shape.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(centerRightX + sideHalfWidth, centerY - halfHeight),
            new Vector2(centerRightX + sideHalfWidth, centerY + halfHeight),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = railSpecular,
        });

        return shape;
    }
}
