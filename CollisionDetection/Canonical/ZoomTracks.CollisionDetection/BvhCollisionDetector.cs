using System;
using System.Collections.Generic;

namespace ZoomTracks.CollisionDetection {
    /// <summary>
    /// Exact detector using a balanced axis-aligned bounding-volume hierarchy.
    /// </summary>
    public sealed class BvhCollisionDetector : CollisionDetectorBase {
        public const int DefaultLeafSize = 8;

        private readonly AabbF[] _edgeBounds;
        private readonly int[] _edgeOrder;
        private readonly Node[] _nodes;

        public BvhCollisionDetector(
            List<CoordinateXY> outline1,
            List<CoordinateXY> outline2)
            : this(outline1, outline2, DefaultLeafSize) {
        }

        public BvhCollisionDetector(
            List<CoordinateXY> outline1,
            List<CoordinateXY> outline2,
            int leafSize)
            : base(outline1, outline2) {
            if (leafSize < 1) {
                throw new ArgumentOutOfRangeException(nameof(leafSize));
            }

            this.LeafSize = leafSize;
            this._edgeBounds = new AabbF[this.EdgeCount];
            this._edgeOrder = new int[this.EdgeCount];
            for (int i = 0; i < this.EdgeCount; ++i) {
                this._edgeBounds[i] = this.GetEdgeBounds(i);
                this._edgeOrder[i] = i;
            }

            this._nodes = new Node[checked(this.EdgeCount * 2 - 1)];
            _ = this.BuildNode(0, this.EdgeCount);
        }

        public int LeafSize { get; }
        public int NodeCount { get; private set; }

        private protected override bool Query(in RectangleQuad rectangle) {
            return this.QueryNode(0, rectangle);
        }

        private bool QueryNode(int nodeIndex, in RectangleQuad rectangle) {
            Node node = this._nodes[nodeIndex];
            if (!node.Bounds.Overlaps(rectangle.Bounds)) {
                return false;
            }

            if (node.Count != 0) {
                int end = node.Start + node.Count;
                for (int i = node.Start; i < end; ++i) {
                    int edgeIndex = this._edgeOrder[i];
                    if (this._edgeBounds[edgeIndex].Overlaps(rectangle.Bounds)
                        && this.EdgeIntersectsRectangleAfterBoundsCheck(edgeIndex, rectangle)) {
                        return true;
                    }
                }

                return false;
            }

            return this.QueryNode(node.Left, rectangle)
                || this.QueryNode(node.Right, rectangle);
        }

        private int BuildNode(int start, int count) {
            int nodeIndex = this.NodeCount++;
            AabbF bounds = this.ComputeRangeBounds(start, count);
            if (count <= this.LeafSize) {
                this._nodes[nodeIndex] = Node.CreateLeaf(bounds, start, count);
                return nodeIndex;
            }

            bool splitOnX = this.CenterSpreadX(start, count) >= this.CenterSpreadY(start, count);
            int middle = start + count / 2;
            this.SelectNth(start, start + count, middle, splitOnX);
            int left = this.BuildNode(start, middle - start);
            int right = this.BuildNode(middle, start + count - middle);
            this._nodes[nodeIndex] = Node.CreateBranch(bounds, left, right);
            return nodeIndex;
        }

        private AabbF ComputeRangeBounds(int start, int count) {
            AabbF first = this._edgeBounds[this._edgeOrder[start]];
            float minX = first.MinX;
            float minY = first.MinY;
            float maxX = first.MaxX;
            float maxY = first.MaxY;
            int end = start + count;
            for (int i = start + 1; i < end; ++i) {
                AabbF bounds = this._edgeBounds[this._edgeOrder[i]];
                minX = Math.Min(minX, bounds.MinX);
                minY = Math.Min(minY, bounds.MinY);
                maxX = Math.Max(maxX, bounds.MaxX);
                maxY = Math.Max(maxY, bounds.MaxY);
            }

            return new AabbF(minX, minY, maxX, maxY);
        }

