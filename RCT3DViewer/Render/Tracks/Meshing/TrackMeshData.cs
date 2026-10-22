using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3D.Render.Tracks.Meshing;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct TrackVertex(Vector3 position, Vector3 normal, TrackRgba8 color, byte roughness, byte metalness)
{
    public Vector3 Position = position;
    public Vector3 Normal = normal;
    public TrackRgba8 Color = color;
    public byte Roughness = roughness;
    public byte Metalness = metalness;
}

internal sealed record TrackMeshData(TrackVertex[] Vertices, uint[] Indices, RenderRegionIndexRange[] Regions);
