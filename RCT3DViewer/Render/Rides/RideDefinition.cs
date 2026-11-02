using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Collections.Concurrent;
using System.Numerics;

namespace RCT3D.Render.Rides;

internal abstract class RideDefinition(RCTRideType rideType)
{
    protected RideSupportStyle SupportStyle;
    protected ITrackStationBuilder? StationBuilder;
    protected RidePlatformSurfaceType PlatformSurface = RidePlatformSurfaceType.Wood;
    protected RidePlatformRailingType PlatformRailing = RidePlatformRailingType.Chained;

    /// <summary>
    /// The vertical offset in world-space for the tracks generation.
    /// </summary>
    protected float CenterlineHeightOffset = 0.0f;

    protected float TrackBankAngleDegrees = 45f;

    /// <summary>
    /// The offset from the support-top to the track-bottom.
    /// </summary>
    protected float SupportTrackOffset = 0.0f;

    protected float CarVerticalOffset = 0.0f;
    protected bool HasSupportPlacement = false;
    protected Vector2[]? WaterPlanePolyline;

    /// <summary>
    /// Enables smoothing of vehicle movement by using the track centerline instead of the game-provided location/rotation.
    /// </summary>
    protected bool SmoothMovement = false;


    public TrackRideConfiguration Build()
    {
        var trackShapesByType = new ConcurrentDictionary<(RCTTrackElementType TrackType, byte HasChainLift), TrackShape>();

        TrackShape GetCachedTrackShape(RCTTrackElementData track) {
            var trackData = track;
            return trackShapesByType.GetOrAdd(
                (track.Part, track.HasChainLift),
                _ => GetTrackShape(trackData));
        }

        var partGenerators = new List<ITrackPartGenerator> {
            new SweptRailProfileGenerator(track => GetCachedTrackShape(track).RailShapes),
            new TrackBraceGenerator(GetCachedTrackShape),
        };

        return new TrackRideConfiguration(rideType, partGenerators, SupportStyle, CenterlineHeightOffset, SupportTrackOffset, CarVerticalOffset,
            HasSupportPlacement ? GetSupportPlacements : null, SmoothMovement, StationBuilder, TrackBankAngleDegrees,
            PlatformSurface, PlatformRailing, WaterPlanePolyline);
    }

    protected abstract TrackShape GetTrackShape(RCTTrackElementData track);

    protected virtual void GetSupportPlacements(RCTTrackElementData track, List<TrackSupportPlacement> placements)
    {
        placements.Clear();
    }

    #region Rail Profile Helpers

    protected static IEnumerable<TrackRailProfileShape> CreateBox(float centerX, float centerY, float halfWidth, float halfHeight, Action<TrackRailProfileShape>? partAction)
    {
        // top
        yield return BuildPart([
            new Vector2(centerX + halfWidth, centerY + halfHeight),
            new Vector2(centerX - halfWidth, centerY + halfHeight),
        ]);

        // left
        yield return BuildPart([
            new Vector2(centerX - halfWidth, centerY + halfHeight),
            new Vector2(centerX - halfWidth, centerY - halfHeight),
        ]);

        // bottom
        yield return BuildPart([
            new Vector2(centerX - halfWidth, centerY - halfHeight),
            new Vector2(centerX + halfWidth, centerY - halfHeight),
        ]);

        // right
        yield return BuildPart([
            new Vector2(centerX + halfWidth, centerY - halfHeight),
            new Vector2(centerX + halfWidth, centerY + halfHeight),
        ]);

        yield break;

        TrackRailProfileShape BuildPart(Vector2[] line) {
            var shape = new TrackRailProfileShape(line, false);
            partAction?.Invoke(shape);
            return shape;
        }
    }

    protected static Vector2[] CreateCircle(float centerX, float centerY, float radius, int vertexCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(vertexCount, 3);

        var vertices = new Vector2[vertexCount];
        for (var i = 0; i < vertexCount; i++) {
            var angle = MathF.PI / 2 + 2 * MathF.PI * i / vertexCount;
            vertices[i] = new Vector2(centerX + radius * MathF.Cos(angle), centerY + radius * MathF.Sin(angle));
        }

        return vertices;
    }

    #endregion
}

internal class TrackShape
{
    public float? BraceInterval;
    public List<TrackRailProfileShape> RailShapes = [];
    public List<TrackBracePart> BraceParts = [];

    public BracePartBuilder AppendBracePart => new(BraceParts);
}

internal enum RidePlatformSurfaceType
{
    Wood,
    Metal,
    Fiber,
}

internal enum RidePlatformRailingType
{
    Fenced,
    Chained,
}

internal class BracePartBuilder(List<TrackBracePart> braces)
{
    public TrackBracePart Box(Vector3 center, Vector3 halfSize, Action<TrackBracePart>? partAction = null)
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

        var part = new TrackBracePart(vertices, indices);
        partAction?.Invoke(part);
        braces.Add(part);
        return part;

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
