using System.Numerics;
using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class ClassicWoodenRollerCoaster : RideDefinition
{
    private const float SupportTopClearance = 0.02f;

    private const float RailHalfSeparation = 0.72f;
    private const float RailHalfWidth = 0.08f;
    private const float RailHalfHeight = 0.03f;
    private const float RailWoodInset = 0.2f;


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

    protected override TrackShape GetTrackShape(RCTTrackElementData track)
    {
        var shape = new TrackShape {
            BraceInterval = 0.6f,
        };

        shape.RailShapes.AddRange(CreateRailProfile(-RailHalfSeparation, -0.2f));
        shape.RailShapes.AddRange(CreateRailProfile( RailHalfSeparation, -0.2f));

        if (!track.Part.IsStationPlatform() && !track.Part.IsLoop()) {
            // base beam
            shape.BraceParts.AddRange(CreateBeam(
                new Vector3(0.0f, -0.4f, 0.0f),
                new Vector3(1.45f, 0.12f, 0.12f)));

            // left beam
            shape.BraceParts.AddRange(CreateBeam(
                new Vector3(-1.47f, 0f, 0.0f),
                new Vector3(0.04f, 0.32f, 0.04f)));

            // right beam
            shape.BraceParts.AddRange(CreateBeam(
                new Vector3( 1.47f, 0f, 0.0f),
                new Vector3(0.04f, 0.32f, 0.04f)));

            const float woodHalfMargin = (1.5f - (RailHalfSeparation - RailWoodInset)) * 0.5f;
            const float woodHeight = 0.06f;

            const float woodPlatformCenterX = 1.5f - woodHalfMargin;
            const float woodPlatformCenterY = -0.26f;

            // side platforms
            shape.RailShapes.AddRange(CreateWoodProfile(-woodPlatformCenterX, woodPlatformCenterY, woodHalfMargin, woodHeight, TrackRailSwatch.Alternate));
            shape.RailShapes.AddRange(CreateWoodProfile(woodPlatformCenterX, woodPlatformCenterY, woodHalfMargin, woodHeight, TrackRailSwatch.Alternate));

            const float WoodRailingWidth = 0.04f;

            var woodRailingCenterX = 1.5f - 0.5f*WoodRailingWidth;
            var woodRailingCenterY = 0.36f;

            // side railing
            shape.RailShapes.AddRange(CreateWoodProfile(-woodRailingCenterX, woodRailingCenterY, WoodRailingWidth, WoodRailingWidth, TrackRailSwatch.Main));
            shape.RailShapes.AddRange(CreateWoodProfile(woodRailingCenterX, woodRailingCenterY, WoodRailingWidth, WoodRailingWidth, TrackRailSwatch.Main));
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

    private static readonly TrackRailSpecularData WoodSpecular = new(220, 0);

    private static IEnumerable<TrackRailProfileShape> CreateWoodProfile(float centerX, float centerY, float halfWidth, float halfHeight, TrackRailSwatch swatch)
    {
        return CreateBox(centerX, centerY, halfWidth, halfHeight, shape => {
            shape.Swatch = swatch;
            shape.Specular = WoodSpecular;
        });
    }

    private static TrackBracePart CreateBeam(Vector3 center, Vector3 halfSize)
    {
        var minX = center.X - halfSize.X;
        var maxX = center.X + halfSize.X;
        var minY = center.Y - halfSize.Y;
        var maxY = center.Y + halfSize.Y;
        var minZ = center.Z - halfSize.Z;
        var maxZ = center.Z + halfSize.Z;

        var a = new Vector3(minX, minY, maxZ);
        var b = new Vector3(minX, minY, minZ);
        var c = new Vector3(maxX, minY, minZ);
        var d = new Vector3(maxX, minY, maxZ);
        var e = new Vector3(minX, maxY, maxZ);
        var f = new Vector3(minX, maxY, minZ);
        var g = new Vector3(maxX, maxY, minZ);
        var h = new Vector3(maxX, maxY, maxZ);

        var vertices = new TrackBraceVertex[24];
        var indices = new uint[36];
        var nextVertex = 0;

        AddFace(a, b, c, d, -Vector3.UnitY);
        AddFace(e, h, g, f, Vector3.UnitY);
        AddFace(a, e, f, b, -Vector3.UnitX);
        AddFace(d, c, g, h, Vector3.UnitX);
        AddFace(a, d, h, e, Vector3.UnitZ);
        AddFace(b, f, g, c, -Vector3.UnitZ);

        return new TrackBracePart(vertices, indices) {
            Swatch = TrackRailSwatch.Main,
            Specular = WoodSpecular,
        };

        void AddFace(Vector3 first, Vector3 second, Vector3 third, Vector3 fourth, Vector3 normal) {
            var vertexOffset = nextVertex;
            var indexOffset = vertexOffset / 4 * 6;

            vertices[vertexOffset] = new TrackBraceVertex(first, normal);
            vertices[vertexOffset + 1] = new TrackBraceVertex(second, normal);
            vertices[vertexOffset + 2] = new TrackBraceVertex(third, normal);
            vertices[vertexOffset + 3] = new TrackBraceVertex(fourth, normal);

            indices[indexOffset] = (uint)vertexOffset;
            indices[indexOffset + 1] = (uint)(vertexOffset + 2);
            indices[indexOffset + 2] = (uint)(vertexOffset + 1);
            indices[indexOffset + 3] = (uint)vertexOffset;
            indices[indexOffset + 4] = (uint)(vertexOffset + 3);
            indices[indexOffset + 5] = (uint)(vertexOffset + 2);

            nextVertex += 4;
        }
    }
}
