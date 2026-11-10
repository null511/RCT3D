using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Vehicles;

namespace RCT3D.Render.Rides;

internal static class RideVehicleDefinitions
{
    private static readonly RideVehicleCarDefinition defaultDefinition = new();
    private static readonly Dictionary<(uint ObjectIdHash, byte VehicleType), RideVehicleCarDefinition> definitions = CreateDefinitions();

    public static RideVehicleCarDefinition Resolve(in RCTVehicleData vehicle) =>
        definitions.GetValueOrDefault((vehicle.ObjectIdHash, vehicle.VehicleType), defaultDefinition);


    private static Dictionary<(uint ObjectIdHash, byte VehicleType), RideVehicleCarDefinition> CreateDefinitions()
    {
        var result = new Dictionary<(uint ObjectIdHash, byte VehicleType), RideVehicleCarDefinition>();

        Add("rct2.ride.spboat", "0A188B80|SPBOAT  |1057649F", new SplashBoats());

        //Add("rct2.ride.ctcar", "0A188A80|CTCAR   |2082E6E1", new() { LocalOffset = 0.2f }, new() { HelperPaintStyle = 1 });
        //Add("rct2.ride.rcr", "0A188B80|RCR     |14407591", new() { LocalOffset = 0.2f }, new() { HelperPaintStyle = 1 });
        //Add("rct2.ride.spcar", "0A188B80|SPCAR   |69F23BC4", new() { LocalOffset = 0.2f }, new() { HelperPaintStyle = 1 });
        //Add("rct2.ride.truck1", "0A188B80|TRUCK1  |58B728C5", new() { LocalOffset = 0.2f }, new() { HelperPaintStyle = 1 });
        //// Legacy descriptors format the checksum with space padding, not leading zeroes.
        //Add("rct2.ride.vcr", "0A188C80|VCR     | 4C462E7", new() { LocalOffset = 0.2f }, new() { HelperPaintStyle = 1 });
        //Add("rct1.ride.racing_cars", null, new() { LocalOffset = 0.2f }, new() { HelperPaintStyle = 1 });
        //Add("rct1.ride.sports_cars", null, new() { LocalOffset = 0.2f }, new() { HelperPaintStyle = 1 });
        //Add("rct1aa.ride.vintage_cars", null, new() { LocalOffset = 0.2f }, new() { HelperPaintStyle = 1 });

        Add("rct1aa.ride.splash_boats", null, new SplashBoats());

        Add("rct2.ride.ding1", "0A188A80|DING1   |7EB3E178", new Dinghies());

        Add("rct2.ride.kart1", "0A188B80|KART1   |56DFF5D7", new GoKarts());
        Add("rct1.ride.go_karts", null, new GoKarts());

        Add("rct2.ride.obs1", "0A188B80|OBS1    |D59D67B3", new SingleDeckCabin());

        Add("rct2.ride.amt1", "0A188A80|AMT1    |53A59CAC", new MineTrains());
        Add("rct1.ride.mine_trains", null, new MineTrains());

        return result;

        void Add(string identifier, string? legacyIdentifier, RideVehicleDefinition train) {
            var objectHash = MaterialLibraryBuilder.HashObjectId(identifier);

            for (var index = 0; index < train.Cars.Length; index++) {
                result.Add((objectHash, checked((byte)index)), train.Cars[index]);

                if (legacyIdentifier is not null)
                    result.Add((MaterialLibraryBuilder.HashObjectId(legacyIdentifier), checked((byte)index)), train.Cars[index]);
            }
        }
    }
}
