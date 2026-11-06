using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class ObservationTower : RideDefinition
{
    public ObservationTower() : base(RCTRideType.ObservationTower)
    {
        PlatformSurface = RidePlatformSurfaceType.Metal;
        PlatformRailing = RidePlatformRailingType.Fenced;
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
        => new();
}