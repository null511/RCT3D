using System.Numerics;

namespace RCT3D.Render.Rides.Vehicles;

internal class MineTrains : RideVehicleDefinition
{
    public MineTrains()
    {
        Cars = [
            new RideVehicleCarDefinition {
                Size = new Vector3(0.8f, 0.65f, 1.6f),
                LocalOffset = 0.40f,
            },
            new RideVehicleCarDefinition {
                Size = new Vector3(0.8f, 0.65f, 1.6f),
                LocalOffset = 0.40f,
            },
        ];
    }
}
