using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZoomTracks {
    /// <summary>
    /// Immutable final local-space bounds of the vehicle rectangle on its X/Z plane.
    /// The two-dimensional collision code calls Unity's Z coordinate Y.
    /// </summary>
    public readonly struct RectangleLocalBounds {
        public RectangleLocalBounds(float minX, float minY, float maxX, float maxY) {
            CollisionMath.ThrowIfNotFinite(minX, nameof(minX));
            CollisionMath.ThrowIfNotFinite(minY, nameof(minY));
            CollisionMath.ThrowIfNotFinite(maxX, nameof(maxX));
            CollisionMath.ThrowIfNotFinite(maxY, nameof(maxY));
            if (!(minX < maxX)) {
                throw new ArgumentException("minX must be less than maxX.", nameof(minX));
            }

            if (!(minY < maxY)) {
                throw new ArgumentException("minY must be less than maxY.", nameof(minY));
            }

            this.MinX = minX;
            this.MinY = minY;
            this.MaxX = maxX;
            this.MaxY = maxY;
        }

        public float MinX { get; }
        public float MinY { get; }
        public float MaxX { get; }
        public float MaxY { get; }

        internal bool IsValid => CollisionMath.IsFinite(this.MinX)
            && CollisionMath.IsFinite(this.MinY)
            && CollisionMath.IsFinite(this.MaxX)
            && CollisionMath.IsFinite(this.MaxY)
            && this.MinX < this.MaxX
            && this.MinY < this.MaxY;
    }

    /// <summary>
    /// World-space X/Z position and clockwise yaw, expressed in degrees.
    /// </summary>
    public readonly struct RectanglePose {
        public RectanglePose(float positionX, float positionY, float rotationDegrees) {
            CollisionMath.ThrowIfNotFinite(positionX, nameof(positionX));
            CollisionMath.ThrowIfNotFinite(positionY, nameof(positionY));
            CollisionMath.ThrowIfNotFinite(rotationDegrees, nameof(rotationDegrees));
            this.PositionX = positionX;
            this.PositionY = positionY;
            this.RotationDegrees = rotationDegrees;
        }

        public float PositionX { get; }
        public float PositionY { get; }
        public float RotationDegrees { get; }
    }

    /// <summary>
    /// Exact vehicle-perimeter versus track-outline collision detector.
    ///
    /// Ordinary edges are stored once, by AABB center, in a vehicle-scale grid.
    /// Edges larger than that scale use a small scan or an immutable AABB BVH.
    /// All broad phases are conservative; the final no-epsilon segment predicate
    /// is exact for the binary32 coordinates supplied to it.
    /// </summary>
    public sealed class TrackCollisionDetector {
        private const int DenseGridMinimumCellLimit = 4096;
        private const int DenseGridCellsPerEdgeLimit = 4;
        private const int DenseGridAbsoluteCellLimit = 1_048_576;
        private const int OutlierLinearScanLimit = 8;
        private const int OutlierBvhLeafSize = 8;
        private const int MinimumBroadQueryCellThreshold = 16;

        private readonly Edge[] _edges;
        private readonly AabbF _allBounds;
        private readonly CenterGrid _grid;
        private readonly OutlierIndex _outliers;

        public TrackCollisionDetector(
            ColliderJson colliderJson,
            RectangleLocalBounds representativeVehicleBounds) {
            if (colliderJson == null) {
                throw new ArgumentNullException(nameof(colliderJson));
            }

            if (!representativeVehicleBounds.IsValid) {
                throw new ArgumentException(
                    "Representative vehicle bounds must be finite and have positive extents.",
                    nameof(representativeVehicleBounds));
            }

            ValidateFormat(colliderJson);
            this._edges = CreateEdges(colliderJson, out int outlineCount);
            this.OutlineCount = outlineCount;
            this.EdgeCount = this._edges.Length;
            this._allBounds = CombineAllBounds(this._edges);

            double width = (double)representativeVehicleBounds.MaxX
                - representativeVehicleBounds.MinX;
            double height = (double)representativeVehicleBounds.MaxY
                - representativeVehicleBounds.MinY;
            double cellSize = Math.Min(width, height);
            if (!(cellSize > 0.0) || double.IsInfinity(cellSize) || double.IsNaN(cellSize)) {
                throw new ArgumentException(
                    "Representative vehicle bounds do not define a usable grid scale.",
                    nameof(representativeVehicleBounds));
            }

            List<int> ordinaryEdgeIds = new(this.EdgeCount);
            List<int> outlierEdgeIds = new();
            for (int edgeIndex = 0; edgeIndex < this.EdgeCount; ++edgeIndex) {
                AabbF bounds = this._edges[edgeIndex].Bounds;
                double edgeWidth = (double)bounds.MaxX - bounds.MinX;
                double edgeHeight = (double)bounds.MaxY - bounds.MinY;
                if (edgeWidth <= cellSize && edgeHeight <= cellSize) {
                    ordinaryEdgeIds.Add(edgeIndex);
                } else {
                    outlierEdgeIds.Add(edgeIndex);
                }
            }

            if (!CenterGrid.TryCreate(
                    this._edges,
                    ordinaryEdgeIds,
                    cellSize,
                    out CenterGrid grid)) {
                // This requires an enormous coordinate span relative to the vehicle.
                // A BVH remains exact and prevents unsafe integer cell arithmetic.
                outlierEdgeIds.AddRange(ordinaryEdgeIds);
                ordinaryEdgeIds.Clear();
                grid = CenterGrid.CreateEmpty(cellSize);
            }

            this._grid = grid;
            this._outliers = new OutlierIndex(this._edges, outlierEdgeIds);

            this.OrdinaryEdgeCount = this._grid.EdgeIds.Length;
            this.OutlierEdgeCount = this._outliers.EdgeCount;
            this.CellSize = cellSize;
            this.GridColumnCount = this._grid.ColumnCount;
            this.GridRowCount = this._grid.RowCount;
            this.OccupiedGridCellCount = this._grid.OccupiedCellCount;
            this.UsesDenseGrid = this._grid.DenseCells != null;
            this.OutlierBvhNodeCount = this._outliers.NodeCount;
            this.BroadQueryCellThreshold = Math.Max(
                MinimumBroadQueryCellThreshold,
                this.EdgeCount / 4);
        }

        public int OutlineCount { get; }
        public int EdgeCount { get; }
        public int OrdinaryEdgeCount { get; }
        public int OutlierEdgeCount { get; }
        public double CellSize { get; }
        public int GridColumnCount { get; }
        public int GridRowCount { get; }
        public int OccupiedGridCellCount { get; }
        public long GridCellCount => (long)this.GridColumnCount * this.GridRowCount;
        public int OccupiedCellCount => this.OccupiedGridCellCount;
        public bool UsesDenseGrid { get; }
        public int StoredGridEdgeReferenceCount => this._grid.EdgeIds.Length;
        public int OutlierBvhNodeCount { get; }
        public int OversizedEdgeCount => this.OutlierEdgeCount;
        public int BroadQueryCellThreshold { get; }

        public bool IsColliding(RectangleLocalBounds localBounds, RectanglePose pose) {
            RectangleQuad rectangle = CreateRectangle(localBounds, pose);
            if (!this._allBounds.Overlaps(rectangle.Bounds)) {
                return false;
            }

            if (this._grid.TryGetQueryRange(
                    rectangle.Bounds,
                    out int minColumn,
                    out int minRow,
                    out int maxColumn,
                    out int maxRow,
                    out long coveredCellCount)) {
                if (coveredCellCount > this.BroadQueryCellThreshold) {
                    return this.ScanAll(rectangle);
                }

                if (this.QueryGrid(
                        rectangle,
                        minColumn,
                        minRow,
                        maxColumn,
                        maxRow)) {
                    return true;
                }
            }

            return this._outliers.Intersects(rectangle);
        }

        /// <summary>
        /// Simple exact implementation retained as a game-side correctness oracle.
        /// </summary>
        internal bool IsCollidingLinear(RectangleLocalBounds localBounds, RectanglePose pose) {
            RectangleQuad rectangle = CreateRectangle(localBounds, pose);
            return this._allBounds.Overlaps(rectangle.Bounds) && this.ScanAll(rectangle);
        }

        private static void ValidateFormat(ColliderJson colliderJson) {
            if (colliderJson.FormatVersion != ColliderJson.CurrentFormatVersion) {
                throw new ArgumentException(
                    $"Unsupported collider-data format version {colliderJson.FormatVersion}; "
                    + $"expected {ColliderJson.CurrentFormatVersion}.",
                    nameof(colliderJson));
            }

            if (!string.Equals(
                    colliderJson.CoordinateSystem,
                    ColliderJson.BlenderWorldXYCoordinateSystem,
                    StringComparison.Ordinal)) {
                throw new ArgumentException(
                    $"Unsupported collider-data coordinate system "
                    + $"'{colliderJson.CoordinateSystem ?? "<null>"}'.",
                    nameof(colliderJson));
            }

            if (colliderJson.Outlines == null) {
                throw new ArgumentException("Collider outlines must not be null.", nameof(colliderJson));
            }

            if (colliderJson.Outlines.Count == 0) {
                throw new ArgumentException("Collider data must contain at least one outline.", nameof(colliderJson));
            }
        }

        private static Edge[] CreateEdges(ColliderJson colliderJson, out int outlineCount) {
            outlineCount = colliderJson.Outlines.Count;
            long edgeCountLong = 0;
            for (int outlineIndex = 0; outlineIndex < outlineCount; ++outlineIndex) {
                Outline outline = colliderJson.Outlines[outlineIndex];
                if (outline == null || outline.Vertices == null) {
                    throw new ArgumentException(
                        $"Outline {outlineIndex} or its vertex list is null.",
                        nameof(colliderJson));
                }

                if (outline.Vertices.Count < 3) {
                    throw new ArgumentException(
                        $"Outline {outlineIndex} must contain at least three vertices.",
                        nameof(colliderJson));
                }

                edgeCountLong += outline.Vertices.Count;
                if (edgeCountLong > int.MaxValue) {
                    throw new ArgumentException("Collider data contains too many vertices.", nameof(colliderJson));
                }
            }

            Edge[] edges = new Edge[(int)edgeCountLong];
            int outputIndex = 0;
            for (int outlineIndex = 0; outlineIndex < outlineCount; ++outlineIndex) {
                List<CoordinateXY> vertices = colliderJson.Outlines[outlineIndex].Vertices;
                for (int vertexIndex = 0; vertexIndex < vertices.Count; ++vertexIndex) {
                    CoordinateXY rawA = vertices[vertexIndex];
                    CoordinateXY rawB = vertices[
                        vertexIndex + 1 == vertices.Count ? 0 : vertexIndex + 1];
                    if (!CollisionMath.IsFinite(rawA.X)
                        || !CollisionMath.IsFinite(rawA.Y)
                        || !CollisionMath.IsFinite(rawB.X)
                        || !CollisionMath.IsFinite(rawB.Y)) {
                        throw new ArgumentException(
                            $"Outline {outlineIndex} contains a nonfinite coordinate.",
                            nameof(colliderJson));
                    }

                    // Format 1 is deliberately raw Blender world X/Y. Unity's FBX
                    // Bake Axis Conversion maps the ground plane as (-X, -Y) -> (X, Z).
                    PointF a = new(-rawA.X, -rawA.Y);
                    PointF b = new(-rawB.X, -rawB.Y);
                    if (a.X == b.X && a.Y == b.Y) {
                        throw new ArgumentException(
                            $"Outline {outlineIndex} contains a zero-length segment.",
                            nameof(colliderJson));
                    }

                    edges[outputIndex++] = new Edge(a, b);
                }
            }

            return edges;
        }

        private static AabbF CombineAllBounds(Edge[] edges) {
            AabbF first = edges[0].Bounds;
            float minX = first.MinX;
            float minY = first.MinY;
            float maxX = first.MaxX;
            float maxY = first.MaxY;
            for (int i = 1; i < edges.Length; ++i) {
                AabbF bounds = edges[i].Bounds;
                minX = Math.Min(minX, bounds.MinX);
                minY = Math.Min(minY, bounds.MinY);
                maxX = Math.Max(maxX, bounds.MaxX);
                maxY = Math.Max(maxY, bounds.MaxY);
            }

            return new AabbF(minX, minY, maxX, maxY);
        }

        private static RectangleQuad CreateRectangle(
            RectangleLocalBounds localBounds,
            RectanglePose pose) {
            if (!localBounds.IsValid) {
                throw new ArgumentException(
                    "Vehicle bounds must be finite and have positive extents.",
                    nameof(localBounds));
            }

            if (!CollisionMath.IsFinite(pose.PositionX)
                || !CollisionMath.IsFinite(pose.PositionY)
                || !CollisionMath.IsFinite(pose.RotationDegrees)) {
                throw new ArgumentException("Vehicle pose must be finite.", nameof(pose));
            }

            return RectangleTransformer.Transform(localBounds, pose);
        }

        private bool QueryGrid(
            in RectangleQuad rectangle,
            int minColumn,
            int minRow,
            int maxColumn,
            int maxRow) {
            if (this._grid.DenseCells != null) {
                for (int row = minRow; row <= maxRow; ++row) {
                    int rowOffset = row * this._grid.ColumnCount;
                    for (int column = minColumn; column <= maxColumn; ++column) {
                        CellRange range = this._grid.DenseCells[rowOffset + column];
                        if (this.QueryCell(range, rectangle)) {
                            return true;
                        }
                    }
                }
            } else {
                Dictionary<long, CellRange> cells = this._grid.SparseCells;
                for (int row = minRow; row <= maxRow; ++row) {
                    for (int column = minColumn; column <= maxColumn; ++column) {
                        if (cells.TryGetValue(CenterGrid.PackCell(column, row), out CellRange range)
                            && this.QueryCell(range, rectangle)) {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool QueryCell(in CellRange range, in RectangleQuad rectangle) {
            int end = range.Offset + range.Count;
            for (int i = range.Offset; i < end; ++i) {
                int edgeIndex = this._grid.EdgeIds[i];
                if (this.EdgeIntersectsRectangle(edgeIndex, rectangle)) {
                    return true;
                }
            }

            return false;
        }

        private bool ScanAll(in RectangleQuad rectangle) {
            for (int edgeIndex = 0; edgeIndex < this._edges.Length; ++edgeIndex) {
                if (this.EdgeIntersectsRectangle(edgeIndex, rectangle)) {
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EdgeIntersectsRectangle(int edgeIndex, in RectangleQuad rectangle) {
            Edge edge = this._edges[edgeIndex];
            return edge.Bounds.Overlaps(rectangle.Bounds)
                && rectangle.IntersectsSegment(edge.A, edge.B);
        }

        private readonly struct PointF {
            internal PointF(float x, float y) {
                this.X = x;
                this.Y = y;
            }

            internal float X { get; }
            internal float Y { get; }
        }

        private readonly struct AabbF {
            internal AabbF(float minX, float minY, float maxX, float maxY) {
                this.MinX = minX;
                this.MinY = minY;
                this.MaxX = maxX;
                this.MaxY = maxY;
            }

            internal float MinX { get; }
            internal float MinY { get; }
            internal float MaxX { get; }
            internal float MaxY { get; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal bool Overlaps(in AabbF other) {
                return this.MinX <= other.MaxX
                    && this.MaxX >= other.MinX
                    && this.MinY <= other.MaxY
                    && this.MaxY >= other.MinY;
            }

            internal static AabbF FromSegment(in PointF a, in PointF b) {
                return new AabbF(
                    Math.Min(a.X, b.X),
                    Math.Min(a.Y, b.Y),
                    Math.Max(a.X, b.X),
                    Math.Max(a.Y, b.Y));
            }

            internal static AabbF Combine(in AabbF a, in AabbF b) {
                return new AabbF(
                    Math.Min(a.MinX, b.MinX),
                    Math.Min(a.MinY, b.MinY),
                    Math.Max(a.MaxX, b.MaxX),
                    Math.Max(a.MaxY, b.MaxY));
            }
        }

        private readonly struct Edge {
            internal Edge(PointF a, PointF b) {
                this.A = a;
                this.B = b;
                this.Bounds = AabbF.FromSegment(a, b);
            }

            internal PointF A { get; }
            internal PointF B { get; }
            internal AabbF Bounds { get; }

            internal double CenterX => ((double)this.Bounds.MinX + this.Bounds.MaxX) * 0.5;
            internal double CenterY => ((double)this.Bounds.MinY + this.Bounds.MaxY) * 0.5;
        }

        private readonly struct CellRange {
            internal CellRange(int offset, int count) {
                this.Offset = offset;
                this.Count = count;
            }

            internal int Offset { get; }
            internal int Count { get; }
        }

        private sealed class CenterGrid {
            private CenterGrid(
                double cellSize,
                double originX,
                double originY,
                double maximumCenterX,
                double maximumCenterY,
                double maximumHalfExtentX,
                double maximumHalfExtentY,
                int columnCount,
                int rowCount,
                int occupiedCellCount,
                CellRange[] denseCells,
                Dictionary<long, CellRange> sparseCells,
                int[] edgeIds) {
                this.CellSize = cellSize;
                this.OriginX = originX;
                this.OriginY = originY;
                this.MaximumCenterX = maximumCenterX;
                this.MaximumCenterY = maximumCenterY;
                this.MaximumHalfExtentX = maximumHalfExtentX;
                this.MaximumHalfExtentY = maximumHalfExtentY;
                this.ColumnCount = columnCount;
                this.RowCount = rowCount;
                this.OccupiedCellCount = occupiedCellCount;
                this.DenseCells = denseCells;
                this.SparseCells = sparseCells;
                this.EdgeIds = edgeIds;
            }

            internal double CellSize { get; }
            internal double OriginX { get; }
            internal double OriginY { get; }
            internal double MaximumCenterX { get; }
            internal double MaximumCenterY { get; }
            internal double MaximumHalfExtentX { get; }
            internal double MaximumHalfExtentY { get; }
            internal int ColumnCount { get; }
            internal int RowCount { get; }
            internal int OccupiedCellCount { get; }
            internal CellRange[] DenseCells { get; }
            internal Dictionary<long, CellRange> SparseCells { get; }
            internal int[] EdgeIds { get; }

            internal static CenterGrid CreateEmpty(double cellSize) {
                return new CenterGrid(
                    cellSize,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0,
                    0,
                    0,
                    null,
                    null,
                    Array.Empty<int>());
            }

            internal static bool TryCreate(
                Edge[] edges,
                List<int> ordinaryEdgeIds,
                double cellSize,
                out CenterGrid grid) {
                if (ordinaryEdgeIds.Count == 0) {
                    grid = CreateEmpty(cellSize);
                    return true;
                }

                double originX = double.PositiveInfinity;
                double originY = double.PositiveInfinity;
                double maximumCenterX = double.NegativeInfinity;
                double maximumCenterY = double.NegativeInfinity;
                double maximumHalfExtentX = 0.0;
                double maximumHalfExtentY = 0.0;
                for (int i = 0; i < ordinaryEdgeIds.Count; ++i) {
                    Edge edge = edges[ordinaryEdgeIds[i]];
                    double centerX = edge.CenterX;
                    double centerY = edge.CenterY;
                    originX = Math.Min(originX, centerX);
                    originY = Math.Min(originY, centerY);
                    maximumCenterX = Math.Max(maximumCenterX, centerX);
                    maximumCenterY = Math.Max(maximumCenterY, centerY);
                    maximumHalfExtentX = Math.Max(
                        maximumHalfExtentX,
                        Math.Max(centerX - edge.Bounds.MinX, edge.Bounds.MaxX - centerX));
                    maximumHalfExtentY = Math.Max(
                        maximumHalfExtentY,
                        Math.Max(centerY - edge.Bounds.MinY, edge.Bounds.MaxY - centerY));
                }

                // Make the global expansion conservative even when a midpoint or
                // subtraction rounded by one binary64 ULP.
                maximumHalfExtentX = CollisionMath.BitIncrement(maximumHalfExtentX);
                maximumHalfExtentY = CollisionMath.BitIncrement(maximumHalfExtentY);

                double maximumColumnDouble = Math.Floor((maximumCenterX - originX) / cellSize);
                double maximumRowDouble = Math.Floor((maximumCenterY - originY) / cellSize);
                if (!(maximumColumnDouble >= 0.0)
                    || !(maximumRowDouble >= 0.0)
                    || maximumColumnDouble >= int.MaxValue
                    || maximumRowDouble >= int.MaxValue) {
                    grid = null;
                    return false;
                }

                int columnCount = (int)maximumColumnDouble + 1;
                int rowCount = (int)maximumRowDouble + 1;
                long totalCellCount = (long)columnCount * rowCount;
                CellAssignment[] assignments = new CellAssignment[ordinaryEdgeIds.Count];
                for (int i = 0; i < ordinaryEdgeIds.Count; ++i) {
                    int edgeIndex = ordinaryEdgeIds[i];
                    Edge edge = edges[edgeIndex];
                    int column = MapCenterToCell(edge.CenterX, originX, cellSize, columnCount);
                    int row = MapCenterToCell(edge.CenterY, originY, cellSize, rowCount);
                    assignments[i] = new CellAssignment(column, row, edgeIndex);
                }

                long edgeScaledDenseLimit = Math.Max(
                    DenseGridMinimumCellLimit,
                    (long)DenseGridCellsPerEdgeLimit * ordinaryEdgeIds.Count);
                bool useDense = totalCellCount <= edgeScaledDenseLimit
                    && totalCellCount <= DenseGridAbsoluteCellLimit;

                if (useDense) {
                    BuildDense(
                        assignments,
                        columnCount,
                        rowCount,
                        out CellRange[] cells,
                        out int[] edgeIds,
                        out int occupiedCellCount);
                    grid = new CenterGrid(
                        cellSize,
                        originX,
                        originY,
                        maximumCenterX,
                        maximumCenterY,
                        maximumHalfExtentX,
                        maximumHalfExtentY,
                        columnCount,
                        rowCount,
                        occupiedCellCount,
                        cells,
                        null,
                        edgeIds);
                } else {
                    BuildSparse(
                        assignments,
                        out Dictionary<long, CellRange> cells,
                        out int[] edgeIds);
                    grid = new CenterGrid(
                        cellSize,
                        originX,
                        originY,
                        maximumCenterX,
                        maximumCenterY,
                        maximumHalfExtentX,
                        maximumHalfExtentY,
                        columnCount,
                        rowCount,
                        cells.Count,
                        null,
                        cells,
                        edgeIds);
                }

                return true;
            }

            internal bool TryGetQueryRange(
                in AabbF queryBounds,
                out int minColumn,
                out int minRow,
                out int maxColumn,
                out int maxRow,
                out long coveredCellCount) {
                minColumn = 0;
                minRow = 0;
                maxColumn = -1;
                maxRow = -1;
                coveredCellCount = 0;
                if (this.EdgeIds.Length == 0) {
                    return false;
                }

                double expandedMinX = CollisionMath.BitDecrement(
                    (double)queryBounds.MinX - this.MaximumHalfExtentX);
                double expandedMinY = CollisionMath.BitDecrement(
                    (double)queryBounds.MinY - this.MaximumHalfExtentY);
                double expandedMaxX = CollisionMath.BitIncrement(
                    (double)queryBounds.MaxX + this.MaximumHalfExtentX);
                double expandedMaxY = CollisionMath.BitIncrement(
                    (double)queryBounds.MaxY + this.MaximumHalfExtentY);
                if (expandedMaxX < this.OriginX
                    || expandedMaxY < this.OriginY
                    || expandedMinX > this.MaximumCenterX
                    || expandedMinY > this.MaximumCenterY) {
                    return false;
                }

                minColumn = expandedMinX <= this.OriginX
                    ? 0
                    : MapInteriorToCell(expandedMinX, this.OriginX, this.CellSize, this.ColumnCount);
                minRow = expandedMinY <= this.OriginY
                    ? 0
                    : MapInteriorToCell(expandedMinY, this.OriginY, this.CellSize, this.RowCount);
                maxColumn = expandedMaxX >= this.MaximumCenterX
                    ? this.ColumnCount - 1
                    : MapInteriorToCell(expandedMaxX, this.OriginX, this.CellSize, this.ColumnCount);
                maxRow = expandedMaxY >= this.MaximumCenterY
                    ? this.RowCount - 1
                    : MapInteriorToCell(expandedMaxY, this.OriginY, this.CellSize, this.RowCount);
                coveredCellCount = (long)(maxColumn - minColumn + 1)
                    * (maxRow - minRow + 1);
                return true;
            }

            internal static long PackCell(int column, int row) {
                return ((long)row << 32) | (uint)column;
            }

            private static int MapCenterToCell(
                double value,
                double origin,
                double cellSize,
                int cellCount) {
                double mapped = Math.Floor((value - origin) / cellSize);
                if (mapped <= 0.0) {
                    return 0;
                }

                if (mapped >= cellCount - 1) {
                    return cellCount - 1;
                }

                return (int)mapped;
            }

            private static int MapInteriorToCell(
                double value,
                double origin,
                double cellSize,
                int cellCount) {
                int mapped = (int)Math.Floor((value - origin) / cellSize);
                if (mapped < 0) {
                    return 0;
                }

                return mapped >= cellCount ? cellCount - 1 : mapped;
            }

            private static void BuildDense(
                CellAssignment[] assignments,
                int columnCount,
                int rowCount,
                out CellRange[] cells,
                out int[] edgeIds,
                out int occupiedCellCount) {
                int cellCount = checked(columnCount * rowCount);
                int[] counts = new int[cellCount];
                for (int i = 0; i < assignments.Length; ++i) {
                    CellAssignment assignment = assignments[i];
                    ++counts[assignment.Row * columnCount + assignment.Column];
                }

                cells = new CellRange[cellCount];
                int[] cursors = new int[cellCount];
                int offset = 0;
                occupiedCellCount = 0;
                for (int cellIndex = 0; cellIndex < cellCount; ++cellIndex) {
                    int count = counts[cellIndex];
                    cells[cellIndex] = new CellRange(offset, count);
                    cursors[cellIndex] = offset;
                    offset += count;
                    if (count != 0) {
                        ++occupiedCellCount;
                    }
                }

                edgeIds = new int[assignments.Length];
                for (int i = 0; i < assignments.Length; ++i) {
                    CellAssignment assignment = assignments[i];
                    int cellIndex = assignment.Row * columnCount + assignment.Column;
                    edgeIds[cursors[cellIndex]++] = assignment.EdgeIndex;
                }
            }

            private static void BuildSparse(
                CellAssignment[] assignments,
                out Dictionary<long, CellRange> cells,
                out int[] edgeIds) {
                Dictionary<long, List<int>> builders = new();
                for (int i = 0; i < assignments.Length; ++i) {
                    CellAssignment assignment = assignments[i];
                    long key = PackCell(assignment.Column, assignment.Row);
                    if (!builders.TryGetValue(key, out List<int> builder)) {
                        builder = new List<int>();
                        builders.Add(key, builder);
                    }

                    builder.Add(assignment.EdgeIndex);
                }

                cells = new Dictionary<long, CellRange>(builders.Count);
                edgeIds = new int[assignments.Length];
                int offset = 0;
                foreach (KeyValuePair<long, List<int>> pair in builders) {
                    List<int> builder = pair.Value;
                    cells.Add(pair.Key, new CellRange(offset, builder.Count));
                    builder.CopyTo(edgeIds, offset);
                    offset += builder.Count;
                }
            }

            private readonly struct CellAssignment {
                internal CellAssignment(int column, int row, int edgeIndex) {
                    this.Column = column;
                    this.Row = row;
                    this.EdgeIndex = edgeIndex;
                }

                internal int Column { get; }
                internal int Row { get; }
                internal int EdgeIndex { get; }
            }
        }

        private sealed class OutlierIndex {
            private readonly Edge[] _edges;
            private readonly int[] _edgeOrder;
            private readonly BvhNode[] _nodes;

            internal OutlierIndex(Edge[] edges, List<int> edgeIds) {
                this._edges = edges;
                this._edgeOrder = edgeIds.ToArray();
                this.EdgeCount = this._edgeOrder.Length;
                if (this.EdgeCount <= OutlierLinearScanLimit) {
                    this._nodes = Array.Empty<BvhNode>();
                    return;
                }

                this._nodes = new BvhNode[checked(this.EdgeCount * 2 - 1)];
                _ = this.BuildNode(0, this.EdgeCount);
            }

            internal int EdgeCount { get; }
            internal int NodeCount { get; private set; }

            internal bool Intersects(in RectangleQuad rectangle) {
                if (this.EdgeCount == 0) {
                    return false;
                }

                if (this._nodes.Length == 0) {
                    for (int i = 0; i < this._edgeOrder.Length; ++i) {
                        Edge edge = this._edges[this._edgeOrder[i]];
                        if (edge.Bounds.Overlaps(rectangle.Bounds)
                            && rectangle.IntersectsSegment(edge.A, edge.B)) {
                            return true;
                        }
                    }

                    return false;
                }

                return this.QueryNode(0, rectangle);
            }

            private bool QueryNode(int nodeIndex, in RectangleQuad rectangle) {
                BvhNode node = this._nodes[nodeIndex];
                if (!node.Bounds.Overlaps(rectangle.Bounds)) {
                    return false;
                }

                if (node.Count != 0) {
                    int end = node.Start + node.Count;
                    for (int i = node.Start; i < end; ++i) {
                        Edge edge = this._edges[this._edgeOrder[i]];
                        if (edge.Bounds.Overlaps(rectangle.Bounds)
                            && rectangle.IntersectsSegment(edge.A, edge.B)) {
                            return true;
                        }
                    }

                    return false;
                }

                return this.QueryNode(node.Left, rectangle)
                    || this.QueryNode(node.Right, rectangle);
            }

            private int BuildNode(int start, int count) {
                int nodeIndex = this.NodeCount++;
                AabbF bounds = this.ComputeRangeBounds(start, count);
                if (count <= OutlierBvhLeafSize) {
                    this._nodes[nodeIndex] = BvhNode.CreateLeaf(bounds, start, count);
                    return nodeIndex;
                }

                bool useX = this.CenterSpread(start, count, true)
                    >= this.CenterSpread(start, count, false);
                int middle = start + count / 2;
                this.SelectNth(start, start + count, middle, useX);
                int left = this.BuildNode(start, middle - start);
                int right = this.BuildNode(middle, start + count - middle);
                this._nodes[nodeIndex] = BvhNode.CreateBranch(bounds, left, right);
                return nodeIndex;
            }

            private AabbF ComputeRangeBounds(int start, int count) {
                AabbF bounds = this._edges[this._edgeOrder[start]].Bounds;
                int end = start + count;
                for (int i = start + 1; i < end; ++i) {
                    bounds = AabbF.Combine(bounds, this._edges[this._edgeOrder[i]].Bounds);
                }

                return bounds;
            }

            private double CenterSpread(int start, int count, bool useX) {
                double minimum = double.PositiveInfinity;
                double maximum = double.NegativeInfinity;
                int end = start + count;
                for (int i = start; i < end; ++i) {
                    double center = this.Center(this._edgeOrder[i], useX);
                    minimum = Math.Min(minimum, center);
                    maximum = Math.Max(maximum, center);
                }

                return maximum - minimum;
            }

            private void SelectNth(int start, int endExclusive, int nth, bool useX) {
                int left = start;
                int right = endExclusive - 1;
                while (left < right) {
                    int pivotEdge = this.MedianOfThreeEdge(
                        left,
                        left + (right - left) / 2,
                        right,
                        useX);
                    double pivotCenter = this.Center(pivotEdge, useX);
                    int i = left;
                    int j = right;
                    while (i <= j) {
                        while (this.CompareToPivot(
                                this._edgeOrder[i],
                                pivotCenter,
                                pivotEdge,
                                useX) < 0) {
                            ++i;
                        }

                        while (this.CompareToPivot(
                                this._edgeOrder[j],
                                pivotCenter,
                                pivotEdge,
                                useX) > 0) {
                            --j;
                        }

                        if (i <= j) {
                            (this._edgeOrder[j], this._edgeOrder[i]) = (this._edgeOrder[i], this._edgeOrder[j]);
                            ++i;
                            --j;
                        }
                    }

                    if (nth <= j) {
                        right = j;
                    } else if (nth >= i) {
                        left = i;
                    } else {
                        return;
                    }
                }
            }

            private int MedianOfThreeEdge(int a, int b, int c, bool useX) {
                int edgeA = this._edgeOrder[a];
                int edgeB = this._edgeOrder[b];
                int edgeC = this._edgeOrder[c];
                if (this.CompareEdges(edgeA, edgeB, useX) > 0) {
                    (edgeB, edgeA) = (edgeA, edgeB);
                }

                if (this.CompareEdges(edgeB, edgeC, useX) > 0) {
                    edgeB = edgeC;
                }

                if (this.CompareEdges(edgeA, edgeB, useX) > 0) {
                    edgeB = edgeA;
                }

                return edgeB;
            }

            private int CompareEdges(int first, int second, bool useX) {
                int centerComparison = this.Center(first, useX).CompareTo(this.Center(second, useX));
                return centerComparison != 0 ? centerComparison : first.CompareTo(second);
            }

            private int CompareToPivot(
                int edgeIndex,
                double pivotCenter,
                int pivotEdge,
                bool useX) {
                int centerComparison = this.Center(edgeIndex, useX).CompareTo(pivotCenter);
                return centerComparison != 0 ? centerComparison : edgeIndex.CompareTo(pivotEdge);
            }

            private double Center(int edgeIndex, bool useX) {
                Edge edge = this._edges[edgeIndex];
                return useX ? edge.CenterX : edge.CenterY;
            }

            private readonly struct BvhNode {
                private BvhNode(AabbF bounds, int left, int right, int start, int count) {
                    this.Bounds = bounds;
                    this.Left = left;
                    this.Right = right;
                    this.Start = start;
                    this.Count = count;
                }

                internal AabbF Bounds { get; }
                internal int Left { get; }
                internal int Right { get; }
                internal int Start { get; }
                internal int Count { get; }

                internal static BvhNode CreateLeaf(AabbF bounds, int start, int count) {
                    return new BvhNode(bounds, -1, -1, start, count);
                }

                internal static BvhNode CreateBranch(AabbF bounds, int left, int right) {
                    return new BvhNode(bounds, left, right, 0, 0);
                }
            }
        }

        private readonly struct RectangleQuad {
            internal RectangleQuad(PointF p0, PointF p1, PointF p2, PointF p3) {
                this.P0 = p0;
                this.P1 = p1;
                this.P2 = p2;
                this.P3 = p3;
                this.Bounds = new AabbF(
                    Math.Min(Math.Min(p0.X, p1.X), Math.Min(p2.X, p3.X)),
                    Math.Min(Math.Min(p0.Y, p1.Y), Math.Min(p2.Y, p3.Y)),
                    Math.Max(Math.Max(p0.X, p1.X), Math.Max(p2.X, p3.X)),
                    Math.Max(Math.Max(p0.Y, p1.Y), Math.Max(p2.Y, p3.Y)));
            }

            internal PointF P0 { get; }
            internal PointF P1 { get; }
            internal PointF P2 { get; }
            internal PointF P3 { get; }
            internal AabbF Bounds { get; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal bool IntersectsSegment(in PointF a, in PointF b) {
                return RobustPredicates.SegmentsIntersect(a, b, this.P0, this.P1)
                    || RobustPredicates.SegmentsIntersect(a, b, this.P1, this.P2)
                    || RobustPredicates.SegmentsIntersect(a, b, this.P2, this.P3)
                    || RobustPredicates.SegmentsIntersect(a, b, this.P3, this.P0);
            }
        }

        private static class RectangleTransformer {
            private const double DegreesToRadians = Math.PI / 180.0;

            internal static RectangleQuad Transform(
                RectangleLocalBounds bounds,
                RectanglePose pose) {
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
                double sine) {
                double xCosine = (double)localX * cosine;
                double ySine = (double)localY * sine;
                double xSine = (double)localX * sine;
                double yCosine = (double)localY * cosine;
                double worldXDouble = (double)pose.PositionX + xCosine + ySine;
                double worldYDouble = (double)pose.PositionY - xSine + yCosine;
                float worldX = (float)worldXDouble;
                float worldY = (float)worldYDouble;
                if (!CollisionMath.IsFinite(worldX) || !CollisionMath.IsFinite(worldY)) {
                    throw new ArgumentOutOfRangeException(
                        nameof(pose),
                        "The transformed vehicle rectangle must have finite coordinates.");
                }

                return new PointF(worldX, worldY);
            }
        }

        private static class RobustPredicates {
            // Shewchuk's orient2d first-stage error bound for IEEE binary64.
            private const double CcwErrorBoundA = 3.3306690738754716e-16;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static bool SegmentsIntersect(
                in PointF a,
                in PointF b,
                in PointF c,
                in PointF d) {
                AabbF firstBounds = AabbF.FromSegment(a, b);
                AabbF secondBounds = AabbF.FromSegment(c, d);
                if (!firstBounds.Overlaps(secondBounds)) {
                    return false;
                }

                int abc = OrientationSign(a, b, c);
                int abd = OrientationSign(a, b, d);
                int cda = OrientationSign(c, d, a);
                int cdb = OrientationSign(c, d, b);
                if (abc == 0 && IsOnClosedSegment(a, b, c)) {
                    return true;
                }

                if (abd == 0 && IsOnClosedSegment(a, b, d)) {
                    return true;
                }

                if (cda == 0 && IsOnClosedSegment(c, d, a)) {
                    return true;
                }

                if (cdb == 0 && IsOnClosedSegment(c, d, b)) {
                    return true;
                }

                return abc != abd && cda != cdb;
            }

            private static int OrientationSign(in PointF a, in PointF b, in PointF c) {
                if (IsSubnormal(a.X)
                    || IsSubnormal(a.Y)
                    || IsSubnormal(b.X)
                    || IsSubnormal(b.Y)
                    || IsSubnormal(c.X)
                    || IsSubnormal(c.Y)) {
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
                if (determinantLeft > 0.0) {
                    if (determinantRight <= 0.0) {
                        return SignOrExact(determinant, a, b, c);
                    }

                    determinantSum = determinantLeft + determinantRight;
                } else if (determinantLeft < 0.0) {
                    if (determinantRight >= 0.0) {
                        return SignOrExact(determinant, a, b, c);
                    }

                    determinantSum = -determinantLeft - determinantRight;
                } else {
                    return SignOrExact(determinant, a, b, c);
                }

                double errorBound = CcwErrorBoundA * determinantSum;
                if (determinant >= errorBound) {
                    return 1;
                }

                if (-determinant >= errorBound) {
                    return -1;
                }

                return ExactOrientationSign(a, b, c);
            }

            private static int SignOrExact(
                double value,
                in PointF a,
                in PointF b,
                in PointF c) {
                if (value > 0.0) {
                    return 1;
                }

                if (value < 0.0) {
                    return -1;
                }

                return ExactOrientationSign(a, b, c);
            }

            private static bool IsSubnormal(float value) {
                SingleBits bits = new(value);
                int magnitude = bits.Bits & 0x7fffffff;
                return magnitude != 0 && (magnitude & 0x7f800000) == 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsOnClosedSegment(in PointF a, in PointF b, in PointF point) {
                return point.X >= Math.Min(a.X, b.X)
                    && point.X <= Math.Max(a.X, b.X)
                    && point.Y >= Math.Min(a.Y, b.Y)
                    && point.Y <= Math.Max(a.Y, b.Y);
            }

            private static int ExactOrientationSign(in PointF a, in PointF b, in PointF c) {
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
                if (commonExponent == int.MaxValue) {
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

            private static void IncludeExponent(Dyadic value, ref int minimum) {
                if (value.Significand != 0 && value.Exponent < minimum) {
                    minimum = value.Exponent;
                }
            }

            private readonly struct Dyadic {
                private Dyadic(int significand, int exponent) {
                    this.Significand = significand;
                    this.Exponent = exponent;
                }

                internal int Significand { get; }
                internal int Exponent { get; }

                internal BigInteger ToIntegerAtExponent(int commonExponent) {
                    if (this.Significand == 0) {
                        return BigInteger.Zero;
                    }

                    return new BigInteger(this.Significand) << (this.Exponent - commonExponent);
                }

                internal static Dyadic FromSingle(float value) {
                    SingleBits union = new(value);
                    int bits = union.Bits;
                    int magnitude = bits & 0x7fffffff;
                    int rawExponent = (magnitude >> 23) & 0xff;
                    int fraction = magnitude & 0x7fffff;
                    if (rawExponent == 0 && fraction == 0) {
                        return new Dyadic(0, 0);
                    }

                    int significand;
                    int exponent;
                    if (rawExponent == 0) {
                        significand = fraction;
                        exponent = -149;
                    } else {
                        significand = (1 << 23) | fraction;
                        exponent = rawExponent - 150;
                    }

                    if (bits < 0) {
                        significand = -significand;
                    }

                    return new Dyadic(significand, exponent);
                }
            }

            [StructLayout(LayoutKind.Explicit)]
            private struct SingleBits {
                internal SingleBits(float value) {
                    this.Bits = 0;
                    this.Value = value;
                }

                [FieldOffset(0)]
                internal float Value;

                [FieldOffset(0)]
                internal int Bits;
            }
        }
    }

    internal static class CollisionMath {
        internal static bool IsFinite(float value) {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static void ThrowIfNotFinite(float value, string parameterName) {
            if (!IsFinite(value)) {
                throw new ArgumentOutOfRangeException(parameterName, "The value must be finite.");
            }
        }

        internal static double BitIncrement(double value) {
            if (double.IsNaN(value) || value == double.PositiveInfinity) {
                return value;
            }

            if (value == 0.0) {
                return double.Epsilon;
            }

            long bits = BitConverter.DoubleToInt64Bits(value);
            bits += value > 0.0 ? 1 : -1;
            return BitConverter.Int64BitsToDouble(bits);
        }

        internal static double BitDecrement(double value) {
            if (double.IsNaN(value) || value == double.NegativeInfinity) {
                return value;
            }

            if (value == 0.0) {
                return -double.Epsilon;
            }

            long bits = BitConverter.DoubleToInt64Bits(value);
            bits += value > 0.0 ? -1 : 1;
            return BitConverter.Int64BitsToDouble(bits);
        }
    }
}
