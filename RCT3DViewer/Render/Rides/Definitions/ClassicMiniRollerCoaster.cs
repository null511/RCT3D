using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal class ClassicMiniRollerCoaster : RideDefinition
{
    private static readonly TrackRailSpecularData railSpecular = new(80, 80);


    public ClassicMiniRollerCoaster() : base(RCTRideType.ClassicMiniRollerCoaster)
    {
        SmoothMovement = true;
        CarVerticalOffset = 0.85f;

        SupportTrackOffset = 0.25f;
        SupportStyle = RideSupportStyle.MetalFork;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackShape GetTrackShape(RCTTrackElementData track)
    {
        var shape = new TrackShape {
            BraceInterval = 0.8f,
            RailShapes = [
                new TrackRailProfileShape(CreateCircle(-0.42f, 0.28f, 0.075f, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = railSpecular,
                },
                new TrackRailProfileShape(CreateCircle(0.42f, 0.28f, 0.075f, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = railSpecular,
                },
            ],
        };

        var (vertices, indices) = BuildCylinder(0.055f, 5);

        shape.BraceParts.Add(new TrackBracePart(vertices, indices) {
            Swatch = TrackRailSwatch.Main,
            Specular = railSpecular,
        });

        return shape;
    }

    private (TrackBraceVertex[] vertices, uint[] indices) BuildCylinder(float radius, int segmentCount)
    {
        const float halfRailSpacing = 0.42f;
        const float railHeight = 0.28f;

        var vertices = new TrackBraceVertex[segmentCount * 2];
        var indices = new uint[segmentCount * 6];

        for (var segment = 0; segment < segmentCount; segment++) {
            var angle = 2.0f * MathF.PI * segment / segmentCount;
            var normal = new Vector3(0.0f, MathF.Cos(angle), MathF.Sin(angle));
            var y = railHeight + radius * normal.Y;
            var z = radius * normal.Z;

            vertices[segment] = new TrackBraceVertex(new Vector3(-halfRailSpacing, y, z), normal);
            vertices[segment + segmentCount] = new TrackBraceVertex(new Vector3(halfRailSpacing, y, z), normal);

            var next = (segment + 1) % segmentCount;
            var index = segment * 6;

            indices[index] = (uint)segment;
            indices[index + 1] = (uint)(segment + segmentCount);
            indices[index + 2] = (uint)(next + segmentCount);
            indices[index + 3] = (uint)segment;
            indices[index + 4] = (uint)(next + segmentCount);
            indices[index + 5] = (uint)next;
        }

        return (vertices, indices);
    }
}
