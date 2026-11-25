using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class MineTrainCoaster : RideDefinition
{
    private static readonly TrackRgba8 railColor = new(149, 155, 158, 255);
    private static readonly TrackRailSpecularData railSpecular = new(90, 255);
    private static readonly TrackRailSpecularData woodSpecular = new(250, 0);


    public MineTrainCoaster() : base(RCTRideType.MineTrainCoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.5f;

        // TODO: supports

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        const float railHalfSeparation = 0.30f;
        const float railHalfWidth = 0.03f;
        const float railHalfHeight = 0.05f;

        var part = new TrackPart {
            BraceInterval = 0.36f,
        };

        part.AppendRailPart.Box(-railHalfSeparation, -railHalfHeight, railHalfWidth, railHalfHeight, shape => {
            shape.Color = railColor;
            shape.Specular = railSpecular;
        });

        part.AppendRailPart.Box( railHalfSeparation, -railHalfHeight, railHalfWidth, railHalfHeight, shape => {
            shape.Color = railColor;
            shape.Specular = railSpecular;
        });

        const float braceHalfHeight = 0.015f;
        const float braceCenterY = -2f*railHalfHeight - braceHalfHeight;

        if (!track.Part.IsStationPlatform()) {
            part.AppendBracePart.Box(
                new Vector3(0f, braceCenterY, 0f),
                new Vector3(0.55f, braceHalfHeight, 0.06f),
                shape => {
                    shape.Swatch = TrackRailSwatch.Main;
                    shape.Specular = woodSpecular;
                });
        }

        return part;
    }
}
