using System;
using System.Collections.Generic;

namespace ZoomTracks.CollisionDetection
{
    /// <summary>
    /// A balanced, immutable AABB hierarchy. Segments are Morton-sorted once so nearby
    /// segment boxes tend to share leaves. The tree is traversed separately for all four
    /// authoritative query edges.
    /// </summary>
    public sealed class MortonBvhIndex : OutlineIndexBase
    {
        public const int DefaultLeafSize = 8;

        private readonly BvhNode[] _nodes;
        private readonly int _leafSize;

        public MortonBvhIndex(
            IReadOnlyList<CoordinateXY> outline1,
            IReadOnlyList<CoordinateXY> outline2,
            int leafSize = DefaultLeafSize)
            : base(outline1, outline2)
        {
            if (leafSize < 1 || leafSize > 64)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(leafSize),
                    "The leaf size must be in [1, 64].");
            }

            _leafSize = leafSize;
            MortonSortSegments();

            int estimatedLeafCount = checked((Segments.Length + leafSize - 1) / leafSize);
            var nodes = new List<BvhNode>(checked(estimatedLeafCount * 2));
            BuildNode(nodes, 0, Segments.Length);
            _nodes = nodes.ToArray();
        }

        public int LeafSize
        {
            get { return _leafSize; }
        }

        public int NodeCount
        {
            get { return _nodes.Length; }
        }

        public override bool IsColliding(ConvexQuadrilateralOutline outline)
        {
            if (!outline.Bounds.Overlaps(OutlineBounds))
            {
                return false;
            }

            for (int edgeIndex = 0; edgeIndex < 4; ++edgeIndex)
            {
                CoordinateXY queryA = outline.GetVertex(edgeIndex);
                CoordinateXY queryB = outline.GetVertex((edgeIndex + 1) & 3);
                Aabb queryBounds = Aabb.FromSegment(queryA, queryB);
                if (queryBounds.Overlaps(OutlineBounds) &&
                    IntersectsNode(0, queryA, queryB, queryBounds))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IntersectsNode(
            int nodeIndex,
            CoordinateXY queryA,
            CoordinateXY queryB,
            Aabb queryBounds)
        {
            BvhNode node = _nodes[nodeIndex];
            if (!node.Bounds.Overlaps(queryBounds))
            {
                return false;
            }

            if (node.Count != 0)
            {
                int end = node.Start + node.Count;
                for (int i = node.Start; i < end; ++i)
                {
                    OutlineSegment segment = Segments[i];
                    if (ExactSegmentPredicates.IntersectsWithKnownBounds(
                        queryA,
                        queryB,
                        queryBounds,
                        segment.A,
                        segment.B,
                        segment.Bounds))
                    {
                        return true;
                    }
                }

                return false;
            }

            return IntersectsNode(node.Left, queryA, queryB, queryBounds) ||
                IntersectsNode(node.Right, queryA, queryB, queryBounds);
        }

        private int BuildNode(List<BvhNode> nodes, int start, int count)
        {
            int nodeIndex = nodes.Count;
            nodes.Add(default(BvhNode));

            if (count <= _leafSize)
            {
                Aabb leafBounds = Segments[start].Bounds;
                for (int i = start + 1; i < start + count; ++i)
                {
                    leafBounds = Aabb.Union(leafBounds, Segments[i].Bounds);
                }

                nodes[nodeIndex] = BvhNode.CreateLeaf(leafBounds, start, count);
                return nodeIndex;
            }

            int leftCount = count / 2;
            int left = BuildNode(nodes, start, leftCount);
            int right = BuildNode(nodes, start + leftCount, count - leftCount);
            Aabb bounds = Aabb.Union(nodes[left].Bounds, nodes[right].Bounds);
            nodes[nodeIndex] = BvhNode.CreateBranch(bounds, left, right);
            return nodeIndex;
        }

        private void MortonSortSegments()
        {
            double minCenterX = Segments[0].Bounds.CenterX;
            double maxCenterX = minCenterX;
            double minCenterY = Segments[0].Bounds.CenterY;
            double maxCenterY = minCenterY;

            for (int i = 1; i < Segments.Length; ++i)
            {
                double centerX = Segments[i].Bounds.CenterX;
                double centerY = Segments[i].Bounds.CenterY;
                minCenterX = Math.Min(minCenterX, centerX);
                maxCenterX = Math.Max(maxCenterX, centerX);
                minCenterY = Math.Min(minCenterY, centerY);
                maxCenterY = Math.Max(maxCenterY, centerY);
            }

            var items = new MortonItem[Segments.Length];
            for (int i = 0; i < Segments.Length; ++i)
            {
                uint x = Quantize(Segments[i].Bounds.CenterX, minCenterX, maxCenterX);
                uint y = Quantize(Segments[i].Bounds.CenterY, minCenterY, maxCenterY);
                items[i] = new MortonItem(Interleave16(x, y), i, Segments[i]);
            }

            Array.Sort(items, MortonItemComparer.Instance);
            for (int i = 0; i < items.Length; ++i)
            {
                Segments[i] = items[i].Segment;
            }
        }

        private static uint Quantize(double value, double min, double max)
        {
            if (max <= min)
            {
                return 0U;
            }

            double normalized = (value - min) / (max - min);
            if (normalized <= 0.0)
            {
                return 0U;
            }

            if (normalized >= 1.0)
            {
                return 65535U;
            }

            return (uint)(normalized * 65535.0);
        }

        private static uint Interleave16(uint x, uint y)
        {
            return Spread16(x) | (Spread16(y) << 1);
        }

        private static uint Spread16(uint value)
        {
            value &= 0x0000ffffU;
            value = (value | (value << 8)) & 0x00ff00ffU;
            value = (value | (value << 4)) & 0x0f0f0f0fU;
            value = (value | (value << 2)) & 0x33333333U;
            value = (value | (value << 1)) & 0x55555555U;
            return value;
        }

        private readonly struct MortonItem
        {
            public MortonItem(uint code, int originalIndex, OutlineSegment segment)
            {
                Code = code;
                OriginalIndex = originalIndex;
                Segment = segment;
            }

            public uint Code { get; }

            public int OriginalIndex { get; }

            public OutlineSegment Segment { get; }
        }

        private sealed class MortonItemComparer : IComparer<MortonItem>
        {
            public static readonly MortonItemComparer Instance = new MortonItemComparer();

            public int Compare(MortonItem x, MortonItem y)
            {
                int codeComparison = x.Code.CompareTo(y.Code);
                return codeComparison != 0
                    ? codeComparison
                    : x.OriginalIndex.CompareTo(y.OriginalIndex);
            }
        }

        private readonly struct BvhNode
        {
            private BvhNode(Aabb bounds, int start, int count, int left, int right)
            {
                Bounds = bounds;
                Start = start;
                Count = count;
                Left = left;
                Right = right;
            }

            public Aabb Bounds { get; }

            public int Start { get; }

            public int Count { get; }

            public int Left { get; }

            public int Right { get; }

            public static BvhNode CreateLeaf(Aabb bounds, int start, int count)
            {
                return new BvhNode(bounds, start, count, -1, -1);
            }

            public static BvhNode CreateBranch(Aabb bounds, int left, int right)
            {
                return new BvhNode(bounds, 0, 0, left, right);
            }
        }
    }
}
