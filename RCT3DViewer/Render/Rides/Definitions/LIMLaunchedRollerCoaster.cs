using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Definitions.Shared;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class LIMLaunchedRollerCoaster : RideDefinition
{
    public LIMLaunchedRollerCoaster() : base(RCTRideType.LIMLaunchedRollerCoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.5f;

        SupportStyle = RideSupportStyle.MetalTubes;

        StationBuilder = new DefaultTrackStationBuilder();

        Vehicles.LocalOffset = 0.7f;
        Vehicles.Size = new Vector3(1.0f, 0.6f, 1.6f);
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        return StandardSteelTrack.Build(track);
    }
}
