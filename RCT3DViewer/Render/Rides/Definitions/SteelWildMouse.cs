using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Definitions.Shared;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks.Builders;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class SteelWildMouse : RideDefinition
{
    public SteelWildMouse() : base(RCTRideType.SteelWildMouse)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = SteelWildMouseTrack.CenterlineHeight;

        SupportStyle = RideSupportStyle.MetalTubes;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        return SteelWildMouseTrack.Build(track);
    }
}
