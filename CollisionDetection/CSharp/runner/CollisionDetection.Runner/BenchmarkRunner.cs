using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace ZoomTracks.CollisionDetection.Runner
{
    internal static class BenchmarkRunner
    {
        private const int TimingSamples = 11;
        private const double TargetSampleSeconds = 0.20;
        private const int BenchmarkSeed = 31415926;

        private static readonly AlgorithmSpec[] Algorithms =
        {
            new AlgorithmSpec(
                "linear",
                (outer, inner) => new LinearScanIndex(outer, inner)),
            new AlgorithmSpec(
                "morton-bvh",
                (outer, inner) => new MortonBvhIndex(outer, inner)),
            new AlgorithmSpec(
                "sparse-grid",
                (outer, inner) => new SparseUniformGridIndex(outer, inner)),
        };

        public static void Run()
        {
            string environment = CaptureEnvironment();
            Console.Write(environment);
            Console.WriteLine("BENCHMARK START samples={0} target_sample_s={1:F2}", TimingSamples, TargetSampleSeconds);

            var csv = new StringBuilder();
            csv.AppendLine(
                "n,n1,n2,workload,queries,hits,algorithm,build_median_ms,build_mad_ms," +
                "build_allocated_bytes,query_median_ns,query_mad_ns," +
                "allocated_bytes_per_query,repetitions,dataset_sha256,index_details");

            int[] sizes = { 64, 1024, 16384 };
            foreach (int size in sizes)
            {
                int outerCount = size / 2;
                int innerCount = size - outerCount;
                FloatPoint[] outer = CorrectnessTests.MakeLoop(
                    outerCount,
                    1000.0,
                    800.0,
                    0.017,
                    0.12,
                    7);
                FloatPoint[] inner = CorrectnessTests.MakeLoop(
                    innerCount,
                    300.0,
                    240.0,
                    0.193,
                    0.08,
                    5);

                var buildResults = new Dictionary<string, BuildMeasurement>();
                foreach (AlgorithmSpec algorithm in Algorithms)
                {
                    BuildMeasurement measurement = MeasureBuild(algorithm, outer, inner, size);
                    buildResults.Add(algorithm.Name, measurement);
                    Console.WriteLine(
                        "BUILD n={0} algorithm={1} median_ms={2:F6} mad_ms={3:F6} " +
                        "allocated_bytes={4:F0} details={5}",
                        size,
                        algorithm.Name,
                        measurement.MedianMilliseconds,
                        measurement.MadMilliseconds,
                        measurement.AllocatedBytes,
                        measurement.Details);
                }

                int queryCount = size >= 16384 ? 768 : 2048;
                string[] workloadNames =
                {
                    "boundary-50pct",
                    "annulus-miss",
                    "enclosing-miss",
                    "far-miss",
                };

                for (int workloadIndex = 0; workloadIndex < workloadNames.Length; ++workloadIndex)
                {
                    string workloadName = workloadNames[workloadIndex];
                    QueryPerimeter[] queries = CreateWorkload(
                        workloadName,
                        queryCount,
                        outer,
                        inner,
                        BenchmarkSeed + size + (workloadIndex * 100003));

                    var labeler = new LinearScanIndex(outer, inner);
                    bool[] expectedResults = LabelQueries(labeler, queries, out int expectedHits);
                    int independentChecks = ValidateWithIndependentOracle(
                        outer,
                        inner,
                        queries,
                        expectedResults,
                        size <= 1024 ? queries.Length : 16);
                    string hash = ComputeDataHash(outer, inner, queries);
                    Console.WriteLine(
                        "DATASET n={0} workload={1} queries={2} hits={3} sha256={4}",
                        size,
                        workloadName,
                        queries.Length,
                        expectedHits,
                        hash);
                    Console.WriteLine(
                        "ORACLE n={0} workload={1} independently_checked_queries={2}",
                        size,
                        workloadName,
                        independentChecks);

                    AlgorithmSpec[] order = RotateAlgorithms(workloadIndex + Array.IndexOf(sizes, size));
                    var queryMeasurements = new Dictionary<string, QueryMeasurement>();
                    foreach (AlgorithmSpec algorithm in order)
                    {
                        IOutlineIntersectionIndex index = algorithm.Create(outer, inner);
                        QueryMeasurement query = MeasureQueries(index, queries, expectedResults);
                        queryMeasurements.Add(algorithm.Name, query);
                        BuildMeasurement build = buildResults[algorithm.Name];

                        Console.WriteLine(
                            "QUERY n={0} workload={1} algorithm={2} median_ns={3:F3} " +
                            "mad_ns={4:F3} alloc_B_per_query={5:F3} repetitions={6} checksum={7}",
                            size,
                            workloadName,
                            algorithm.Name,
                            query.MedianNanoseconds,
                            query.MadNanoseconds,
                            query.AllocatedBytesPerQuery,
                            query.Repetitions,
                            query.Checksum);

                        csv.AppendLine(string.Join(
                            ",",
                            size.ToString(CultureInfo.InvariantCulture),
                            outerCount.ToString(CultureInfo.InvariantCulture),
                            innerCount.ToString(CultureInfo.InvariantCulture),
                            workloadName,
                            queries.Length.ToString(CultureInfo.InvariantCulture),
                            expectedHits.ToString(CultureInfo.InvariantCulture),
                            algorithm.Name,
                            build.MedianMilliseconds.ToString("F9", CultureInfo.InvariantCulture),
                            build.MadMilliseconds.ToString("F9", CultureInfo.InvariantCulture),
                            build.AllocatedBytes.ToString("F0", CultureInfo.InvariantCulture),
                            query.MedianNanoseconds.ToString("F6", CultureInfo.InvariantCulture),
                            query.MadNanoseconds.ToString("F6", CultureInfo.InvariantCulture),
                            query.AllocatedBytesPerQuery.ToString("F6", CultureInfo.InvariantCulture),
                            query.Repetitions.ToString(CultureInfo.InvariantCulture),
                            hash,
                            QuoteCsv(build.Details)));
                    }

                    PrintBreakEven(size, workloadName, buildResults, queryMeasurements);
                }
            }

            string artifactDirectory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
            Directory.CreateDirectory(artifactDirectory);
            string csvPath = Path.Combine(artifactDirectory, "benchmark-results.csv");
            File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(false));
            string environmentPath = Path.Combine(artifactDirectory, "benchmark-environment.txt");
            File.WriteAllText(environmentPath, environment, new UTF8Encoding(false));
            Console.WriteLine("BENCHMARK PASS csv={0}", csvPath);
        }

        private static BuildMeasurement MeasureBuild(
            AlgorithmSpec algorithm,
            FloatPoint[] outer,
            FloatPoint[] inner,
            int size)
        {
            for (int i = 0; i < 3; ++i)
            {
                GC.KeepAlive(algorithm.Create(outer, inner));
            }

            long probeAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long probeStart = Stopwatch.GetTimestamp();
            IOutlineIntersectionIndex probe = algorithm.Create(outer, inner);
            long probeTicks = Math.Max(1L, Stopwatch.GetTimestamp() - probeStart);
            long probeAllocated = Math.Max(
                1L,
                GC.GetAllocatedBytesForCurrentThread() - probeAllocatedBefore);
            GC.KeepAlive(probe);

            long targetTicks = Math.Max(1L, (long)(Stopwatch.Frequency * 0.02));
            long repetitionsForTime = Math.Max(1L, (targetTicks + probeTicks - 1L) / probeTicks);
            long repetitionsForAllocation = Math.Max(1L, (64L * 1024L * 1024L) / probeAllocated);
            int repetitions = (int)Math.Clamp(
                Math.Min(repetitionsForTime, repetitionsForAllocation),
                1L,
                8192L);
            var times = new double[TimingSamples];
            var allocations = new double[TimingSamples];
            IOutlineIntersectionIndex? retained = null;

            for (int sample = 0; sample < TimingSamples; ++sample)
            {
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
                for (int repetition = 0; repetition < repetitions; ++repetition)
                {
                    retained = algorithm.Create(outer, inner);
                }

                long end = Stopwatch.GetTimestamp();
                long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
                GC.KeepAlive(retained);
                times[sample] = TicksToMilliseconds(end - start) / repetitions;
                allocations[sample] = (allocatedAfter - allocatedBefore) / (double)repetitions;
            }

            IOutlineIntersectionIndex detailsIndex = algorithm.Create(outer, inner);
            return new BuildMeasurement(
                Median(times),
                MedianAbsoluteDeviation(times),
                Median(allocations),
                Describe(detailsIndex));
        }

        private static QueryMeasurement MeasureQueries(
            IOutlineIntersectionIndex index,
            QueryPerimeter[] queries,
            bool[] expectedResults)
        {
            VerifyPerQuery(index, queries, expectedResults);
            long expectedSignature = ExpectedSignature(expectedResults);

            for (int warmup = 0; warmup < 4; ++warmup)
            {
                long warmupSignature = ExecuteSignature(index, queries);
                if (warmupSignature != expectedSignature)
                {
                    throw new InvalidOperationException("Warmup checksum mismatch.");
                }
            }

            long probeStart = Stopwatch.GetTimestamp();
            long probeSignature = ExecuteSignature(index, queries);
            long probeTicks = Math.Max(1L, Stopwatch.GetTimestamp() - probeStart);
            if (probeSignature != expectedSignature)
            {
                throw new InvalidOperationException("Probe checksum mismatch.");
            }

            long targetTicks = (long)(Stopwatch.Frequency * TargetSampleSeconds);
            int repetitions = (int)Math.Clamp(
                (targetTicks + probeTicks - 1L) / probeTicks,
                1L,
                100000L);

            var samples = new double[TimingSamples];
            long finalChecksum = 0;
            for (int sample = 0; sample < TimingSamples; ++sample)
            {
                long checksum = 0;
                long start = Stopwatch.GetTimestamp();
                for (int repetition = 0; repetition < repetitions; ++repetition)
                {
                    checksum = unchecked(checksum + ExecuteSignature(index, queries));
                }

                long elapsed = Stopwatch.GetTimestamp() - start;
                long requiredChecksum = unchecked(expectedSignature * repetitions);
                if (checksum != requiredChecksum)
                {
                    throw new InvalidOperationException("Measured checksum mismatch.");
                }

                finalChecksum = checksum;
                samples[sample] = TicksToNanoseconds(elapsed) / (queries.Length * (double)repetitions);
            }

            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long allocationSignature = ExecuteSignature(index, queries);
            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
            if (allocationSignature != expectedSignature)
            {
                throw new InvalidOperationException("Allocation-pass checksum mismatch.");
            }

            return new QueryMeasurement(
                Median(samples),
                MedianAbsoluteDeviation(samples),
                (allocatedAfter - allocatedBefore) / (double)queries.Length,
                repetitions,
                finalChecksum);
        }

        private static QueryPerimeter[] CreateWorkload(
            string name,
            int count,
            FloatPoint[] outer,
            FloatPoint[] inner,
            int seed)
        {
            var random = new Random(seed);
            var result = new QueryPerimeter[count];
            for (int i = 0; i < result.Length; ++i)
            {
                switch (name)
                {
                    case "boundary-50pct":
                    {
                        FloatPoint[] loop = (i & 2) == 0 ? outer : inner;
                        int edgeIndex = random.Next(loop.Length);
                        FloatPoint a = loop[edgeIndex];
                        FloatPoint b = loop[(edgeIndex + 1) % loop.Length];
                        if ((i & 1) == 0)
                        {
                            result[i] = RectangleFromEdge(a, b, 2.0 + (random.NextDouble() * 8.0));
                        }
                        else
                        {
                            double midpointX = ((double)a.X + b.X) * 0.5;
                            double midpointY = ((double)a.Y + b.Y) * 0.5;
                            double dx = (double)b.X - a.X;
                            double dy = (double)b.Y - a.Y;
                            double length = Math.Sqrt((dx * dx) + (dy * dy));
                            double offset = 30.0 + (random.NextDouble() * 20.0);
                            result[i] = CorrectnessTests.MakeRectangle(
                                midpointX + ((-dy / length) * offset),
                                midpointY + ((dx / length) * offset),
                                0.5,
                                0.35,
                                random.NextDouble() * Math.PI);
                        }

                        break;
                    }
                    case "annulus-miss":
                    {
                        double angle = random.NextDouble() * Math.PI * 2.0;
                        result[i] = CorrectnessTests.MakeRectangle(
                            610.0 * Math.Cos(angle),
                            490.0 * Math.Sin(angle),
                            0.25 + (random.NextDouble() * 1.5),
                            0.25 + (random.NextDouble() * 1.0),
                            random.NextDouble() * Math.PI);
                        break;
                    }
                    case "enclosing-miss":
                        result[i] = CorrectnessTests.MakeRectangle(
                            0.0,
                            0.0,
                            1800.0 + (i % 7),
                            1800.0 + (i % 11),
                            (i % 97) * (Math.PI / 194.0));
                        break;
                    case "far-miss":
                        result[i] = CorrectnessTests.MakeRectangle(
                            1000000.0 + (i * 3.0),
                            -1000000.0 - (i * 2.0),
                            10.0,
                            5.0,
                            random.NextDouble() * Math.PI);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(name));
                }
            }

            return result;
        }

        private static QueryPerimeter RectangleFromEdge(FloatPoint a, FloatPoint b, double depth)
        {
            double dx = (double)b.X - a.X;
            double dy = (double)b.Y - a.Y;
            double length = Math.Sqrt((dx * dx) + (dy * dy));
            double nx = (-dy / length) * depth;
            double ny = (dx / length) * depth;
            return new QueryPerimeter(
                a,
                b,
                new FloatPoint((float)(b.X + nx), (float)(b.Y + ny)),
                new FloatPoint((float)(a.X + nx), (float)(a.Y + ny)));
        }

        private static bool[] LabelQueries(
            LinearScanIndex index,
            QueryPerimeter[] queries,
            out int hitCount)
        {
            var results = new bool[queries.Length];
            int hits = 0;
            for (int i = 0; i < queries.Length; ++i)
            {
                bool result = index.Intersects(queries[i]);
                results[i] = result;
                if (result)
                {
                    hits++;
                }
            }

            hitCount = hits;
            return results;
        }

        private static void VerifyPerQuery(
            IOutlineIntersectionIndex index,
            QueryPerimeter[] queries,
            bool[] expectedResults)
        {
            for (int i = 0; i < queries.Length; ++i)
            {
                if (index.Intersects(queries[i]) != expectedResults[i])
                {
                    throw new InvalidOperationException(
                        "Per-query benchmark verification failed at query " +
                        i.ToString(CultureInfo.InvariantCulture) + ".");
                }
            }
        }

        private static long ExecuteSignature(
            IOutlineIntersectionIndex index,
            QueryPerimeter[] queries)
        {
            long signature = 1469598103934665603L;
            for (int i = 0; i < queries.Length; ++i)
            {
                if (index.Intersects(queries[i]))
                {
                    signature = unchecked((signature * 1099511628211L) ^ (i + 1L));
                }
            }

            return signature;
        }

        private static long ExpectedSignature(bool[] expectedResults)
        {
            long signature = 1469598103934665603L;
            for (int i = 0; i < expectedResults.Length; ++i)
            {
                if (expectedResults[i])
                {
                    signature = unchecked((signature * 1099511628211L) ^ (i + 1L));
                }
            }

            return signature;
        }

        private static int ValidateWithIndependentOracle(
            FloatPoint[] outer,
            FloatPoint[] inner,
            QueryPerimeter[] queries,
            bool[] expectedResults,
            int maximumChecks)
        {
            ReferenceOracle.PreparedOutlines oracle = ReferenceOracle.Prepare(outer, inner);
            int checks = Math.Min(maximumChecks, queries.Length);
            for (int check = 0; check < checks; ++check)
            {
                int queryIndex = checks == queries.Length
                    ? check
                    : (int)(((long)check * queries.Length) / checks);
                bool exactResult = oracle.Intersects(queries[queryIndex]);
                if (exactResult != expectedResults[queryIndex])
                {
                    throw new InvalidOperationException(
                        "Independent benchmark oracle mismatch at query " +
                        queryIndex.ToString(CultureInfo.InvariantCulture) + ".");
                }
            }

            return checks;
        }

        private static void PrintBreakEven(
            int size,
            string workload,
            IReadOnlyDictionary<string, BuildMeasurement> builds,
            IReadOnlyDictionary<string, QueryMeasurement> queries)
        {
            BuildMeasurement linearBuild = builds["linear"];
            QueryMeasurement linearQuery = queries["linear"];
            foreach (string indexedName in new[] { "morton-bvh", "sparse-grid" })
            {
                BuildMeasurement indexedBuild = builds[indexedName];
                QueryMeasurement indexedQuery = queries[indexedName];
                double savedNanoseconds = linearQuery.MedianNanoseconds - indexedQuery.MedianNanoseconds;
                if (savedNanoseconds <= 0.0)
                {
                    Console.WriteLine(
                        "BREAKEVEN n={0} workload={1} indexed={2} derived_queries=never_index_slower",
                        size,
                        workload,
                        indexedName);
                    continue;
                }

                double extraBuildNanoseconds = Math.Max(
                    0.0,
                    (indexedBuild.MedianMilliseconds - linearBuild.MedianMilliseconds) * 1000000.0);
                double breakEven = extraBuildNanoseconds / savedNanoseconds;
                Console.WriteLine(
                    "BREAKEVEN n={0} workload={1} indexed={2} derived_queries={3:F1}",
                    size,
                    workload,
                    indexedName,
                    breakEven);
            }
        }

        private static string Describe(IOutlineIntersectionIndex index)
        {
            if (index is MortonBvhIndex bvh)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "leaf={0};nodes={1}",
                    bvh.LeafSize,
                    bvh.NodeCount);
            }

            if (index is SparseUniformGridIndex grid)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "dims={0}x{1};occupied={2};refs={3};overflow={4};target={5};cap={6}",
                    grid.CellsX,
                    grid.CellsY,
                    grid.OccupiedCellCount,
                    grid.CellReferenceCount,
                    grid.OverflowSegmentCount,
                    grid.TargetSegmentsPerCell,
                    grid.MaxCellsPerSegment);
            }

            return "segments=" + index.SegmentCount.ToString(CultureInfo.InvariantCulture);
        }

        private static AlgorithmSpec[] RotateAlgorithms(int amount)
        {
            var result = new AlgorithmSpec[Algorithms.Length];
            for (int i = 0; i < result.Length; ++i)
            {
                result[i] = Algorithms[(i + amount) % Algorithms.Length];
            }

            return result;
        }

        private static string ComputeDataHash(
            FloatPoint[] outer,
            FloatPoint[] inner,
            QueryPerimeter[] queries)
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            AppendPoints(hash, outer);
            AppendPoints(hash, inner);
            Span<byte> bytes = stackalloc byte[4];
            foreach (QueryPerimeter query in queries)
            {
                for (int i = 0; i < 4; ++i)
                {
                    FloatPoint point = query.GetVertex(i);
                    BitConverter.TryWriteBytes(bytes, BitConverter.SingleToInt32Bits(point.X));
                    hash.AppendData(bytes);
                    BitConverter.TryWriteBytes(bytes, BitConverter.SingleToInt32Bits(point.Y));
                    hash.AppendData(bytes);
                }
            }

            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }

        private static void AppendPoints(IncrementalHash hash, FloatPoint[] points)
        {
            Span<byte> bytes = stackalloc byte[4];
            foreach (FloatPoint point in points)
            {
                BitConverter.TryWriteBytes(bytes, BitConverter.SingleToInt32Bits(point.X));
                hash.AppendData(bytes);
                BitConverter.TryWriteBytes(bytes, BitConverter.SingleToInt32Bits(point.Y));
                hash.AppendData(bytes);
            }
        }

        private static string QuoteCsv(string text)
        {
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        private static string CaptureEnvironment()
        {
            string cpu = "unavailable";
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    cpu = Convert.ToString(
                        Registry.GetValue(
                            @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                            "ProcessorNameString",
                            "unavailable"),
                        CultureInfo.InvariantCulture) ?? "unavailable";
                }
            }
            catch (Exception)
            {
                cpu = "unavailable";
            }

            using Process process = Process.GetCurrentProcess();
            string affinity = "unavailable";
            if (OperatingSystem.IsWindows())
            {
                affinity = "0x" + process.ProcessorAffinity.ToInt64().ToString("x", CultureInfo.InvariantCulture);
            }

            var text = new StringBuilder();
            text.AppendLine(string.Format(CultureInfo.InvariantCulture, "ENV timestamp_utc={0:O}", DateTime.UtcNow));
            text.AppendLine("ENV cpu=" + cpu.Trim());
            text.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "ENV os={0} framework={1} process_arch={2} os_arch={3}",
                RuntimeInformation.OSDescription,
                RuntimeInformation.FrameworkDescription,
                RuntimeInformation.ProcessArchitecture,
                RuntimeInformation.OSArchitecture));
            text.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "ENV logical_processors_visible={0} gc_server={1} gc_latency={2}",
                Environment.ProcessorCount,
                GCSettings.IsServerGC,
                GCSettings.LatencyMode));
            text.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "ENV stopwatch_frequency={0} stopwatch_high_resolution={1} priority={2} affinity={3}",
                Stopwatch.Frequency,
                Stopwatch.IsHighResolution,
                process.PriorityClass,
                affinity));
            text.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "ENV library_target=netstandard2.1 runner_target=net10.0 unity_version=6000.3.22f1 " +
                "build=Release debugger_attached={0}",
                Debugger.IsAttached));
            text.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "ENV controls=standalone_runner;affinity_not_modified;priority_not_modified;" +
                "power_mode_uncontrolled;background_load_uncontrolled seed={0}",
                BenchmarkSeed));
            text.AppendLine("ENV source_sha256=" + ComputeSourceHash());
            return text.ToString();
        }

        private static string ComputeSourceHash()
        {
            string root = Directory.GetCurrentDirectory();
            string[] sourceRoots =
            {
                Path.Combine(root, "src"),
                Path.Combine(root, "runner"),
            };

            var files = new List<string>();
            foreach (string sourceRoot in sourceRoots)
            {
                foreach (string path in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
                {
                    if (path.IndexOf(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0 &&
                        path.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        files.Add(path);
                    }
                }
            }

            files.Sort(StringComparer.OrdinalIgnoreCase);
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (string path in files)
            {
                byte[] name = Encoding.UTF8.GetBytes(Path.GetRelativePath(root, path).Replace('\\', '/'));
                hash.AppendData(name);
                hash.AppendData(File.ReadAllBytes(path));
            }

            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }

        private static double TicksToMilliseconds(long ticks)
        {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }

        private static double TicksToNanoseconds(long ticks)
        {
            return ticks * 1000000000.0 / Stopwatch.Frequency;
        }

        private static double Median(double[] values)
        {
            double[] sorted = (double[])values.Clone();
            Array.Sort(sorted);
            int middle = sorted.Length / 2;
            return (sorted.Length & 1) != 0
                ? sorted[middle]
                : (sorted[middle - 1] + sorted[middle]) * 0.5;
        }

        private static double MedianAbsoluteDeviation(double[] values)
        {
            double median = Median(values);
            double[] deviations = values.Select(value => Math.Abs(value - median)).ToArray();
            return Median(deviations);
        }

        private readonly record struct AlgorithmSpec(
            string Name,
            Func<FloatPoint[], FloatPoint[], IOutlineIntersectionIndex> Create);

        private readonly record struct BuildMeasurement(
            double MedianMilliseconds,
            double MadMilliseconds,
            double AllocatedBytes,
            string Details);

        private readonly record struct QueryMeasurement(
            double MedianNanoseconds,
            double MadNanoseconds,
            double AllocatedBytesPerQuery,
            int Repetitions,
            long Checksum);
    }
}
