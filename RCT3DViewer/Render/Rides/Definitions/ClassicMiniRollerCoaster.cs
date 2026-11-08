using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal class ClassicMiniRollerCoaster : RideDefinition
{
    private const float RailHalfSeparation = 0.42f;
    private const float RailRadius = 0.075f;
    private const float RailCenterY = 0.0f;

    private const float BraceRadius = 0.055f;

    private static readonly TrackRailSpecularData railSpecular = new(80, 80);


    public ClassicMiniRollerCoaster() : base(RCTRideType.ClassicMiniRollerCoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.5f;
        CarVerticalOffset = 0.25f;

        SupportTrackOffset = 0.25f;
        SupportStyle = RideSupportStyle.MetalFork;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        var shape = new TrackPart {
            BraceInterval = 0.8f,
            RailProfiles = [
                new TrackRailProfileShape(CreateCircle(-RailHalfSeparation, RailCenterY, RailRadius, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = railSpecular,
                },
                new TrackRailProfileShape(CreateCircle( RailHalfSeparation, RailCenterY, RailRadius, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = railSpecular,
                },
            ],
        };

        var brace = shape.AppendBracePart.Cylinder(
            new Vector3(-RailHalfSeparation, RailCenterY, 0f),
            new Vector3( RailHalfSeparation, RailCenterY, 0f),
            BraceRadius, 5);

        brace.Swatch = TrackRailSwatch.Main;
        brace.Specular = railSpecular;

        return shape;
    }
}
