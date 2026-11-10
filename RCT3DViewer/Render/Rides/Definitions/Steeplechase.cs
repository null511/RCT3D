using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class Steeplechase : RideDefinition
{
    private static readonly TrackRailSpecularData paintSpecular = new(100, 20);


    public Steeplechase() : base(RCTRideType.Steeplechase)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.3f;
        Vehicles.LocalOffset = 0.25f;

        SupportStyle = RideSupportStyle.MetalStick;
        SupportTrackOffset = -0.2f;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        var part = new TrackPart {
            BraceInterval = 0.6f,
        };

        // upper
        part.AppendRailPart.Circle(0f, 0.2f, 0.08f, 5, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = paintSpecular;
        });

        // lower
        part.AppendRailPart.Circle(0f, -0.2f, 0.06f, 5, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = paintSpecular;
        });

        part.AppendBracePart.Cylinder(
            new Vector3(0f, -0.2f, 0f),
            new Vector3(0f,  0.2f, 0f),
            0.06f, 4, shape => {
                shape.Swatch = TrackRailSwatch.Alternate;
                shape.Specular = paintSpecular;
            });

        return part;
    }
}
