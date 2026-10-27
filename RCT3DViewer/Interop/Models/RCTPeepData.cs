using System.Runtime.InteropServices;

namespace RCT3D.Interop.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RCTPeepData
{
    public ushort EntityId;
    public int X;
    public int Y;
    public int Z;
    public byte State;
    public ushort RideId;
    public byte StationIndex;
    public byte TrainIndex;
    public byte CarIndex;
    public byte SeatIndex;
    public ushort TrainHeadEntityId;
    public byte Reserved;
}
