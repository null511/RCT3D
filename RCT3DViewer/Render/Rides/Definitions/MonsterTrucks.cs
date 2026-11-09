using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Definitions.Shared;
using RCT3D.Render.Rides.Supports;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class MonsterTrucks : RideDefinition
{
    public MonsterTrucks() : base(RCTRideType.MonsterTrucks)
    {
        SupportStyle = RideSupportStyle.MetalBoxed;

        CenterlineHeightOffset = 0.12f;
        SupportTrackOffset = -0.08f;
        CarVerticalOffset = 0.2f;
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        return CarRideTrack.Build(track);
    }
}
