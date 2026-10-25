using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Rides.Entrances;

internal sealed record RideEntranceExitMeshData(TrackVertex[] Vertices, uint[] Indices, RenderRegionIndexRange[] Regions);
