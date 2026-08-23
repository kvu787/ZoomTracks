using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZoomTracks.CollisionDetection.Runner
{
    /// <summary>
    /// Test-only exact oracle. It deliberately shares no broad-phase or predicate helper
    /// with the production assembly.
    /// </summary>
    internal static class ReferenceOracle
    {
        public static int OrientationSign(FloatPoint a, FloatPoint b, FloatPoint c)
        {
            ExactPoint exactA = Decode(a);
            ExactPoint exactB = Decode(b);
            ExactPoint exactC = Decode(c);
            return Cross(exactB - exactA, exactC - exactA).Sign;
        }

        public static bool SegmentIntersects(
            FloatPoint a,
            FloatPoint b,
            FloatPoint c,
            FloatPoint d)
        {
            return SegmentIntersects(Decode(a), Decode(b), Decode(c), Decode(d));
        }

        public static bool ParametricSegmentIntersects(
            FloatPoint a,
            FloatPoint b,
            FloatPoint c,
            FloatPoint d)
        {
            ExactPoint exactA = Decode(a);
            ExactPoint exactB = Decode(b);
            ExactPoint exactC = Decode(c);
            ExactPoint exactD = Decode(d);

            if (!BoxesOverlap(exactA, exactB, exactC, exactD))
            {
                return false;
            }

            ExactPoint r = exactB - exactA;
            ExactPoint s = exactD - exactC;
            ExactPoint w = exactC - exactA;
            BigInteger denominator = Cross(r, s);
            BigInteger uNumerator = Cross(w, r);

            if (denominator.IsZero)
            {
                return uNumerator.IsZero;
            }

            BigInteger tNumerator = Cross(w, s);
            if (denominator.Sign > 0)
            {
                return tNumerator.Sign >= 0 &&
                    tNumerator <= denominator &&
                    uNumerator.Sign >= 0 &&
                    uNumerator <= denominator;
            }

            return tNumerator.Sign <= 0 &&
                tNumerator >= denominator &&
                uNumerator.Sign <= 0 &&
                uNumerator >= denominator;
        }

        public static PreparedOutlines Prepare(
            IReadOnlyList<FloatPoint> outline1,
            IReadOnlyList<FloatPoint> outline2)
        {
            return new PreparedOutlines(outline1, outline2);
        }

        private static bool SegmentIntersects(
            ExactPoint a,
            ExactPoint b,
            ExactPoint c,
            ExactPoint d)
        {
            if (!BoxesOverlap(a, b, c, d))
            {
                return false;
            }

            int abc = Cross(b - a, c - a).Sign;
            int abd = Cross(b - a, d - a).Sign;
            int cda = Cross(d - c, a - c).Sign;
            int cdb = Cross(d - c, b - c).Sign;

            if (abc == 0 && InBox(a, b, c))
            {
                return true;
            }

            if (abd == 0 && InBox(a, b, d))
            {
                return true;
            }

            if (cda == 0 && InBox(c, d, a))
            {
                return true;
            }

            if (cdb == 0 && InBox(c, d, b))
            {
                return true;
            }

            return Opposite(abc, abd) && Opposite(cda, cdb);
        }

        private static bool BoxesOverlap(
            ExactPoint a,
            ExactPoint b,
            ExactPoint c,
            ExactPoint d)
        {
            return BigInteger.Min(a.X, b.X) <= BigInteger.Max(c.X, d.X) &&
                BigInteger.Min(c.X, d.X) <= BigInteger.Max(a.X, b.X) &&
                BigInteger.Min(a.Y, b.Y) <= BigInteger.Max(c.Y, d.Y) &&
                BigInteger.Min(c.Y, d.Y) <= BigInteger.Max(a.Y, b.Y);
        }

        private static bool InBox(ExactPoint a, ExactPoint b, ExactPoint point)
        {
            return BigInteger.Min(a.X, b.X) <= point.X &&
                point.X <= BigInteger.Max(a.X, b.X) &&
                BigInteger.Min(a.Y, b.Y) <= point.Y &&
                point.Y <= BigInteger.Max(a.Y, b.Y);
        }

        private static bool Opposite(int first, int second)
        {
            return (first < 0 && second > 0) || (first > 0 && second < 0);
        }

        private static BigInteger Cross(ExactPoint a, ExactPoint b)
        {
            return (a.X * b.Y) - (a.Y * b.X);
        }

        private static ExactPoint Decode(FloatPoint point)
        {
            return new ExactPoint(Decode(point.X), Decode(point.Y));
        }

        private static BigInteger Decode(float value)
        {
            uint bits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
            uint rawExponent = (bits >> 23) & 0xffU;
            uint fraction = bits & 0x7fffffU;
            uint magnitude = rawExponent == 0U ? fraction : 0x800000U | fraction;
            int shift = rawExponent == 0U ? 0 : checked((int)rawExponent - 1);
            BigInteger decoded = new BigInteger(magnitude) << shift;
            return (bits & 0x80000000U) == 0U ? decoded : -decoded;
        }

        internal sealed class PreparedOutlines
        {
            private readonly ExactSegment[] _segments;

            public PreparedOutlines(
                IReadOnlyList<FloatPoint> outline1,
                IReadOnlyList<FloatPoint> outline2)
            {
                _segments = new ExactSegment[outline1.Count + outline2.Count];
                Copy(outline1, 0);
                Copy(outline2, outline1.Count);
            }

            public bool Intersects(QueryPerimeter perimeter)
            {
                ExactPoint q0 = Decode(perimeter.P0);
                ExactPoint q1 = Decode(perimeter.P1);
                ExactPoint q2 = Decode(perimeter.P2);
                ExactPoint q3 = Decode(perimeter.P3);

                for (int i = 0; i < _segments.Length; ++i)
                {
                    ExactSegment segment = _segments[i];
                    if (SegmentIntersects(q0, q1, segment.A, segment.B) ||
                        SegmentIntersects(q1, q2, segment.A, segment.B) ||
                        SegmentIntersects(q2, q3, segment.A, segment.B) ||
                        SegmentIntersects(q3, q0, segment.A, segment.B))
                    {
                        return true;
                    }
                }

                return false;
            }

            private void Copy(IReadOnlyList<FloatPoint> outline, int offset)
            {
                for (int i = 0; i < outline.Count; ++i)
                {
                    _segments[offset + i] = new ExactSegment(
                        Decode(outline[i]),
                        Decode(outline[(i + 1) % outline.Count]));
                }
            }
        }

        private readonly struct ExactSegment
        {
            public ExactSegment(ExactPoint a, ExactPoint b)
            {
                A = a;
                B = b;
            }

            public ExactPoint A { get; }

            public ExactPoint B { get; }
        }

        private readonly struct ExactPoint
        {
            public ExactPoint(BigInteger x, BigInteger y)
            {
                X = x;
                Y = y;
            }

            public BigInteger X { get; }

            public BigInteger Y { get; }

            public static ExactPoint operator -(ExactPoint left, ExactPoint right)
            {
                return new ExactPoint(left.X - right.X, left.Y - right.Y);
            }
        }
    }
}
