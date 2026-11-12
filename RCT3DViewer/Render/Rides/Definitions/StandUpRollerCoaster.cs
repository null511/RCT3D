using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Definitions.Shared;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks.Builders;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class StandUpRollerCoaster : RideDefinition
{
    public StandUpRollerCoaster() : base(RCTRideType.StandUpRollerCoaster)
    {
        SupportStyle = RideSupportStyle.MetalTubes;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        return SchwarzkopfTrack.Build(track);
    }
}
