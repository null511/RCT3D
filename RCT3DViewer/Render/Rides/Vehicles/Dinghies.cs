using System.Numerics;

namespace RCT3D.Render.Rides.Vehicles;

internal class Dinghies : RideVehicleDefinition
{
    public Dinghies()
    {
        Cars = [
            new RideVehicleCarDefinition {
                Size = new Vector3(0.86f, 0.42f, 1.8f),
                WorldOffset = 1.0f,
                LocalOffset = -0.9f,
            },
        ];
    }
}
