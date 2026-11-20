using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class Chairlift : RideDefinition
{
    private static readonly TrackRgba8 RailColor = new(230, 230, 230, 255);
    private static readonly TrackRailSpecularData RailSpecular = new(80, 255);


    public Chairlift() : base(RCTRideType.Chairlift)
    {
        CenterlineHeightOffset = 2.8f;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        const float RailSeparation = 1.0f;
        const float RailCenterY = 0f;

        var part = new TrackPart();

        if (track.Part != RCTTrackElementType.EndStation) {
            part.AppendRailPart.Circle(-RailSeparation, RailCenterY, 0.02f, 4, shape => {
                shape.Color = RailColor;
                shape.Specular = RailSpecular;
            });

            part.AppendRailPart.Circle( RailSeparation, RailCenterY, 0.02f, 4, shape => {
                shape.Color = RailColor;
                shape.Specular = RailSpecular;
            });
        }

        return part;
    }
}
