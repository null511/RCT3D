using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Definitions.Shared;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks.Builders;

namespace RCT3D.Render.Rides.Definitions;

internal class ClassicMiniRollerCoaster : RideDefinition
{
    public ClassicMiniRollerCoaster() : base(RCTRideType.ClassicMiniRollerCoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.5f;

        //Vehicles.LocalOffset = 0.25f;

        SupportTrackOffset = 0.25f;
        SupportStyle = RideSupportStyle.MetalFork;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        return MiniSteelTrack.Build(track);
    }
}
