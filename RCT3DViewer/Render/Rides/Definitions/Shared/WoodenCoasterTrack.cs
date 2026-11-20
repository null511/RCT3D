using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions.Shared;

internal static class WoodenCoasterTrack
{
    private const float RailHalfSeparation = 0.72f;
    private const float RailCenterY = -0.2f;
    private const float RailHalfWidth = 0.08f;
    private const float RailHalfHeight = 0.03f;

    private const float WoodPlatformCenterY = -0.26f;
    private const float WoodPlatformHalfHeight = 0.06f;
    private const float RailWoodInset = 0.2f;

    private static readonly TrackRgba8 RailColor = new(149, 155, 158, 255);
    private static readonly TrackRailSpecularData RailSpecular = new(80, 255);

    private static readonly TrackRailSpecularData WoodSpecular = new(220, 0);


    public static TrackPart Build(RCTTrackElementData track)
    {
        var part = new TrackPart {
            BraceInterval = 0.6f,
        };

        // left rail
        part.AppendRailPart.Box(-RailHalfSeparation, RailCenterY, RailHalfWidth, RailHalfHeight, shape => {
            shape.Color = RailColor;
            shape.Specular = RailSpecular;
        });

        // right rail
        part.AppendRailPart.Box( RailHalfSeparation, RailCenterY, RailHalfWidth, RailHalfHeight, shape => {
            shape.Color = RailColor;
            shape.Specular = RailSpecular;
        });

        if (track.Part.IsLoop()) {
            // side platform left
            part.AppendRailPart.Box(-RailHalfSeparation, WoodPlatformCenterY, RailWoodInset, WoodPlatformHalfHeight, shape => {
                shape.Swatch = TrackRailSwatch.Alternate;
                shape.Specular = WoodSpecular;
            });

            // side platform right
            part.AppendRailPart.Box( RailHalfSeparation, WoodPlatformCenterY, RailWoodInset, WoodPlatformHalfHeight, shape => {
                shape.Swatch = TrackRailSwatch.Alternate;
                shape.Specular = WoodSpecular;
            });

            // main tube
            // TODO: double vertically
            part.AppendRailPart.Circle(0f, -0.52f, 0.28f, 6, shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = WoodSpecular;
            });

            var braceWidth = RailHalfSeparation + RailWoodInset;

            // TODO: triangle brace beams
            part.AppendBracePart.Box(
                new Vector3(0f, -0.35f, 0f),
                new Vector3(braceWidth, 0.06f, 0.06f),
                shape => {
                    shape.Swatch = TrackRailSwatch.Main;
                    shape.Specular = WoodSpecular;
                });
        }
        else if (!track.Part.IsStationPlatform()) {
            // base beam
            part.AppendBracePart.Box(
                new Vector3(0.0f, -0.4f, 0.0f),
                new Vector3(1.45f, 0.12f, 0.12f),
                shape => {
                    shape.Swatch = TrackRailSwatch.Support;
                    shape.Specular = WoodSpecular;
                });

            // left beam
            part.AppendBracePart.Box(
                new Vector3(-1.47f, 0f, 0.0f),
                new Vector3(0.04f, 0.32f, 0.04f),
                shape => {
                    shape.Swatch = TrackRailSwatch.Support;
                    shape.Specular = WoodSpecular;
                });

            // right beam
            part.AppendBracePart.Box(
                new Vector3( 1.47f, 0f, 0.0f),
                new Vector3(0.04f, 0.32f, 0.04f),
                shape => {
                    shape.Swatch = TrackRailSwatch.Support;
                    shape.Specular = WoodSpecular;
                });

            const float woodHalfMargin = (1.5f - (RailHalfSeparation - RailWoodInset)) * 0.5f;
            const float woodPlatformCenterX = 1.5f - woodHalfMargin;

            // side platform left
            part.AppendRailPart.Box(-woodPlatformCenterX, WoodPlatformCenterY, woodHalfMargin, WoodPlatformHalfHeight, shape => {
                shape.Swatch = TrackRailSwatch.Alternate;
                shape.Specular = WoodSpecular;
            });

            // side platform right
            part.AppendRailPart.Box( woodPlatformCenterX, WoodPlatformCenterY, woodHalfMargin, WoodPlatformHalfHeight, shape => {
                shape.Swatch = TrackRailSwatch.Alternate;
                shape.Specular = WoodSpecular;
            });

            const float WoodRailingWidth = 0.04f;

            var woodRailingCenterX = 1.5f - 0.5f*WoodRailingWidth;
            var woodRailingCenterY = 0.36f;

            // side railing left
            part.AppendRailPart.Box(-woodRailingCenterX, woodRailingCenterY, WoodRailingWidth, WoodRailingWidth, shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = WoodSpecular;
            });

            // side railing right
            part.AppendRailPart.Box( woodRailingCenterX, woodRailingCenterY, WoodRailingWidth, WoodRailingWidth, shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = WoodSpecular;
            });

            if (track.HasChainLift != 0) {
                part.AppendRailPart.Box(0f, RailCenterY, 0.16f, 0.05f, shape => {
                    shape.Color = new TrackRgba8(30, 30, 30, 255);
                    shape.Specular = new TrackRailSpecularData(160, 255);
                });
            }
        }

        return part;
    }
}
