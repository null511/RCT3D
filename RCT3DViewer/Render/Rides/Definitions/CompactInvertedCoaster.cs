using System.Numerics;
using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class CompactInvertedCoaster : RideDefinition
{
    private static readonly TrackRailSpecularData paintSpecular = new(100, 0);


    public CompactInvertedCoaster() : base(RCTRideType.CompactInvertedCoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 3.0f;
        CarVerticalOffset = -0.5f;

        SupportStyle = RideSupportStyle.MetalTubesInverted;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        const float railSeparation = 0.42f;
        const float railCenterY = 0.12f;
        const float railRadius = 0.05f;

        var part = new TrackPart {
            BraceInterval = 0.6f,
        };

        // left
        part.AppendRailPart.Circle(-railSeparation, railCenterY, railRadius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = paintSpecular;
        });

        // right
        part.AppendRailPart.Circle( railSeparation, railCenterY, railRadius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = paintSpecular;
        });

        // center
        part.AppendRailPart.Circle(0f, railCenterY + 0.32f, 0.18f, 6, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = paintSpecular;
        });

        var braceWidth = railSeparation + 0.08f;

        // left
        part.AppendBracePart.Cylinder(
            new Vector3(-0.04f, railCenterY + 0.24f, 0f),
            new Vector3(-braceWidth, railCenterY + 0.16f, 0f),
            0.06f, 4, shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = paintSpecular;
            });

        part.AppendBracePart.Cylinder(
            new Vector3(-braceWidth, railCenterY + 0.16f, 0f),
            new Vector3(-braceWidth, railCenterY, 0f),
            0.06f, 4, shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = paintSpecular;
            });

        // right
        part.AppendBracePart.Cylinder(
            new Vector3( 0.04f, railCenterY + 0.24f, 0f),
            new Vector3( braceWidth, railCenterY + 0.16f, 0f),
            0.06f, 4, shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = paintSpecular;
            });

        part.AppendBracePart.Cylinder(
            new Vector3( braceWidth, railCenterY + 0.16f, 0f),
            new Vector3( braceWidth, railCenterY, 0f),
            0.06f, 4, shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = paintSpecular;
            });

        return part;
    }
}
