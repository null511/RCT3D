using System.Numerics;

namespace RCT3D.Render.Rides.Vehicles;

[RideVehicleMapping("rct2.ride.spboat", "0A188B80|SPBOAT  |1057649F")]
[RideVehicleMapping("rct1aa.ride.splash_boats")]
internal class SplashBoats : RideVehicleDefinition
{
    public SplashBoats()
    {
        Cars = [
            new RideVehicleCarDefinition {
                Size = new Vector3(2.2f, 0.25f, 3.2f),
                LocalOffset = 0.15f,
            },
            new RideVehicleCarDefinition {
                HelperPaintStyle = 8,
            },
        ];
    }
}
