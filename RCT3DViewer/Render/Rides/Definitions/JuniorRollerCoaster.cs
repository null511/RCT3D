using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;

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

        StationBuilder = new DefaultStationBuilder();
    }

    protected override IEnumerable<TrackRailProfileShape> GetRailParts(RCTTrackElementData track)
    {
        var railSpecular = new TrackRailSpecularData(80, 20);

        return [
            new TrackRailProfileShape(CreateCircle(-RailSeparation, RailCenterY, RailRadius, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = railSpecular,
            },
            new TrackRailProfileShape(CreateCircle( RailSeparation, RailCenterY, RailRadius, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = railSpecular,
            },
        ];
    }
}
