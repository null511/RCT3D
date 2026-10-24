using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Rides.Supports;

internal sealed record RideSupportMeshData(TrackVertex[] Vertices, uint[] Indices, RenderRegionIndexRange[] Regions);
