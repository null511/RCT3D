using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides;

internal class TrackPart
{
    public float? BraceInterval;
    public List<TrackRailProfileShape> RailProfiles = [];
    public List<TrackBracePart> BraceParts = [];
    public Vector2[]? WaterPlane;

    public RailPartBuilder AppendRailPart => new(RailProfiles);
    public BracePartBuilder AppendBracePart => new(BraceParts);
}

internal class RailPartBuilder(List<TrackRailProfileShape> list)
{
    public void Box(float centerX, float centerY, float halfWidth, float halfHeight, Action<TrackRailProfileShape>? partAction)
    {
        // top
        list.Add(BuildPart([
            new Vector2(centerX + halfWidth, centerY + halfHeight),
            new Vector2(centerX - halfWidth, centerY + halfHeight),
        ]));

        // left
        list.Add(BuildPart([
            new Vector2(centerX - halfWidth, centerY + halfHeight),
            new Vector2(centerX - halfWidth, centerY - halfHeight),
        ]));

        // bottom
        list.Add(BuildPart([
            new Vector2(centerX - halfWidth, centerY - halfHeight),
            new Vector2(centerX + halfWidth, centerY - halfHeight),
        ]));

        // right
        list.Add(BuildPart([
            new Vector2(centerX + halfWidth, centerY - halfHeight),
            new Vector2(centerX + halfWidth, centerY + halfHeight),
        ]));

        return;

        TrackRailProfileShape BuildPart(Vector2[] line) {
            var shape = new TrackRailProfileShape(line, false);
            partAction?.Invoke(shape);
            return shape;
        }
    }

    public void Circle(float centerX, float centerY, float radius, int vertexCount, Action<TrackRailProfileShape>? partAction)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(vertexCount, 3);

        var vertices = new Vector2[vertexCount];
        for (var i = 0; i < vertexCount; i++) {
            var angle = MathF.PI * 0.5f + 2f * MathF.PI * (i / (float)vertexCount);
            vertices[i] = new Vector2(centerX + radius * MathF.Cos(angle), centerY + radius * MathF.Sin(angle));
        }

        var shape = new TrackRailProfileShape(vertices, true);
        partAction?.Invoke(shape);
        list.Add(shape);
    }
}
