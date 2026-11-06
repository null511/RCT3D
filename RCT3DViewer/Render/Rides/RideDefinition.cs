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
    protected IDirectTrackMeshBuilder? DirectMeshBuilder;

    /// <summary>
    /// Enables smoothing of vehicle movement by using the track centerline instead of the game-provided location/rotation.
    /// </summary>
    protected bool SmoothMovement = false;


    public TrackRideConfiguration Build()
    {
        var trackShapesByType = new ConcurrentDictionary<(RCTTrackElementType TrackType, byte HasChainLift), TrackPart>();

        TrackPart GetCachedTrackShape(RCTTrackElementData track) {
            var trackData = track;
            return trackShapesByType.GetOrAdd(
                (track.Part, track.HasChainLift),
                _ => GetTrackShape(trackData));
        }

        IReadOnlyList<ITrackPartGenerator> partGenerators = DirectMeshBuilder is not null ? [] : new List<ITrackPartGenerator> {
            new SweptRailProfileGenerator(track => GetCachedTrackShape(track).RailProfiles),
            new TrackBraceGenerator(GetCachedTrackShape),
        };

        return new TrackRideConfiguration(rideType, partGenerators, SupportStyle, CenterlineHeightOffset, SupportTrackOffset, CarVerticalOffset,
            HasSupportPlacement ? GetSupportPlacements : null, SmoothMovement, StationBuilder, TrackBankAngleDegrees,
            PlatformSurface, PlatformRailing, WaterPlanePolyline, DirectMeshBuilder);
    }

    protected virtual TrackPart GetTrackShape(RCTTrackElementData track) =>
        throw new InvalidOperationException("Swept-track rides must define their track shape.");

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
