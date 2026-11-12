using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Definitions.Shared;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks.Builders;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class TwisterRollerCoaster : RideDefinition
{
    public TwisterRollerCoaster() : base(RCTRideType.TwisterRollerCoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.75f;
        //Vehicles.LocalOffset = 0.5f;

        SupportStyle = RideSupportStyle.MetalTubes;
        SupportTrackOffset = -0.5f;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        return BoxSpineTrack.Build(track);
    }
}
