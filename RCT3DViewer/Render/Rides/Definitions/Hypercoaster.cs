using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal class Hypercoaster : RideDefinition
{
    private static readonly TrackRailSpecularData paintSpecular = new(100, 0);


    public Hypercoaster() : base(RCTRideType.Hypercoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.5f;
        Vehicles.LocalOffset = 0.5f;

        SupportStyle = RideSupportStyle.MetalTubes;
        SupportTrackOffset = -0.24f;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        const float railSeparation = 0.42f;

        var part = new TrackPart {
            BraceInterval = 0.6f,
        };

        // left rail
        part.AppendRailPart.Circle(-railSeparation, 0f, 0.08f, 5, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = paintSpecular;
        });
        
        // right rail
        part.AppendRailPart.Circle( railSeparation, 0f, 0.08f, 5, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = paintSpecular;
        });

        // center rail
        part.AppendRailPart.Circle(0f, -0.24f, 0.14f, 6, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = paintSpecular;
        });

        // center braces
        part.AppendBracePart.Cylinder(
            new Vector3(-railSeparation, -0.26f, 0f),
            new Vector3( railSeparation, -0.26f, 0f),
            0.06f, 4, p => {
                p.Swatch = TrackRailSwatch.Main;
                p.Specular = paintSpecular;
            });

        // left brace
        part.AppendBracePart.Cylinder(
            new Vector3(-railSeparation, -0.26f, 0f),
            new Vector3(-railSeparation,  0.00f, 0f),
            0.06f, 4, p => {
                p.Swatch = TrackRailSwatch.Main;
                p.Specular = paintSpecular;
            });

        // right brace
        part.AppendBracePart.Cylinder(
            new Vector3( railSeparation, -0.26f, 0f),
            new Vector3( railSeparation,  0.00f, 0f),
            0.06f, 4, p => {
                p.Swatch = TrackRailSwatch.Main;
                p.Specular = paintSpecular;
            });

        return part;
    }
}
