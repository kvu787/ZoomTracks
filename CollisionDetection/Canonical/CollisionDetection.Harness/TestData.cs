using System;
using System.Collections.Generic;
using ZoomTracks.CollisionDetection;

namespace CollisionDetection.Harness
{
    internal readonly struct QueryInput
    {
        internal QueryInput(RectangleLocalBounds bounds, RectanglePose pose)
        {
            Bounds = bounds;
            Pose = pose;
        }

        internal RectangleLocalBounds Bounds { get; }
        internal RectanglePose Pose { get; }
    }

    internal sealed class OutlinePair
    {
        internal OutlinePair(CoordinateXY[] outline1, CoordinateXY[] outline2)
        {
            Outline1 = outline1;
            Outline2 = outline2;
        }

        internal CoordinateXY[] Outline1 { get; }
        internal CoordinateXY[] Outline2 { get; }
        internal int EdgeCount => Outline1.Length + Outline2.Length;

        internal List<CoordinateXY> CloneOutline1()
        {
            return new List<CoordinateXY>(Outline1);
        }

        internal List<CoordinateXY> CloneOutline2()
        {
            return new List<CoordinateXY>(Outline2);
        }
    }

    internal static class TestData
    {
        internal static OutlinePair NestedSquares(float outerHalfExtent, float innerHalfExtent)
        {
            return new OutlinePair(
                new[]
                {
                    new CoordinateXY(-outerHalfExtent, -outerHalfExtent),
                    new CoordinateXY(outerHalfExtent, -outerHalfExtent),
                    new CoordinateXY(outerHalfExtent, outerHalfExtent),
                    new CoordinateXY(-outerHalfExtent, outerHalfExtent),
                },
                new[]
                {
                    new CoordinateXY(-innerHalfExtent, -innerHalfExtent),
                    new CoordinateXY(innerHalfExtent, -innerHalfExtent),
                    new CoordinateXY(innerHalfExtent, innerHalfExtent),
                    new CoordinateXY(-innerHalfExtent, innerHalfExtent),
                });
        }

        internal static OutlinePair NestedSquaresAt(
            float centerX,
            float centerY,
            float outerHalfExtent,
            float innerHalfExtent)
        {
            return new OutlinePair(
                Rectangle(centerX, centerY, outerHalfExtent, outerHalfExtent),
                Rectangle(centerX, centerY, innerHalfExtent, innerHalfExtent));
        }

        internal static OutlinePair SmoothNestedLoops(
            int outerCount,
            int innerCount,
            float outerRadius,
            double aspect = 1.0,
            bool nonuniform = false)
        {
            CoordinateXY[] outer = RadialLoop(
                outerCount,
                outerRadius,
                aspect,
                0.035,
                0.17,
                nonuniform);
            CoordinateXY[] inner = RadialLoop(
                innerCount,
                outerRadius * 0.36f,
                aspect,
                0.025,
                0.91,
                nonuniform);
            return new OutlinePair(outer, inner);
        }

        internal static OutlinePair SubdividedNestedRectangles(
            int verticesPerSide,
            float outerHalfExtent,
            float innerHalfExtent)
        {
            return new OutlinePair(
                SubdividedRectangle(verticesPerSide, outerHalfExtent, outerHalfExtent),
                SubdividedRectangle(verticesPerSide, innerHalfExtent, innerHalfExtent));
        }

        internal static OutlinePair AlternatingRadiusNestedLoops(
            int outerCount,
            int innerCount,
            float minimumOuterRadius,
            float maximumOuterRadius,
            float innerRadius)
        {
            if (minimumOuterRadius <= innerRadius || maximumOuterRadius <= minimumOuterRadius)
            {
                throw new ArgumentException("Radii must define strictly nested loops.");
            }

            CoordinateXY[] outer = new CoordinateXY[outerCount];
            for (int i = 0; i < outerCount; ++i)
            {
                double angle = i * (Math.PI * 2.0 / outerCount);
                double radius = (i & 1) == 0 ? minimumOuterRadius : maximumOuterRadius;
                outer[i] = new CoordinateXY(
                    (float)(radius * Math.Cos(angle)),
                    (float)(radius * Math.Sin(angle)));
            }

            CoordinateXY[] inner = RadialLoop(
                innerCount,
                innerRadius,
                1.0,
                0.0,
                0.0,
                false);
            return new OutlinePair(outer, inner);
        }

        internal static CoordinateXY[] Reverse(CoordinateXY[] source)
        {
            CoordinateXY[] result = new CoordinateXY[source.Length];
            for (int i = 0; i < source.Length; ++i)
            {
                result[i] = source[source.Length - 1 - i];
            }

            return result;
        }

        private static CoordinateXY[] Rectangle(
            float centerX,
            float centerY,
            float halfWidth,
            float halfHeight)
        {
            return new[]
            {
                new CoordinateXY(centerX - halfWidth, centerY - halfHeight),
                new CoordinateXY(centerX + halfWidth, centerY - halfHeight),
                new CoordinateXY(centerX + halfWidth, centerY + halfHeight),
                new CoordinateXY(centerX - halfWidth, centerY + halfHeight),
            };
        }

        private static CoordinateXY[] RadialLoop(
            int count,
            float radius,
            double aspect,
            double wobble,
            double phase,
            bool nonuniform)
        {
            if (count < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            CoordinateXY[] result = new CoordinateXY[count];
            for (int i = 0; i < count; ++i)
            {
                double unit = (double)i / count;
                if (nonuniform)
                {
                    // Monotone angular mapping with dense sampling near angle zero.
                    unit = 0.75 * unit * unit + 0.25 * unit;
                }

                double angle = unit * Math.PI * 2.0;
                double localRadius = radius
                    * (1.0 + wobble * Math.Sin(5.0 * angle + phase));
                float x = (float)(localRadius * aspect * Math.Cos(angle));
                float y = (float)(localRadius * Math.Sin(angle));
                result[i] = new CoordinateXY(x, y);
            }

            return result;
        }

        private static CoordinateXY[] SubdividedRectangle(
            int verticesPerSide,
            float halfWidth,
            float halfHeight)
        {
            if (verticesPerSide < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(verticesPerSide));
            }

            CoordinateXY[] corners = Rectangle(0.0f, 0.0f, halfWidth, halfHeight);
            CoordinateXY[] result = new CoordinateXY[checked(verticesPerSide * 4)];
            int output = 0;
            for (int side = 0; side < 4; ++side)
            {
                CoordinateXY start = corners[side];
                CoordinateXY end = corners[(side + 1) & 3];
                for (int i = 0; i < verticesPerSide; ++i)
                {
                    double t = (double)i / verticesPerSide;
                    result[output++] = new CoordinateXY(
                        (float)(start.X + (end.X - start.X) * t),
                        (float)(start.Y + (end.Y - start.Y) * t));
                }
            }

            return result;
        }
    }
}
