using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class InvertedImpulseCoaster : RideDefinition
{
    private static readonly TrackRailSpecularData paintSpecular = new(100, 0);


    public InvertedImpulseCoaster() : base(RCTRideType.InvertedImpulseCoaster)
    {
        CenterlineHeightOffset = 3.0f;
        Vehicles.LocalOffset = -0.5f;

        SupportStyle = RideSupportStyle.MetalTubesInverted;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        const float halfSize = 0.15f;
        const float railRadius = 0.06f;
        const float braceRadius = 0.03f;

        var part = new TrackPart {
            BraceInterval = 0.5f,
        };

        // top-left
        part.AppendRailPart.Circle(-halfSize, halfSize, railRadius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = paintSpecular;
        });

        // top-right
        part.AppendRailPart.Circle( halfSize, halfSize, railRadius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = paintSpecular;
        });

        // bottom-left
        part.AppendRailPart.Circle(-halfSize, -halfSize, railRadius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = paintSpecular;
        });

        // bottom-right
        part.AppendRailPart.Circle( halfSize, -halfSize, railRadius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = paintSpecular;
        });

        // top
        part.AppendBracePart.Cylinder(
            new Vector3(-halfSize, halfSize, 0f),
            new Vector3( halfSize, halfSize, 0f),
            braceRadius, 4, shape => {
                shape.Swatch = TrackRailSwatch.Alternate;
                shape.Specular = paintSpecular;
            });

        // bottom
        part.AppendBracePart.Cylinder(
            new Vector3(-halfSize, -halfSize, 0f),
            new Vector3( halfSize, -halfSize, 0f),
            braceRadius, 4, shape => {
                shape.Swatch = TrackRailSwatch.Alternate;
                shape.Specular = paintSpecular;
            });

        // left
        part.AppendBracePart.Cylinder(
            new Vector3(-halfSize, -halfSize, 0f),
            new Vector3(-halfSize,  halfSize, 0f),
            braceRadius, 4, shape => {
                shape.Swatch = TrackRailSwatch.Alternate;
                shape.Specular = paintSpecular;
            });

        // right
        part.AppendBracePart.Cylinder(
            new Vector3( halfSize, -halfSize, 0f),
            new Vector3( halfSize,  halfSize, 0f),
            braceRadius, 4, shape => {
                shape.Swatch = TrackRailSwatch.Alternate;
                shape.Specular = paintSpecular;
            });

        return part;
    }
}
