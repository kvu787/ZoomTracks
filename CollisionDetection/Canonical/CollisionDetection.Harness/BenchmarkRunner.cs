using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using ZoomTracks.CollisionDetection;

namespace CollisionDetection.Harness {
    internal static class BenchmarkRunner {
        private const int BuildSampleCount = 9;
        private const int QuerySampleCount = 11;
        private const double TargetSampleSeconds = 0.15;

        internal static void Run(string outputDirectory) {
            DateTimeOffset runStarted = DateTimeOffset.UtcNow;
            Console.WriteLine("Benchmark environment:");
            Console.WriteLine("  Runtime: " + RuntimeInformation.FrameworkDescription);
            Console.WriteLine("  OS: " + RuntimeInformation.OSDescription);
            Console.WriteLine("  Architecture: " + RuntimeInformation.ProcessArchitecture);
            Console.WriteLine("  Logical processors: " + Environment.ProcessorCount);
            Console.WriteLine("  GC: " + (GCSettingsIsServer() ? "server" : "workstation"));
            Console.WriteLine("  Stopwatch frequency: " + Stopwatch.Frequency.ToString("N0", CultureInfo.InvariantCulture) + " Hz");
            Console.WriteLine("  Build: Release; no debugger=" + (!Debugger.IsAttached));
            Console.WriteLine("  DOTNET_TieredCompilation: "
                + (Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "<unset>"));
            Console.WriteLine();

            List<BenchmarkScenario> scenarios = BuildScenarios();
            AlgorithmSpec[] algorithms = Algorithms();
            List<RawRow> rawRows = new();

            Console.WriteLine("Preprocessing (median +/- MAD; allocations exclude transferred input lists):");
            Console.WriteLine("| Scenario | N | Algorithm | Build ms | MAD ms | Alloc KiB | Index details |");
            Console.WriteLine("|---|---:|---|---:|---:|---:|---|");

            List<ScenarioMeasurements> allMeasurements = new();
            for (int scenarioIndex = 0; scenarioIndex < scenarios.Count; ++scenarioIndex) {
                BenchmarkScenario scenario = scenarios[scenarioIndex];
                scenario.PrepareExpected();
                ScenarioMeasurements measurements = new(scenario);

                // Rotate order to spread tiering/thermal effects across algorithms.
                for (int offset = 0; offset < algorithms.Length; ++offset) {
                    AlgorithmSpec algorithm = algorithms[(scenarioIndex + offset) % algorithms.Length];
                    BuildMeasurement build = MeasureBuild(algorithm, scenario);
                    measurements.Builds.Add(build);
                    Console.WriteLine(
                        "| {0} | {1} | {2} | {3:F4} | {4:F4} | {5:F2} | {6} |",
                        scenario.Name,
                        scenario.Outlines.EdgeCount,
                        algorithm.Name,
                        build.MedianMilliseconds,
                        build.MadMilliseconds,
                        build.MedianAllocatedBytes / 1024.0,
                        build.IndexDetails);
                    rawRows.Add(RawRow.Build(scenario, algorithm, build));
                }

                allMeasurements.Add(measurements);
            }

            Console.WriteLine();
            Console.WriteLine("Queries (IsColliding includes rectangle transformation; median +/- MAD):");
            Console.WriteLine("| Scenario | N | Q | Hits | Algorithm | us/query | MAD | B/query | Repeats/sample |");
            Console.WriteLine("|---|---:|---:|---:|---|---:|---:|---:|---:|");

            foreach (ScenarioMeasurements scenarioMeasurements in allMeasurements) {
                BenchmarkScenario scenario = scenarioMeasurements.Scenario;
                foreach (BuildMeasurement build in scenarioMeasurements.Builds) {
                    QueryMeasurement query = MeasureQueries(build.Detector, scenario);
                    scenarioMeasurements.Queries.Add(
                        new AlgorithmQueryMeasurement(build.Algorithm, query));
                    Console.WriteLine(
                        "| {0} | {1} | {2} | {3} | {4} | {5:F4} | {6:F4} | {7:F2} | {8} |",
                        scenario.Name,
                        scenario.Outlines.EdgeCount,
                        scenario.Queries.Length,
                        scenario.HitCount,
                        build.Algorithm.Name,
                        query.MedianNanosecondsPerQuery / 1000.0,
                        query.MadNanosecondsPerQuery / 1000.0,
                        query.BytesPerQuery,
                        query.RepeatsPerSample);
                    rawRows.Add(RawRow.Query(scenario, build.Algorithm, query));
                }
            }

            Console.WriteLine();
            Console.WriteLine("Checksums and all exact-oracle comparisons passed.");

            if (!string.IsNullOrEmpty(outputDirectory)) {
                _ = Directory.CreateDirectory(outputDirectory);
                string csvPath = Path.Combine(outputDirectory, "benchmark-results.csv");
                List<string> lines = new() {
                    "scenario,N,queries,hits,algorithm,metric,median,mad,unit,allocated_bytes,index_details"
                };
                lines.AddRange(rawRows.Select(row => row.ToCsv()));
                File.WriteAllLines(csvPath, lines);
                Console.WriteLine("Raw CSV: " + Path.GetFullPath(csvPath));

                string manifestPath = Path.Combine(outputDirectory, "benchmark-environment.txt");
                File.WriteAllText(
                    manifestPath,
                    CreateEnvironmentAndSourceManifest(runStarted, DateTimeOffset.UtcNow));
                Console.WriteLine("Environment/source manifest: " + Path.GetFullPath(manifestPath));
            }

            GC.KeepAlive(allMeasurements);
        }

