using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal class WoodenWildMouse : RideDefinition
{
    public WoodenWildMouse() : base(RCTRideType.WoodenWildMouse)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.5f;

        SupportStyle = RideSupportStyle.WoodenTruss;

        StationBuilder = new DefaultStationBuilder();
    }

    protected override IEnumerable<TrackRailProfileShape> GetRailParts(RCTTrackElementData track)
    {
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
        yield return new TrackRailProfileShape([
            new Vector2(centerLeftX + sideHalfWidth, centerY + halfHeight),
            new Vector2(centerLeftX - sideHalfWidth, centerY + halfHeight),
        ], false) {
            Color = railColor,
            Specular = railSpecular,
        };

        // left rail - left
        yield return new TrackRailProfileShape([
            new Vector2(centerLeftX - sideHalfWidth, centerY + halfHeight),
            new Vector2(centerLeftX - sideHalfWidth, centerY - halfHeight),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = railSpecular,
        };

        // left rail - bottom
        yield return new TrackRailProfileShape([
            new Vector2(centerLeftX - sideHalfWidth, centerY - halfHeight),
            new Vector2(centerLeftX + sideHalfWidth, centerY - halfHeight),
        ], false) {
            Color = railColor,
            Specular = railSpecular,
        };

        // left rail - right
        yield return new TrackRailProfileShape([
            new Vector2(centerLeftX + sideHalfWidth, centerY - halfHeight),
            new Vector2(centerLeftX + sideHalfWidth, centerY + halfHeight),
        ], false) {
            Color = railColor,
            Specular = railSpecular,
        };

        // center rail
        foreach (var part in CreateBox(centerX, centerY, halfWidth, halfHeight, shape => {
            shape.Color = railColor;
            shape.Specular = railSpecular;
        })) yield return part;

        // right rail - top
        yield return new TrackRailProfileShape([
            new Vector2(centerRightX + sideHalfWidth, centerY + halfHeight),
            new Vector2(centerRightX - sideHalfWidth, centerY + halfHeight),
        ], false) {
            Color = railColor,
            Specular = railSpecular,
        };

        // right rail - left
        yield return new TrackRailProfileShape([
            new Vector2(centerRightX - sideHalfWidth, centerY + halfHeight),
            new Vector2(centerRightX - sideHalfWidth, centerY - halfHeight),
        ], false) {
            Color = railColor,
            Specular = railSpecular,
        };

        // right rail - bottom
        yield return new TrackRailProfileShape([
            new Vector2(centerRightX - sideHalfWidth, centerY - halfHeight),
            new Vector2(centerRightX + sideHalfWidth, centerY - halfHeight),
        ], false) {
            Color = railColor,
            Specular = railSpecular,
        };

        // right rail - right
        yield return new TrackRailProfileShape([
            new Vector2(centerRightX + sideHalfWidth, centerY - halfHeight),
            new Vector2(centerRightX + sideHalfWidth, centerY + halfHeight),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = railSpecular,
        };
    }
}
