using System;
using System.Collections.Generic;
using System.Numerics;
using ZoomTracks.CollisionDetection;

namespace CollisionDetection.Harness
{
    /// <summary>
    /// Test-only oracle which shares no geometry or indexing code with the production
    /// implementations. Every binary32 coordinate is decoded onto the common 2^-149
    /// integer lattice, and every geometric predicate is evaluated with BigInteger.
    /// </summary>
    internal static class IndependentOracle
    {
        private const double DegreesToRadians = Math.PI / 180.0;

        internal static OracleRectangle Transform(
            RectangleLocalBounds localBounds,
            RectanglePose pose)
        {
            ValidateQuery(localBounds, pose);

            // Keep this sequence explicit. It independently mirrors the documented
            // binary64 policy used to define the rectangle's binary32 vertices.
            double radians = (double)pose.RotationDegrees * DegreesToRadians;
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);

            OraclePoint p0 = TransformPoint(
                localBounds.MinX,
                localBounds.MinY,
                pose,
                cosine,
                sine);
            OraclePoint p1 = TransformPoint(
                localBounds.MaxX,
                localBounds.MinY,
                pose,
                cosine,
                sine);
            OraclePoint p2 = TransformPoint(
                localBounds.MaxX,
                localBounds.MaxY,
                pose,
                cosine,
                sine);
            OraclePoint p3 = TransformPoint(
                localBounds.MinX,
                localBounds.MaxY,
                pose,
                cosine,
                sine);
            return new OracleRectangle(p0, p1, p2, p3);
        }

        internal static bool IsColliding(
            IReadOnlyList<CoordinateXY> outline1,
            IReadOnlyList<CoordinateXY> outline2,
            RectangleLocalBounds localBounds,
            RectanglePose pose)
        {
            return Prepare(outline1, outline2).IsColliding(localBounds, pose);
        }

        internal static bool IsColliding(
            IReadOnlyList<CoordinateXY> outline1,
            IReadOnlyList<CoordinateXY> outline2,
            OracleRectangle rectangle)
        {
            return Prepare(outline1, outline2).IsColliding(rectangle);
        }

        internal static PreparedOutlines Prepare(
            IReadOnlyList<CoordinateXY> outline1,
            IReadOnlyList<CoordinateXY> outline2)
        {
            ArgumentNullException.ThrowIfNull(outline1, nameof(outline1));
            ArgumentNullException.ThrowIfNull(outline2, nameof(outline2));

            return new PreparedOutlines(outline1, outline2);
        }

        internal static bool SegmentsIntersect(
            CoordinateXY a,
            CoordinateXY b,
            CoordinateXY c,
            CoordinateXY d)
        {
            return SegmentsIntersect(
                ExactPoint.Decode(a),
                ExactPoint.Decode(b),
                ExactPoint.Decode(c),
                ExactPoint.Decode(d));
        }

        internal static int OrientationSign(
            CoordinateXY a,
            CoordinateXY b,
            CoordinateXY c)
        {
            return OrientationSign(
                ExactPoint.Decode(a),
                ExactPoint.Decode(b),
                ExactPoint.Decode(c));
        }

