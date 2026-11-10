namespace RCT3D.Render;

internal sealed class RegionQuadtree
{
    private ParkRenderRegion[]? sourceRegions;
    private ParkRenderRegion[] regions = [];
    private Node[] nodes = [];

    public void EnsureTopology(ParkRenderRegion[] parkRegions)
    {
        if (ReferenceEquals(sourceRegions, parkRegions)) return;

        regions = (ParkRenderRegion[])parkRegions.Clone();
        var builtNodes = new List<Node>();
        if (regions.Length > 0)
            BuildNode(builtNodes, 0, regions.Length);
        nodes = builtNodes.ToArray();
        sourceRegions = parkRegions;
    }

    public void Refit()
    {
        for (var i = nodes.Length - 1; i >= 0; i--) {
            var node = nodes[i];
            node.HasBounds = false;
            if (node.Children.Length == 0) {
                var region = regions[node.FirstRegion];
                if (region.HasBounds) {
                    node.Bounds = region.Bounds;
                    node.HasBounds = true;
                }
                continue;
            }

            foreach (var childIndex in node.Children) {
                var child = nodes[childIndex];
                if (!child.HasBounds) continue;

                node.Bounds = node.HasBounds ? node.Bounds.Encapsulate(child.Bounds) : child.Bounds;
                node.HasBounds = true;
            }
        }
    }

    public VisibilityQueryMetrics Query(in ViewFrustum frustum, int shadowCascade = -1)
    {
        var metrics = new VisibilityQueryMetrics();
        if (nodes.Length > 0)
            QueryNode(0, in frustum, ViewFrustum.AllPlanes, shadowCascade, ref metrics);
        return metrics;
    }

    private void QueryNode(int index, in ViewFrustum frustum, int activePlanes, int shadowCascade,
        ref VisibilityQueryMetrics metrics)
    {
        var node = nodes[index];
        if (!node.HasBounds) return;

        var isLeaf = node.Children.Length == 0;
        if (isLeaf) metrics.LeafTests++;
        else metrics.NodeTests++;
        var remainingPlanes = frustum.Classify(node.Bounds, activePlanes, ref metrics.PlaneTests);
        if (remainingPlanes < 0) {
            metrics.RejectedSubtrees++;
            return;
        }
        if (remainingPlanes == 0 || isLeaf) {
            if (remainingPlanes == 0) metrics.AcceptedSubtrees++;
            for (var i = node.FirstRegion; i < node.FirstRegion + node.RegionCount; i++) {
                var region = regions[i];
                if (!region.HasBounds) continue;

                if (shadowCascade < 0) region.CameraVisible = true;
                else region.ShadowVisibilityMask |= 1u << shadowCascade;
            }
            return;
        }
        foreach (var child in node.Children)
            QueryNode(child, in frustum, remainingPlanes, shadowCascade, ref metrics);
    }

    private int BuildNode(List<Node> builtNodes, int firstRegion, int regionCount)
    {
        var index = builtNodes.Count;
        var node = new Node(firstRegion, regionCount);
        builtNodes.Add(node);
        if (regionCount == 1) return index;

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        for (var i = firstRegion; i < firstRegion + regionCount; i++) {
            var coordinate = regions[i].Coordinate;
            minX = Math.Min(minX, coordinate.X);
            minY = Math.Min(minY, coordinate.Y);
            maxX = Math.Max(maxX, coordinate.X);
            maxY = Math.Max(maxY, coordinate.Y);
        }
        if (minX == maxX && minY == maxY)
            throw new ArgumentException("Render region coordinates must be unique.");

        var splitX = minX + (maxX - minX) / 2;
        var splitY = minY + (maxY - minY) / 2;
        var children = new List<int>(4);
        var next = firstRegion;
        for (var quadrant = 0; quadrant < 4; quadrant++) {
            var start = next;
            for (var i = next; i < firstRegion + regionCount; i++) {
                var coordinate = regions[i].Coordinate;
                var regionQuadrant = (coordinate.X > splitX ? 1 : 0) | (coordinate.Y > splitY ? 2 : 0);
                if (regionQuadrant != quadrant) continue;

                (regions[next], regions[i]) = (regions[i], regions[next]);
                next++;
            }
            if (next > start)
                children.Add(BuildNode(builtNodes, start, next - start));
        }
        node.Children = children.ToArray();
        return index;
    }

    private sealed class Node(int firstRegion, int regionCount)
    {
        public int FirstRegion { get; } = firstRegion;
        public int RegionCount { get; } = regionCount;
        public int[] Children { get; set; } = [];
        public bool HasBounds { get; set; }
        public AxisAlignedBounds Bounds { get; set; }
    }
}

internal struct VisibilityQueryMetrics
{
    public int NodeTests;
    public int LeafTests;
    public int PlaneTests;
    public int AcceptedSubtrees;
    public int RejectedSubtrees;
}