        private double CenterSpreadX(int start, int count) {
            int end = start + count;
            double minimum = double.PositiveInfinity;
            double maximum = double.NegativeInfinity;
            for (int i = start; i < end; ++i) {
                AabbF bounds = this._edgeBounds[this._edgeOrder[i]];
                double center = (double)bounds.MinX + bounds.MaxX;
                minimum = Math.Min(minimum, center);
                maximum = Math.Max(maximum, center);
            }

            return maximum - minimum;
        }

        private double CenterSpreadY(int start, int count) {
            int end = start + count;
            double minimum = double.PositiveInfinity;
            double maximum = double.NegativeInfinity;
            for (int i = start; i < end; ++i) {
                AabbF bounds = this._edgeBounds[this._edgeOrder[i]];
                double center = (double)bounds.MinY + bounds.MaxY;
                minimum = Math.Min(minimum, center);
                maximum = Math.Max(maximum, center);
            }

            return maximum - minimum;
        }

        private void SelectNth(int start, int endExclusive, int nth, bool useX) {
            int left = start;
            int right = endExclusive - 1;
            while (left < right) {
                int pivotEdge = this.MedianOfThreeEdge(left, left + (right - left) / 2, right, useX);
                double pivotCenter = this.Center(pivotEdge, useX);
                int i = left;
                int j = right;

                while (i <= j) {
                    while (this.CompareToPivot(this._edgeOrder[i], pivotCenter, pivotEdge, useX) < 0) {
                        ++i;
                    }

                    while (this.CompareToPivot(this._edgeOrder[j], pivotCenter, pivotEdge, useX) > 0) {
                        --j;
                    }

                    if (i <= j) {
                        (this._edgeOrder[j], this._edgeOrder[i]) = (this._edgeOrder[i], this._edgeOrder[j]);
                        ++i;
                        --j;
                    }
                }

                if (nth <= j) {
                    right = j;
                } else if (nth >= i) {
                    left = i;
                } else {
                    return;
                }
            }
        }

        private int MedianOfThreeEdge(int a, int b, int c, bool useX) {
            int ea = this._edgeOrder[a];
            int eb = this._edgeOrder[b];
            int ec = this._edgeOrder[c];
            if (this.CompareEdges(ea, eb, useX) > 0) {
                (eb, ea) = (ea, eb);
            }

            if (this.CompareEdges(eb, ec, useX) > 0) {
                eb = ec;
            }

            if (this.CompareEdges(ea, eb, useX) > 0) {
                eb = ea;
            }

            return eb;
        }

        private int CompareEdges(int first, int second, bool useX) {
            double firstCenter = this.Center(first, useX);
            double secondCenter = this.Center(second, useX);
            int centerComparison = firstCenter.CompareTo(secondCenter);
            return centerComparison != 0 ? centerComparison : first.CompareTo(second);
        }

        private int CompareToPivot(int edge, double pivotCenter, int pivotEdge, bool useX) {
            int centerComparison = this.Center(edge, useX).CompareTo(pivotCenter);
            return centerComparison != 0 ? centerComparison : edge.CompareTo(pivotEdge);
        }

        private double Center(int edgeIndex, bool useX) {
            AabbF bounds = this._edgeBounds[edgeIndex];
            return useX
                ? (double)bounds.MinX + bounds.MaxX
                : (double)bounds.MinY + bounds.MaxY;
        }

        private readonly struct Node {
            private Node(AabbF bounds, int left, int right, int start, int count) {
                this.Bounds = bounds;
                this.Left = left;
                this.Right = right;
                this.Start = start;
                this.Count = count;
            }

            internal AabbF Bounds { get; }
            internal int Left { get; }
            internal int Right { get; }
            internal int Start { get; }
            internal int Count { get; }

            internal static Node CreateLeaf(AabbF bounds, int start, int count) {
                return new Node(bounds, -1, -1, start, count);
            }

            internal static Node CreateBranch(AabbF bounds, int left, int right) {
                return new Node(bounds, left, right, 0, 0);
            }
        }
    }
}
