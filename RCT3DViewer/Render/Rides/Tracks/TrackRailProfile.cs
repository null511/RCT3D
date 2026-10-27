using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3D.Render.Rides.Tracks;

internal enum TrackRailSwatch : byte
{
    Main,
    Alternate,
    None
}

internal readonly record struct TrackRailSpecularData(byte Roughness, byte Metalness);

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct TrackRgba8(byte Red, byte Green, byte Blue, byte Alpha)
{
    public static TrackRgba8 White => new(255, 255, 255, 255);
}

internal sealed record TrackRailProfileShape(Vector2[] Vertices, bool Closed)
{
    public TrackRailSwatch Swatch { get; init; } = TrackRailSwatch.None;
    public TrackRgba8 Color { get; init; } = TrackRgba8.White;
    public TrackRailSpecularData Specular { get; init; } = new(250, 0);
}