        private static BuildMeasurement MeasureBuild(
            AlgorithmSpec algorithm,
            BenchmarkScenario scenario) {
            ICollisionDetector warm = algorithm.Create(
                scenario.Outlines.CloneOutline1(),
                scenario.Outlines.CloneOutline2());
            _ = warm.IsColliding(scenario.Queries[0].Bounds, scenario.Queries[0].Pose);
            GC.KeepAlive(warm);

            double[] elapsedMilliseconds = new double[BuildSampleCount];
            double[] allocatedBytes = new double[BuildSampleCount];
            ICollisionDetector retained = null;
            int batchSize = Math.Max(
                8,
                Math.Min(4_096, 262_144 / scenario.Outlines.EdgeCount));
            for (int sample = 0; sample < BuildSampleCount; ++sample) {
                List<CoordinateXY>[] firstLists = new List<CoordinateXY>[batchSize];
                List<CoordinateXY>[] secondLists = new List<CoordinateXY>[batchSize];
                ICollisionDetector[] detectors = new ICollisionDetector[batchSize];
                for (int i = 0; i < batchSize; ++i) {
                    firstLists[i] = scenario.Outlines.CloneOutline1();
                    secondLists[i] = scenario.Outlines.CloneOutline2();
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                long started = Stopwatch.GetTimestamp();
                for (int i = 0; i < batchSize; ++i) {
                    detectors[i] = algorithm.Create(firstLists[i], secondLists[i]);
                }

                long stopped = Stopwatch.GetTimestamp();
                long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
                elapsedMilliseconds[sample] = (stopped - started) * 1000.0
                    / (Stopwatch.Frequency * (double)batchSize);
                allocatedBytes[sample] = (allocatedAfter - allocatedBefore) / (double)batchSize;
                retained = detectors[^1];
                GC.KeepAlive(detectors);
            }

            return new BuildMeasurement(
                algorithm,
                retained,
                Median(elapsedMilliseconds),
                MedianAbsoluteDeviation(elapsedMilliseconds),
                Median(allocatedBytes),
                DescribeIndex(retained) + "; build-batch=" + batchSize);
        }

        private static QueryMeasurement MeasureQueries(
            ICollisionDetector detector,
            BenchmarkScenario scenario) {
            VerifyDetector(detector, scenario);

            for (int i = 0; i < 4; ++i) {
                _ = RunQueryBatch(detector, scenario.Queries, 1);
            }

            long pilotStart = Stopwatch.GetTimestamp();
            int pilotChecksum = RunQueryBatch(detector, scenario.Queries, 1);
            long pilotStop = Stopwatch.GetTimestamp();
            double pilotSeconds = Math.Max(
                1.0 / Stopwatch.Frequency,
                (pilotStop - pilotStart) / (double)Stopwatch.Frequency);
            int repeats = (int)Math.Ceiling(TargetSampleSeconds / pilotSeconds);
            repeats = Math.Max(1, Math.Min(repeats, 100_000));

            long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
            int allocationChecksum = RunQueryBatch(detector, scenario.Queries, 1);
            long allocationAfter = GC.GetAllocatedBytesForCurrentThread();
            double bytesPerQuery = (allocationAfter - allocationBefore) / (double)scenario.Queries.Length;

            double[] nanosecondsPerQuery = new double[QuerySampleCount];
            int combinedChecksum = pilotChecksum ^ allocationChecksum;
            for (int sample = 0; sample < QuerySampleCount; ++sample) {
                long started = Stopwatch.GetTimestamp();
                int checksum = RunQueryBatch(detector, scenario.Queries, repeats);
                long stopped = Stopwatch.GetTimestamp();
                combinedChecksum = unchecked((combinedChecksum * 397) ^ checksum);
                double nanoseconds = (stopped - started) * 1_000_000_000.0 / Stopwatch.Frequency;
                nanosecondsPerQuery[sample] = nanoseconds / (repeats * (double)scenario.Queries.Length);
            }

            if (combinedChecksum == int.MinValue) {
                throw new InvalidOperationException("Unreachable checksum sentinel.");
            }

            return new QueryMeasurement(
                Median(nanosecondsPerQuery),
                MedianAbsoluteDeviation(nanosecondsPerQuery),
                bytesPerQuery,
                repeats,
                combinedChecksum);
        }

        private static void VerifyDetector(
            ICollisionDetector detector,
            BenchmarkScenario scenario) {
            for (int i = 0; i < scenario.Queries.Length; ++i) {
                QueryInput query = scenario.Queries[i];
                bool actual = detector.IsColliding(query.Bounds, query.Pose);
                if (actual != scenario.Expected[i]) {
                    throw new InvalidOperationException(
                        "Benchmark verification failed in " + scenario.Name + " at query " + i + ".");
                }
            }
        }

        private static int RunQueryBatch(
            ICollisionDetector detector,
            QueryInput[] queries,
            int repeats) {
            int checksum = unchecked((int)2166136261U);
            for (int repeat = 0; repeat < repeats; ++repeat) {
                for (int i = 0; i < queries.Length; ++i) {
                    QueryInput query = queries[i];
                    bool result = detector.IsColliding(query.Bounds, query.Pose);
                    checksum = unchecked((checksum ^ (result ? i + 1 : ~i)) * 16777619);
                }
            }

            return checksum;
        }

        private static List<BenchmarkScenario> BuildScenarios() {
            const double edgeLength = 12.0;
            List<BenchmarkScenario> scenarios = new() {
                CreateLocalizedScenario("small mixed", 32, 16, edgeLength, 8_192, 0x1101),
                CreateLocalizedScenario("medium localized", 256, 128, edgeLength, 4_096, 0x2202),
                CreateLocalizedScenario("large localized", 2_048, 1_024, edgeLength, 2_048, 0x3303)
            };

            float veryLargeRadius = RadiusForEdgeLength(8_192, edgeLength);
            OutlinePair veryLarge = TestData.SmoothNestedLoops(
                8_192, 4_096, veryLargeRadius, 1.0, false);
            scenarios.Add(new BenchmarkScenario(
                "very-large localized",
                veryLarge,
                LocalizedQueries(veryLarge, 2_048, veryLargeRadius, 1.0, 0x4404)));
            scenarios.Add(new BenchmarkScenario(
                "very-large enclosing miss",
                veryLarge,
                EnclosingQueries(512, veryLargeRadius, 1.0, 0x5505)));
            scenarios.Add(new BenchmarkScenario(
                "very-large early-edge hit",
                veryLarge,
                EarlyEdgeHitQueries(veryLarge, 2_048, 0x6606)));

            float elongatedRadius = RadiusForEdgeLength(2_048, edgeLength);
            OutlinePair elongated = TestData.SmoothNestedLoops(
                2_048, 1_024, elongatedRadius, 8.0, true);
            scenarios.Add(new BenchmarkScenario(
                "elongated nonuniform",
                elongated,
                LocalizedQueries(elongated, 2_048, elongatedRadius, 8.0, 0x7707)));

            OutlinePair collinear = TestData.SubdividedNestedRectangles(128, 1_000.0f, 400.0f);
            scenarios.Add(new BenchmarkScenario(
                "exact collinear/1-ULP",
                collinear,
                CollinearQueries(2_048, 0x8808)));

            OutlinePair multiscale = TestData.AlternatingRadiusNestedLoops(
                2_048, 1_024, 100.0f, 900.0f, 30.0f);
            scenarios.Add(new BenchmarkScenario(
                "multiscale grid-overflow miss",
                multiscale,
                InteriorQueries(2_048, 0x9909)));
            return scenarios;
        }

        private static BenchmarkScenario CreateLocalizedScenario(
            string name,
            int outerCount,
            int innerCount,
            double edgeLength,
            int queryCount,
            int seed) {
            float radius = RadiusForEdgeLength(outerCount, edgeLength);
            OutlinePair outlines = TestData.SmoothNestedLoops(
                outerCount, innerCount, radius, 1.0, false);
            return new BenchmarkScenario(
                name,
                outlines,
                LocalizedQueries(outlines, queryCount, radius, 1.0, seed));
        }

        private static float RadiusForEdgeLength(int vertexCount, double edgeLength) {
            return (float)(vertexCount * edgeLength / (Math.PI * 2.0));
        }

        private static QueryInput[] LocalizedQueries(
            OutlinePair outlines,
            int count,
            float outerRadius,
            double aspect,
            int seed) {
            Random random = new(seed);
            QueryInput[] queries = new QueryInput[count];
            RectangleLocalBounds[] boundsChoices =
            {
                new(-6.0f, -3.0f, 6.0f, 3.0f),
                new(-9.0f, -2.0f, 4.0f, 5.0f),
                new(-2.0f, -8.0f, 5.0f, 7.0f),
            };
            for (int i = 0; i < count; ++i) {
                double angle = random.NextDouble() * Math.PI * 2.0;
                int category = i & 3;
                double radius = 0.0;
                float positionX;
                float positionY;
                RectangleLocalBounds bounds;
                switch (category) {
                case 0:
                    int edgeIndex = random.Next(outlines.Outline1.Length);
                    CoordinateXY edgeStart = outlines.Outline1[edgeIndex];
                    CoordinateXY edgeEnd = outlines.Outline1[
                        edgeIndex + 1 == outlines.Outline1.Length ? 0 : edgeIndex + 1];
                    positionX = (edgeStart.X + edgeEnd.X) * 0.5f;
                    positionY = (edgeStart.Y + edgeEnd.Y) * 0.5f;
                    bounds = new RectangleLocalBounds(-2.0f, -2.0f, 2.0f, 2.0f);
                    break;
                case 1:
                    radius = outerRadius * (0.05 + random.NextDouble() * 0.18);
                    goto default;
                case 2:
                    radius = outerRadius * (0.50 + random.NextDouble() * 0.18);
                    goto default;
                default:
                    if (category == 3) {
                        radius = outerRadius * (1.30 + random.NextDouble() * 0.25);
                    }

                    positionX = (float)(radius * aspect * Math.Cos(angle));
                    positionY = (float)(radius * Math.Sin(angle));
                    bounds = boundsChoices[i % boundsChoices.Length];
                    break;
                }

                queries[i] = new QueryInput(
                    bounds,
                    new RectanglePose(
                        positionX,
                        positionY,
                        (float)(random.NextDouble() * 1440.0 - 720.0)));
            }

            Shuffle(queries, random);
            return queries;
        }

        private static QueryInput[] EnclosingQueries(
            int count,
            float outerRadius,
            double aspect,
            int seed) {
            Random random = new(seed);
            QueryInput[] queries = new QueryInput[count];
            RectangleLocalBounds bounds = new(
                (float)(-1.25 * outerRadius * aspect),
                -1.25f * outerRadius,
                (float)(1.25 * outerRadius * aspect),
                1.25f * outerRadius);
            for (int i = 0; i < count; ++i) {
                queries[i] = new QueryInput(
                    bounds,
                    new RectanglePose(
                        (float)((random.NextDouble() - 0.5) * outerRadius * 0.06),
                        (float)((random.NextDouble() - 0.5) * outerRadius * 0.06),
                        0.0f));
            }

            return queries;
        }

        private static QueryInput[] EarlyEdgeHitQueries(
            OutlinePair outlines,
            int count,
            int seed) {
            Random random = new(seed);
            CoordinateXY a = outlines.Outline1[0];
            CoordinateXY b = outlines.Outline1[1];
            float centerX = (a.X + b.X) * 0.5f;
            float centerY = (a.Y + b.Y) * 0.5f;
            QueryInput[] queries = new QueryInput[count];
            RectangleLocalBounds bounds = new(-10, -10, 10, 10);
            for (int i = 0; i < count; ++i) {
                queries[i] = new QueryInput(
                    bounds,
                    new RectanglePose(
                        centerX + (float)((random.NextDouble() - 0.5) * 2.0),
                        centerY + (float)((random.NextDouble() - 0.5) * 2.0),
                        (float)(random.NextDouble() * 20.0 - 10.0)));
            }

            return queries;
        }

        private static QueryInput[] CollinearQueries(int count, int seed) {
            Random random = new(seed);
            QueryInput[] queries = new QueryInput[count];
            float below = NextDown(-1_000.0f);
            for (int i = 0; i < count; ++i) {
                float x = (float)(-950.0 + random.NextDouble() * 1_900.0);
                if ((i & 1) == 0) {
                    queries[i] = new QueryInput(
                        new RectangleLocalBounds(-10, 0, 10, 10),
                        new RectanglePose(x, -1_000.0f, 0.0f));
                } else {
                    queries[i] = new QueryInput(
                        new RectangleLocalBounds(-10, -10, 10, 0),
                        new RectanglePose(x, below, 0.0f));
                }
            }

            Shuffle(queries, random);
            return queries;
        }

        private static QueryInput[] InteriorQueries(int count, int seed) {
            Random random = new(seed);
            QueryInput[] queries = new QueryInput[count];
            RectangleLocalBounds bounds = new(-1, -1, 1, 1);
            for (int i = 0; i < count; ++i) {
                double angle = random.NextDouble() * Math.PI * 2.0;
                double radius = random.NextDouble() * 15.0;
                queries[i] = new QueryInput(
                    bounds,
                    new RectanglePose(
                        (float)(radius * Math.Cos(angle)),
                        (float)(radius * Math.Sin(angle)),
                        (float)(random.NextDouble() * 720.0 - 360.0)));
            }

            return queries;
        }

        private static float NextDown(float value) {
            int bits = BitConverter.SingleToInt32Bits(value);
            return BitConverter.Int32BitsToSingle(value > 0.0f ? bits - 1 : bits + 1);
        }

        private static void Shuffle(QueryInput[] values, Random random) {
            for (int i = values.Length - 1; i > 0; --i) {
                int other = random.Next(i + 1);
                (values[other], values[i]) = (values[i], values[other]);
            }
        }

        private static AlgorithmSpec[] Algorithms() {
            return new[]
            {
                new AlgorithmSpec(
                    "Linear",
                    (first, second) => new LinearScanCollisionDetector(first, second)),
                new AlgorithmSpec(
                    "BVH-8",
                    (first, second) => new BvhCollisionDetector(first, second)),
                new AlgorithmSpec(
                    "Grid-default",
                    (first, second) => new UniformGridCollisionDetector(first, second)),
            };
        }

        private static string DescribeIndex(ICollisionDetector detector) {
            if (detector is BvhCollisionDetector bvh) {
                return "nodes=" + bvh.NodeCount + "; leaf=" + bvh.LeafSize;
            }

            if (detector is UniformGridCollisionDetector grid) {
                return grid.ColumnCount + "x" + grid.RowCount
                    + "; refs=" + grid.StoredEdgeReferenceCount
                    + "; overflow=" + grid.OverflowEdgeCount;
            }

            return "none";
        }

        private static double Median(double[] values) {
            double[] copy = (double[])values.Clone();
            Array.Sort(copy);
            int middle = copy.Length / 2;
            return (copy.Length & 1) != 0
                ? copy[middle]
                : (copy[middle - 1] + copy[middle]) * 0.5;
        }

        private static double MedianAbsoluteDeviation(double[] values) {
            double median = Median(values);
            double[] deviations = new double[values.Length];
            for (int i = 0; i < values.Length; ++i) {
                deviations[i] = Math.Abs(values[i] - median);
            }

            return Median(deviations);
        }

        private static bool GCSettingsIsServer() {
            return System.Runtime.GCSettings.IsServerGC;
        }

        private static string CreateEnvironmentAndSourceManifest(
            DateTimeOffset started,
            DateTimeOffset completed) {
            StringBuilder text = new();
            _ = text.AppendLine("CollisionDetection canonical benchmark manifest");
            _ = text.AppendLine("started_utc=" + started.ToString("O", CultureInfo.InvariantCulture));
            _ = text.AppendLine("completed_utc=" + completed.ToString("O", CultureInfo.InvariantCulture));
            _ = text.AppendLine("runtime=" + RuntimeInformation.FrameworkDescription);
            _ = text.AppendLine("os=" + RuntimeInformation.OSDescription);
            _ = text.AppendLine("architecture=" + RuntimeInformation.ProcessArchitecture);
            _ = text.AppendLine("logical_processors=" + Environment.ProcessorCount);
            _ = text.AppendLine("processor_identifier="
                + (Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "<unavailable>"));
            _ = text.AppendLine("gc=" + (GCSettingsIsServer() ? "server" : "workstation"));
            _ = text.AppendLine("stopwatch_frequency=" + Stopwatch.Frequency);
            _ = text.AppendLine("debugger_attached=" + Debugger.IsAttached);
            _ = text.AppendLine("DOTNET_TieredCompilation="
                + (Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "<unset>"));
            _ = text.AppendLine("DOTNET_TieredPGO="
                + (Environment.GetEnvironmentVariable("DOTNET_TieredPGO") ?? "<unset>"));
            _ = text.AppendLine();
            _ = text.AppendLine("SHA256 executable-source manifest:");

            string root = Directory.GetCurrentDirectory();
            string[] sourceRoots =
            {
                Path.Combine(root, "ZoomTracks.CollisionDetection"),
                Path.Combine(root, "CollisionDetection.Harness"),
            };
            List<string> files = new();
            foreach (string sourceRoot in sourceRoots) {
                files.AddRange(Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                    .Where(path => !IsBuildArtifactPath(path)));
                files.AddRange(Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
                    .Where(path => !IsBuildArtifactPath(path)));
            }

            files.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string file in files) {
                byte[] hash = SHA256.HashData(File.ReadAllBytes(file));
                string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                _ = text.AppendLine(Convert.ToHexString(hash) + "  " + relative);
            }

            return text.ToString();
        }

