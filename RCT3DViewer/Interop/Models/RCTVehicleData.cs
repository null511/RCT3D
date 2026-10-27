using System.Runtime.InteropServices;

namespace RCT3D.Interop.Models;

[StructLayout(LayoutKind.Sequential)]
public struct RCTVehicleData
{
    public ushort EntityId;
    public int X;
    public int Y;
    public int Z;
    public byte Orientation;
    public byte Pitch;
    public byte Roll;
    public byte Reserved;
    public float PitchRadians;
    public float RollRadians;
    public ushort RideId;
    public ushort TrainHeadEntityId;
    public byte CurrentStation;
    public byte Status;
    public int TrackLocationX;
    public int TrackLocationY;
    public int TrackLocationZ;
    public ushort TrackProgress;
    public ushort TrackProgressTotal;
    public ushort TrackTypeAndDirection;
    public byte TrackSubposition;
    public byte TrackReserved;
}
