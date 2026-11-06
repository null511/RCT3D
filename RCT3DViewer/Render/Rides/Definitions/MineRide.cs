using System.Numerics;
using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class MineRide : RideDefinition
{
    public MineRide() : base(RCTRideType.MineRide)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.5f;
        //CarVerticalOffset = 0.5f;

        SupportStyle = RideSupportStyle.MetalFork;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        var railSpecular = new TrackRailSpecularData(80, 20);

        var shape = new TrackPart {
            BraceInterval = 0.6f,
            RailProfiles = [
                new TrackRailProfileShape(CreateCircle(-0.42f, 0f, 0.075f, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = railSpecular,
                },
                new TrackRailProfileShape(CreateCircle( 0.42f, 0f, 0.075f, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = railSpecular,
                },
            ],
        };

        shape.RailProfiles.AddRange(CreateBox(0f, 0f, 0.06f, 0.05f, p => {
            p.Swatch = TrackRailSwatch.Main;
            p.Specular = railSpecular;
        }));

        shape.AppendBracePart.Cylinder(
            new Vector3(-0.42f, 0f, 0f),
            new Vector3( 0.42f, 0f, 0f),
            0.05f, 4, p => {
                p.Swatch = TrackRailSwatch.Main;
                p.Specular = railSpecular;
            });

        return shape;
    }
}
