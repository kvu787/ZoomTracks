using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ZoomTracks;

namespace ZoomTracksProductionTests;

internal static class Program {
    private const int ExpectedTrack001OutlineCount = 3;
    private const int ExpectedTrack001EdgeCount = 1984;
    private const int RandomSeed = 0x5A17C011;
    private const int RandomQueryCount = 8192;
    private const int AllocationQueryCount = 100_000;
    private const int BenchmarkQueryCount = 4096;
    private const int BenchmarkRepetitions = 4;

    // Blender local Y reverses when imported as Unity local Z, so the Blender
    // [-3.157522, 3.0] interval becomes Unity Z [-3.0, 3.157522].
    private static readonly RectangleLocalBounds Track001CarBounds = new(
        -1.5f,
        -3.0f,
        1.5f,
        3.157522f);

    public static int Main() {
        try {
            Run();
            Console.WriteLine("PASS: ZoomTracks production collision validation completed.");
            return 0;
        } catch (Exception exception) {
            Console.Error.WriteLine("FAIL: " + exception);
            return 1;
        }
    }

    private static void Run() {
        string repositoryRoot = FindRepositoryRoot();
        string jsonPath = Path.Combine(
            repositoryRoot,
            "ZoomTracks",
            "Assets",
            "StreamingAssets",
            "Track001_ColliderData.json");
        ColliderJson colliderJson = LoadColliderJson(jsonPath);

        ValidateTrack001SchemaAndShape(colliderJson);
        RuntimeBounds runtimeBounds = ValidateTrack001CoordinateMapping(colliderJson);

        TrackCollisionDetector detector = new(colliderJson, Track001CarBounds);
        ValidateTrack001Index(detector);
        ValidateExactContactSemantics();
        ValidateArbitraryOutlineCount();
        ValidateSparseGrid();
        ValidateBothAxisConversion();
        ValidateTrack001KnownEndpoint(detector, colliderJson);

        BuildTrack001Queries(
            colliderJson,
            runtimeBounds,
            out List<QueryCase> randomQueries,
            out List<QueryCase> allQueries);
        CompareOptimizedWithLinear(detector, allQueries);
        ValidateZeroSteadyStateAllocation(detector, randomQueries);
        RunComparativeBenchmark(detector, randomQueries);
    }

