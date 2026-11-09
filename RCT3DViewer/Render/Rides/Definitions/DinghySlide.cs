using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class DinghySlide : RideDefinition
{
    private const float TubeRadius = 1.2f;
    private const float TubeThickness = 0.06f;

    private static readonly TrackRailSpecularData paintSpecular = new(160, 0);


    public DinghySlide() : base(RCTRideType.DinghySlide)
    {
        CenterlineHeightOffset = 1.25f;
        CarVerticalOffset = -1.0f;

        SupportStyle = RideSupportStyle.MetalTubes;
        SupportTrackOffset = -1.2f;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        var part = new TrackPart();

        if (track.HasChainLift != 0) {
            // belt
            part.AppendRailPart.Box(0f, -1.1f, 0.6f, 0.1f, shape => {
                shape.Color = new TrackRgba8(30, 30, 30, 255);
                shape.Specular = new TrackRailSpecularData(80, 0);
            });

            // TODO: left and right edges
        }
        else if (track.Part.IsCovered()) {
            FullTube(part);
        }
        else {
            HalfTube(part);
        }

        return part;
    }

    private static void FullTube(TrackPart part)
    {
        const int faceCount = 12;

        part.RailProfiles.Add(new TrackRailProfileShape(Circle(0f, 0f, TubeRadius, faceCount), true) {
            Swatch = TrackRailSwatch.Main,
            Specular = paintSpecular,
        });

        part.RailProfiles.Add(new TrackRailProfileShape(Circle(0f, 0f, TubeRadius - TubeThickness, faceCount).Reverse().ToArray(), true) {
            Swatch = TrackRailSwatch.Alternate,
            Specular = paintSpecular,
        });
    }

    private static void HalfTube(TrackPart part)
    {
        part.RailProfiles.Add(new TrackRailProfileShape(HalfCircle(0f, 0f, TubeRadius, 6), false) {
            Swatch = TrackRailSwatch.Main,
            Specular = paintSpecular,
        });

        part.RailProfiles.Add(new TrackRailProfileShape(HalfCircle(0f, 0f, TubeRadius - TubeThickness, 6).Reverse().ToArray(), false) {
            Swatch = TrackRailSwatch.Alternate,
            Specular = paintSpecular,
        });
    }

    private static Vector2[] Circle(float centerX, float centerY, float radius, int vertexCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(vertexCount, 3);

        var vertices = new Vector2[vertexCount];
        for (var i = 0; i < vertexCount; i++) {
            var angle = MathF.PI * 0.5f + 2f * MathF.PI * (i / (float)vertexCount);
            vertices[i] = new Vector2(centerX + radius * MathF.Cos(angle), centerY + radius * MathF.Sin(angle));
        }

        return vertices;
    }

    private static Vector2[] HalfCircle(float centerX, float centerY, float radius, int vertexCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(vertexCount, 3);

        var vertices = new Vector2[vertexCount + 1];
        for (var i = 0; i <= vertexCount; i++) {
            //var angle = (MathF.PI / 2f) + (2f * MathF.PI) * (i / (float)vertexCount);
            var angle = MathF.PI * (i / (float)vertexCount + 1f);
            vertices[i] = new Vector2(centerX + radius * MathF.Cos(angle), centerY + radius * MathF.Sin(angle));
        }

        return vertices;
    }
}
