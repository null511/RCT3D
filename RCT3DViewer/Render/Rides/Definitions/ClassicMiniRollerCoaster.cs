using RCT3D.Interop;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Rides.Definitions;

internal class ClassicMiniRollerCoaster : RideDefinition
{
    public ClassicMiniRollerCoaster() : base(RCTRideType.ClassicMiniRollerCoaster)
    {
        SmoothMovement = true;
        CarVerticalOffset = 0.5f;

        SupportStyle = RideSupportStyle.MetalFork;
        SupportTrackOffset = 0.25f;
    }

    protected override TrackRailProfile CreateProfile()
    {
        return new TrackRailProfile([
            new TrackRailProfileShape(CreateCircle(-0.42f, 0.28f, 0.075f, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(80, 80),
            },
            new TrackRailProfileShape(CreateCircle(0.42f, 0.28f, 0.075f, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(80, 80),
            },
        ]);
    }
}