    private static string FindRepositoryRoot() {
        string[] starts = [Environment.CurrentDirectory, AppContext.BaseDirectory];
        foreach (string start in starts) {
            DirectoryInfo directory = new(start);
            while (directory != null) {
                string marker = Path.Combine(
                    directory.FullName,
                    "ZoomTracks",
                    "Assets",
                    "StreamingAssets",
                    "Track001_ColliderData.json");
                if (File.Exists(marker)) {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the ZoomTracks repository root from the current "
            + "directory or the harness output directory.");
    }

    private static ColliderJson LoadColliderJson(string path) {
        Require(File.Exists(path), $"Track001 collider JSON is missing at '{path}'.");
        string contents = File.ReadAllText(path);
        JsonSerializerOptions options = new() {
            IncludeFields = true,
        };
        ColliderJson colliderJson = JsonSerializer.Deserialize<ColliderJson>(contents, options);
        Require(colliderJson != null, "Track001 collider JSON deserialized to null.");
        return colliderJson;
    }

    private static void ValidateTrack001SchemaAndShape(ColliderJson colliderJson) {
        Require(
            colliderJson.FormatVersion == ColliderJson.CurrentFormatVersion,
            $"Expected FormatVersion {ColliderJson.CurrentFormatVersion}, found "
            + $"{colliderJson.FormatVersion}.");
        Require(
            string.Equals(
                colliderJson.CoordinateSystem,
                ColliderJson.BlenderWorldXYCoordinateSystem,
                StringComparison.Ordinal),
            $"Expected CoordinateSystem '{ColliderJson.BlenderWorldXYCoordinateSystem}', "
            + $"found '{colliderJson.CoordinateSystem ?? "<null>"}'.");
        Require(colliderJson.Outlines != null, "Track001 outlines are null.");
        Require(
            colliderJson.Outlines.Count == ExpectedTrack001OutlineCount,
            $"Expected {ExpectedTrack001OutlineCount} Track001 outlines, found "
            + $"{colliderJson.Outlines.Count}.");

        int[] expectedCounts = [1024, 448, 512];
        int edgeCount = 0;
        for (int outlineIndex = 0; outlineIndex < colliderJson.Outlines.Count; ++outlineIndex) {
            Outline outline = colliderJson.Outlines[outlineIndex];
            Require(outline?.Vertices != null, $"Track001 outline {outlineIndex} is null.");
            Require(
                outline.Vertices.Count == expectedCounts[outlineIndex],
                $"Track001 outline {outlineIndex} expected {expectedCounts[outlineIndex]} "
                + $"vertices, found {outline.Vertices.Count}.");
            edgeCount += outline.Vertices.Count;
        }

        Require(
            edgeCount == ExpectedTrack001EdgeCount,
            $"Expected {ExpectedTrack001EdgeCount} Track001 edges, found {edgeCount}.");
    }

    private static RuntimeBounds ValidateTrack001CoordinateMapping(ColliderJson colliderJson) {
        RuntimeBounds bounds = ComputeRuntimeBounds(colliderJson);
        RequireNear(bounds.MinX, -94.7042007446289f, 0.00001f, "runtime minimum X");
        RequireNear(bounds.MinY, -61.13170623779297f, 0.00001f, "runtime minimum Y/Z");
        RequireNear(bounds.MaxX, 59.99061965942383f, 0.00001f, "runtime maximum X");
        RequireNear(bounds.MaxY, 72.60057067871094f, 0.00001f, "runtime maximum Y/Z");

        CoordinateXY firstRaw = colliderJson.Outlines[0].Vertices[0];
        RuntimePoint firstRuntime = ToRuntime(firstRaw);
        Require(
            firstRaw.X < 0f
                && firstRaw.Y < 0f
                && firstRuntime.X > 0f
                && firstRuntime.Y > 0f,
            "The first Track001 vertex did not demonstrate both required sign flips.");
        Require(firstRuntime.X == -firstRaw.X, "Track001 X conversion is not -Blender X.");
        Require(firstRuntime.Y == -firstRaw.Y, "Track001 Y/Z conversion is not -Blender Y.");
        return bounds;
    }

    private static void ValidateTrack001Index(TrackCollisionDetector detector) {
        Require(
            detector.OutlineCount == ExpectedTrack001OutlineCount,
            $"Detector indexed {detector.OutlineCount} outlines instead of "
            + $"{ExpectedTrack001OutlineCount}.");
        Require(
            detector.EdgeCount == ExpectedTrack001EdgeCount,
            $"Detector indexed {detector.EdgeCount} edges instead of "
            + $"{ExpectedTrack001EdgeCount}.");
        RequireNear((float)detector.CellSize, 3f, 0.00001f, "Track001 grid cell size");
        Require(
            detector.OrdinaryEdgeCount == ExpectedTrack001EdgeCount,
            $"Expected all Track001 edges to be ordinary, found "
            + $"{detector.OrdinaryEdgeCount} ordinary edges.");
        Require(
            detector.OutlierEdgeCount == 0,
            $"Expected no Track001 outlier edges, found {detector.OutlierEdgeCount}.");
        Require(
            detector.StoredGridEdgeReferenceCount == ExpectedTrack001EdgeCount,
            "Each Track001 edge must have exactly one center-grid reference; found "
            + $"{detector.StoredGridEdgeReferenceCount} references for "
            + $"{ExpectedTrack001EdgeCount} edges.");
        Require(
            detector.GridColumnCount == 52 && detector.GridRowCount == 45,
            "Expected a 52x45 Track001 logical grid, found "
            + $"{detector.GridColumnCount}x{detector.GridRowCount}.");
        Require(
            detector.OccupiedGridCellCount == 340,
            $"Expected 340 occupied Track001 cells, found "
            + $"{detector.OccupiedGridCellCount}.");
        Require(detector.UsesDenseGrid, "Track001 should use the dense grid layout.");

        long gridCellCount = checked(
            (long)detector.GridColumnCount * detector.GridRowCount);
        Console.WriteLine(
            "Track001 index: "
            + $"edges={detector.EdgeCount}, cell={detector.CellSize:F6}, "
            + $"grid={detector.GridColumnCount}x{detector.GridRowCount} "
            + $"({gridCellCount} cells), occupied={detector.OccupiedGridCellCount}, "
            + $"ordinary={detector.OrdinaryEdgeCount}, outliers={detector.OutlierEdgeCount}, "
            + $"references={detector.StoredGridEdgeReferenceCount}, "
            + $"dense={detector.UsesDenseGrid}.");
    }

    private static void ValidateExactContactSemantics() {
        ColliderJson square = CreateRuntimeCollider(
            new RuntimePoint(0f, 0f),
            new RuntimePoint(10f, 0f),
            new RuntimePoint(10f, 10f),
            new RuntimePoint(0f, 10f));
        TrackCollisionDetector detector = new(
            square,
            new RectangleLocalBounds(-1f, -1f, 1f, 1f));

        AssertQuery(detector, new RectangleLocalBounds(0f, 0f, 2f, 2f),
            new RectanglePose(10f, 10f, 0f), true, "endpoint contact");
        AssertQuery(detector, new RectangleLocalBounds(0f, -1f, 2f, 1f),
            new RectanglePose(9f, 5f, 0f), true, "proper crossing");
        AssertQuery(detector, new RectangleLocalBounds(0f, -1f, 2f, 1f),
            new RectanglePose(10f, 5f, 0f), true, "collinear overlap");

        const float tangentAngle = 45f;
        double radians = tangentAngle * Math.PI / 180.0;
        double cornerOffsetX = -Math.Cos(radians) - Math.Sin(radians);
        double cornerOffsetY = Math.Sin(radians) - Math.Cos(radians);
        float tangentPositionX = FindPositionProducingCoordinate(10f, cornerOffsetX);
        float tangentPositionY = FindPositionProducingCoordinate(5f, cornerOffsetY);
        AssertQuery(detector, new RectangleLocalBounds(-1f, -1f, 1f, 1f),
            new RectanglePose(tangentPositionX, tangentPositionY, tangentAngle),
            true, "single-point tangent contact");

        AssertQuery(detector, new RectangleLocalBounds(-1f, -1f, 1f, 1f),
            new RectanglePose(5f, 5f, 0f), false, "rectangle contained by outline");
        AssertQuery(detector, new RectangleLocalBounds(-6f, -6f, 6f, 6f),
            new RectanglePose(5f, 5f, 0f), false, "outline contained by rectangle");
        AssertQuery(detector, new RectangleLocalBounds(-1f, -1f, 1f, 1f),
            new RectanglePose(20f, 20f, 0f), false, "disjoint geometry");
    }

    private static void ValidateArbitraryOutlineCount() {
        ColliderJson colliderJson = CreateRuntimeCollider(
            SquareLoop(-20f, 0f, 2f),
            SquareLoop(0f, 0f, 2f),
            SquareLoop(20f, 0f, 2f));
        TrackCollisionDetector detector = new(
            colliderJson,
            new RectangleLocalBounds(-0.5f, -0.5f, 0.5f, 0.5f));
        Require(detector.OutlineCount == 3, "Arbitrary-loop detector lost an outline.");
        Require(detector.EdgeCount == 12, "Arbitrary-loop detector expected 12 edges.");
        Require(
            detector.OutlierEdgeCount == 12 && detector.OutlierBvhNodeCount > 0,
            "The arbitrary-loop fixture must exercise the oversized-edge BVH.");

        foreach (float centerX in new[] { -20f, 0f, 20f }) {
            AssertQuery(detector, new RectangleLocalBounds(-2.5f, -0.25f, 2.5f, 0.25f),
                new RectanglePose(centerX, 0f, 0f), true,
                $"collision against loop centered at X={centerX}");
        }

        AssertQuery(detector, new RectangleLocalBounds(-0.5f, -0.5f, 0.5f, 0.5f),
            new RectanglePose(40f, 40f, 0f), false, "disjoint arbitrary loops");
    }

    private static void ValidateSparseGrid() {
        ColliderJson colliderJson = CreateRuntimeCollider(
            SquareLoop(-10_000f, 0f, 0.25f),
            SquareLoop(0f, 0f, 0.25f),
            SquareLoop(10_000f, 0f, 0.25f));
        TrackCollisionDetector detector = new(
            colliderJson,
            new RectangleLocalBounds(-0.5f, -0.5f, 0.5f, 0.5f));
        Require(!detector.UsesDenseGrid, "Widely separated loops must use the sparse grid.");
        Require(
            detector.OrdinaryEdgeCount == 12
                && detector.StoredGridEdgeReferenceCount == 12
                && detector.OutlierEdgeCount == 0,
            "The sparse grid must store each of its 12 ordinary edges exactly once.");

        foreach (float centerX in new[] { -10_000f, 0f, 10_000f }) {
            AssertQuery(
                detector,
                new RectangleLocalBounds(-0.5f, -0.1f, 0.5f, 0.1f),
                new RectanglePose(centerX, 0f, 0f),
                true,
                $"sparse-grid loop centered at X={centerX}");
        }

        AssertQuery(
            detector,
            new RectangleLocalBounds(-0.5f, -0.5f, 0.5f, 0.5f),
            new RectanglePose(5_000f, 5_000f, 0f),
            false,
            "sparse-grid miss");
    }

    private static void ValidateBothAxisConversion() {
        ColliderJson colliderJson = CreateRuntimeCollider(
            new RuntimePoint(-12f, -22f),
            new RuntimePoint(-10f, -22f),
            new RuntimePoint(-10f, -20f),
            new RuntimePoint(-12f, -20f));
        Require(
            colliderJson.Outlines[0].Vertices[0].X == 12f
                && colliderJson.Outlines[0].Vertices[0].Y == 22f,
            "Synthetic mapping fixture did not store raw positive Blender coordinates.");

        TrackCollisionDetector detector = new(
            colliderJson,
            new RectangleLocalBounds(-0.5f, -0.5f, 0.5f, 0.5f));
        RectangleLocalBounds crossingBounds = new(0f, -0.25f, 1f, 0.25f);
        AssertQuery(detector, crossingBounds, new RectanglePose(-10.5f, -21f, 0f),
            true, "both-axis converted location");
        AssertQuery(detector, crossingBounds, new RectanglePose(10.5f, 21f, 0f),
            false, "unconverted Blender location");
        AssertQuery(detector, crossingBounds, new RectanglePose(-10.5f, 21f, 0f),
            false, "X-only converted location");
        AssertQuery(detector, crossingBounds, new RectanglePose(10.5f, -21f, 0f),
            false, "Y-only converted location");
    }

    private static void ValidateTrack001KnownEndpoint(
        TrackCollisionDetector detector,
        ColliderJson colliderJson) {
        RuntimePoint vertex = ToRuntime(colliderJson.Outlines[0].Vertices[0]);
        AssertQuery(
            detector,
            new RectangleLocalBounds(0f, 0f, 1f, 1f),
            new RectanglePose(vertex.X, vertex.Y, 0f),
            true,
            "Track001 first/closing-edge endpoint");
    }

    private static void BuildTrack001Queries(
        ColliderJson colliderJson,
        RuntimeBounds runtimeBounds,
        out List<QueryCase> randomQueries,
        out List<QueryCase> allQueries) {
        Random random = new(RandomSeed);
        randomQueries = new List<QueryCase>(RandomQueryCount);
        const float margin = 10f;
        for (int queryIndex = 0; queryIndex < RandomQueryCount; ++queryIndex) {
            float x = NextFloat(random, runtimeBounds.MinX - margin, runtimeBounds.MaxX + margin);
            float y = NextFloat(random, runtimeBounds.MinY - margin, runtimeBounds.MaxY + margin);
            float rotation = NextFloat(random, -720f, 720f);
            randomQueries.Add(new QueryCase(
                Track001CarBounds,
                new RectanglePose(x, y, rotation),
                $"random query {queryIndex}"));
        }

        allQueries = new List<QueryCase>(
            RandomQueryCount + ExpectedTrack001EdgeCount * 2);
        allQueries.AddRange(randomQueries);

        int visitedEdges = 0;
        int visitedClosingEdges = 0;
        for (int outlineIndex = 0; outlineIndex < colliderJson.Outlines.Count; ++outlineIndex) {
            List<CoordinateXY> vertices = colliderJson.Outlines[outlineIndex].Vertices;
            for (int edgeIndex = 0; edgeIndex < vertices.Count; ++edgeIndex) {
                RuntimePoint a = ToRuntime(vertices[edgeIndex]);
                RuntimePoint b = ToRuntime(vertices[(edgeIndex + 1) % vertices.Count]);
                float dx = b.X - a.X;
                float dy = b.Y - a.Y;
                float edgeLength = MathF.Sqrt(dx * dx + dy * dy);
                Require(edgeLength > 0f, $"Track001 edge {outlineIndex}:{edgeIndex} is zero.");

                float rotation = MathF.Atan2(dx, dy) * (180f / MathF.PI);
                RuntimePoint midpoint = new((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f);
                float localCenterY = (Track001CarBounds.MinY + Track001CarBounds.MaxY) * 0.5f;
                RectanglePose onEdge = PlaceLocalPointAtWorld(
                    Track001CarBounds.MinX,
                    localCenterY,
                    midpoint,
                    rotation);
                allQueries.Add(new QueryCase(
                    Track001CarBounds,
                    onEdge,
                    $"edge neighborhood {outlineIndex}:{edgeIndex} on-edge"));

                double radians = (double)rotation * Math.PI / 180.0;
                float normalX = (float)Math.Cos(radians);
                float normalY = (float)-Math.Sin(radians);
                allQueries.Add(new QueryCase(
                    Track001CarBounds,
                    new RectanglePose(
                        onEdge.PositionX + normalX * 0.35f,
                        onEdge.PositionY + normalY * 0.35f,
                        rotation),
                    $"edge neighborhood {outlineIndex}:{edgeIndex} offset"));

                ++visitedEdges;
                if (edgeIndex + 1 == vertices.Count) {
                    ++visitedClosingEdges;
                }
            }
        }

        Require(
            visitedEdges == ExpectedTrack001EdgeCount,
            $"Edge-neighborhood generation visited {visitedEdges} of "
            + $"{ExpectedTrack001EdgeCount} edges.");
        Require(
            visitedClosingEdges == ExpectedTrack001OutlineCount,
            $"Edge-neighborhood generation visited {visitedClosingEdges} closing edges.");
    }

    private static void CompareOptimizedWithLinear(
        TrackCollisionDetector detector,
        List<QueryCase> queries) {
        int collisionCount = 0;
        for (int queryIndex = 0; queryIndex < queries.Count; ++queryIndex) {
            QueryCase query = queries[queryIndex];
            bool expected = detector.IsCollidingLinear(query.Bounds, query.Pose);
            bool actual = detector.IsColliding(query.Bounds, query.Pose);
            if (actual != expected) {
                throw new InvalidOperationException(
                    $"Optimized/linear mismatch for {query.Description}: expected "
                    + $"{expected}, found {actual}; pose=({query.Pose.PositionX:R}, "
                    + $"{query.Pose.PositionY:R}, {query.Pose.RotationDegrees:R}).");
            }

            if (actual) {
                ++collisionCount;
            }
        }

        Console.WriteLine(
            $"Correctness workload: {queries.Count} optimized/linear comparisons, "
            + $"{collisionCount} collisions; covered all {ExpectedTrack001EdgeCount} "
            + "edges and all 3 closing edges.");
    }

    private static void ValidateZeroSteadyStateAllocation(
        TrackCollisionDetector detector,
        List<QueryCase> randomQueries) {
        bool checksum = false;
        for (int iteration = 0; iteration < 20_000; ++iteration) {
            QueryCase query = randomQueries[iteration & (RandomQueryCount - 1)];
            checksum ^= detector.IsColliding(query.Bounds, query.Pose);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < AllocationQueryCount; ++iteration) {
            QueryCase query = randomQueries[iteration & (RandomQueryCount - 1)];
            checksum ^= detector.IsColliding(query.Bounds, query.Pose);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(checksum);
        Require(
            allocated == 0,
            $"Ordinary optimized queries allocated {allocated} bytes after warmup.");
        Console.WriteLine(
            $"Allocation workload: {AllocationQueryCount} optimized queries, 0 bytes.");
    }

    private static void RunComparativeBenchmark(
        TrackCollisionDetector detector,
        List<QueryCase> randomQueries) {
        RunComparativeBenchmarkCase(
            "mixed deterministic",
            detector,
            randomQueries,
            BenchmarkQueryCount);

        List<QueryCase> nonContactQueries = new(BenchmarkQueryCount);
        for (int i = 0;
            i < randomQueries.Count && nonContactQueries.Count < BenchmarkQueryCount;
            ++i) {
            QueryCase query = randomQueries[i];
            if (!detector.IsCollidingLinear(query.Bounds, query.Pose)) {
                nonContactQueries.Add(query);
            }
        }

        Require(
            nonContactQueries.Count == BenchmarkQueryCount,
            $"Expected {BenchmarkQueryCount} deterministic non-contact benchmark "
            + $"queries, found {nonContactQueries.Count}.");
        RunComparativeBenchmarkCase(
            "non-contact steady-state",
            detector,
            nonContactQueries,
            nonContactQueries.Count);
    }

    private static void RunComparativeBenchmarkCase(
        string name,
        TrackCollisionDetector detector,
        List<QueryCase> queries,
        int queryCount) {
        for (int i = 0; i < queryCount; ++i) {
            QueryCase query = queries[i];
            _ = detector.IsColliding(query.Bounds, query.Pose);
            _ = detector.IsCollidingLinear(query.Bounds, query.Pose);
        }

        Measurement optimized = Measure(
            detector,
            queries,
            queryCount,
            BenchmarkRepetitions,
            useLinear: false);
        Measurement linear = Measure(
            detector,
            queries,
            queryCount,
            BenchmarkRepetitions,
            useLinear: true);
        Require(
            optimized.CollisionCount == linear.CollisionCount,
            "Benchmark optimized/linear checksums differ.");

        double optimizedNanoseconds = optimized.Elapsed.TotalMilliseconds
            * 1_000_000.0 / optimized.QueryCount;
        double linearNanoseconds = linear.Elapsed.TotalMilliseconds
            * 1_000_000.0 / linear.QueryCount;
        double speedup = linearNanoseconds / optimizedNanoseconds;
        Console.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "Benchmark {0} ({1:N0} queries each): optimized={2:N1} ns/query, "
                + "linear={3:N1} ns/query, relative speedup={4:N2}x. "
                + "No absolute timing threshold is enforced.",
            name,
            optimized.QueryCount,
            optimizedNanoseconds,
            linearNanoseconds,
            speedup));
    }

    private static Measurement Measure(
        TrackCollisionDetector detector,
        List<QueryCase> queries,
        int queryCount,
        int repetitions,
        bool useLinear) {
        int collisions = 0;
        long start = Stopwatch.GetTimestamp();
        for (int repetition = 0; repetition < repetitions; ++repetition) {
            for (int queryIndex = 0; queryIndex < queryCount; ++queryIndex) {
                QueryCase query = queries[queryIndex];
                bool colliding = useLinear
                    ? detector.IsCollidingLinear(query.Bounds, query.Pose)
                    : detector.IsColliding(query.Bounds, query.Pose);
                if (colliding) {
                    ++collisions;
                }
            }
        }

        TimeSpan elapsed = Stopwatch.GetElapsedTime(start);
        return new Measurement(elapsed, checked(queryCount * repetitions), collisions);
    }

    private static void AssertQuery(
        TrackCollisionDetector detector,
        RectangleLocalBounds bounds,
        RectanglePose pose,
        bool expected,
        string description) {
        bool linear = detector.IsCollidingLinear(bounds, pose);
        bool optimized = detector.IsColliding(bounds, pose);
        Require(
            linear == expected,
            $"Linear result for {description}: expected {expected}, found {linear}.");
        Require(
            optimized == expected,
            $"Optimized result for {description}: expected {expected}, found {optimized}.");
    }

    private static ColliderJson CreateRuntimeCollider(params RuntimePoint[] runtimeLoop) {
        return CreateRuntimeCollider([runtimeLoop]);
    }

    private static ColliderJson CreateRuntimeCollider(params RuntimePoint[][] runtimeLoops) {
        List<Outline> outlines = new(runtimeLoops.Length);
        foreach (RuntimePoint[] runtimeLoop in runtimeLoops) {
            List<CoordinateXY> vertices = new(runtimeLoop.Length);
            foreach (RuntimePoint point in runtimeLoop) {
                vertices.Add(new CoordinateXY(-point.X, -point.Y));
            }

            outlines.Add(new Outline { Vertices = vertices });
        }

        return new ColliderJson {
            FormatVersion = ColliderJson.CurrentFormatVersion,
            CoordinateSystem = ColliderJson.BlenderWorldXYCoordinateSystem,
            Outlines = outlines,
        };
    }

    private static RuntimePoint[] SquareLoop(float centerX, float centerY, float halfExtent) {
        return [
            new RuntimePoint(centerX - halfExtent, centerY - halfExtent),
            new RuntimePoint(centerX + halfExtent, centerY - halfExtent),
            new RuntimePoint(centerX + halfExtent, centerY + halfExtent),
            new RuntimePoint(centerX - halfExtent, centerY + halfExtent),
        ];
    }

    private static RuntimeBounds ComputeRuntimeBounds(ColliderJson colliderJson) {
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        foreach (Outline outline in colliderJson.Outlines) {
            foreach (CoordinateXY rawPoint in outline.Vertices) {
                RuntimePoint point = ToRuntime(rawPoint);
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }

        return new RuntimeBounds(minX, minY, maxX, maxY);
    }

    private static RuntimePoint ToRuntime(CoordinateXY rawPoint) {
        return new RuntimePoint(-rawPoint.X, -rawPoint.Y);
    }

    private static RectanglePose PlaceLocalPointAtWorld(
        float localX,
        float localY,
        RuntimePoint worldPoint,
        float rotationDegrees) {
        double radians = (double)rotationDegrees * Math.PI / 180.0;
        double cosine = Math.Cos(radians);
        double sine = Math.Sin(radians);
        double offsetX = (double)localX * cosine + (double)localY * sine;
        double offsetY = -(double)localX * sine + (double)localY * cosine;
        return new RectanglePose(
            (float)((double)worldPoint.X - offsetX),
            (float)((double)worldPoint.Y - offsetY),
            rotationDegrees);
    }

    private static float FindPositionProducingCoordinate(float target, double offset) {
        float candidate = (float)((double)target - offset);
        for (int step = 0; step < 16; ++step) {
            if ((float)((double)candidate + offset) == target) {
                return candidate;
            }

            float below = candidate;
            float above = candidate;
            for (int neighbor = 0; neighbor <= step; ++neighbor) {
                below = MathF.BitDecrement(below);
                above = MathF.BitIncrement(above);
            }

            if ((float)((double)below + offset) == target) {
                return below;
            }

            if ((float)((double)above + offset) == target) {
                return above;
            }
        }

        throw new InvalidOperationException(
            $"Could not construct an exact transformed coordinate for {target:R}.");
    }

    private static float NextFloat(Random random, float minimum, float maximum) {
        return minimum + random.NextSingle() * (maximum - minimum);
    }

    private static void RequireNear(float actual, float expected, float tolerance, string name) {
        Require(
            Math.Abs(actual - expected) <= tolerance,
            $"Unexpected {name}: expected {expected:R} +/- {tolerance:R}, "
            + $"found {actual:R}.");
    }

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
        }
    }

    private readonly record struct RuntimePoint(float X, float Y);

    private readonly record struct RuntimeBounds(float MinX, float MinY, float MaxX, float MaxY);

    private readonly record struct QueryCase(
        RectangleLocalBounds Bounds,
        RectanglePose Pose,
        string Description);

    private readonly record struct Measurement(
        TimeSpan Elapsed,
        int QueryCount,
        int CollisionCount);
}
