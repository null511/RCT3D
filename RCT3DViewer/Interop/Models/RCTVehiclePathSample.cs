using System.Runtime.InteropServices;

namespace RCT3D.Interop.Models;

[StructLayout(LayoutKind.Sequential)]
public struct RCTVehiclePathSample
{
    public uint PathId;
    public int X;
    public int Y;
    public int Z;
    public float PitchRadians;
    public float RollRadians;
    public ushort RideId;
    public byte Orientation;
    public byte TrackSubposition;
}
