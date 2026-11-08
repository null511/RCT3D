using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class MiniHelicopters : RideDefinition
{
    private const float RailSeparation = 0.42f;
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
        var shape = new TrackPart {
            BraceInterval = 0.8f,
            RailProfiles = [
                new TrackRailProfileShape(CreateCircle(-RailSeparation, RailCenterY, RailRadius, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = railSpecular,
                },
                new TrackRailProfileShape(CreateCircle( RailSeparation, RailCenterY, RailRadius, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = railSpecular,
                },
            ],
        };

        const float braceCenterY = RailCenterY - 1.5f * RailRadius;

        shape.AppendBracePart.Cylinder(
            new Vector3(-RailSeparation, braceCenterY, 0f),
            new Vector3( RailSeparation, braceCenterY, 0f),
            0.055f, 5, p => {
                p.Swatch = TrackRailSwatch.Main;
                p.Specular = railSpecular;
            });

        return shape;
    }
}
