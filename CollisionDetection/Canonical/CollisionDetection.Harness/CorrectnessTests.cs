using System;
using System.Collections.Generic;
using System.Reflection;
using ZoomTracks.CollisionDetection;

namespace CollisionDetection.Harness {
    internal static class CorrectnessTests {
        private static int _assertionCount;

        internal static void Run() {
            _assertionCount = 0;
            DateTime started = DateTime.UtcNow;
            TestPublicValueContracts();
            TestDetectorConstructionContracts();
            TestAlgorithmSettingsAndFallbacks();
            TestFixedGeometrySemantics();
            TestRoundedAndExtremeQueries();
            TestExactPredicateExhaustiveGrid();
            TestExactPredicateRandomBits();
            TestRandomEndToEnd();
            TestWindingAndIndexStress();
            TimeSpan elapsed = DateTime.UtcNow - started;
            Console.WriteLine(
                "Correctness: PASS ({0:N0} assertions, {1:F3} s)",
                _assertionCount,
                elapsed.TotalSeconds);
        }

        private static void TestPublicValueContracts() {
            AssertThrows<ArgumentOutOfRangeException>(
                () => new CoordinateXY(float.NaN, 0.0f), "Coordinate NaN X");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new CoordinateXY(0.0f, float.PositiveInfinity), "Coordinate infinity Y");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new RectanglePose(float.NegativeInfinity, 0.0f, 0.0f), "Pose infinity");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new RectanglePose(0.0f, 0.0f, float.NaN), "Pose NaN angle");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new RectangleLocalBounds(float.NaN, 0.0f, 1.0f, 1.0f), "Bounds NaN");
            AssertThrows<ArgumentException>(
                () => new RectangleLocalBounds(1.0f, 0.0f, 1.0f, 1.0f), "Bounds zero X");
            AssertThrows<ArgumentException>(
                () => new RectangleLocalBounds(2.0f, 0.0f, 1.0f, 1.0f), "Bounds reversed X");
            AssertThrows<ArgumentException>(
                () => new RectangleLocalBounds(0.0f, -0.0f, 1.0f, 0.0f), "Bounds signed-zero Y");

            RectanglePose defaultPose = default;
            Assert(defaultPose.PositionX == 0.0f
                && defaultPose.PositionY == 0.0f
                && defaultPose.RotationDegrees == 0.0f, "Default pose is valid zero pose");
            CoordinateXY defaultCoordinate = default;
            Assert(defaultCoordinate.X == 0.0f && defaultCoordinate.Y == 0.0f,
                "Default coordinate is finite origin");
        }

        private static void TestDetectorConstructionContracts() {
            Type[] detectorTypes = DetectorTypes();
            foreach (Type detectorType in detectorTypes) {
                ConstructorInfo constructor = detectorType.GetConstructor(
                    new[] { typeof(List<CoordinateXY>), typeof(List<CoordinateXY>) });
                Assert(constructor != null, detectorType.Name + " has required public constructor");
                Assert(typeof(ICollisionDetector).IsAssignableFrom(detectorType),
                    detectorType.Name + " implements ICollisionDetector");
                Assert(detectorType.Namespace == "ZoomTracks.CollisionDetection",
                    detectorType.Name + " namespace");
            }

            OutlinePair valid = TestData.NestedSquares(10.0f, 2.0f);
            foreach (DetectorKind kind in DetectorKinds()) {
                AssertThrows<ArgumentNullException>(
                    () => Create(kind, null, valid.CloneOutline2()), kind + " null first list");
                AssertThrows<ArgumentNullException>(
                    () => Create(kind, valid.CloneOutline1(), null), kind + " null second list");
                AssertThrows<ArgumentException>(
                    () => Create(kind,
                        new List<CoordinateXY>
                        {
                            new(0, 0),
                            new(1, 0),
                        },
                        valid.CloneOutline2()), kind + " too few vertices");
                AssertThrows<ArgumentException>(
                    () => Create(kind,
                        new List<CoordinateXY>
                        {
                            new(0, 0),
                            new(1, 0),
                            new(1, 0),
                        },
                        valid.CloneOutline2()), kind + " adjacent duplicate");
                AssertThrows<ArgumentException>(
                    () => Create(kind,
                        new List<CoordinateXY>
                        {
                            new(0, 0),
                            new(1, 0),
                            new(0, 0),
                        },
                        valid.CloneOutline2()), kind + " closing duplicate");

                List<CoordinateXY> first = valid.CloneOutline1();
                CoordinateXY[] before = first.ToArray();
                List<CoordinateXY> invalidSecond = new() {
                    new CoordinateXY(0, 0),
                    new CoordinateXY(1, 0),
                };
                AssertThrows<ArgumentException>(
                    () => Create(kind, first, invalidSecond), kind + " invalid second outline");
                AssertListsEqual(before, first, kind + " leaves first list unchanged on throw");

                ICollisionDetector detector = Create(
                    kind,
                    valid.CloneOutline1(),
                    valid.CloneOutline2());
                AssertThrows<ArgumentException>(
                    () => detector.IsColliding(default, default),
                    kind + " rejects default bounds");
            }

            List<CoordinateXY> subnormalTriangle = new() {
                new CoordinateXY(0.0f, 0.0f),
                new CoordinateXY(float.Epsilon, 0.0f),
                new CoordinateXY(0.0f, float.Epsilon),
            };
            ICollisionDetector subnormalDetector = new LinearScanCollisionDetector(
                subnormalTriangle,
                valid.CloneOutline2());
            Assert(subnormalDetector != null, "Subnormal positive-length segments are accepted");
        }

        private static void TestFixedGeometrySemantics() {
            OutlinePair outlines = TestData.NestedSquares(10.0f, 2.0f);
            CheckFixed(outlines, "inside inner containment", Bounds(-1, -1, 1, 1), Pose(0, 0, 0), false);
            CheckFixed(outlines, "annulus containment", Bounds(-1, -1, 1, 1), Pose(5, 0, 0), false);
            CheckFixed(outlines, "contains inner without contact", Bounds(-5, -5, 5, 5), Pose(0, 0, 0), false);
            CheckFixed(outlines, "contains both without contact", Bounds(-20, -20, 20, 20), Pose(0, 0, 0), false);
            CheckFixed(outlines, "outside", Bounds(-1, -1, 1, 1), Pose(30, 30, 0), false);
            CheckFixed(outlines, "proper inner crossing", Bounds(-1, -1, 1, 1), Pose(2, 0, 0), true);
            CheckFixed(outlines, "proper outer crossing", Bounds(-1, -1, 1, 1), Pose(10, 0, 0), true);
            CheckFixed(outlines, "outer closing edge crossing", Bounds(-1, -1, 1, 1), Pose(-10, 0, 0), true);
            CheckFixed(outlines, "endpoint contact", Bounds(0, 0, 1, 1), Pose(10, 10, 0), true);
            CheckFixed(outlines, "corner tangency", Bounds(-1, -1, 0, 0), Pose(11, 11, 0), true);
            CheckFixed(outlines, "collinear partial overlap", Bounds(-5, 0, 5, 1), Pose(0, -10, 0), true);
            CheckFixed(outlines, "collinear disjoint", Bounds(11, 0, 12, 1), Pose(0, -10, 0), false);
            CheckFixed(outlines, "non-centered clockwise 90", Bounds(0, 0, 2, 1), Pose(9, 1, 90), true);
            CheckFixed(outlines, "negative rotation", Bounds(-2, -1, 2, 1), Pose(9, 0, -30), true);
            CheckFixed(outlines, "positive rotation", Bounds(-2, -1, 2, 1), Pose(9, 0, 30), true);
            CheckFixed(outlines, "subnormal rectangle inside", Bounds(0, 0, float.Epsilon, float.Epsilon), Pose(0, 0, 0), false);
        }

        private static void TestAlgorithmSettingsAndFallbacks() {
            OutlinePair outlines = TestData.NestedSquares(10.0f, 2.0f);
            AssertThrows<ArgumentOutOfRangeException>(
                () => new BvhCollisionDetector(
                    outlines.CloneOutline1(), outlines.CloneOutline2(), 0),
                "BVH rejects zero leaf size");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new UniformGridCollisionDetector(
                    outlines.CloneOutline1(), outlines.CloneOutline2(), 0, 1),
                "Grid rejects zero target cells");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new UniformGridCollisionDetector(
                    outlines.CloneOutline1(), outlines.CloneOutline2(), 16, 0),
                "Grid rejects zero replication cap");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new UniformGridCollisionDetector(
                    outlines.CloneOutline1(),
                    outlines.CloneOutline2(),
                    UniformGridCollisionDetector.MaximumSupportedTargetCellCount + 1,
                    1),
                "Grid rejects impractically large target");

            UniformGridCollisionDetector overflowGrid = new(
                outlines.CloneOutline1(), outlines.CloneOutline2(), 1024, 1);
            Assert(overflowGrid.OverflowEdgeCount > 0, "Grid overflow path is populated");
            QueryInput[] queries =
            {
                new(Bounds(-1, -1, 1, 1), Pose(0, 0, 0)),
                new(Bounds(-1, -1, 1, 1), Pose(10, 0, 0)),
                new(Bounds(-20, -20, 20, 20), Pose(0, 0, 0)),
            };
            for (int i = 0; i < queries.Length; ++i) {
                bool expected = IndependentOracle.IsColliding(
                    outlines.Outline1,
                    outlines.Outline2,
                    queries[i].Bounds,
                    queries[i].Pose);
                Assert(overflowGrid.IsColliding(queries[i].Bounds, queries[i].Pose) == expected,
                    "Grid overflow query " + i);
            }

            BvhCollisionDetector singleEdgeLeaves = new(
                outlines.CloneOutline1(), outlines.CloneOutline2(), 1);
            Assert(singleEdgeLeaves.NodeCount == outlines.EdgeCount * 2 - 1,
                "BVH one-edge leaf structure");
        }

        private static void TestRoundedAndExtremeQueries() {
            const float center = 1_073_741_824.0f;
            OutlinePair large = TestData.NestedSquaresAt(center, center, 4096.0f, 1024.0f);
            CheckFixed(large, "rectangle rounds to interior point", Bounds(0, 0, 1, 1), Pose(center, center, 0), false);
            CheckFixed(large, "rectangle rounds to boundary point", Bounds(0, 0, 1, 1), Pose(center + 1024.0f, center, 0), true);

            OutlinePair ordinary = TestData.NestedSquares(10.0f, 2.0f);
            foreach (DetectorKind kind in DetectorKinds()) {
                ICollisionDetector detector = Create(kind, ordinary.CloneOutline1(), ordinary.CloneOutline2());
                RectangleLocalBounds overflowing = Bounds(0.0f, 0.0f, float.MaxValue, 1.0f);
                AssertThrows<ArgumentOutOfRangeException>(
                    () => detector.IsColliding(overflowing, Pose(float.MaxValue, 0.0f, 0.0f)),
                    kind + " rejects nonfinite transformed corner");
            }

            // Huge angles are deliberately accepted; their runtime-specific trig result
            // defines the float rectangle that the exact predicate then tests.
            CheckAgainstOracle(
                ordinary,
                Bounds(-1.25f, -0.75f, 2.0f, 0.5f),
                Pose(3.0f, -4.0f, float.MaxValue),
                "huge finite rotation");
        }

        private static void TestExactPredicateExhaustiveGrid() {
            List<CoordinateXY> points = new();
            for (int y = -2; y <= 2; ++y) {
                for (int x = -2; x <= 2; ++x) {
                    points.Add(new CoordinateXY(x, y));
                }
            }

            for (int ai = 0; ai < points.Count; ++ai) {
                for (int bi = 0; bi < points.Count; ++bi) {
                    for (int ci = 0; ci < points.Count; ++ci) {
                        for (int di = 0; di < points.Count; ++di) {
                            CompareSegmentPredicate(points[ai], points[bi], points[ci], points[di],
                                "exhaustive integer grid");
                        }
                    }
                }
            }
        }

        private static void TestExactPredicateRandomBits() {
            Random random = new(0x51a9c3);
            for (int i = 0; i < 100_000; ++i) {
                CoordinateXY a = RandomCoordinate(random);
                CoordinateXY b = RandomCoordinate(random);
                CoordinateXY c = RandomCoordinate(random);
                int expectedOrientation = IndependentOracle.OrientationSign(a, b, c);
                int actualOrientation = RobustPredicates.OrientationSign(
                    new PointF(a.X, a.Y),
                    new PointF(b.X, b.Y),
                    new PointF(c.X, c.Y));
                Assert(expectedOrientation == actualOrientation, "random exact orientation");

                CoordinateXY d = RandomCoordinate(random);
                CompareSegmentPredicate(a, b, c, d, "random finite-bit segments");
            }

            // Construct exact collinearity and nearby one-ULP perturbations at large translations.
            for (int i = 0; i < 20_000; ++i) {
                float originX = (float)(random.Next(-1_000_000, 1_000_001) * 128);
                float originY = (float)(random.Next(-1_000_000, 1_000_001) * 128);
                float dx = random.Next(1, 1024) * 128.0f;
                float dy = random.Next(-1024, 1024) * 128.0f;
                CoordinateXY a = new(originX, originY);
                CoordinateXY b = new(originX + dx, originY + dy);
                CoordinateXY c = new(originX + dx * 2.0f, originY + dy * 2.0f);
                int expected = IndependentOracle.OrientationSign(a, b, c);
                int actual = RobustPredicates.OrientationSign(
                    new PointF(a.X, a.Y), new PointF(b.X, b.Y), new PointF(c.X, c.Y));
                Assert(expected == actual, "constructed near-collinear orientation");

                float perturbedY = (i & 1) == 0 ? NextUp(c.Y) : NextDown(c.Y);
                CoordinateXY perturbed = new(c.X, perturbedY);
                int perturbedExpected = IndependentOracle.OrientationSign(a, b, perturbed);
                int perturbedActual = RobustPredicates.OrientationSign(
                    new PointF(a.X, a.Y),
                    new PointF(b.X, b.Y),
                    new PointF(perturbed.X, perturbed.Y));
                Assert(perturbedExpected != 0, "one-ULP perturbation changes orientation");
                Assert(perturbedExpected == perturbedActual,
                    "constructed one-ULP near-collinear orientation");
            }
        }

        private static void TestRandomEndToEnd() {
            Random random = new(0x42c011);
            int[] sizes = { 24, 192, 768 };
            foreach (int outerCount in sizes) {
                int innerCount = outerCount / 2;
                float radius = Math.Max(40.0f, outerCount * 1.6f);
                OutlinePair outlines = TestData.SmoothNestedLoops(
                    outerCount, innerCount, radius, 1.35, false);
                ICollisionDetector[] detectors = CreateAll(outlines);
                int queryCount = outerCount < 100 ? 4_000 : 2_000;
                for (int i = 0; i < queryCount; ++i) {
                    float scale = radius * 1.4f;
                    RectangleLocalBounds bounds = new(
                        -3.25f,
                        -1.75f,
                        6.5f,
                        4.25f);
                    RectanglePose pose = new(
                        NextFloat(random, -scale, scale),
                        NextFloat(random, -scale, scale),
                        NextFloat(random, -1080.0f, 1080.0f));
                    bool expected = IndependentOracle.IsColliding(
                        outlines.Outline1, outlines.Outline2, bounds, pose);
                    for (int detectorIndex = 0; detectorIndex < detectors.Length; ++detectorIndex) {
                        bool actual = detectors[detectorIndex].IsColliding(bounds, pose);
                        Assert(actual == expected,
                            "random end-to-end N=" + outlines.EdgeCount + " detector=" + detectorIndex);
                    }
                }
            }
        }

        private static void TestWindingAndIndexStress() {
            OutlinePair original = TestData.SmoothNestedLoops(1024, 127, 1800.0f, 7.5, true);
            OutlinePair reversed = new(
                TestData.Reverse(original.Outline1),
                TestData.Reverse(original.Outline2));
            Random random = new(0x730d);
            ICollisionDetector[] originalDetectors = CreateAll(original);
            ICollisionDetector[] reversedDetectors = CreateAll(reversed);
            for (int i = 0; i < 2_000; ++i) {
                RectangleLocalBounds bounds = Bounds(-20, -8, 15, 13);
                RectanglePose pose = Pose(
                    NextFloat(random, -14_000, 14_000),
                    NextFloat(random, -2_500, 2_500),
                    NextFloat(random, -180, 180));
                bool expected = IndependentOracle.IsColliding(
                    original.Outline1, original.Outline2, bounds, pose);
                bool reversedExpected = IndependentOracle.IsColliding(
                    reversed.Outline1, reversed.Outline2, bounds, pose);
                Assert(expected == reversedExpected, "winding reversal oracle invariance");
                for (int detectorIndex = 0; detectorIndex < originalDetectors.Length; ++detectorIndex) {
                    Assert(originalDetectors[detectorIndex].IsColliding(bounds, pose) == expected,
                        "elongated/nonuniform index stress");
                    Assert(reversedDetectors[detectorIndex].IsColliding(bounds, pose) == expected,
                        "reversed winding index stress");
                }
            }

            OutlinePair degenerate = TestData.SubdividedNestedRectangles(128, 1000.0f, 400.0f);
            for (int i = -10; i <= 10; ++i) {
                CheckAgainstOracle(
                    degenerate,
                    Bounds(-25, 0, 25, 10),
                    Pose(i * 50, -1000, 0),
                    "subdivided collinear boundary");
            }
        }

        private static void CheckFixed(
            OutlinePair outlines,
            string name,
            RectangleLocalBounds bounds,
            RectanglePose pose,
            bool expected) {
            bool oracle = IndependentOracle.IsColliding(
                outlines.Outline1, outlines.Outline2, bounds, pose);
            Assert(oracle == expected, name + " independent oracle expectation");
            ICollisionDetector[] detectors = CreateAll(outlines);
            for (int i = 0; i < detectors.Length; ++i) {
                Assert(detectors[i].IsColliding(bounds, pose) == expected,
                    name + " detector " + i);
            }
        }

        private static void CheckAgainstOracle(
            OutlinePair outlines,
            RectangleLocalBounds bounds,
            RectanglePose pose,
            string name) {
            bool expected = IndependentOracle.IsColliding(
                outlines.Outline1, outlines.Outline2, bounds, pose);
            ICollisionDetector[] detectors = CreateAll(outlines);
            for (int i = 0; i < detectors.Length; ++i) {
                Assert(detectors[i].IsColliding(bounds, pose) == expected,
                    name + " detector " + i);
            }
        }

        private static void CompareSegmentPredicate(
            CoordinateXY a,
            CoordinateXY b,
            CoordinateXY c,
            CoordinateXY d,
            string name) {
            bool expected = IndependentOracle.SegmentsIntersect(a, b, c, d);
            bool actual = RobustPredicates.SegmentsIntersect(
                new PointF(a.X, a.Y),
                new PointF(b.X, b.Y),
                new PointF(c.X, c.Y),
                new PointF(d.X, d.Y));
            Assert(expected == actual, name);
        }

        private static CoordinateXY RandomCoordinate(Random random) {
            return new CoordinateXY(RandomFiniteSingle(random), RandomFiniteSingle(random));
        }

        private static float RandomFiniteSingle(Random random) {
            int mode = random.Next(4);
            if (mode == 0) {
                return NextFloat(random, -1_000_000.0f, 1_000_000.0f);
            }

            byte[] bytes = new byte[4];
            random.NextBytes(bytes);
            int bits = BitConverter.ToInt32(bytes, 0);
            if ((bits & 0x7f800000) == 0x7f800000) {
                bits &= unchecked((int)0x807fffff);
                bits |= 0x7f000000;
            }

            return BitConverter.Int32BitsToSingle(bits);
        }

        private static float NextFloat(Random random, float minimum, float maximum) {
            return (float)(minimum + (maximum - minimum) * random.NextDouble());
        }

        private static float NextUp(float value) {
            if (value == 0.0f) {
                return float.Epsilon;
            }

            int bits = BitConverter.SingleToInt32Bits(value);
            return BitConverter.Int32BitsToSingle(value > 0.0f ? bits + 1 : bits - 1);
        }

        private static float NextDown(float value) {
            if (value == 0.0f) {
                return -float.Epsilon;
            }

            int bits = BitConverter.SingleToInt32Bits(value);
            return BitConverter.Int32BitsToSingle(value > 0.0f ? bits - 1 : bits + 1);
        }

        private static RectangleLocalBounds Bounds(float minX, float minY, float maxX, float maxY) {
            return new RectangleLocalBounds(minX, minY, maxX, maxY);
        }

        private static RectanglePose Pose(float x, float y, float degrees) {
            return new RectanglePose(x, y, degrees);
        }

        private static Type[] DetectorTypes() {
            return new[]
            {
                typeof(LinearScanCollisionDetector),
                typeof(BvhCollisionDetector),
                typeof(UniformGridCollisionDetector),
            };
        }

        private static DetectorKind[] DetectorKinds() {
            return new[] { DetectorKind.Linear, DetectorKind.Bvh, DetectorKind.Grid };
        }

        private static ICollisionDetector[] CreateAll(OutlinePair outlines) {
            return new[]
            {
                Create(DetectorKind.Linear, outlines.CloneOutline1(), outlines.CloneOutline2()),
                Create(DetectorKind.Bvh, outlines.CloneOutline1(), outlines.CloneOutline2()),
                Create(DetectorKind.Grid, outlines.CloneOutline1(), outlines.CloneOutline2()),
            };
        }

        private static ICollisionDetector Create(
            DetectorKind kind,
            List<CoordinateXY> outline1,
            List<CoordinateXY> outline2) {
            return kind switch {
                DetectorKind.Linear => new LinearScanCollisionDetector(outline1, outline2),
                DetectorKind.Bvh => new BvhCollisionDetector(outline1, outline2),
                DetectorKind.Grid => new UniformGridCollisionDetector(outline1, outline2),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
        }

        private static void AssertListsEqual(
            CoordinateXY[] expected,
            List<CoordinateXY> actual,
            string name) {
            Assert(expected.Length == actual.Count, name + " count");
            for (int i = 0; i < expected.Length; ++i) {
                Assert(BitConverter.SingleToInt32Bits(expected[i].X)
                        == BitConverter.SingleToInt32Bits(actual[i].X)
                    && BitConverter.SingleToInt32Bits(expected[i].Y)
                        == BitConverter.SingleToInt32Bits(actual[i].Y),
                    name + " element " + i);
            }
        }

        private static void AssertThrows<TException>(Action action, string name)
            where TException : Exception {
            try {
                action();
            } catch (TException) {
                ++_assertionCount;
                return;
            }

            throw new InvalidOperationException("Expected " + typeof(TException).Name + ": " + name);
        }

        private static void Assert(bool condition, string name) {
            ++_assertionCount;
            if (!condition) {
                throw new InvalidOperationException("Assertion failed: " + name);
            }
        }

        private enum DetectorKind {
            Linear,
            Bvh,
            Grid,
        }
    }
}
