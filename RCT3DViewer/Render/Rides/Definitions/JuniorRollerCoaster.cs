using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class JuniorRollerCoaster : RideDefinition
{
    private const float RailCenterY = 0f;
    private const float RailRadius = 0.075f;
    private const float RailSeparation = 0.42f;

    public JuniorRollerCoaster() : base(RCTRideType.JuniorRollerCoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.5f;

        SupportStyle = RideSupportStyle.MetalFork;
        SupportTrackOffset = RailCenterY - RailRadius;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        var railSpecular = new TrackRailSpecularData(80, 20);

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

        var brace = shape.AppendBracePart.Cylinder(
            new Vector3(-RailSeparation, RailCenterY, 0f),
            new Vector3( RailSeparation, RailCenterY, 0f),
            0.055f, 5);

        brace.Swatch = TrackRailSwatch.Main;
        brace.Specular = railSpecular;

        return shape;
    }
}
