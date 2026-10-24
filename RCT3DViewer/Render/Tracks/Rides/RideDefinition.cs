using RCT3D.Interop;
using RCT3D.Render.Tracks.Meshing;
using System.Numerics;

namespace RCT3D.Render.Tracks.Rides;

internal abstract class RideDefinition(RCTRideType rideType)
{
    public TrackRideConfiguration Build()
    {
        var profile = CreateProfile();

        TrackPieceBuilderRegistry registry = new();
        BuildRegistry(registry);

        return new TrackRideConfiguration(rideType, registry, [
            new SweptRailProfileGenerator(profile),
        ], SupportStyle, CreateSupportPlacementMap());
    }

    protected abstract TrackRailProfile CreateProfile();

    protected abstract void BuildRegistry(TrackPieceBuilderRegistry registry);

    protected abstract RideSupportStyle SupportStyle { get; }

    protected virtual ITrackSupportPlacementMap? CreateSupportPlacementMap() => null;

    protected static Vector2[] CreateCircle(float radius, float centerX, float centerY, int vertexCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(vertexCount, 3);

        var vertices = new Vector2[vertexCount];
        for (var i = 0; i < vertexCount; i++) {
            var angle = MathF.PI / 2 - 2 * MathF.PI * i / vertexCount;
            vertices[i] = new Vector2(centerX + radius * MathF.Cos(angle), centerY + radius * MathF.Sin(angle));
        }

        return vertices;
    }
}
