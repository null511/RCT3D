using System.Numerics;

namespace RCT3D.Render.Rides.Vehicles;

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
