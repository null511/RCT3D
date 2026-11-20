using RCT3D.Interop.Models;
using System.Numerics;

namespace RCT3D.Render.Rides;

internal class RideVehicleCarDefinition
{
    public static readonly Vector3 DefaultSize = new(0.7f);

    public string? Model { get; init; }
    public float ModelScale { get; init; } = 1.0f;

    /// <summary>
    /// The track-local Y offset to apply to the vehicle position.
    /// </summary>
    public float LocalOffset {get; init;} = 0.37f;

    /// <summary>
    /// The world Y offset to apply to vehicle positioning.
    /// </summary>
    public float WorldOffset {get; init;} = 0.0f;

    /// <summary>
    /// The vehicle-local Y offset for the camera attachment point.
    /// </summary>
    public float CameraOffset {get; init;} = 0.6f;

    /// <summary>
    /// Full fallback cube dimensions in world units along local X (width), Y (height), and Z (length).
    /// </summary>
    public Vector3 Size { get; init; } = DefaultSize;

    public byte? PaintStyle { get; init; }

    public bool ShouldRender(in RCTVehicleData vehicle) =>
        PaintStyle is not { } style || vehicle.PaintStyle != style || vehicle.RotationFrameMask != 0;
}
