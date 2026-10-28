using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Rides.Definitions;

internal class LoopingRollerCoaster : RideDefinition
{
    public LoopingRollerCoaster() : base(RCTRideType.LoopingRollerCoaster)
    {
        StationBuilder = new DefaultStationBuilder();
        SmoothMovement = true;
        CarVerticalOffset = 0.85f;

        SupportStyle = RideSupportStyle.MetalTubes;
        SupportTrackOffset = 0.20f;
    }

    protected override IEnumerable<TrackRailProfileShape> GetRailParts(RCTTrackElementData track)
    {
        return [
            new TrackRailProfileShape(CreateCircle(-0.42f, 0.28f, 0.06f, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(100, 0),
            },
            new TrackRailProfileShape(CreateCircle(0.00f, 0.20f, 0.18f, 6), true) {
                Swatch = TrackRailSwatch.Main,
                Specular = new TrackRailSpecularData(100, 0),
            },
            new TrackRailProfileShape(CreateCircle(0.42f, 0.28f, 0.06f, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(100, 0),
            },
        ];
    }
}
