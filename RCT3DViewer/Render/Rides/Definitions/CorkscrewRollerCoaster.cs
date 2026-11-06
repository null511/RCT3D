using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal class CorkscrewRollerCoaster : RideDefinition
{
    private static readonly TrackRailSpecularData paintSpecular = new(100, 0);


    public CorkscrewRollerCoaster() : base(RCTRideType.CorkscrewRollerCoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.5f;
        CarVerticalOffset = 0.5f;

        SupportStyle = RideSupportStyle.MetalTubes;
        SupportTrackOffset = -0.24f;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        const float railSeparation = 0.42f;

        var shape = new TrackPart {
            BraceInterval = 0.6f,
            RailProfiles = [
                // left rail
                new TrackRailProfileShape(CreateCircle(-railSeparation, 0f, 0.08f, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = paintSpecular,
                },
                // right rail
                new TrackRailProfileShape(CreateCircle(railSeparation, 0f, 0.08f, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = paintSpecular,
                },
                // center rail
                new TrackRailProfileShape(CreateCircle(0f, -0.24f, 0.14f, 6), true) {
                    Swatch = TrackRailSwatch.Main,
                    Specular = paintSpecular,
                },
            ],
        };

        // center braces
        shape.AppendBracePart.Cylinder(
            new Vector3(-railSeparation, -0.26f, 0f),
            new Vector3( railSeparation, -0.26f, 0f),
            0.06f, 4, p => {
                p.Swatch = TrackRailSwatch.Main;
                p.Specular = paintSpecular;
            });

        // left brace
        shape.AppendBracePart.Cylinder(
            new Vector3(-railSeparation, -0.26f, 0f),
            new Vector3(-railSeparation,  0.00f, 0f),
            0.06f, 4, p => {
                p.Swatch = TrackRailSwatch.Main;
                p.Specular = paintSpecular;
            });

        // right brace
        shape.AppendBracePart.Cylinder(
            new Vector3( railSeparation, -0.26f, 0f),
            new Vector3( railSeparation,  0.00f, 0f),
            0.06f, 4, p => {
                p.Swatch = TrackRailSwatch.Main;
                p.Specular = paintSpecular;
            });

        return shape;
    }
}
