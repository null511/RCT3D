using System.Numerics;

namespace RCT3D.Render.Rides;

internal class RideVehicleDefinition
{
    public static readonly Vector3 DefaultSize = new(0.7f);

    public float LocalOffset = 0.0f;
    public float WorldOffset = 0.0f;

    /// <summary>
    /// Full car dimensions in world units along local X (width), Y (height), and Z (length).
    /// </summary>
    public Vector3 Size = DefaultSize;
}
