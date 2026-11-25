using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class MineRide : RideDefinition
{
    private static readonly TrackRailSpecularData railSpecular = new(80, 20);


    public MineRide() : base(RCTRideType.MineRide)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.5f;

        SupportStyle = RideSupportStyle.MetalFork;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        var part = new TrackPart {
            BraceInterval = 0.6f,
        };

        part.AppendRailPart.Circle(-0.42f, 0f, 0.075f, 5, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = railSpecular;
        });

        part.AppendRailPart.Circle( 0.42f, 0f, 0.075f, 5, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = railSpecular;
        });

        part.AppendRailPart.Box(0f, 0f, 0.06f, 0.05f, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = railSpecular;
        });

        part.AppendBracePart.Cylinder(
            new Vector3(-0.42f, 0f, 0f),
            new Vector3( 0.42f, 0f, 0f),
            0.05f, 4, shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = railSpecular;
            });

        return part;
    }
}
