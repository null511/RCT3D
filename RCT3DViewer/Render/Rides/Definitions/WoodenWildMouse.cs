using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Rides.Definitions;

internal class WoodenWildMouse : RideDefinition
{
    public WoodenWildMouse() : base(RCTRideType.WoodenWildMouse)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.5f;

        SupportStyle = RideSupportStyle.WoodenTruss;

        StationBuilder = new DefaultStationBuilder();
    }

    protected override IEnumerable<TrackRailProfileShape> GetRailParts(RCTTrackElementData track)
    {
        return [
            .. CreateBox(0f, 0f, 0.22f, 0.06f, shape => {
                shape.Color = new TrackRgba8(200, 200, 200, 255);
                shape.Specular = new TrackRailSpecularData(180, 255);
            }),
        ];
    }
}
