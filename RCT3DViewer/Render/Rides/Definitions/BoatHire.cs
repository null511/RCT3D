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
        SmoothMovement = true;

        SupportStyle = RideSupportStyle.None;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        const float centerY = TerrainMeshBuilder.WaterPlaneVerticalOffset;
        const float separation = 0.75f;
        const float radius = 0.09f;

        var part = new TrackPart();

        // left
        part.AppendRailPart.Circle(-separation, centerY, radius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = paintSpecular;
        });

        // right
        part.AppendRailPart.Circle( separation, centerY, radius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = paintSpecular;
        });

        return part;
    }
}
