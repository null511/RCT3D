using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class MiniHelicopters : RideDefinition
{
    private const float RailRadius = 0.075f;
    private const float RailCenterY = 0.0f;

    private static readonly TrackRailSpecularData railSpecular = new(80, 80);


    public MiniHelicopters() : base(RCTRideType.MiniHelicopters)
    {
        CenterlineHeightOffset = 0.5f;

        SupportTrackOffset = RailCenterY - 2f * RailRadius;
        SupportStyle = RideSupportStyle.MetalStick;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        const float railSeparation = 0.42f;

        var part = new TrackPart {
            BraceInterval = 0.8f,
        };

        // left rail
        part.AppendRailPart.Circle(-railSeparation, RailCenterY, RailRadius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = railSpecular;
        });

        // right rail
        part.AppendRailPart.Circle( railSeparation, RailCenterY, RailRadius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = railSpecular;
        });

        const float braceCenterY = RailCenterY - 1.5f * RailRadius;

        part.AppendBracePart.Cylinder(
            new Vector3(-railSeparation, braceCenterY, 0f),
            new Vector3( railSeparation, braceCenterY, 0f),
            0.055f, 5, shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = railSpecular;
            });

        return part;
    }
}
