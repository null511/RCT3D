using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class MineRide : RideDefinition
{
    public MineRide() : base(RCTRideType.MineRide)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.5f;
        //CarVerticalOffset = 0.5f;

        SupportStyle = RideSupportStyle.MetalFork;

        StationBuilder = new DefaultStationBuilder();
    }

    protected override IEnumerable<TrackRailProfileShape> GetRailParts(RCTTrackElementData track)
    {
        var railSpecular = new TrackRailSpecularData(80, 20);

        return [
            new TrackRailProfileShape(CreateCircle(-0.42f, 0f, 0.075f, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = railSpecular,
            },
            new TrackRailProfileShape(CreateCircle( 0.42f, 0f, 0.075f, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = railSpecular,
            },
            .. CreateBox(0f, 0f, 0.08f, 0.07f, shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = railSpecular;
            }),
        ];
    }
}
