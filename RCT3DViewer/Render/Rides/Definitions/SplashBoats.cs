using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal class SplashBoats : RideDefinition
{
    private static readonly TrackRailSpecularData paintSpecular = new(120, 0);


    public SplashBoats() : base(RCTRideType.SplashBoats)
    {
        CenterlineHeightOffset = 0.5f;

        SupportStyle = RideSupportStyle.WoodenTruss;

        StationBuilder = new DefaultTrackStationBuilder();

        Vehicles.Size = new Vector3(2.2f, 0.25f, 3.2f);
        Vehicles.LocalOffset = 0.15f;
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        var part = new TrackPart {
            WaterPlane = [
                new Vector2(-1.45f, 0.1f),
                new Vector2( 1.45f, 0.1f),
            ],
        };

        // top
        part.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2( 1.44f,  0.25f),
            new Vector2( 1.34f, -0.20f),
            new Vector2(-1.34f, -0.20f),
            new Vector2(-1.44f,  0.25f),
        ], false) {
            Swatch = TrackRailSwatch.Alternate,
            Specular = paintSpecular,
        });

        // bottom
        part.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(-1.50f,  0.23f),
            new Vector2(-1.40f, -0.22f),
            new Vector2( 1.40f, -0.22f),
            new Vector2( 1.50f,  0.23f),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = paintSpecular,
        });

        // left
        part.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(-1.44f,  0.25f),
            new Vector2(-1.50f,  0.23f),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = paintSpecular,
        });

        // right
        part.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2( 1.50f,  0.23f),
            new Vector2( 1.44f,  0.25f),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = paintSpecular,
        });

        return part;
    }
}
