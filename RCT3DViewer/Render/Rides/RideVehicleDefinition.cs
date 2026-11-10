using RCT3D.Interop.Models;
using System.Numerics;

namespace RCT3D.Render.Rides;

internal class RideVehicleDefinition
{
    public RideVehicleCarDefinition[] Cars = [];
}

internal class RideVehicleCarDefinition
{
    public static readonly Vector3 DefaultSize = new(0.7f);

    public float LocalOffset { get; init; }
    public float WorldOffset { get; init; }

    /// <summary>
    /// Full car dimensions in world units along local X (width), Y (height), and Z (length).
    /// </summary>
    public Vector3 Size { get; init; } = DefaultSize;

    public byte? HelperPaintStyle { get; init; }

    public bool ShouldRender(in RCTVehicleData vehicle) =>
        HelperPaintStyle is not { } style || vehicle.PaintStyle != style || vehicle.RotationFrameMask != 0;
}
