using System.Numerics;

namespace RCT3D.Render.Rides.Vehicles;

[RideVehicleMapping("rct2.ride.kart1", "0A188B80|KART1   |56DFF5D7")]
[RideVehicleMapping("rct1.ride.go_karts")]
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
