using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions.Shared;

internal static class WoodenCoasterTrack
{
    private const float RailHalfSeparation = 0.72f;
    private const float RailCenterY = -0.015f;
    private const float RailHalfWidth = 0.08f;
    private const float RailHalfHeight = 0.03f;

    private const float RailWoodInset = 0.2f;
    private const float WoodPlatformHalfHeight = 0.06f;
    private const float WoodPlatformCenterY = -WoodPlatformHalfHeight;

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
            part.AppendRailPart.Circle(0f, -0.32f, 0.28f, 6, shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = WoodSpecular;
            });

            var braceWidth = RailHalfSeparation + RailWoodInset;

            // TODO: triangle brace beams
            part.AppendBracePart.Box(
                new Vector3(0f, -0.15f, 0f),
                new Vector3(braceWidth, 0.06f, 0.06f),
                shape => {
                    shape.Swatch = TrackRailSwatch.Main;
                    shape.Specular = WoodSpecular;
                });
        }
        else if (!track.Part.IsStationPlatform()) {
            const float baseBeamHalfSize = 0.12f;
            const float postHalfSize = 0.04f;

            const float baseBeamHalfWidth = 1.5f - 2f*postHalfSize;
            const float baseBeamCenterY = WoodPlatformCenterY - WoodPlatformHalfHeight - baseBeamHalfSize;

            // base beam
            part.AppendBracePart.Box(
                new Vector3(0.0f, baseBeamCenterY, 0.0f),
                new Vector3(baseBeamHalfWidth, baseBeamHalfSize, baseBeamHalfSize),
                shape => {
                    shape.Swatch = TrackRailSwatch.Support;
                    shape.Specular = WoodSpecular;
                });

            const float postCenterX = 1.5f - postHalfSize;

            // left railing post
            part.AppendBracePart.Box(
                new Vector3(-postCenterX, 0f, 0f),
                new Vector3(postHalfSize, 0.32f, postHalfSize),
                shape => {
                    shape.Swatch = TrackRailSwatch.Support;
                    shape.Specular = WoodSpecular;
                });

            // right railing post
            part.AppendBracePart.Box(
                new Vector3( postCenterX, 0f, 0f),
                new Vector3(postHalfSize, 0.32f, postHalfSize),
                shape => {
                    shape.Swatch = TrackRailSwatch.Support;
                    shape.Specular = WoodSpecular;
                });

            const float platformMinX = RailHalfSeparation - RailWoodInset;
            const float platformMaxX = 1.5f - 2f*postHalfSize;

            const float platformCenterX = (platformMinX + platformMaxX) * 0.5f;
            const float platformHalfWidth = (platformMaxX - platformMinX) * 0.5f;
            //const float woodHalfMargin = (1.5f - 2f*postHalfSize - (RailHalfSeparation - RailWoodInset)) * 0.5f;
            //const float woodPlatformCenterX = 1.5f - woodHalfMargin;

            // side platform left
            part.AppendRailPart.Box(-platformCenterX, WoodPlatformCenterY, platformHalfWidth, WoodPlatformHalfHeight, shape => {
                shape.Swatch = TrackRailSwatch.Alternate;
                shape.Specular = WoodSpecular;
            });

            // side platform right
            part.AppendRailPart.Box( platformCenterX, WoodPlatformCenterY, platformHalfWidth, WoodPlatformHalfHeight, shape => {
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
