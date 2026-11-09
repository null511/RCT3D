using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class GhostTrain : RideDefinition
{
    private static readonly TrackRailSpecularData platformSpecular = new(100, 0);
    private static readonly TrackRailSpecularData railSpecular = new(180, 0);


    public GhostTrain() : base(RCTRideType.GhostTrain)
    {
        CenterlineHeightOffset = 0.25f;

        SupportStyle = RideSupportStyle.MetalBoxed;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        const float platformHalfHeight = 0.08f;
        const float railSeparation = 0.40f;

        var part = new TrackPart();

        part.AppendRailPart.Box(0f, -platformHalfHeight, 0.55f, platformHalfHeight, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = platformSpecular;
        });

        if (track.Part.HasIncline()) {
            part.RailProfiles.Add(BuildRail(-railSeparation));
            part.RailProfiles.Add(BuildRail( railSeparation));
        }

        return part;
    }

    private static TrackRailProfileShape BuildRail(float centerX)
    {
        return new TrackRailProfileShape([
            new Vector2(centerX + 0.04f,  0.00f),
            new Vector2(centerX + 0.04f,  0.04f),
            new Vector2(centerX        ,  0.06f),
            new Vector2(centerX - 0.04f, -0.04f),
            new Vector2(centerX - 0.04f, -0.00f),
        ], false) {
            Swatch = TrackRailSwatch.Alternate,
            Specular = railSpecular,
        };
    }
}
