using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3D.Render.Tracks.Meshing;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct TrackVertex(Vector3 position, Vector3 normal, byte paletteIndex, byte roughness, byte metalness, bool untinted)
{
    public Vector3 Position = position;
    public Vector3 Normal = normal;
    public byte PaletteIndex = paletteIndex;
    public byte Roughness = roughness;
    public byte Metalness = metalness;
    public byte Untinted = untinted ? (byte)1 : (byte)0;
}

internal sealed record TrackMeshData(TrackVertex[] Vertices, uint[] Indices, RenderRegionIndexRange[] Regions);