        private static bool IsBuildArtifactPath(string path) {
            string marker = Path.DirectorySeparatorChar.ToString();
            return path.IndexOf(marker + "bin" + marker, StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf(marker + "obj" + marker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class AlgorithmSpec {
            internal AlgorithmSpec(
                string name,
                Func<List<CoordinateXY>, List<CoordinateXY>, ICollisionDetector> create) {
                this.Name = name;
                this.Create = create;
            }

            internal string Name { get; }
            internal Func<List<CoordinateXY>, List<CoordinateXY>, ICollisionDetector> Create { get; }
        }

        private sealed class BenchmarkScenario {
            internal BenchmarkScenario(string name, OutlinePair outlines, QueryInput[] queries) {
                this.Name = name;
                this.Outlines = outlines;
                this.Queries = queries;
            }

            internal string Name { get; }
            internal OutlinePair Outlines { get; }
            internal QueryInput[] Queries { get; }
            internal bool[] Expected { get; private set; }
            internal int HitCount { get; private set; }

            internal void PrepareExpected() {
                IndependentOracle.PreparedOutlines oracle = IndependentOracle.Prepare(
                    this.Outlines.Outline1, this.Outlines.Outline2);
                this.Expected = new bool[this.Queries.Length];
                int hits = 0;
                for (int i = 0; i < this.Queries.Length; ++i) {
                    this.Expected[i] = oracle.IsColliding(this.Queries[i].Bounds, this.Queries[i].Pose);
                    if (this.Expected[i]) {
                        ++hits;
                    }
                }

                this.HitCount = hits;
            }
        }

        private sealed class BuildMeasurement {
            internal BuildMeasurement(
                AlgorithmSpec algorithm,
                ICollisionDetector detector,
                double medianMilliseconds,
                double madMilliseconds,
                double medianAllocatedBytes,
                string indexDetails) {
                this.Algorithm = algorithm;
                this.Detector = detector;
                this.MedianMilliseconds = medianMilliseconds;
                this.MadMilliseconds = madMilliseconds;
                this.MedianAllocatedBytes = medianAllocatedBytes;
                this.IndexDetails = indexDetails;
            }

            internal AlgorithmSpec Algorithm { get; }
            internal ICollisionDetector Detector { get; }
            internal double MedianMilliseconds { get; }
            internal double MadMilliseconds { get; }
            internal double MedianAllocatedBytes { get; }
            internal string IndexDetails { get; }
        }

        private readonly struct QueryMeasurement {
            internal QueryMeasurement(
                double medianNanosecondsPerQuery,
                double madNanosecondsPerQuery,
                double bytesPerQuery,
                int repeatsPerSample,
                int checksum) {
                this.MedianNanosecondsPerQuery = medianNanosecondsPerQuery;
                this.MadNanosecondsPerQuery = madNanosecondsPerQuery;
                this.BytesPerQuery = bytesPerQuery;
                this.RepeatsPerSample = repeatsPerSample;
                this.Checksum = checksum;
            }

            internal double MedianNanosecondsPerQuery { get; }
            internal double MadNanosecondsPerQuery { get; }
            internal double BytesPerQuery { get; }
            internal int RepeatsPerSample { get; }
            internal int Checksum { get; }
        }

        private sealed class ScenarioMeasurements {
            internal ScenarioMeasurements(BenchmarkScenario scenario) {
                this.Scenario = scenario;
                this.Builds = new List<BuildMeasurement>();
                this.Queries = new List<AlgorithmQueryMeasurement>();
            }

            internal BenchmarkScenario Scenario { get; }
            internal List<BuildMeasurement> Builds { get; }
            internal List<AlgorithmQueryMeasurement> Queries { get; }
        }

        private readonly struct AlgorithmQueryMeasurement {
            internal AlgorithmQueryMeasurement(AlgorithmSpec algorithm, QueryMeasurement query) {
                this.Algorithm = algorithm;
                this.Query = query;
            }

            internal AlgorithmSpec Algorithm { get; }
            internal QueryMeasurement Query { get; }
        }

        private readonly struct RawRow {
            private RawRow(
                BenchmarkScenario scenario,
                AlgorithmSpec algorithm,
                string metric,
                double median,
                double mad,
                string unit,
                double allocatedBytes,
                string details) {
                this.Scenario = scenario;
                this.Algorithm = algorithm;
                this.Metric = metric;
                this.Median = median;
                this.Mad = mad;
                this.Unit = unit;
                this.AllocatedBytes = allocatedBytes;
                this.Details = details;
            }

            private BenchmarkScenario Scenario { get; }
            private AlgorithmSpec Algorithm { get; }
            private string Metric { get; }
            private double Median { get; }
            private double Mad { get; }
            private string Unit { get; }
            private double AllocatedBytes { get; }
            private string Details { get; }

            internal static RawRow Build(
                BenchmarkScenario scenario,
                AlgorithmSpec algorithm,
                BuildMeasurement measurement) {
                return new RawRow(
                    scenario,
                    algorithm,
                    "build",
                    measurement.MedianMilliseconds,
                    measurement.MadMilliseconds,
                    "ms",
                    measurement.MedianAllocatedBytes,
                    measurement.IndexDetails);
            }

            internal static RawRow Query(
                BenchmarkScenario scenario,
                AlgorithmSpec algorithm,
                QueryMeasurement measurement) {
                return new RawRow(
                    scenario,
                    algorithm,
                    "query",
                    measurement.MedianNanosecondsPerQuery,
                    measurement.MadNanosecondsPerQuery,
                    "ns/query",
                    measurement.BytesPerQuery,
                    "repeats=" + measurement.RepeatsPerSample + "; checksum=" + measurement.Checksum);
            }

            internal string ToCsv() {
                return string.Join(",", new[]
                {
                    Escape(this.Scenario.Name),
                    this.Scenario.Outlines.EdgeCount.ToString(CultureInfo.InvariantCulture),
                    this.Scenario.Queries.Length.ToString(CultureInfo.InvariantCulture),
                    this.Scenario.HitCount.ToString(CultureInfo.InvariantCulture),
                    Escape(this.Algorithm.Name),
                    this.Metric,
                    this.Median.ToString("R", CultureInfo.InvariantCulture),
                    this.Mad.ToString("R", CultureInfo.InvariantCulture),
                    this.Unit,
                    this.AllocatedBytes.ToString("R", CultureInfo.InvariantCulture),
                    Escape(this.Details),
                });
            }

            private static string Escape(string value) {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
        }
    }
}