        internal static BigInteger ToBinary32LatticeInteger(float value)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "The value must be finite.");
            }

            uint bits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
            uint rawExponent = (bits >> 23) & 0xffU;
            uint fraction = bits & 0x7fffffU;
            uint magnitude = rawExponent == 0U ? fraction : 0x800000U | fraction;
            if (magnitude == 0U)
            {
                return BigInteger.Zero;
            }

            int shift = rawExponent == 0U ? 0 : checked((int)rawExponent - 1);
            BigInteger integer = new BigInteger(magnitude) << shift;
            return (bits & 0x80000000U) == 0U ? integer : -integer;
        }

        private static OraclePoint TransformPoint(
            float localX,
            float localY,
            RectanglePose pose,
            double cosine,
            double sine)
        {
            double xCosine = (double)localX * cosine;
            double ySine = (double)localY * sine;
            double xSine = (double)localX * sine;
            double yCosine = (double)localY * cosine;
            double worldXDouble = (double)pose.PositionX + xCosine + ySine;
            double worldYDouble = (double)pose.PositionY - xSine + yCosine;
            float worldX = (float)worldXDouble;
            float worldY = (float)worldYDouble;

            if (!IsFinite(worldX) || !IsFinite(worldY))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pose),
                    "The transformed rectangle must have finite binary32 coordinates.");
            }

            return new OraclePoint(worldX, worldY);
        }

        private static void ValidateQuery(
            RectangleLocalBounds localBounds,
            RectanglePose pose)
        {
            if (!IsFinite(localBounds.MinX)
                || !IsFinite(localBounds.MinY)
                || !IsFinite(localBounds.MaxX)
                || !IsFinite(localBounds.MaxY)
                || !(localBounds.MinX < localBounds.MaxX)
                || !(localBounds.MinY < localBounds.MaxY))
            {
                throw new ArgumentException(
                    "The rectangle bounds must be finite and have positive extents.",
                    nameof(localBounds));
            }

            if (!IsFinite(pose.PositionX)
                || !IsFinite(pose.PositionY)
                || !IsFinite(pose.RotationDegrees))
            {
                throw new ArgumentException("The rectangle pose must be finite.", nameof(pose));
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool SegmentsIntersect(
            ExactPoint a,
            ExactPoint b,
            ExactPoint c,
            ExactPoint d)
        {
            if (!BoxesOverlap(a, b, c, d))
            {
                return false;
            }

            int abc = OrientationSign(a, b, c);
            int abd = OrientationSign(a, b, d);
            int cda = OrientationSign(c, d, a);
            int cdb = OrientationSign(c, d, b);

            if (abc == 0 && IsInClosedBox(a, b, c))
            {
                return true;
            }

            if (abd == 0 && IsInClosedBox(a, b, d))
            {
                return true;
            }

            if (cda == 0 && IsInClosedBox(c, d, a))
            {
                return true;
            }

            if (cdb == 0 && IsInClosedBox(c, d, b))
            {
                return true;
            }

            return AreStrictlyOpposite(abc, abd) && AreStrictlyOpposite(cda, cdb);
        }

        private static int OrientationSign(ExactPoint a, ExactPoint b, ExactPoint c)
        {
            BigInteger abX = b.X - a.X;
            BigInteger abY = b.Y - a.Y;
            BigInteger acX = c.X - a.X;
            BigInteger acY = c.Y - a.Y;
            return ((abX * acY) - (abY * acX)).Sign;
        }

        private static bool BoxesOverlap(
            ExactPoint a,
            ExactPoint b,
            ExactPoint c,
            ExactPoint d)
        {
            return BigInteger.Min(a.X, b.X) <= BigInteger.Max(c.X, d.X)
                && BigInteger.Min(c.X, d.X) <= BigInteger.Max(a.X, b.X)
                && BigInteger.Min(a.Y, b.Y) <= BigInteger.Max(c.Y, d.Y)
                && BigInteger.Min(c.Y, d.Y) <= BigInteger.Max(a.Y, b.Y);
        }

        private static bool IsInClosedBox(ExactPoint a, ExactPoint b, ExactPoint point)
        {
            return BigInteger.Min(a.X, b.X) <= point.X
                && point.X <= BigInteger.Max(a.X, b.X)
                && BigInteger.Min(a.Y, b.Y) <= point.Y
                && point.Y <= BigInteger.Max(a.Y, b.Y);
        }

        private static bool AreStrictlyOpposite(int first, int second)
        {
            return (first < 0 && second > 0) || (first > 0 && second < 0);
        }

        internal readonly struct OraclePoint
        {
            internal OraclePoint(float x, float y)
            {
                X = x;
                Y = y;
            }

            internal float X { get; }
            internal float Y { get; }
        }

        internal readonly struct OracleRectangle
        {
            internal OracleRectangle(
                OraclePoint p0,
                OraclePoint p1,
                OraclePoint p2,
                OraclePoint p3)
            {
                P0 = p0;
                P1 = p1;
                P2 = p2;
                P3 = p3;
            }

            internal OraclePoint P0 { get; }
            internal OraclePoint P1 { get; }
            internal OraclePoint P2 { get; }
            internal OraclePoint P3 { get; }
        }

        internal sealed class PreparedOutlines
        {
            private readonly ExactSegment[] _segments;

            internal PreparedOutlines(
                IReadOnlyList<CoordinateXY> outline1,
                IReadOnlyList<CoordinateXY> outline2)
            {
                _segments = new ExactSegment[checked(outline1.Count + outline2.Count)];
                DecodeOutline(outline1, 0);
                DecodeOutline(outline2, outline1.Count);
            }

            internal bool IsColliding(
                RectangleLocalBounds localBounds,
                RectanglePose pose)
            {
                return IsColliding(Transform(localBounds, pose));
            }

            internal bool IsColliding(OracleRectangle rectangle)
            {
                ExactPoint p0 = ExactPoint.Decode(rectangle.P0);
                ExactPoint p1 = ExactPoint.Decode(rectangle.P1);
                ExactPoint p2 = ExactPoint.Decode(rectangle.P2);
                ExactPoint p3 = ExactPoint.Decode(rectangle.P3);

                for (int i = 0; i < _segments.Length; ++i)
                {
                    ExactSegment segment = _segments[i];
                    if (SegmentsIntersect(p0, p1, segment.A, segment.B)
                        || SegmentsIntersect(p1, p2, segment.A, segment.B)
                        || SegmentsIntersect(p2, p3, segment.A, segment.B)
                        || SegmentsIntersect(p3, p0, segment.A, segment.B))
                    {
                        return true;
                    }
                }

                return false;
            }

            private void DecodeOutline(IReadOnlyList<CoordinateXY> outline, int offset)
            {
                for (int i = 0; i < outline.Count; ++i)
                {
                    _segments[offset + i] = new ExactSegment(
                        ExactPoint.Decode(outline[i]),
                        ExactPoint.Decode(outline[i + 1 == outline.Count ? 0 : i + 1]));
                }
            }
        }

        private readonly struct ExactSegment
        {
            internal ExactSegment(ExactPoint a, ExactPoint b)
            {
                A = a;
                B = b;
            }

            internal ExactPoint A { get; }
            internal ExactPoint B { get; }
        }

        private readonly struct ExactPoint
        {
            private ExactPoint(BigInteger x, BigInteger y)
            {
                X = x;
                Y = y;
            }

            internal BigInteger X { get; }
            internal BigInteger Y { get; }

            internal static ExactPoint Decode(CoordinateXY point)
            {
                return new ExactPoint(
                    ToBinary32LatticeInteger(point.X),
                    ToBinary32LatticeInteger(point.Y));
            }

            internal static ExactPoint Decode(OraclePoint point)
            {
                return new ExactPoint(
                    ToBinary32LatticeInteger(point.X),
                    ToBinary32LatticeInteger(point.Y));
            }
        }
    }
}
