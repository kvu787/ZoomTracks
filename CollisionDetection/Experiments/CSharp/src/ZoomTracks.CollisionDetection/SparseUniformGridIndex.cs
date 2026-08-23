using System;
using System.Collections.Generic;

namespace ZoomTracks.CollisionDetection
{
    /// <summary>
    /// A sparse uniform grid over segment AABBs. It targets short, locally coherent
    /// queries on fairly uniformly sampled outlines. Per-segment cell references are
    /// capped; edges exceeding the cap go into an overflow list, and broad query edges
    /// fall back to a direct scan.
    /// </summary>
    public sealed class SparseUniformGridIndex : OutlineIndexBase
    {
        public const int DefaultTargetSegmentsPerCell = 4;
        public const int DefaultMaxAxisCells = 4096;
        public const int DefaultMaxCellsPerSegment = 64;

        private readonly Dictionary<long, int[]> _cells;
        private readonly int[] _overflowSegments;
        private readonly double _width;
        private readonly double _height;

        public SparseUniformGridIndex(
            IReadOnlyList<CoordinateXY> outline1,
            IReadOnlyList<CoordinateXY> outline2,
            int targetSegmentsPerCell = DefaultTargetSegmentsPerCell,
            int maxAxisCells = DefaultMaxAxisCells,
            int maxCellsPerSegment = DefaultMaxCellsPerSegment)
            : base(outline1, outline2)
        {
            if (targetSegmentsPerCell < 1 || targetSegmentsPerCell > 1024)
            {
                throw new ArgumentOutOfRangeException(nameof(targetSegmentsPerCell));
            }

            if (maxAxisCells < 1 || maxAxisCells > 65536)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAxisCells));
            }

            if (maxCellsPerSegment < 1 || maxCellsPerSegment > 1048576)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCellsPerSegment));
            }

            TargetSegmentsPerCell = targetSegmentsPerCell;
            MaxAxisCells = maxAxisCells;
            MaxCellsPerSegment = maxCellsPerSegment;

            _width = (double)OutlineBounds.MaxX - OutlineBounds.MinX;
            _height = (double)OutlineBounds.MaxY - OutlineBounds.MinY;

            double meanLength = ComputeMeanSegmentLength();
            double targetCellSize = meanLength * targetSegmentsPerCell;
            CellsX = ComputeDimension(_width, targetCellSize, maxAxisCells);
            CellsY = ComputeDimension(_height, targetCellSize, maxAxisCells);

            var builders = new Dictionary<long, List<int>>(CellKeyComparer.Instance);
            var overflow = new List<int>();
            int referenceCount = 0;

            for (int segmentIndex = 0; segmentIndex < Segments.Length; ++segmentIndex)
            {
                CellRange range = GetCellRange(Segments[segmentIndex].Bounds);
                if (range.CellCount > maxCellsPerSegment)
                {
                    overflow.Add(segmentIndex);
                    continue;
                }

                for (int x = range.MinX; x <= range.MaxX; ++x)
                {
                    for (int y = range.MinY; y <= range.MaxY; ++y)
                    {
                        long key = GetCellKey(x, y);
                        List<int>? bucket;
                        if (!builders.TryGetValue(key, out bucket))
                        {
                            bucket = new List<int>();
                            builders.Add(key, bucket);
                        }

                        bucket.Add(segmentIndex);
                        referenceCount = checked(referenceCount + 1);
                    }
                }
            }

            _cells = new Dictionary<long, int[]>(builders.Count, CellKeyComparer.Instance);
            foreach (KeyValuePair<long, List<int>> pair in builders)
            {
                _cells.Add(pair.Key, pair.Value.ToArray());
            }

            _overflowSegments = overflow.ToArray();
            CellReferenceCount = referenceCount;
        }

        public int TargetSegmentsPerCell { get; }

        public int MaxAxisCells { get; }

        public int MaxCellsPerSegment { get; }

        public int CellsX { get; }

        public int CellsY { get; }

        public int OccupiedCellCount
        {
            get { return _cells.Count; }
        }

        public int CellReferenceCount { get; }

        public int OverflowSegmentCount
        {
            get { return _overflowSegments.Length; }
        }

        public override bool IsColliding(ConvexQuadrilateralOutline outline)
        {
            if (!outline.Bounds.Overlaps(OutlineBounds))
            {
                return false;
            }

            for (int edgeIndex = 0; edgeIndex < 4; ++edgeIndex)
            {
                CoordinateXY queryA = outline.GetVertex(edgeIndex);
                CoordinateXY queryB = outline.GetVertex((edgeIndex + 1) & 3);
                Aabb queryBounds = Aabb.FromSegment(queryA, queryB);
                if (!queryBounds.Overlaps(OutlineBounds))
                {
                    continue;
                }

                CellRange range = GetCellRange(queryBounds);
                if (range.CellCount > Segments.Length)
                {
                    if (ScanAll(queryA, queryB))
                    {
                        return true;
                    }

                    continue;
                }

                int stamp = ThreadQueryScratch.NextStamp(Segments.Length, out int[] stamps);

                for (int i = 0; i < _overflowSegments.Length; ++i)
                {
                    int segmentIndex = _overflowSegments[i];
                    stamps[segmentIndex] = stamp;
                    OutlineSegment segment = Segments[segmentIndex];
                    if (ExactSegmentPredicates.IntersectsWithKnownBounds(
                        queryA,
                        queryB,
                        queryBounds,
                        segment.A,
                        segment.B,
                        segment.Bounds))
                    {
                        return true;
                    }
                }

                for (int x = range.MinX; x <= range.MaxX; ++x)
                {
                    for (int y = range.MinY; y <= range.MaxY; ++y)
                    {
                        int[]? bucket;
                        if (!_cells.TryGetValue(GetCellKey(x, y), out bucket))
                        {
                            continue;
                        }

                        for (int i = 0; i < bucket.Length; ++i)
                        {
                            int segmentIndex = bucket[i];
                            if (stamps[segmentIndex] == stamp)
                            {
                                continue;
                            }

                            stamps[segmentIndex] = stamp;
                            OutlineSegment segment = Segments[segmentIndex];
                            if (ExactSegmentPredicates.IntersectsWithKnownBounds(
                                queryA,
                                queryB,
                                queryBounds,
                                segment.A,
                                segment.B,
                                segment.Bounds))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private bool ScanAll(CoordinateXY queryA, CoordinateXY queryB)
        {
            Aabb queryBounds = Aabb.FromSegment(queryA, queryB);
            for (int i = 0; i < Segments.Length; ++i)
            {
                OutlineSegment segment = Segments[i];
                if (ExactSegmentPredicates.IntersectsWithKnownBounds(
                    queryA,
                    queryB,
                    queryBounds,
                    segment.A,
                    segment.B,
                    segment.Bounds))
                {
                    return true;
                }
            }

            return false;
        }

        private double ComputeMeanSegmentLength()
        {
            double sum = 0.0;
            for (int i = 0; i < Segments.Length; ++i)
            {
                double dx = (double)Segments[i].B.X - Segments[i].A.X;
                double dy = (double)Segments[i].B.Y - Segments[i].A.Y;
                sum += Math.Sqrt((dx * dx) + (dy * dy));
            }

            return sum / Segments.Length;
        }

        private static int ComputeDimension(double extent, double targetCellSize, int maxAxisCells)
        {
            if (extent <= 0.0 || targetCellSize <= 0.0)
            {
                return 1;
            }

            double raw = Math.Ceiling(extent / targetCellSize);
            if (raw >= maxAxisCells)
            {
                return maxAxisCells;
            }

            return Math.Max(1, (int)raw);
        }

        private CellRange GetCellRange(Aabb bounds)
        {
            int minX = ToCell(bounds.MinX, OutlineBounds.MinX, OutlineBounds.MaxX, _width, CellsX);
            int minY = ToCell(bounds.MinY, OutlineBounds.MinY, OutlineBounds.MaxY, _height, CellsY);
            int maxX = ToCell(bounds.MaxX, OutlineBounds.MinX, OutlineBounds.MaxX, _width, CellsX);
            int maxY = ToCell(bounds.MaxY, OutlineBounds.MinY, OutlineBounds.MaxY, _height, CellsY);
            return new CellRange(minX, minY, maxX, maxY);
        }

        private static int ToCell(
            float coordinate,
            float min,
            float max,
            double extent,
            int dimension)
        {
            if (dimension == 1 || extent <= 0.0 || coordinate <= min)
            {
                return 0;
            }

            if (coordinate >= max)
            {
                return dimension - 1;
            }

            double normalized = ((double)coordinate - min) / extent;
            int cell = (int)(normalized * dimension);
            return Math.Min(dimension - 1, cell);
        }

        private static long GetCellKey(int x, int y)
        {
            return ((long)x << 32) | (uint)y;
        }

        private sealed class CellKeyComparer : IEqualityComparer<long>
        {
            public static readonly CellKeyComparer Instance = new CellKeyComparer();

            public bool Equals(long x, long y)
            {
                return x == y;
            }

            public int GetHashCode(long value)
            {
                // Avalanche both packed coordinates; long.GetHashCode is x^y for this
                // key layout and collapses every diagonal cell (i,i) to the same hash.
                unchecked
                {
                    ulong mixed = (ulong)value;
                    mixed ^= mixed >> 33;
                    mixed *= 0xff51afd7ed558ccdUL;
                    mixed ^= mixed >> 33;
                    mixed *= 0xc4ceb9fe1a85ec53UL;
                    mixed ^= mixed >> 33;
                    return (int)(mixed ^ (mixed >> 32));
                }
            }
        }

        private readonly struct CellRange
        {
            public CellRange(int minX, int minY, int maxX, int maxY)
            {
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
            }

            public int MinX { get; }

            public int MinY { get; }

            public int MaxX { get; }

            public int MaxY { get; }

            public long CellCount
            {
                get { return ((long)MaxX - MinX + 1L) * ((long)MaxY - MinY + 1L); }
            }
        }

        private static class ThreadQueryScratch
        {
            [ThreadStatic]
            private static int[]? _stamps;

            [ThreadStatic]
            private static int _nextStamp;

            public static int NextStamp(int requiredLength, out int[] stamps)
            {
                if (_stamps == null || _stamps.Length < requiredLength)
                {
                    _stamps = new int[requiredLength];
                    _nextStamp = 0;
                }

                if (_nextStamp == int.MaxValue)
                {
                    Array.Clear(_stamps, 0, _stamps.Length);
                    _nextStamp = 0;
                }

                _nextStamp++;
                stamps = _stamps;
                return _nextStamp;
            }
        }
    }
}
