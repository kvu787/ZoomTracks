using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ZoomTracks.CollisionDetection {
    public abstract class CollisionDetectorBase : ICollisionDetector {
        private readonly List<CoordinateXY> _outline1;
        private readonly List<CoordinateXY> _outline2;

        protected CollisionDetectorBase(
            List<CoordinateXY> outline1,
            List<CoordinateXY> outline2) {
            if (outline1 == null) {
                throw new ArgumentNullException(nameof(outline1));
            }

            if (outline2 == null) {
                throw new ArgumentNullException(nameof(outline2));
            }

            ValidateOutline(outline1, nameof(outline1));
            ValidateOutline(outline2, nameof(outline2));

            // Keep the transferred lists themselves. Index metadata in derived classes
            // refers back to these lists rather than copying their vertices.
            this._outline1 = outline1;
            this._outline2 = outline2;
            this.EdgeCount = outline1.Count + outline2.Count;
        }

        private protected int EdgeCount { get; }

        public bool IsColliding(RectangleLocalBounds localBounds, RectanglePose pose) {
            if (!localBounds.IsValid) {
                throw new ArgumentException(
                    "The rectangle bounds must be finite and have positive extents.",
                    nameof(localBounds));
            }

            if (!Guard.IsFinite(pose.PositionX)
                || !Guard.IsFinite(pose.PositionY)
                || !Guard.IsFinite(pose.RotationDegrees)) {
                throw new ArgumentException("The rectangle pose must be finite.", nameof(pose));
            }

            RectangleQuad rectangle = RectangleTransformer.Transform(localBounds, pose);
            return this.Query(rectangle);
        }

        private protected abstract bool Query(in RectangleQuad rectangle);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected void GetEdge(int edgeIndex, out PointF a, out PointF b) {
            List<CoordinateXY> outline;
            int localIndex;
            if (edgeIndex < this._outline1.Count) {
                outline = this._outline1;
                localIndex = edgeIndex;
            } else {
                outline = this._outline2;
                localIndex = edgeIndex - this._outline1.Count;
            }

            CoordinateXY first = outline[localIndex];
            CoordinateXY second = outline[localIndex + 1 == outline.Count ? 0 : localIndex + 1];
            a = new PointF(first.X, first.Y);
            b = new PointF(second.X, second.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected AabbF GetEdgeBounds(int edgeIndex) {
            this.GetEdge(edgeIndex, out PointF a, out PointF b);
            return AabbF.FromSegment(a, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected bool EdgeIntersectsRectangle(int edgeIndex, in RectangleQuad rectangle) {
            this.GetEdge(edgeIndex, out PointF a, out PointF b);
            AabbF edgeBounds = AabbF.FromSegment(a, b);
            return edgeBounds.Overlaps(rectangle.Bounds)
                && rectangle.IntersectsSegment(a, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected bool EdgeIntersectsRectangleAfterBoundsCheck(
            int edgeIndex,
            in RectangleQuad rectangle) {
            this.GetEdge(edgeIndex, out PointF a, out PointF b);
            return rectangle.IntersectsSegment(a, b);
        }

        private static void ValidateOutline(
            List<CoordinateXY> outline,
            string parameterName) {
            if (outline.Count < 3) {
                throw new ArgumentException(
                    "An outline must contain at least three vertices.",
                    parameterName);
            }

            for (int i = 0; i < outline.Count; ++i) {
                CoordinateXY current = outline[i];
                CoordinateXY next = outline[i + 1 == outline.Count ? 0 : i + 1];
                if (current.X == next.X && current.Y == next.Y) {
                    throw new ArgumentException(
                        "Every outline segment must have positive length.",
                        parameterName);
                }
            }
        }
    }
}
