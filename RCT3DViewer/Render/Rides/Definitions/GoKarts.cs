using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class GoKarts : RideDefinition
{
    public GoKarts() : base(RCTRideType.GoKarts)
    {
        CarVerticalOffset = 0.25f;

        SupportStyle = RideSupportStyle.WoodenTruss;
    }

    protected override TrackShape GetTrackShape(RCTTrackElementData track)
    {
        return new TrackShape {
            RailShapes = [
            // left-outside
            new TrackRailProfileShape([
                new Vector2(-1.40f, 0.3f),
                new Vector2(-1.40f, 0.0f),
            ], false) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(220, 0),
            },
            // left-top
            new TrackRailProfileShape([
                new Vector2(-1.30f, 0.3f),
                new Vector2(-1.40f, 0.3f),
            ], false) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(220, 0),
            },
            // left-inside
            new TrackRailProfileShape([
                new Vector2(-1.30f, 0.1f),
                new Vector2(-1.30f, 0.3f),
            ], false) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(220, 0),
            },
            // top
            new TrackRailProfileShape([
                new Vector2(1.30f, 0.1f),
                new Vector2(-1.30f, 0.1f),
            ], false) {
                Color = new TrackRgba8(30, 30, 30, 255),
                Specular = new TrackRailSpecularData(220, 0),
            },
            // right-inside
            new TrackRailProfileShape([
                new Vector2(1.30f, 0.3f),
                new Vector2(1.30f, 0.1f),
            ], false) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(220, 0),
            },
            // right-top
            new TrackRailProfileShape([
                new Vector2(1.40f, 0.3f),
                new Vector2(1.30f, 0.3f),
            ], false) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(220, 0),
            },
            // right-outside
            new TrackRailProfileShape([
                new Vector2(1.40f, 0.0f),
                new Vector2(1.40f, 0.3f),
            ], false) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(220, 0),
            },
            ],
        };
    }
}
