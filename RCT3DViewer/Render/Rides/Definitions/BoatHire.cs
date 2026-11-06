using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using RCT3D.Render.Terrain;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class BoatHire : RideDefinition
{
    private static readonly TrackRailSpecularData paintSpecular = new(120, 0);


    public BoatHire() : base(RCTRideType.BoatHire)
    {
        CarVerticalOffset = 0.20f;

        SupportStyle = RideSupportStyle.None;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        const float separation = 0.75f;

        var part = new TrackPart {
            RailProfiles = [
                // left
                new TrackRailProfileShape(CreateCircle(-separation, TerrainMeshBuilder.WaterPlaneVerticalOffset, 0.09f, 5), true) {
                    Swatch = TrackRailSwatch.Main,
                    Specular = paintSpecular,
                },
                // right
                new TrackRailProfileShape(CreateCircle( separation, TerrainMeshBuilder.WaterPlaneVerticalOffset, 0.09f, 5), true) {
                    Swatch = TrackRailSwatch.Main,
                    Specular = paintSpecular,
                },
            ],
        };

        return part;
    }
}
