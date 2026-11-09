using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Definitions.Shared;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks.Builders;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class HyperTwisterRollerCoaster : RideDefinition
{
    public HyperTwisterRollerCoaster() : base(RCTRideType.HyperTwister)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.75f;
        CarVerticalOffset = 0.5f;

        SupportStyle = RideSupportStyle.MetalTubes;
        SupportTrackOffset = -0.5f;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        return LargeSteelTrack.Build(track);
    }
}
