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
        SmoothMovement = true;
        CenterlineHeightOffset = 2.0f;
        CarVerticalOffset = -1.5f;

        SupportStyle = RideSupportStyle.MetalBoxed;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        const float railSeparation = 0.53f;
        const float railRadius = 0.07f;
        const float railCenterY = 0.90f;

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
        part.AppendRailPart.Box(0f, 1.35f, 0.30f, 0.25f, p => {
            p.Swatch = TrackRailSwatch.Alternate;
            p.Specular = railSpecular;
        });

        return part;
    }
}
