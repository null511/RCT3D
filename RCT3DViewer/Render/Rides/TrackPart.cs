using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Rides;

internal class TrackPart
{
    public float? BraceInterval;
    public List<TrackRailProfileShape> RailProfiles = [];
    public List<TrackBracePart> BraceParts = [];

    public BracePartBuilder AppendBracePart => new(BraceParts);
}
