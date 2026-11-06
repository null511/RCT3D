using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class LogFlume : RideDefinition
{
    private static readonly TrackRailSpecularData specular = new(220, 0);


    public LogFlume() : base(RCTRideType.LogFlume)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.5f;
        CarVerticalOffset = -0.25f;

        SupportStyle = RideSupportStyle.MetalBoxed;
        SupportTrackOffset = -0.21f;

        WaterPlanePolyline = [
            new Vector2(-1.0f, 0.1f),
            new Vector2( 1.0f, 0.1f),
        ];

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        var shape = new TrackPart();

        // top
        shape.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2( 1.00f,  0.25f),
            new Vector2( 0.90f, -0.20f),
            new Vector2(-0.90f, -0.20f),
            new Vector2(-1.00f,  0.25f),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = specular,
        });

        // bottom
        shape.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(-1.02f,  0.23f),
            new Vector2(-0.92f, -0.22f),
            new Vector2( 0.92f, -0.22f),
            new Vector2( 1.02f,  0.23f),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = specular,
        });

        // left
        shape.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(-1.00f,  0.25f),
            new Vector2(-1.02f,  0.23f),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = specular,
        });

        // right
        shape.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2( 1.02f,  0.23f),
            new Vector2( 1.00f,  0.25f),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = specular,
        });

        return shape;
    }
}
