using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ZoomTracks.CollisionDetection
{
    /// <summary>
    /// Exact closed-segment predicates for finite binary32 endpoints.
    /// Binary64 is only a certified sign filter; uncertain orientation signs use BigInteger.
    /// The certificate assumes ordinary IEEE binary64 round-to-nearest evaluation without
    /// unsafe reassociation (the normal managed .NET/Unity mode, not an unverified fast-math path).
    /// </summary>
    public static class ExactSegmentPredicates
    {
        // Shewchuk's orient2d "A" error bound for IEEE binary64 round-to-nearest.
        private const double OrientationErrorBound = 3.3306690738754716e-16;

        public static bool Intersects(
            FloatPoint a,
            FloatPoint b,
            FloatPoint c,
            FloatPoint d)
        {
            Aabb firstBounds = Aabb.FromSegment(a, b);
            Aabb secondBounds = Aabb.FromSegment(c, d);
            return IntersectsWithKnownBounds(a, b, firstBounds, c, d, secondBounds);
        }

        internal static bool IntersectsWithKnownBounds(
            FloatPoint a,
            FloatPoint b,
            Aabb firstBounds,
            FloatPoint c,
            FloatPoint d,
            Aabb secondBounds)
        {
            if (!firstBounds.Overlaps(secondBounds))
            {
                return false;
            }

            int abc = OrientationSign(a, b, c);
            int abd = OrientationSign(a, b, d);
            int cda = OrientationSign(c, d, a);
            int cdb = OrientationSign(c, d, b);

            if (abc == 0 && OnClosedSegment(a, b, c))
            {
                return true;
            }

            if (abd == 0 && OnClosedSegment(a, b, d))
            {
                return true;
            }

            if (cda == 0 && OnClosedSegment(c, d, a))
            {
                return true;
            }

            if (cdb == 0 && OnClosedSegment(c, d, b))
            {
                return true;
            }

            return HaveOppositeSigns(abc, abd) && HaveOppositeSigns(cda, cdb);
        }

        /// <summary>
        /// Returns -1, 0, or +1 for the exact orientation of (a,b,c).
        /// </summary>
        public static int OrientationSign(FloatPoint a, FloatPoint b, FloatPoint c)
        {
            double acx = (double)a.X - c.X;
            double bcx = (double)b.X - c.X;
            double acy = (double)a.Y - c.Y;
            double bcy = (double)b.Y - c.Y;

            double determinantLeft = acx * bcy;
            double determinantRight = acy * bcx;
            double determinant = determinantLeft - determinantRight;

            double determinantSum;
            if (determinantLeft > 0.0)
            {
                if (determinantRight <= 0.0)
                {
                    return Sign(determinant);
                }

                determinantSum = determinantLeft + determinantRight;
            }
            else if (determinantLeft < 0.0)
            {
                if (determinantRight >= 0.0)
                {
                    return Sign(determinant);
                }

                determinantSum = -determinantLeft - determinantRight;
            }
            else
            {
                return determinantRight == 0.0 ? ExactOrientationSign(a, b, c) : Sign(determinant);
            }

            double errorBound = OrientationErrorBound * determinantSum;
            if (determinant > errorBound)
            {
                return 1;
            }

            if (determinant < -errorBound)
            {
                return -1;
            }

            return ExactOrientationSign(a, b, c);
        }

        private static bool OnClosedSegment(FloatPoint a, FloatPoint b, FloatPoint point)
        {
            return Math.Min(a.X, b.X) <= point.X &&
                point.X <= Math.Max(a.X, b.X) &&
                Math.Min(a.Y, b.Y) <= point.Y &&
                point.Y <= Math.Max(a.Y, b.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HaveOppositeSigns(int first, int second)
        {
            return (first < 0 && second > 0) || (first > 0 && second < 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Sign(double value)
        {
            return value > 0.0 ? 1 : value < 0.0 ? -1 : 0;
        }

        private static int ExactOrientationSign(FloatPoint a, FloatPoint b, FloatPoint c)
        {
            BigInteger ax = ToScaledInteger(a.X);
            BigInteger ay = ToScaledInteger(a.Y);
            BigInteger bx = ToScaledInteger(b.X);
            BigInteger by = ToScaledInteger(b.Y);
            BigInteger cx = ToScaledInteger(c.X);
            BigInteger cy = ToScaledInteger(c.Y);

            BigInteger determinant =
                ((bx - ax) * (cy - ay)) -
                ((by - ay) * (cx - ax));
            return determinant.Sign;
        }

        // Every finite binary32 number multiplied by 2^149 is an integer.
        internal static BigInteger ToScaledInteger(float value)
        {
            uint bits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
            uint exponent = (bits >> 23) & 0xffU;
            uint fraction = bits & 0x7fffffU;

            uint significand;
            int shift;
            if (exponent == 0U)
            {
                significand = fraction;
                shift = 0;
            }
            else
            {
                significand = 0x800000U | fraction;
                shift = checked((int)exponent - 1);
            }

            BigInteger result = new BigInteger(significand) << shift;
            return (bits & 0x80000000U) == 0U ? result : -result;
        }
    }
}
