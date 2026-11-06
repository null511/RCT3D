using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal class GigaCoaster : RideDefinition
{
    private static readonly TrackRailSpecularData railSpecular = new(80, 20);


    public GigaCoaster() : base(RCTRideType.GigaCoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.8f;

        SupportStyle = RideSupportStyle.MetalTubes;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        var shape = new TrackPart {
            BraceInterval = 0.5f,
            RailProfiles = [
                new TrackRailProfileShape(CreateCircle(-0.32f, -0.15f, 0.075f, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = railSpecular,
                },
                new TrackRailProfileShape(CreateCircle( 0.32f, -0.15f, 0.075f, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = railSpecular,
                },
                new TrackRailProfileShape(CreateCircle( 0.00f, -0.55f, 0.075f, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = railSpecular,
                },
            ],
        };

        // top
        shape.AppendBracePart.Cylinder(
            new Vector3(-0.32f, -0.15f, 0f),
            new Vector3( 0.32f, -0.15f, 0f),
            0.045f, 5, part => {
                part.Swatch = TrackRailSwatch.Main;
                part.Specular = railSpecular;
            });

        // left
        shape.AppendBracePart.Cylinder(
            new Vector3(-0.32f, -0.15f, 0f),
            new Vector3( 0.00f, -0.55f, 0f),
            0.045f, 5, part => {
                part.Swatch = TrackRailSwatch.Main;
                part.Specular = railSpecular;
            });

        shape.AppendBracePart.Cylinder(
            new Vector3( 0.32f, -0.15f, 0f),
            new Vector3( 0.00f, -0.55f, 0f),
            0.045f, 5, part => {
                part.Swatch = TrackRailSwatch.Main;
                part.Specular = railSpecular;
            });


        return shape;
    }
}
