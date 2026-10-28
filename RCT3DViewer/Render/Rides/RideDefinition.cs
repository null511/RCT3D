using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides;

internal abstract class RideDefinition(RCTRideType rideType)
{
    protected RideSupportStyle SupportStyle;
    protected IStationBuilder? StationBuilder;

    /// <summary>
    /// The vertical offset in world-space for the tracks generation.
    /// </summary>
    protected float CenterlineHeightOffset = 0.0f;

    /// <summary>
    /// The offset from the support-top to the track-bottom.
    /// </summary>
    protected float SupportTrackOffset = 0.0f;

    protected float CarVerticalOffset = 0.0f;
    protected bool HasSupportPlacement = false;

    /// <summary>
    /// Enables smoothing of vehicle movement by using the track centerline instead of the game-provided location/rotation.
    /// </summary>
    protected bool SmoothMovement = false;


    public TrackRideConfiguration Build()
    {
        return new TrackRideConfiguration(rideType, [
            new SweptRailProfileGenerator(GetRailParts),
        ], SupportStyle, CenterlineHeightOffset, SupportTrackOffset, CarVerticalOffset,
            HasSupportPlacement ? GetSupportPlacements : null, SmoothMovement, StationBuilder);
    }

    protected abstract IEnumerable<TrackRailProfileShape> GetRailParts(RCTTrackElementData track);

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
