using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Rides.Definitions;

internal class ClassicMiniRollerCoaster : RideDefinition
{
    public ClassicMiniRollerCoaster() : base(RCTRideType.ClassicMiniRollerCoaster)
    {
        StationBuilder = new DefaultStationBuilder();
        SmoothMovement = true;
        CarVerticalOffset = 0.85f;

        SupportStyle = RideSupportStyle.MetalFork;
        SupportTrackOffset = 0.25f;
    }

    protected override IEnumerable<TrackRailProfileShape> GetRailParts(RCTTrackElementData track)
    {
        return [
            new TrackRailProfileShape(CreateCircle(-0.42f, 0.28f, 0.075f, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(80, 80),
            },
            new TrackRailProfileShape(CreateCircle(0.42f, 0.28f, 0.075f, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(80, 80),
            },
        ];
    }
}
