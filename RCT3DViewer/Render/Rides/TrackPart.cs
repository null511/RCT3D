using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides;

internal class TrackPart
{
    public float? BraceInterval;
    public List<TrackRailProfileShape> RailProfiles = [];
    public List<TrackBracePart> BraceParts = [];
    public Vector2[]? WaterPlane;

    public BracePartBuilder AppendBracePart => new(BraceParts);
}
