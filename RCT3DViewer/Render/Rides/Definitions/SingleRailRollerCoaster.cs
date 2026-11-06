using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class SingleRailRollerCoaster : RideDefinition
{
    public SingleRailRollerCoaster() : base(RCTRideType.SingleRailRollerCoaster)
    {
        //SmoothMovement = true;
        CenterlineHeightOffset = 0.3f;

        SupportStyle = RideSupportStyle.MetalTubes;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        const float RailCenterY = 0.0f;
        const float RailHalfWidth = 0.08f;
        const float RailHalfHeight = 0.08f;

        var shape = new TrackPart();

        shape.RailProfiles.AddRange(CreateBox(0f, RailCenterY, RailHalfWidth, RailHalfHeight, p => {
            p.Swatch = TrackRailSwatch.Main;
            p.Specular = new TrackRailSpecularData(80, 20);
        }));

        return shape;
    }
}
