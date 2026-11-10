using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class DinghySlide : RideDefinition
{
    private const float TubeRadius = 1.25f;
    private const float TubeThickness = 0.06f;

    private static readonly TrackRailSpecularData paintSpecular = new(160, 0);


    public DinghySlide() : base(RCTRideType.DinghySlide)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 1.35f;

        SupportStyle = RideSupportStyle.MetalTubes;
        SupportTrackOffset = -1.25f;

        StationBuilder = new DefaultTrackStationBuilder();

        Vehicles = new RideVehicleDefinition {
            Size = new Vector3(0.86f, 0.42f, 1.8f),
            WorldOffset =  1.0f,
            LocalOffset = -0.9f,
        };
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
        const int faceCount = 16;

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
        const int faceCount = 8;

        part.RailProfiles.Add(new TrackRailProfileShape(HalfCircle(0f, 0f, TubeRadius, faceCount), false) {
            Swatch = TrackRailSwatch.Main,
            Specular = paintSpecular,
        });

        var innerRadius = TubeRadius - TubeThickness;

        part.RailProfiles.Add(new TrackRailProfileShape(HalfCircle(0f, 0f, innerRadius, faceCount).Reverse().ToArray(), false) {
            Swatch = TrackRailSwatch.Alternate,
            Specular = paintSpecular,
        });

        // left
        part.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(-innerRadius, 0f),
            new Vector2(-TubeRadius,  0f),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = paintSpecular,
        });

        // right
        part.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2( TubeRadius,  0f),
            new Vector2( innerRadius, 0f),
        ], false) {
            Swatch = TrackRailSwatch.Main,
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
