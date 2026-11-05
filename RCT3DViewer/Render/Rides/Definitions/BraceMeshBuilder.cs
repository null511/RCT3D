using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Rides.Definitions;

internal class BraceMeshBuilder
{
    private readonly List<TrackBraceVertex> vertexList = [];
    private readonly List<uint> indexList = [];


    public BraceMeshBuilder Append(TrackBraceVertex[] vertices, uint[] indices)
    {
        var offset = (uint)vertexList.Count;

        vertexList.AddRange(vertices);
        indexList.AddRange(indices.Select(i => i + offset));

        return this;
    }

    public TrackBracePart Build(Action<TrackBracePart>? partAction = null)
    {
        var part = new TrackBracePart(vertexList.ToArray(), indexList.ToArray());
        partAction?.Invoke(part);
        return part;
    }
}
