using System.Numerics;

namespace RCT3D.Render.Rides.Vehicles;

[RideVehicleMapping("rct2.ride.obs1", "0A188B80|OBS1    |D59D67B3")]
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
