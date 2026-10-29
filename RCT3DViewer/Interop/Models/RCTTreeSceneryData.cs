using System.Runtime.InteropServices;

namespace RCT3D.Interop.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RCTTreeSceneryData
{
    public int X;
    public int Y;
    public int Z;
    public byte Direction;
    public byte Reserved0;
    public byte Reserved1;
    public byte Reserved2;
}
