using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Definitions.Shared;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks.Builders;

namespace RCT3D.Render.Rides.Definitions;

internal class CorkscrewRollerCoaster : RideDefinition
{
    public CorkscrewRollerCoaster() : base(RCTRideType.CorkscrewRollerCoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.5f;
        CenterlineVerticalOffset = -1.0f;

        SupportStyle = RideSupportStyle.MetalTubes;
        SupportTrackOffset = -0.24f;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        return ArrowTubularTrack.Build(track);
    }
}
