using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Definitions.Shared;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks.Builders;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class ClassicWoodenTwisterRollerCoaster : RideDefinition
{
    public ClassicWoodenTwisterRollerCoaster() : base(RCTRideType.ClassicWoodenTwisterRollerCoaster)
    {
        SmoothMovement = true;
        TrackBankAngleDegrees = 30f;
        CenterlineHeightOffset = 0.65f;
        //CarVerticalOffset = 0.5f;

        SupportStyle = RideSupportStyle.WoodenTruss;
        //SupportTrackOffset = CenterlineHeightOffset - RailHalfHeight - SupportTopClearance;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        return WoodenCoasterTrack.Build(track);
    }
}
