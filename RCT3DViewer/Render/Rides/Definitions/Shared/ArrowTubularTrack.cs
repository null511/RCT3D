using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions.Shared;

internal class ArrowTubularTrack
{
    private static readonly TrackRailSpecularData paintSpecular = new(100, 0);


    public static TrackPart Build(RCTTrackElementData track)
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

        if (track.HasChainLift != 0) {
            // lift
            part.AppendRailPart.Box(0f, -0.04f, 0.08f, 0.04f, shape => {
                shape.Color = new TrackRgba8(30, 30, 30, 255);
                shape.Specular = new TrackRailSpecularData(80, 255);
            });
        }

        return part;
    }
}
