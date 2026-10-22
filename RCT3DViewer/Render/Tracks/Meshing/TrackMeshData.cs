using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3D.Render.Tracks.Meshing;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct TrackVertex(Vector3 position, Vector3 normal, byte paletteIndex)
{
    public Vector3 Position = position;
    public Vector3 Normal = normal;
    public byte PaletteIndex = paletteIndex;
    private byte _padding0;
    private byte _padding1;
    private byte _padding2;
}

internal sealed record TrackMeshData(TrackVertex[] Vertices, uint[] Indices, RenderRegionIndexRange[] Regions);
