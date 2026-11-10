using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class MiniSuspendedCoaster : RideDefinition
{
    private static readonly TrackRailSpecularData paintSpecular = new(160, 0);


    public MiniSuspendedCoaster() : base(RCTRideType.MiniSuspendedCoaster)
    {
        //SmoothMovement = true;
        CenterlineHeightOffset = 2.5f;

        SupportStyle = RideSupportStyle.MetalFork;

        StationBuilder = new DefaultTrackStationBuilder();

        //Vehicles.Size = new Vector3(1.0f, 1.4f, 0.8f);
        //Vehicles.LocalOffset = -0.5f;
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        var part = new TrackPart();

        part.AppendRailPart.Circle(0f, 0f, 0.08f, 6, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = paintSpecular;
        });

        return part;
    }
}
