using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class InvertedRollerCoaster : RideDefinition
{
    private static readonly TrackRailSpecularData railSpecular = new(100, 0);


    public InvertedRollerCoaster() : base(RCTRideType.InvertedRollerCoaster)
    {
        StationBuilder = new DefaultTrackStationBuilder();
        SmoothMovement = true;
        CenterlineHeightOffset = 2.0f;
        CarVerticalOffset = -1.5f;

        SupportStyle = RideSupportStyle.MetalBoxed;
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        const float railSeparation = 0.53f;
        const float railRadius = 0.07f;

        var part = new TrackPart {
            RailProfiles = [
                // left
                new TrackRailProfileShape(CreateCircle(-railSeparation, 0.90f, railRadius, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = railSpecular,
                },
                // right
                new TrackRailProfileShape(CreateCircle(railSeparation, 0.90f, railRadius, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = railSpecular,
                },
                // center
                .. CreateBox(0f, 1.35f, 0.30f, 0.25f, p => {
                    p.Swatch = TrackRailSwatch.Alternate;
                    p.Specular = railSpecular;
                })
            ],
        };

        return part;
    }
}
