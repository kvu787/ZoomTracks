using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ZoomTracks.CollisionDetection.Runner
{
    internal static class CorrectnessTests
    {
        private const int RandomOrientationSeed = 731991;
        private const int RandomSegmentSeed = 918273;
        private const int RandomQuerySeed = 20260823;

        private static long _assertions;
        private static long _orientationCases;
        private static long _segmentCases;
        private static long _queryCases;
        private static int _generatorRejections;

        public static void Run()
        {
            var stopwatch = Stopwatch.StartNew();
            Console.WriteLine("CORRECTNESS START");

            TestCuratedSegmentCases();
            TestExhaustiveSmallGrid();
            TestRandomOrientations();
            TestRandomSegments();
            TestCuratedOutlineQueries();
            TestMinimumAndRedundantOutlines();
            TestExtremeOutlineQueries();
            TestRandomOutlineQueries();
            TestConcurrentQueries();

            stopwatch.Stop();
            Console.WriteLine(
                "CORRECTNESS PASS assertions={0} orientations={1} segment_pairs={2} " +
                "outline_queries={3} generator_rejections={4} elapsed_s={5:F3}",
                _assertions,
                _orientationCases,
                _segmentCases,
                _queryCases,
                _generatorRejections,
                stopwatch.Elapsed.TotalSeconds);
        }

        private static void TestCuratedSegmentCases()
        {
            float negativeZero = BitConverter.Int32BitsToSingle(unchecked((int)0x80000000U));
            float oneUp = MathF.BitIncrement(1.0f);

            var cases = new[]
            {
                SegmentCase.Create("proper crossing", P(0, 0), P(4, 4), P(0, 4), P(4, 0), true),
                SegmentCase.Create("endpoint-endpoint", P(0, 0), P(2, 0), P(2, 0), P(3, 2), true),
                SegmentCase.Create("endpoint-interior", P(0, 0), P(4, 0), P(2, 0), P(2, 3), true),
                SegmentCase.Create("collinear partial", P(0, 0), P(4, 0), P(2, 0), P(6, 0), true),
                SegmentCase.Create("collinear contained", P(0, 0), P(8, 0), P(2, 0), P(6, 0), true),
                SegmentCase.Create("collinear point touch", P(0, 0), P(2, 0), P(2, 0), P(4, 0), true),
                SegmentCase.Create("collinear disjoint", P(0, 0), P(2, 0), P(3, 0), P(4, 0), false),
                SegmentCase.Create("parallel one ulp", P(0, 1), P(4, 1), P(0, oneUp), P(4, oneUp), false),
                SegmentCase.Create("aabb touch only", P(0, 0), P(2, 2), P(2, 0), P(4, 1), false),
                SegmentCase.Create("signed zero", P(negativeZero, negativeZero), P(2, 0), P(1, -1), P(1, 1), true),
                SegmentCase.Create(
                    "subnormal crossing",
                    P(-float.Epsilon, 0),
                    P(float.Epsilon, 0),
                    P(0, -float.Epsilon),
                    P(0, float.Epsilon),
                    true),
                SegmentCase.Create(
                    "maximum crossing",
                    P(-float.MaxValue, 0),
                    P(float.MaxValue, 0),
                    P(0, -float.MaxValue),
                    P(0, float.MaxValue),
                    true),
            };

            foreach (SegmentCase testCase in cases)
            {
                bool reference = ReferenceOracle.SegmentIntersects(
                    testCase.A,
                    testCase.B,
                    testCase.C,
                    testCase.D);
                Check(reference == testCase.Expected, "Reference classification failed: " + testCase.Name);
                Check(
                    ReferenceOracle.ParametricSegmentIntersects(
                        testCase.A,
                        testCase.B,
                        testCase.C,
                        testCase.D) == testCase.Expected,
                    "Parametric classification failed: " + testCase.Name);
                Check(
                    ExactSegmentPredicates.Intersects(
                        testCase.A,
                        testCase.B,
                        testCase.C,
                        testCase.D) == testCase.Expected,
                    "Production classification failed: " + testCase.Name);
                _segmentCases++;
            }

            CoordinateXY hugeA = P(float.MaxValue, float.MaxValue);
            CoordinateXY hugeB = P(-float.MaxValue, -float.MaxValue);
            CoordinateXY offLine = P(1.0f, 0.0f);
            int expectedOrientation = ReferenceOracle.OrientationSign(hugeA, hugeB, offLine);
            Check(expectedOrientation != 0, "Mixed-exponent orientation fixture must be non-collinear.");
            Check(
                ExactSegmentPredicates.OrientationSign(hugeA, hugeB, offLine) == expectedOrientation,
                "Filtered orientation failed mixed-exponent cancellation fixture.");
            _orientationCases++;
        }

        private static void TestExhaustiveSmallGrid()
        {
            var points = new List<CoordinateXY>();
            for (int x = -3; x <= 3; ++x)
            {
                for (int y = -3; y <= 3; ++y)
                {
                    points.Add(P(x, y));
                }
            }

            var segments = new List<SegmentPair>();
            for (int i = 0; i < points.Count; ++i)
            {
                for (int j = i + 1; j < points.Count; ++j)
                {
                    segments.Add(new SegmentPair(points[i], points[j]));
                }
            }

            foreach (SegmentPair first in segments)
            {
                foreach (SegmentPair second in segments)
                {
                    bool orientationOracle = ReferenceOracle.SegmentIntersects(
                        first.A,
                        first.B,
                        second.A,
                        second.B);
                    bool parametricOracle = ReferenceOracle.ParametricSegmentIntersects(
                        first.A,
                        first.B,
                        second.A,
                        second.B);
                    bool actual = ExactSegmentPredicates.Intersects(
                        first.A,
                        first.B,
                        second.A,
                        second.B);

                    CheckBatch(
                        orientationOracle == parametricOracle && actual == orientationOracle,
                        "Exhaustive small-grid segment mismatch.");
                    _segmentCases++;
                }
            }
        }

        private static void TestRandomOrientations()
        {
            var random = new Random(RandomOrientationSeed);
            const int count = 250000;
            for (int i = 0; i < count; ++i)
            {
                CoordinateXY a;
                CoordinateXY b;
                CoordinateXY c;

                if ((i % 5) == 0)
                {
                    int ax = random.Next(-1000000, 1000001);
                    int ay = random.Next(-1000000, 1000001);
                    int dx = random.Next(-1000, 1001);
                    int dy = random.Next(-1000, 1001);
                    int step = random.Next(-1000, 1001);
                    a = P(ax, ay);
                    b = P(ax + dx, ay + dy);
                    c = P(ax + (dx * step), ay + (dy * step));

                    if ((i & 1) != 0)
                    {
                        c = new CoordinateXY(c.X, MathF.BitIncrement(c.Y));
                    }
                }
                else if ((i % 17) == 0)
                {
                    a = P(float.MaxValue, float.MaxValue);
                    b = P(-float.MaxValue, -float.MaxValue);
                    c = P(RandomFiniteFloat(random), 0.0f);
                }
                else
                {
                    a = RandomPoint(random);
                    b = RandomPoint(random);
                    c = RandomPoint(random);
                }

                int expected = ReferenceOracle.OrientationSign(a, b, c);
                int actual = ExactSegmentPredicates.OrientationSign(a, b, c);
                CheckBatch(actual == expected, "Random orientation mismatch.");
                _orientationCases++;
            }
        }

        private static void TestRandomSegments()
        {
            var random = new Random(RandomSegmentSeed);
            const int count = 125000;
            for (int i = 0; i < count; ++i)
            {
                CoordinateXY a = RandomPoint(random);
                CoordinateXY b = RandomDistinctPoint(random, a);
                CoordinateXY c = RandomPoint(random);
                CoordinateXY d = RandomDistinctPoint(random, c);

                bool expected = ReferenceOracle.SegmentIntersects(a, b, c, d);
                bool parametric = ReferenceOracle.ParametricSegmentIntersects(a, b, c, d);
                bool actual = ExactSegmentPredicates.Intersects(a, b, c, d);
                bool symmetric = ExactSegmentPredicates.Intersects(d, c, b, a);
                CheckBatch(
                    expected == parametric && actual == expected && symmetric == expected,
                    "Random segment mismatch.");
                _segmentCases++;
            }
        }

        private static void TestCuratedOutlineQueries()
        {
            CoordinateXY[] outer =
            {
                P(-10, -10), P(10, -10), P(10, 10), P(-10, 10),
            };
            CoordinateXY[] inner =
            {
                P(-3, -3), P(-3, 3), P(3, 3), P(3, -3),
            };

            var cases = new[]
            {
                QueryCase.Create("outer crossing", AxisRectangle(9, -1, 11, 1), true),
                QueryCase.Create("inner crossing", AxisRectangle(2, -1, 4, 1), true),
                QueryCase.Create("inside inner", AxisRectangle(-1, -1, 1, 1), false),
                QueryCase.Create("annular miss", AxisRectangle(5, -1, 6, 1), false),
                QueryCase.Create("encloses both", AxisRectangle(-20, -20, 20, 20), false),
                QueryCase.Create("outside", AxisRectangle(20, 20, 21, 21), false),
                QueryCase.Create("endpoint contact", AxisRectangle(10, 10, 11, 11), true),
                QueryCase.Create("collinear overlap", AxisRectangle(-2, 10, 2, 11), true),
                QueryCase.Create(
                    "rotated vertex tangent",
                    new ConvexQuadrilateralOutline(P(10, 10), P(12, 8), P(14, 10), P(12, 12)),
                    true),
                QueryCase.Create("crosses both loops", AxisRectangle(-11, -0.5f, 11, 0.5f), true),
            };

            ReferenceOracle.PreparedOutlines oracle = ReferenceOracle.Prepare(outer, inner);
            List<NamedIndex> indexes = CreateIndexes(outer, inner);
            foreach (QueryCase testCase in cases)
            {
                Check(oracle.IsColliding(testCase.Query) == testCase.Expected, "Curated oracle failed: " + testCase.Name);
                CheckIndexes(indexes, testCase.Query, testCase.Expected, "curated " + testCase.Name);
                CheckIndexes(indexes, Reverse(testCase.Query), testCase.Expected, "reversed " + testCase.Name);
                CheckIndexes(indexes, Rotate(testCase.Query), testCase.Expected, "rotated-order " + testCase.Name);
            }

            CoordinateXY[] reversedOuter = ReverseLoop(outer);
            CoordinateXY[] rotatedInner = RotateLoop(inner);
            List<NamedIndex> transformedIndexes = CreateIndexes(reversedOuter, rotatedInner);
            foreach (QueryCase testCase in cases)
            {
                CheckIndexes(
                    transformedIndexes,
                    testCase.Query,
                    testCase.Expected,
                    "outline-order transform " + testCase.Name);
            }
        }

        private static void TestExtremeOutlineQueries()
        {
            float max = float.MaxValue;
            float beforeMax = MathF.BitDecrement(max);
            CoordinateXY[] hugeOuter =
            {
                P(-max, -max), P(max, -max), P(max, max), P(-max, max),
            };
            CoordinateXY[] unitInner =
            {
                P(-1, -1), P(-1, 1), P(1, 1), P(1, -1),
            };

            ConvexQuadrilateralOutline hugeContact = new ConvexQuadrilateralOutline(
                P(beforeMax, -1),
                P(max, -1),
                P(max, 1),
                P(beforeMax, 1));
            CheckIndexes(CreateIndexes(hugeOuter, unitInner), hugeContact, true, "maximum finite grid mapping");

            float e = float.Epsilon;
            CoordinateXY[] tinyOuter =
            {
                P(-4 * e, -4 * e), P(4 * e, -4 * e), P(4 * e, 4 * e), P(-4 * e, 4 * e),
            };
            CoordinateXY[] tinyInner =
            {
                P(-e, -e), P(-e, e), P(e, e), P(e, -e),
            };
            ConvexQuadrilateralOutline tinyContact = new ConvexQuadrilateralOutline(
                P(3 * e, -e),
                P(4 * e, -e),
                P(4 * e, e),
                P(3 * e, e));
            CheckIndexes(CreateIndexes(tinyOuter, tinyInner), tinyContact, true, "subnormal grid mapping");
        }

        private static void TestMinimumAndRedundantOutlines()
        {
            CoordinateXY[] triangularOuter =
            {
                P(-10, -10), P(10, -10), P(0, 10),
            };
            CoordinateXY[] triangularInner =
            {
                P(-1, -1), P(1, -1), P(0, 1),
            };
            List<NamedIndex> triangleIndexes = CreateIndexes(triangularOuter, triangularInner);
            CheckIndexes(
                triangleIndexes,
                AxisRectangle(0, 10, 1, 11),
                true,
                "minimum n1=n2=3 vertex contact");
            CheckIndexes(
                triangleIndexes,
                AxisRectangle(-0.25f, -0.25f, 0.25f, 0.25f),
                false,
                "minimum n1=n2=3 containment miss");

            CoordinateXY[] redundantOuter =
            {
                P(-10, -10), P(0, -10), P(10, -10), P(10, 10),
                P(0, 10), P(-10, 10),
            };
            CoordinateXY[] squareInner =
            {
                P(-2, -2), P(-2, 2), P(2, 2), P(2, -2),
            };
            List<NamedIndex> redundantIndexes = CreateIndexes(redundantOuter, squareInner);
            CheckIndexes(
                redundantIndexes,
                AxisRectangle(-1, 10, 1, 11),
                true,
                "redundant collinear outline vertices");

            var leafOne = new MortonBvhIndex(redundantOuter, squareInner, 1);
            var leafSixtyFour = new MortonBvhIndex(redundantOuter, squareInner, 64);
            var forcedOverflow = new SparseUniformGridIndex(
                redundantOuter,
                squareInner,
                targetSegmentsPerCell: 1,
                maxAxisCells: 1024,
                maxCellsPerSegment: 1);
            Check(forcedOverflow.OverflowSegmentCount > 0, "Forced grid-overflow fixture did not overflow.");
            var parameterized = new List<NamedIndex>
            {
                new NamedIndex("bvh-leaf-1", leafOne),
                new NamedIndex("bvh-leaf-64", leafSixtyFour),
                new NamedIndex("grid-forced-overflow", forcedOverflow),
            };
            CheckIndexes(
                parameterized,
                AxisRectangle(-1, 10, 1, 11),
                true,
                "nondefault parameters contact");
            CheckIndexes(
                parameterized,
                AxisRectangle(-20, -20, 20, 20),
                false,
                "nondefault parameters broad-query fallback");
        }

        private static void TestRandomOutlineQueries()
        {
            var random = new Random(RandomQuerySeed);
            for (int dataSet = 0; dataSet < 10; ++dataSet)
            {
                int outerCount = 24 + (dataSet * 11);
                int innerCount = 13 + (dataSet * 5);
                CoordinateXY[] outer = MakeLoop(
                    outerCount,
                    1000.0,
                    760.0,
                    dataSet * 0.071,
                    0.16,
                    3 + (dataSet % 5));
                CoordinateXY[] inner = MakeLoop(
                    innerCount,
                    300.0,
                    220.0,
                    0.3 + (dataSet * 0.037),
                    0.11,
                    2 + (dataSet % 3));

                List<NamedIndex> indexes = CreateIndexes(outer, inner);
                ReferenceOracle.PreparedOutlines oracle = ReferenceOracle.Prepare(outer, inner);
                var queries = new List<ConvexQuadrilateralOutline>();
                var expectedResults = new List<bool>();

                while (queries.Count < 500)
                {
                    ConvexQuadrilateralOutline query;
                    if ((queries.Count % 25) == 0)
                    {
                        int edge = random.Next(outer.Length);
                        query = RectangleFromEdge(
                            outer[edge],
                            outer[(edge + 1) % outer.Length],
                            5.0 + random.NextDouble() * 20.0);
                    }
                    else
                    {
                        double centerX = (random.NextDouble() * 2400.0) - 1200.0;
                        double centerY = (random.NextDouble() * 2000.0) - 1000.0;
                        double halfWidth = 0.2 + (random.NextDouble() * 90.0);
                        double halfHeight = 0.2 + (random.NextDouble() * 50.0);
                        double angle = random.NextDouble() * Math.PI;
                        query = MakeRectangle(centerX, centerY, halfWidth, halfHeight, angle);
                    }

                    if (!IsStrictlyConvex(query))
                    {
                        _generatorRejections++;
                        continue;
                    }

                    queries.Add(query);
                    expectedResults.Add(oracle.IsColliding(query));
                }

                for (int i = 0; i < queries.Count; ++i)
                {
                    CheckIndexes(indexes, queries[i], expectedResults[i], "random query");
                    if ((i % 10) == 0)
                    {
                        CheckIndexes(indexes, Reverse(queries[i]), expectedResults[i], "random reversed query");
                        CheckIndexes(indexes, Rotate(queries[i]), expectedResults[i], "random rotated query");
                    }
                }

                for (int i = queries.Count - 1; i >= 0; --i)
                {
                    CheckIndexes(indexes, queries[i], expectedResults[i], "state/order independence");
                }
            }
        }

        private static void TestConcurrentQueries()
        {
            CoordinateXY[] outer = MakeLoop(512, 1000.0, 800.0, 0.0, 0.12, 7);
            CoordinateXY[] inner = MakeLoop(257, 300.0, 240.0, 0.2, 0.08, 5);
            var random = new Random(77331);
            var queries = new ConvexQuadrilateralOutline[512];
            var expected = new bool[queries.Length];
            var oracle = ReferenceOracle.Prepare(outer, inner);
            for (int i = 0; i < queries.Length; ++i)
            {
                queries[i] = MakeRectangle(
                    (random.NextDouble() * 2200.0) - 1100.0,
                    (random.NextDouble() * 1800.0) - 900.0,
                    1.0 + (random.NextDouble() * 30.0),
                    1.0 + (random.NextDouble() * 20.0),
                    random.NextDouble() * Math.PI);
                expected[i] = oracle.IsColliding(queries[i]);
            }

            foreach (NamedIndex named in CreateIndexes(outer, inner))
            {
                Parallel.For(
                    0,
                    queries.Length,
                    i =>
                    {
                        if (named.Index.IsColliding(queries[i]) != expected[i])
                        {
                            throw new InvalidOperationException("Concurrent mismatch: " + named.Name);
                        }
                    });
                _assertions += queries.Length;
                _queryCases += queries.Length;
            }
        }

        internal static CoordinateXY[] MakeLoop(
            int count,
            double radiusX,
            double radiusY,
            double phase,
            double wobble,
            int harmonic)
        {
            var result = new CoordinateXY[count];
            for (int i = 0; i < count; ++i)
            {
                double angle = phase + ((Math.PI * 2.0 * i) / count);
                double scale = 1.0 + (wobble * Math.Sin((harmonic * angle) + 0.37));
                result[i] = new CoordinateXY(
                    (float)(radiusX * scale * Math.Cos(angle)),
                    (float)(radiusY * scale * Math.Sin(angle)));
            }

            return result;
        }

        internal static ConvexQuadrilateralOutline MakeRectangle(
            double centerX,
            double centerY,
            double halfWidth,
            double halfHeight,
            double angle)
        {
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            double ux = cos * halfWidth;
            double uy = sin * halfWidth;
            double vx = -sin * halfHeight;
            double vy = cos * halfHeight;

            return new ConvexQuadrilateralOutline(
                P(centerX - ux - vx, centerY - uy - vy),
                P(centerX + ux - vx, centerY + uy - vy),
                P(centerX + ux + vx, centerY + uy + vy),
                P(centerX - ux + vx, centerY - uy + vy));
        }

        private static ConvexQuadrilateralOutline RectangleFromEdge(CoordinateXY a, CoordinateXY b, double depth)
        {
            double dx = (double)b.X - a.X;
            double dy = (double)b.Y - a.Y;
            double length = Math.Sqrt((dx * dx) + (dy * dy));
            double nx = (-dy / length) * depth;
            double ny = (dx / length) * depth;
            return new ConvexQuadrilateralOutline(
                a,
                b,
                P(b.X + nx, b.Y + ny),
                P(a.X + nx, a.Y + ny));
        }

        private static bool IsStrictlyConvex(ConvexQuadrilateralOutline query)
        {
            int sign = 0;
            for (int i = 0; i < 4; ++i)
            {
                int current = ReferenceOracle.OrientationSign(
                    query.GetVertex(i),
                    query.GetVertex((i + 1) & 3),
                    query.GetVertex((i + 2) & 3));
                if (current == 0)
                {
                    return false;
                }

                if (sign == 0)
                {
                    sign = current;
                }
                else if (sign != current)
                {
                    return false;
                }
            }

            return true;
        }

        private static List<NamedIndex> CreateIndexes(
            IReadOnlyList<CoordinateXY> outer,
            IReadOnlyList<CoordinateXY> inner)
        {
            return new List<NamedIndex>
            {
                new NamedIndex("linear", new LinearScanIndex(outer, inner)),
                new NamedIndex("morton-bvh", new MortonBvhIndex(outer, inner)),
                new NamedIndex("sparse-grid", new SparseUniformGridIndex(outer, inner)),
            };
        }

        private static void CheckIndexes(
            IReadOnlyList<NamedIndex> indexes,
            ConvexQuadrilateralOutline query,
            bool expected,
            string context)
        {
            foreach (NamedIndex named in indexes)
            {
                bool actual = named.Index.IsColliding(query);
                Check(actual == expected, named.Name + " mismatch: " + context);
                _queryCases++;
            }
        }

        private static ConvexQuadrilateralOutline AxisRectangle(float minX, float minY, float maxX, float maxY)
        {
            return new ConvexQuadrilateralOutline(P(minX, minY), P(maxX, minY), P(maxX, maxY), P(minX, maxY));
        }

        private static ConvexQuadrilateralOutline Reverse(ConvexQuadrilateralOutline query)
        {
            return new ConvexQuadrilateralOutline(query.P0, query.P3, query.P2, query.P1);
        }

        private static ConvexQuadrilateralOutline Rotate(ConvexQuadrilateralOutline query)
        {
            return new ConvexQuadrilateralOutline(query.P1, query.P2, query.P3, query.P0);
        }

        private static CoordinateXY[] ReverseLoop(CoordinateXY[] loop)
        {
            var result = new CoordinateXY[loop.Length];
            result[0] = loop[0];
            for (int i = 1; i < loop.Length; ++i)
            {
                result[i] = loop[loop.Length - i];
            }

            return result;
        }

        private static CoordinateXY[] RotateLoop(CoordinateXY[] loop)
        {
            var result = new CoordinateXY[loop.Length];
            for (int i = 0; i < loop.Length; ++i)
            {
                result[i] = loop[(i + 1) % loop.Length];
            }

            return result;
        }

        private static CoordinateXY RandomPoint(Random random)
        {
            return new CoordinateXY(RandomFiniteFloat(random), RandomFiniteFloat(random));
        }

        private static CoordinateXY RandomDistinctPoint(Random random, CoordinateXY other)
        {
            CoordinateXY result;
            do
            {
                result = RandomPoint(random);
            }
            while (result.X == other.X && result.Y == other.Y);

            return result;
        }

        private static float RandomFiniteFloat(Random random)
        {
            int mode = random.Next(4);
            if (mode == 0)
            {
                return random.Next(-1000000, 1000001) / 16.0f;
            }

            if (mode == 1)
            {
                int exponent = random.Next(-149, 128);
                double mantissa = 0.5 + random.NextDouble();
                double value = Math.ScaleB(mantissa, exponent);
                return (float)(random.Next(2) == 0 ? value : -value);
            }

            uint bits = (uint)random.NextInt64(0, 1L << 32);
            if (((bits >> 23) & 0xffU) == 0xffU)
            {
                bits ^= 0x00800000U;
            }

            return BitConverter.Int32BitsToSingle(unchecked((int)bits));
        }

        private static CoordinateXY P(double x, double y)
        {
            return new CoordinateXY((float)x, (float)y);
        }

        private static void Check(bool condition, string message)
        {
            _assertions++;
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void CheckBatch(bool condition, string message)
        {
            _assertions++;
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private readonly record struct NamedIndex(string Name, ICollisionDetector Index);

        private readonly record struct SegmentPair(CoordinateXY A, CoordinateXY B);

        private readonly record struct SegmentCase(
            string Name,
            CoordinateXY A,
            CoordinateXY B,
            CoordinateXY C,
            CoordinateXY D,
            bool Expected)
        {
            public static SegmentCase Create(
                string name,
                CoordinateXY a,
                CoordinateXY b,
                CoordinateXY c,
                CoordinateXY d,
                bool expected)
            {
                return new SegmentCase(name, a, b, c, d, expected);
            }
        }

        private readonly record struct QueryCase(string Name, ConvexQuadrilateralOutline Query, bool Expected)
        {
            public static QueryCase Create(string name, ConvexQuadrilateralOutline query, bool expected)
            {
                return new QueryCase(name, query, expected);
            }
        }
    }
}
