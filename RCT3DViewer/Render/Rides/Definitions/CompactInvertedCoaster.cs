using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class CompactInvertedCoaster : RideDefinition
{
    public CompactInvertedCoaster() : base(RCTRideType.CompactInvertedCoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 2.0f;
        CarVerticalOffset = -1.5f;

        SupportStyle = RideSupportStyle.MetalTubesInverted;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        return new TrackPart {
            RailProfiles = [
                new TrackRailProfileShape(CreateCircle(-0.42f, 1.12f, 0.06f, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = new TrackRailSpecularData(100, 0),
                },
                new TrackRailProfileShape(CreateCircle(0.00f, 1.20f, 0.18f, 6), true) {
                    Swatch = TrackRailSwatch.Main,
                    Specular = new TrackRailSpecularData(100, 0),
                },
                new TrackRailProfileShape(CreateCircle(0.42f, 1.12f, 0.06f, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = new TrackRailSpecularData(100, 0),
                },
            ],
        };
    }
}
