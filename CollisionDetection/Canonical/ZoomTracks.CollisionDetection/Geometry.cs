using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZoomTracks.CollisionDetection
{
    internal readonly struct PointF
    {
        internal PointF(float x, float y)
        {
            X = x;
            Y = y;
        }

        internal float X { get; }
        internal float Y { get; }
    }

    internal readonly struct AabbF
    {
        internal AabbF(float minX, float minY, float maxX, float maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        internal float MinX { get; }
        internal float MinY { get; }
        internal float MaxX { get; }
        internal float MaxY { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Overlaps(in AabbF other)
        {
            return MinX <= other.MaxX
                && MaxX >= other.MinX
                && MinY <= other.MaxY
                && MaxY >= other.MinY;
        }

        internal static AabbF FromSegment(in PointF a, in PointF b)
        {
            return new AabbF(
                Math.Min(a.X, b.X),
                Math.Min(a.Y, b.Y),
                Math.Max(a.X, b.X),
                Math.Max(a.Y, b.Y));
        }
    }

    internal readonly struct RectangleQuad
    {
        internal RectangleQuad(PointF p0, PointF p1, PointF p2, PointF p3)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
            P3 = p3;

            float minX = Math.Min(Math.Min(p0.X, p1.X), Math.Min(p2.X, p3.X));
            float minY = Math.Min(Math.Min(p0.Y, p1.Y), Math.Min(p2.Y, p3.Y));
            float maxX = Math.Max(Math.Max(p0.X, p1.X), Math.Max(p2.X, p3.X));
            float maxY = Math.Max(Math.Max(p0.Y, p1.Y), Math.Max(p2.Y, p3.Y));
            Bounds = new AabbF(minX, minY, maxX, maxY);
        }

        internal PointF P0 { get; }
        internal PointF P1 { get; }
        internal PointF P2 { get; }
        internal PointF P3 { get; }
        internal AabbF Bounds { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IntersectsSegment(in PointF a, in PointF b)
        {
            return RobustPredicates.SegmentsIntersect(a, b, P0, P1)
                || RobustPredicates.SegmentsIntersect(a, b, P1, P2)
                || RobustPredicates.SegmentsIntersect(a, b, P2, P3)
                || RobustPredicates.SegmentsIntersect(a, b, P3, P0);
        }
    }

    internal static class RectangleTransformer
    {
        private const double DegreesToRadians = Math.PI / 180.0;

        internal static RectangleQuad Transform(
            RectangleLocalBounds bounds,
            RectanglePose pose)
        {
            double radians = (double)pose.RotationDegrees * DegreesToRadians;
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);

            PointF p0 = TransformPoint(bounds.MinX, bounds.MinY, pose, cosine, sine);
            PointF p1 = TransformPoint(bounds.MaxX, bounds.MinY, pose, cosine, sine);
            PointF p2 = TransformPoint(bounds.MaxX, bounds.MaxY, pose, cosine, sine);
            PointF p3 = TransformPoint(bounds.MinX, bounds.MaxY, pose, cosine, sine);
            return new RectangleQuad(p0, p1, p2, p3);
        }

        private static PointF TransformPoint(
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

            if (!Guard.IsFinite(worldX) || !Guard.IsFinite(worldY))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pose),
                    "The transformed rectangle must have finite binary32 coordinates.");
            }

            return new PointF(worldX, worldY);
        }
    }

    internal static class RobustPredicates
    {
        // Shewchuk's orient2d first-stage error bound for IEEE binary64.
        private const double CcwErrorBoundA = 3.3306690738754716e-16;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool SegmentsIntersect(
            in PointF a,
            in PointF b,
            in PointF c,
            in PointF d)
        {
            AabbF firstBounds = AabbF.FromSegment(a, b);
            AabbF secondBounds = AabbF.FromSegment(c, d);
            if (!firstBounds.Overlaps(secondBounds))
            {
                return false;
            }

            int abc = OrientationSign(a, b, c);
            int abd = OrientationSign(a, b, d);
            int cda = OrientationSign(c, d, a);
            int cdb = OrientationSign(c, d, b);

            if (abc == 0 && IsOnClosedSegment(a, b, c))
            {
                return true;
            }

            if (abd == 0 && IsOnClosedSegment(a, b, d))
            {
                return true;
            }

            if (cda == 0 && IsOnClosedSegment(c, d, a))
            {
                return true;
            }

            if (cdb == 0 && IsOnClosedSegment(c, d, b))
            {
                return true;
            }

            return abc != abd && cda != cdb;
        }

        internal static int OrientationSign(in PointF a, in PointF b, in PointF c)
        {
            // Some native targets can enable flush-to-zero for binary32 arithmetic.
            // Route subnormal inputs directly through the bit-decoded exact path so
            // the filter never depends on a floating conversion preserving them.
            if (IsSubnormal(a.X)
                || IsSubnormal(a.Y)
                || IsSubnormal(b.X)
                || IsSubnormal(b.Y)
                || IsSubnormal(c.X)
                || IsSubnormal(c.Y))
            {
                return ExactOrientationSign(a, b, c);
            }

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
                    return SignOrExact(determinant, a, b, c);
                }

                determinantSum = determinantLeft + determinantRight;
            }
            else if (determinantLeft < 0.0)
            {
                if (determinantRight >= 0.0)
                {
                    return SignOrExact(determinant, a, b, c);
                }

                determinantSum = -determinantLeft - determinantRight;
            }
            else
            {
                return SignOrExact(determinant, a, b, c);
            }

            double errorBound = CcwErrorBoundA * determinantSum;
            if (determinant >= errorBound)
            {
                return 1;
            }

            if (-determinant >= errorBound)
            {
                return -1;
            }

            return ExactOrientationSign(a, b, c);
        }

        private static int SignOrExact(
            double value,
            in PointF a,
            in PointF b,
            in PointF c)
        {
            if (value > 0.0)
            {
                return 1;
            }

            if (value < 0.0)
            {
                return -1;
            }

            return ExactOrientationSign(a, b, c);
        }

        private static bool IsSubnormal(float value)
        {
            SingleBits bits = new SingleBits(value);
            int magnitude = bits.Bits & 0x7fffffff;
            return magnitude != 0 && (magnitude & 0x7f800000) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsOnClosedSegment(in PointF a, in PointF b, in PointF p)
        {
            return p.X >= Math.Min(a.X, b.X)
                && p.X <= Math.Max(a.X, b.X)
                && p.Y >= Math.Min(a.Y, b.Y)
                && p.Y <= Math.Max(a.Y, b.Y);
        }

        private static int ExactOrientationSign(in PointF a, in PointF b, in PointF c)
        {
            Dyadic ax = Dyadic.FromSingle(a.X);
            Dyadic ay = Dyadic.FromSingle(a.Y);
            Dyadic bx = Dyadic.FromSingle(b.X);
            Dyadic by = Dyadic.FromSingle(b.Y);
            Dyadic cx = Dyadic.FromSingle(c.X);
            Dyadic cy = Dyadic.FromSingle(c.Y);

            int commonExponent = int.MaxValue;
            IncludeExponent(ax, ref commonExponent);
            IncludeExponent(ay, ref commonExponent);
            IncludeExponent(bx, ref commonExponent);
            IncludeExponent(by, ref commonExponent);
            IncludeExponent(cx, ref commonExponent);
            IncludeExponent(cy, ref commonExponent);

            if (commonExponent == int.MaxValue)
            {
                return 0;
            }

            BigInteger axi = ax.ToIntegerAtExponent(commonExponent);
            BigInteger ayi = ay.ToIntegerAtExponent(commonExponent);
            BigInteger bxi = bx.ToIntegerAtExponent(commonExponent);
            BigInteger byi = by.ToIntegerAtExponent(commonExponent);
            BigInteger cxi = cx.ToIntegerAtExponent(commonExponent);
            BigInteger cyi = cy.ToIntegerAtExponent(commonExponent);
            BigInteger determinant = ((bxi - axi) * (cyi - ayi))
                - ((byi - ayi) * (cxi - axi));
            return determinant.Sign;
        }

        private static void IncludeExponent(Dyadic value, ref int minimum)
        {
            if (value.Significand != 0 && value.Exponent < minimum)
            {
                minimum = value.Exponent;
            }
        }

        private readonly struct Dyadic
        {
            private Dyadic(int significand, int exponent)
            {
                Significand = significand;
                Exponent = exponent;
            }

            internal int Significand { get; }
            internal int Exponent { get; }

            internal BigInteger ToIntegerAtExponent(int commonExponent)
            {
                if (Significand == 0)
                {
                    return BigInteger.Zero;
                }

                return new BigInteger(Significand) << (Exponent - commonExponent);
            }

            internal static Dyadic FromSingle(float value)
            {
                SingleBits union = new SingleBits(value);
                int bits = union.Bits;
                int magnitude = bits & 0x7fffffff;
                int rawExponent = (magnitude >> 23) & 0xff;
                int fraction = magnitude & 0x7fffff;

                if (rawExponent == 0 && fraction == 0)
                {
                    return new Dyadic(0, 0);
                }

                int significand;
                int exponent;
                if (rawExponent == 0)
                {
                    significand = fraction;
                    exponent = -149;
                }
                else
                {
                    significand = (1 << 23) | fraction;
                    exponent = rawExponent - 150;
                }

                if (bits < 0)
                {
                    significand = -significand;
                }

                return new Dyadic(significand, exponent);
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct SingleBits
        {
            internal SingleBits(float value)
            {
                Bits = 0;
                Value = value;
            }

            [FieldOffset(0)]
            internal float Value;

            [FieldOffset(0)]
            internal int Bits;
        }
    }
}
