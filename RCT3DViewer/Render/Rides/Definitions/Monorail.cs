using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal class Monorail : RideDefinition
{
    public Monorail() : base(RCTRideType.Monorail)
    {
        StationBuilder = new DefaultTrackStationBuilder();
        SmoothMovement = true;
        CarVerticalOffset = 1.0f;

        SupportStyle = RideSupportStyle.MetalBoxed;
        SupportTrackOffset = 0.2f;
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        return new TrackPart {
            RailProfiles = [
            new TrackRailProfileShape([
                new Vector2(0.20f, 0.40f),
                new Vector2(0.20f, 0.50f),
                new Vector2(0.20f, 0.55f),
                new Vector2(0.15f, 0.60f),
                new Vector2(0.10f, 0.60f),
                new Vector2(-0.10f, 0.60f),
                new Vector2(-0.15f, 0.60f),
                new Vector2(-0.20f, 0.55f),
                new Vector2(-0.20f, 0.50f),
                new Vector2(-0.20f, 0.40f),
            ], false) {
                Swatch = TrackRailSwatch.Main,
                Specular = new TrackRailSpecularData(180, 0),
            },
            new TrackRailProfileShape([
                new Vector2(-0.20f, 0.40f),
                new Vector2(-0.20f, 0.09f),
                new Vector2(-0.20f, 0.07f),
                new Vector2(-0.18f, 0.05f),
                new Vector2(-0.16f, 0.05f),
                new Vector2(0.16f, 0.05f),
                new Vector2(0.18f, 0.05f),
                new Vector2(0.20f, 0.07f),
                new Vector2(0.20f, 0.09f),
                new Vector2(0.20f, 0.40f),
            ], false) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(240, 0),
            },
            ],
        };
    }
}
