using System.Numerics;

namespace RCT3D.Render.Rides.Vehicles;

internal class SingleDeckCabin : RideVehicleDefinition
{
    public SingleDeckCabin()
    {
        Cars = [
            new RideVehicleCarDefinition {
                Size = new Vector3(3f, 2f, 3f),
            },
        ];
    }
}
