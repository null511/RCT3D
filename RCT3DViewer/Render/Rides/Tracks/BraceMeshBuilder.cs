using System.Numerics;

namespace RCT3D.Render.Rides.Tracks;

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

    public BraceMeshBuilder AppendQuad(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
    {
        var normal = Vector3.Normalize(Vector3.Cross(p3 - p1, p2 - p1));

        var v1 = new TrackBraceVertex(p1, normal);
        var v2 = new TrackBraceVertex(p2, normal);
        var v3 = new TrackBraceVertex(p3, normal);
        var v4 = new TrackBraceVertex(p4, normal);

        Append([v1, v2, v3, v4], [0, 1, 2, 0, 2, 3]);

        return this;
    }

    public TrackBracePart Build(Action<TrackBracePart>? partAction = null)
    {
        var part = new TrackBracePart(vertexList.ToArray(), indexList.ToArray());
        partAction?.Invoke(part);
        return part;
    }
}
