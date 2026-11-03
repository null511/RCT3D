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
        CarVerticalOffset = 0.5f;

        CenterlineHeightOffset = 0.28f;
        SupportStyle = RideSupportStyle.MetalFork;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackShape GetTrackShape(RCTTrackElementData track)
    {
        var shape = new TrackShape {
            BraceInterval = 0.8f,
            RailShapes = [
                new TrackRailProfileShape(CreateCircle(-0.42f, 0f, 0.075f, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = railSpecular,
                },
                new TrackRailProfileShape(CreateCircle( 0.42f, 0f, 0.075f, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = railSpecular,
                },
            ],
        };

        var brace = shape.AppendBracePart.Cylinder(
            new Vector3(-0.42f, 0f, 0f),
            new Vector3( 0.42f, 0f, 0f),
            0.055f, 5);

        brace.Swatch = TrackRailSwatch.Main;
        brace.Specular = railSpecular;

        return shape;
    }
}
