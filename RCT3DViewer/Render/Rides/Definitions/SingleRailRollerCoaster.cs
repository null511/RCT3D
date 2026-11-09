using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class SingleRailRollerCoaster : RideDefinition
{
    private static readonly TrackRailSpecularData paintSpecular = new(80, 20);


    public SingleRailRollerCoaster() : base(RCTRideType.SingleRailRollerCoaster)
    {
        //SmoothMovement = true;
        CenterlineHeightOffset = 0.3f;
        //CarVerticalOffset = 1.4f;

        SupportStyle = RideSupportStyle.MetalTubes;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        const float RailCenterY = 0f;
        const float RailHalfWidth = 0.08f;
        const float RailHalfHeight = 0.08f;

        var part = new TrackPart();

        part.AppendRailPart.Box(0f, RailCenterY, RailHalfWidth, RailHalfHeight, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = paintSpecular;
        });

        return part;
    }
}
