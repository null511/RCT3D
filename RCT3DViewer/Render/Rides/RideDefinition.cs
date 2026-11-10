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

    /// <summary>
    /// The local profile-up displacement from the tile-centered vertical track reference.
    /// </summary>
    protected float CenterlineVerticalOffset = 0.0f;

    protected float TrackBankAngleDegrees = 45f;

    /// <summary>
    /// The offset from the support-top to the track-bottom.
    /// </summary>
    protected float SupportTrackOffset = 0.0f;

    protected bool HasSupportPlacement = false;
    protected IDirectTrackMeshBuilder? DirectMeshBuilder;

    /// <summary>
    /// Enables smoothing of vehicle movement by using the track centerline instead of the game-provided location/rotation.
    /// </summary>
    protected bool SmoothMovement = false;

    protected RideVehicleDefinition Vehicles = new();


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

        return new TrackRideConfiguration(rideType, partGenerators, SupportStyle, CenterlineHeightOffset, CenterlineVerticalOffset, SupportTrackOffset, Vehicles, 
            HasSupportPlacement ? GetSupportPlacements : null, SmoothMovement, StationBuilder, TrackBankAngleDegrees,
            PlatformSurface, PlatformRailing, DirectMeshBuilder is null ? track => GetCachedTrackShape(track).WaterPlane : null, DirectMeshBuilder);
    }

    protected virtual TrackPart GetTrackShape(RCTTrackElementData track) =>
        throw new InvalidOperationException("Swept-track rides must define their track shape.");

    protected virtual void GetSupportPlacements(RCTTrackElementData track, List<TrackSupportPlacement> placements)
    {
        placements.Clear();
    }
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
