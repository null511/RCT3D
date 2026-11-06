using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class ClassicWoodenRollerCoaster : RideDefinition
{
    private const float SupportTopClearance = 0.02f;

    private const float RailHalfSeparation = 0.72f;
    private const float RailHalfWidth = 0.08f;
    private const float RailHalfHeight = 0.03f;
    private const float RailWoodInset = 0.2f;

    private static readonly TrackRailSpecularData WoodSpecular = new(220, 0);


    public ClassicWoodenRollerCoaster() : base(RCTRideType.ClassicWoodenRollerCoaster)
    {
        SmoothMovement = true;
        TrackBankAngleDegrees = 30f;
        CenterlineHeightOffset = 0.65f;
        //CarVerticalOffset = 0.5f;

        SupportStyle = RideSupportStyle.WoodenTruss;
        SupportTrackOffset = CenterlineHeightOffset - RailHalfHeight - SupportTopClearance;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        var shape = new TrackPart {
            BraceInterval = 0.6f,
        };

        shape.RailProfiles.AddRange(CreateRailProfile(-RailHalfSeparation, -0.2f));
        shape.RailProfiles.AddRange(CreateRailProfile( RailHalfSeparation, -0.2f));

        if (!track.Part.IsStationPlatform() && !track.Part.IsLoop()) {
            // base beam
            shape.AppendBracePart.Box(
                new Vector3(0.0f, -0.4f, 0.0f),
                new Vector3(1.45f, 0.12f, 0.12f),
                part => {
                    part.Swatch = TrackRailSwatch.Main;
                    part.Specular = WoodSpecular;
                });

            // left beam
            shape.AppendBracePart.Box(
                new Vector3(-1.47f, 0f, 0.0f),
                new Vector3(0.04f, 0.32f, 0.04f),
                part => {
                    part.Swatch = TrackRailSwatch.Main;
                    part.Specular = WoodSpecular;
                });

            // right beam
            shape.AppendBracePart.Box(
                new Vector3( 1.47f, 0f, 0.0f),
                new Vector3(0.04f, 0.32f, 0.04f),
                part => {
                    part.Swatch = TrackRailSwatch.Main;
                    part.Specular = WoodSpecular;
                });

            const float woodHalfMargin = (1.5f - (RailHalfSeparation - RailWoodInset)) * 0.5f;
            const float woodHeight = 0.06f;

            const float woodPlatformCenterX = 1.5f - woodHalfMargin;
            const float woodPlatformCenterY = -0.26f;

            // side platforms
            shape.RailProfiles.AddRange(CreateWoodProfile(-woodPlatformCenterX, woodPlatformCenterY, woodHalfMargin, woodHeight, TrackRailSwatch.Alternate));
            shape.RailProfiles.AddRange(CreateWoodProfile( woodPlatformCenterX, woodPlatformCenterY, woodHalfMargin, woodHeight, TrackRailSwatch.Alternate));

            const float WoodRailingWidth = 0.04f;

            var woodRailingCenterX = 1.5f - 0.5f*WoodRailingWidth;
            var woodRailingCenterY = 0.36f;

            // side railing
            shape.RailProfiles.AddRange(CreateWoodProfile(-woodRailingCenterX, woodRailingCenterY, WoodRailingWidth, WoodRailingWidth, TrackRailSwatch.Main));
            shape.RailProfiles.AddRange(CreateWoodProfile(woodRailingCenterX, woodRailingCenterY, WoodRailingWidth, WoodRailingWidth, TrackRailSwatch.Main));
        }

        return shape;
    }

    private static IEnumerable<TrackRailProfileShape> CreateRailProfile(float centerX, float centerY)
    {
        return CreateBox(centerX, centerY, RailHalfWidth, RailHalfHeight, shape => {
            shape.Color = new TrackRgba8(149, 155, 158, 255);
            shape.Specular = new TrackRailSpecularData(80, 255);
        });
    }

    private static IEnumerable<TrackRailProfileShape> CreateWoodProfile(float centerX, float centerY, float halfWidth, float halfHeight, TrackRailSwatch swatch)
    {
        return CreateBox(centerX, centerY, halfWidth, halfHeight, shape => {
            shape.Swatch = swatch;
            shape.Specular = WoodSpecular;
        });
    }
}
