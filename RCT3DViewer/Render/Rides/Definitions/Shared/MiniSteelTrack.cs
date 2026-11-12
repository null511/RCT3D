using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions.Shared;

internal class MiniSteelTrack
{
    public const float CenterlineHeight = 0.5f;

    private const float RailHalfSeparation = 0.42f;
    private const float RailRadius = 0.075f;
    private const float RailCenterY = 0.0f;

    private const float BraceRadius = 0.055f;

    private static readonly TrackRailSpecularData railSpecular = new(80, 80);


    public static TrackPart Build(RCTTrackElementData track)
    {
        var part = new TrackPart {
            BraceInterval = 0.8f,
        };

        // left
        part.AppendRailPart.Circle(-RailHalfSeparation, RailCenterY, RailRadius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = railSpecular;
        });

        // right
        part.AppendRailPart.Circle( RailHalfSeparation, RailCenterY, RailRadius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = railSpecular;
        });

        part.AppendBracePart.Cylinder(
            new Vector3(-RailHalfSeparation, RailCenterY, 0f),
            new Vector3( RailHalfSeparation, RailCenterY, 0f),
            BraceRadius, 5, shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = railSpecular;
            });

        return part;
    }
}
