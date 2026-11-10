using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Definitions.Shared;
using RCT3D.Render.Rides.Supports;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class CarRide : RideDefinition
{
    public CarRide() : base(RCTRideType.CarRide)
    {
        SupportStyle = RideSupportStyle.MetalBoxed;

        CenterlineHeightOffset = 0.12f;
        SupportTrackOffset = -0.08f;
        //Vehicles.LocalOffset = 0.2f;
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        return CarRideTrack.Build(track);
    }
}
