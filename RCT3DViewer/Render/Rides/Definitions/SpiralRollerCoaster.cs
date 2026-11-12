using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal class SpiralRollerCoaster : RideDefinition
{
    private static readonly TrackRailSpecularData railSpecular = new(80, 20);


    public SpiralRollerCoaster() : base(RCTRideType.SpiralRollerCoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.5f;

        SupportStyle = RideSupportStyle.MetalFork;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        const float railSeparation = 0.42f;
        const float railRadius = 0.075f;
        const float railCenterY = 0f;

        var part = new TrackPart {
            BraceInterval = 0.8f,
        };

        part.AppendRailPart.Circle(-railSeparation, railCenterY, railRadius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = railSpecular;
        });

        part.AppendRailPart.Circle( railSeparation, railCenterY, railRadius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = railSpecular;
        });

        part.AppendBracePart.Cylinder(
            new Vector3(-railSeparation, railCenterY, 0f),
            new Vector3( railSeparation, railCenterY, 0f),
            0.055f, 5, shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = railSpecular;
            });

        return part;
    }
}
