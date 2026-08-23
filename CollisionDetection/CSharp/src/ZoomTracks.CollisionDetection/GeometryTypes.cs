using System;
using System.Collections.Generic;
using System.Globalization;

namespace ZoomTracks.CollisionDetection
{
    /// <summary>
    /// A dependency-free binary32 point. Unity callers can copy Vector2.x/y into this type.
    /// </summary>
    public readonly struct CoordinateXY
    {
        public CoordinateXY(float x, float y)
        {
            if (!IsFinite(x))
            {
                throw new ArgumentOutOfRangeException(nameof(x), "Coordinates must be finite float values.");
            }

            if (!IsFinite(y))
            {
                throw new ArgumentOutOfRangeException(nameof(y), "Coordinates must be finite float values.");
            }

            X = x;
            Y = y;
        }

        public float X { get; }

        public float Y { get; }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:R}, {1:R})",
                X,
                Y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Four authoritative binary32 vertices in cyclic perimeter order.
    /// No idealized rectangle is reconstructed.
    /// </summary>
    public readonly struct ConvexQuadrilateralOutline
    {
        public ConvexQuadrilateralOutline(CoordinateXY p0, CoordinateXY p1, CoordinateXY p2, CoordinateXY p3)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
            P3 = p3;
        }

        public CoordinateXY P0 { get; }

        public CoordinateXY P1 { get; }

        public CoordinateXY P2 { get; }

        public CoordinateXY P3 { get; }

        public CoordinateXY GetVertex(int index)
        {
            switch (index)
            {
                case 0:
                    return P0;
                case 1:
                    return P1;
                case 2:
                    return P2;
                case 3:
                    return P3;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        internal Aabb Bounds
        {
            get
            {
                Aabb bounds = Aabb.FromSegment(P0, P1);
                bounds = Aabb.Union(bounds, Aabb.FromSegment(P2, P3));
                return bounds;
            }
        }
    }

    /// <summary>
    /// A preprocessed, immutable outline-edge query structure.
    /// Implementations are safe for concurrent queries after construction.
    /// </summary>
    public interface ICollisionDetector
    {
        bool IsColliding(ConvexQuadrilateralOutline outline);
    }

    internal readonly struct Aabb
    {
        public Aabb(float minX, float minY, float maxX, float maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public float MinX { get; }

        public float MinY { get; }

        public float MaxX { get; }

        public float MaxY { get; }

        public double CenterX
        {
            get { return ((double)MinX + MaxX) * 0.5; }
        }

        public double CenterY
        {
            get { return ((double)MinY + MaxY) * 0.5; }
        }

        public static Aabb FromSegment(CoordinateXY a, CoordinateXY b)
        {
            return new Aabb(
                Math.Min(a.X, b.X),
                Math.Min(a.Y, b.Y),
                Math.Max(a.X, b.X),
                Math.Max(a.Y, b.Y));
        }

        public static Aabb Union(Aabb a, Aabb b)
        {
            return new Aabb(
                Math.Min(a.MinX, b.MinX),
                Math.Min(a.MinY, b.MinY),
                Math.Max(a.MaxX, b.MaxX),
                Math.Max(a.MaxY, b.MaxY));
        }

        public bool Overlaps(Aabb other)
        {
            return MinX <= other.MaxX &&
                other.MinX <= MaxX &&
                MinY <= other.MaxY &&
                other.MinY <= MaxY;
        }
    }

    internal readonly struct OutlineSegment
    {
        public OutlineSegment(CoordinateXY a, CoordinateXY b)
        {
            A = a;
            B = b;
            Bounds = Aabb.FromSegment(a, b);
        }

        public CoordinateXY A { get; }

        public CoordinateXY B { get; }

        public Aabb Bounds { get; }
    }

    internal static class OutlineData
    {
        public static OutlineSegment[] CopySegments(
            IReadOnlyList<CoordinateXY> outline1,
            IReadOnlyList<CoordinateXY> outline2)
        {
            if (outline1 == null)
            {
                throw new ArgumentNullException(nameof(outline1));
            }

            if (outline2 == null)
            {
                throw new ArgumentNullException(nameof(outline2));
            }

            ValidateOutline(outline1, nameof(outline1));
            ValidateOutline(outline2, nameof(outline2));

            var segments = new OutlineSegment[checked(outline1.Count + outline2.Count)];
            CopyOneOutline(outline1, segments, 0);
            CopyOneOutline(outline2, segments, outline1.Count);
            return segments;
        }

        public static Aabb ComputeBounds(OutlineSegment[] segments)
        {
            Aabb bounds = segments[0].Bounds;
            for (int i = 1; i < segments.Length; ++i)
            {
                bounds = Aabb.Union(bounds, segments[i].Bounds);
            }

            return bounds;
        }

        private static void ValidateOutline(IReadOnlyList<CoordinateXY> outline, string argumentName)
        {
            if (outline.Count < 3)
            {
                throw new ArgumentException("Each outline must contain at least three vertices.", argumentName);
            }

            for (int i = 0; i < outline.Count; ++i)
            {
                CoordinateXY a = outline[i];
                CoordinateXY b = outline[(i + 1) % outline.Count];
                if (a.X == b.X && a.Y == b.Y)
                {
                    throw new ArgumentException("Every outline segment must have positive length.", argumentName);
                }
            }
        }

        private static void CopyOneOutline(
            IReadOnlyList<CoordinateXY> outline,
            OutlineSegment[] destination,
            int destinationOffset)
        {
            for (int i = 0; i < outline.Count; ++i)
            {
                destination[destinationOffset + i] = new OutlineSegment(
                    outline[i],
                    outline[(i + 1) % outline.Count]);
            }
        }
    }

    public abstract class OutlineIndexBase : ICollisionDetector
    {
        protected OutlineIndexBase(
            IReadOnlyList<CoordinateXY> outline1,
            IReadOnlyList<CoordinateXY> outline2)
        {
            Segments = OutlineData.CopySegments(outline1, outline2);
            OutlineBounds = OutlineData.ComputeBounds(Segments);
        }

        private protected OutlineSegment[] Segments { get; }

        private protected Aabb OutlineBounds { get; }

        public abstract bool IsColliding(ConvexQuadrilateralOutline outline);
    }
}
