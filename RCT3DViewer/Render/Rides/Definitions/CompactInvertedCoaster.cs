using RCT3D.Interop;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class CompactInvertedCoaster : RideDefinition
{
    public CompactInvertedCoaster() : base(RCTRideType.CompactInvertedCoaster)
    {
        SupportStyle = RideSupportStyle.MetalTubesInverted;
        CenterlineHeightOffset = 3.0f;
    }

    protected override TrackRailProfile CreateProfile()
    {
        return new TrackRailProfile([
            new TrackRailProfileShape(CreateCircle(0.06f, -0.42f, 1.12f, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(100, 0),
            },
            new TrackRailProfileShape(CreateCircle(0.18f,  0.00f, 1.20f, 6), true) {
                Swatch = TrackRailSwatch.Main,
                Specular = new TrackRailSpecularData(100, 0),
            },
            new TrackRailProfileShape(CreateCircle(0.06f,  0.42f, 1.12f, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(100, 0),
            },
        ]);
    }
}
