using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions.Shared;

internal class SteelWildMouseTrack
{
    public const float CenterlineHeight = 0.5f;

    private const float RailHalfSeparation = 0.35f;
    private const float RailRadius = 0.1f;
    private const float RailCenterY = 0.0f;

    private const float BraceRadius = 0.055f;

    private static readonly TrackRailSpecularData railSpecular = new(120, 40);


    public static TrackPart Build(RCTTrackElementData track)
    {
        var part = new TrackPart {
            BraceInterval = 0.6f,
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

        part.AppendBracePart.Box(
            new Vector3(0f, RailCenterY - RailRadius, 0f),
            new Vector3( RailHalfSeparation, BraceRadius, BraceRadius),
            shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = railSpecular;
            });

        return part;
    }
}
