using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class AirPoweredVerticalCoaster : RideDefinition
{
    private static readonly TrackRailSpecularData paintSpecular = new(120, 0);


    public AirPoweredVerticalCoaster() : base(RCTRideType.AirPoweredVerticalCoaster)
    {
        CenterlineHeightOffset = 0.25f;

        SupportStyle = RideSupportStyle.WoodenTruss;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        const float railSeparation = 0.61f;
        const float railCenterY = 0.0f;

        var part = new TrackPart {
            BraceInterval = 0.8f,
        };

        // left rail
        part.AppendRailPart.Box(-railSeparation, railCenterY, 0.10f, 0.15f, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = paintSpecular;
        });

        // right rail
        part.AppendRailPart.Box( railSeparation, railCenterY, 0.10f, 0.15f, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = paintSpecular;
        });

        // center brace
        part.AppendBracePart.Box(
            new Vector3(0f, railCenterY, 0f),
            new Vector3(railSeparation, 0.05f, 0.05f),
            shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = paintSpecular;
            });

        return part;
    }
}
