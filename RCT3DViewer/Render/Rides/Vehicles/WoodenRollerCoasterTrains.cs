using System.Numerics;

namespace RCT3D.Render.Rides.Vehicles;

[RideVehicleMapping("rct2.ride.ptct1", "0A188B80|PTCT1   |A6A5C09F")]
[RideVehicleMapping("rct1.ride.wooden_rc_trains")]
internal class WoodenRollerCoasterTrains : RideVehicleDefinition
{
    public WoodenRollerCoasterTrains()
    {
        Cars = [
            new RideVehicleCarDefinition {
                Size = new Vector3(1.40f, 0.90f, 1.90f),
                LocalOffset = 0.50f,
            },
        ];
    }
}
