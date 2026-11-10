using System.Numerics;

namespace RCT3D.Render.Rides.Vehicles;

internal class GoKarts : RideVehicleDefinition
{
    public GoKarts()
    {
        Cars = [
            new RideVehicleCarDefinition {
                Size = new Vector3(0.6f, 0.3f, 0.8f),
                LocalOffset = 0.20f,
            },
        ];
    }
}
