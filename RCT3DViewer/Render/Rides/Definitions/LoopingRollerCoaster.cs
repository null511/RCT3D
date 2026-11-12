using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Definitions.Shared;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks.Builders;

namespace RCT3D.Render.Rides.Definitions;

internal class LoopingRollerCoaster : RideDefinition
{
    public LoopingRollerCoaster() : base(RCTRideType.LoopingRollerCoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.5f;

        SupportStyle = RideSupportStyle.MetalTubes;
        SupportTrackOffset = -0.07f;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        return SchwarzkopfTrack.Build(track);
    }
}
