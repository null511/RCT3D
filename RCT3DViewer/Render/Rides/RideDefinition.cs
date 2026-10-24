using RCT3D.Interop;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides;

internal abstract class RideDefinition(RCTRideType rideType)
{
    protected RideSupportStyle SupportStyle;
    protected float TrackHeightOffset = 0.0f;
    protected bool HasSupportPlacement = false;


    public TrackRideConfiguration Build()
    {
        var profile = CreateProfile();

        TrackPieceBuilderRegistry registry = new();
        BuildRegistry(registry);

        return new TrackRideConfiguration(rideType, registry, [
            new SweptRailProfileGenerator(profile),
        ], SupportStyle, TrackHeightOffset, HasSupportPlacement ? GetSupportPlacements : null);
    }

    protected abstract TrackRailProfile CreateProfile();

    protected abstract void BuildRegistry(TrackPieceBuilderRegistry registry);

    protected virtual void GetSupportPlacements(RCTTrackElementData track, List<TrackSupportPlacement> placements)
    {
        placements.Clear();
    }

    protected static Vector2[] CreateCircle(float radius, float centerX, float centerY, int vertexCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(vertexCount, 3);

        var vertices = new Vector2[vertexCount];
        for (var i = 0; i < vertexCount; i++) {
            var angle = MathF.PI / 2 + 2 * MathF.PI * i / vertexCount;
            vertices[i] = new Vector2(centerX + radius * MathF.Cos(angle), centerY + radius * MathF.Sin(angle));
        }

        return vertices;
    }
}
