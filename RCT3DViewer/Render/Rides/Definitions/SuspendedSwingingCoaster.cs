using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class SuspendedSwingingCoaster : RideDefinition
{
    private static readonly TrackRailSpecularData railSpecular = new(160, 0);


    public SuspendedSwingingCoaster() : base(RCTRideType.SuspendedSwingingCoaster)
    {
        CenterlineHeightOffset = 3.0f;

        SupportStyle = RideSupportStyle.MetalTubes;

        StationBuilder = new DefaultTrackStationBuilder();

        //Vehicles.LocalOffset = -0.5f;
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        const float railSeparation = 0.53f;
        const float railRadius = 0.07f;
        const float railCenterY = 0.0f;

        var part = new TrackPart();

        // left
        part.AppendRailPart.Circle(-railSeparation, railCenterY, railRadius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = railSpecular;
        });

        // right
        part.AppendRailPart.Circle( railSeparation, railCenterY, railRadius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = railSpecular;
        });

        // center
        part.AppendRailPart.Circle(0f, railCenterY + 0.50f, 0.18f, 6, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = railSpecular;
        });

        return part;
    }
}
