using System.Numerics;
using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class GhostTrain : RideDefinition
{
    private const float PlatformHalfWidth = 0.55f;
    private const float PlatformHalfHeight = 0.08f;

    private const float RailSeparation = 0.40f;

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
        var part = new TrackPart {
            RailProfiles = [
                .. CreateBox(0f, -PlatformHalfHeight, PlatformHalfWidth, PlatformHalfHeight, p => {
                    p.Swatch = TrackRailSwatch.Main;
                    p.Specular = platformSpecular;
                }),
            ],
        };

        if (track.Part.HasIncline()) {
            part.RailProfiles.Add(BuildRail(-RailSeparation));
            part.RailProfiles.Add(BuildRail( RailSeparation));
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
