using System;
using System.Collections.Generic;

namespace ZoomTracks.CollisionDetection {
    /// <summary>
    /// Exact detector using a bounded-storage uniform grid. Long edge AABBs are
    /// placed in a small overflow list rather than replicated into many cells.
    /// </summary>
    public sealed class UniformGridCollisionDetector : CollisionDetectorBase {
        public const int DefaultMaximumCellsPerEdge = 64;
        public const int DefaultMaximumTargetCellCount = 65_536;
        public const int MaximumSupportedTargetCellCount = 1_048_576;

        private readonly AabbF[] _edgeBounds;
        private readonly int[][] _cells;
        private readonly int[] _overflowEdges;
        private readonly AabbF _outlineBounds;
        private readonly double _width;
        private readonly double _height;

        public UniformGridCollisionDetector(
            List<CoordinateXY> outline1,
            List<CoordinateXY> outline2)
            : this(
                outline1,
                outline2,
                Math.Min(outline1 == null || outline2 == null ? 1 : outline1.Count + outline2.Count,
                    DefaultMaximumTargetCellCount),
                DefaultMaximumCellsPerEdge) {
        }

        public UniformGridCollisionDetector(
            List<CoordinateXY> outline1,
            List<CoordinateXY> outline2,
            int targetCellCount,
            int maximumCellsPerEdge)
            : base(outline1, outline2) {
            if (targetCellCount < 1) {
                throw new ArgumentOutOfRangeException(nameof(targetCellCount));
            }

            if (targetCellCount > MaximumSupportedTargetCellCount) {
                throw new ArgumentOutOfRangeException(
                    nameof(targetCellCount),
                    "The requested grid is too large for this implementation.");
            }

            if (maximumCellsPerEdge < 1) {
                throw new ArgumentOutOfRangeException(nameof(maximumCellsPerEdge));
            }

            this.TargetCellCount = targetCellCount;
            this.MaximumCellsPerEdge = maximumCellsPerEdge;
            this._edgeBounds = new AabbF[this.EdgeCount];
            for (int i = 0; i < this.EdgeCount; ++i) {
                this._edgeBounds[i] = this.GetEdgeBounds(i);
            }

            this._outlineBounds = CombineAllBounds(this._edgeBounds);
            this._width = (double)this._outlineBounds.MaxX - this._outlineBounds.MinX;
            this._height = (double)this._outlineBounds.MaxY - this._outlineBounds.MinY;
            ChooseDimensions(targetCellCount, this._width, this._height, out int columns, out int rows);
            this.ColumnCount = columns;
            this.RowCount = rows;

            List<int>[] builders = new List<int>[checked(columns * rows)];
            List<int> overflow = new();
            int storedReferences = 0;
            for (int edgeIndex = 0; edgeIndex < this.EdgeCount; ++edgeIndex) {
                AabbF bounds = this._edgeBounds[edgeIndex];
                this.GetCellRange(bounds, out int minColumn, out int minRow, out int maxColumn, out int maxRow);
                long coveredCellCount = (long)(maxColumn - minColumn + 1)
                    * (maxRow - minRow + 1);
                if (coveredCellCount > maximumCellsPerEdge) {
                    overflow.Add(edgeIndex);
                    continue;
                }

                for (int row = minRow; row <= maxRow; ++row) {
                    int rowOffset = row * columns;
                    for (int column = minColumn; column <= maxColumn; ++column) {
                        int cellIndex = rowOffset + column;
                        List<int> builder = builders[cellIndex];
                        if (builder == null) {
                            builder = new List<int>();
                            builders[cellIndex] = builder;
                        }

                        builder.Add(edgeIndex);
                        ++storedReferences;
                    }
                }
            }

            this._cells = new int[builders.Length][];
            for (int i = 0; i < builders.Length; ++i) {
                this._cells[i] = builders[i] == null ? Array.Empty<int>() : builders[i].ToArray();
            }

            this._overflowEdges = overflow.ToArray();
            this.StoredEdgeReferenceCount = storedReferences;
        }

        public int TargetCellCount { get; }
        public int MaximumCellsPerEdge { get; }
        public int ColumnCount { get; }
        public int RowCount { get; }
        public int StoredEdgeReferenceCount { get; }
        public int OverflowEdgeCount => this._overflowEdges.Length;

        private protected override bool Query(in RectangleQuad rectangle) {
            if (!this._outlineBounds.Overlaps(rectangle.Bounds)) {
                return false;
            }

            this.GetCellRange(rectangle.Bounds, out int minColumn, out int minRow, out int maxColumn, out int maxRow);
            long coveredCellCount = (long)(maxColumn - minColumn + 1)
                * (maxRow - minRow + 1);
            long linearScanThreshold = Math.Max(16L, this._cells.Length / 8L);
            if (coveredCellCount > linearScanThreshold) {
                // Large rectangle AABBs would revisit grid edges many times. A single
                // exact scan bounds that failure mode and needs no mutable dedup state.
                for (int edgeIndex = 0; edgeIndex < this.EdgeCount; ++edgeIndex) {
                    if (this._edgeBounds[edgeIndex].Overlaps(rectangle.Bounds)
                        && this.EdgeIntersectsRectangleAfterBoundsCheck(edgeIndex, rectangle)) {
                        return true;
                    }
                }

                return false;
            }

            for (int i = 0; i < this._overflowEdges.Length; ++i) {
                int edgeIndex = this._overflowEdges[i];
                if (this._edgeBounds[edgeIndex].Overlaps(rectangle.Bounds)
                    && this.EdgeIntersectsRectangleAfterBoundsCheck(edgeIndex, rectangle)) {
                    return true;
                }
            }

            for (int row = minRow; row <= maxRow; ++row) {
                int rowOffset = row * this.ColumnCount;
                for (int column = minColumn; column <= maxColumn; ++column) {
                    int[] edges = this._cells[rowOffset + column];
                    for (int i = 0; i < edges.Length; ++i) {
                        int edgeIndex = edges[i];
                        if (this._edgeBounds[edgeIndex].Overlaps(rectangle.Bounds)
                            && this.EdgeIntersectsRectangleAfterBoundsCheck(edgeIndex, rectangle)) {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static AabbF CombineAllBounds(AabbF[] bounds) {
            float minX = bounds[0].MinX;
            float minY = bounds[0].MinY;
            float maxX = bounds[0].MaxX;
            float maxY = bounds[0].MaxY;
            for (int i = 1; i < bounds.Length; ++i) {
                minX = Math.Min(minX, bounds[i].MinX);
                minY = Math.Min(minY, bounds[i].MinY);
                maxX = Math.Max(maxX, bounds[i].MaxX);
                maxY = Math.Max(maxY, bounds[i].MaxY);
            }

            return new AabbF(minX, minY, maxX, maxY);
        }

        private static void ChooseDimensions(
            int targetCellCount,
            double width,
            double height,
            out int columns,
            out int rows) {
            if (!(width > 0.0) || !(height > 0.0) || targetCellCount == 1) {
                columns = 1;
                rows = 1;
                return;
            }

            double aspect = width / height;
            if (aspect >= targetCellCount) {
                columns = targetCellCount;
                rows = 1;
                return;
            }

            if (aspect <= 1.0 / targetCellCount) {
                columns = 1;
                rows = targetCellCount;
                return;
            }

            columns = Math.Max(1, (int)Math.Round(Math.Sqrt(targetCellCount * aspect)));
            columns = Math.Min(columns, targetCellCount);
            rows = Math.Max(1, (int)Math.Round((double)targetCellCount / columns));
        }

        private void GetCellRange(
            in AabbF bounds,
            out int minColumn,
            out int minRow,
            out int maxColumn,
            out int maxRow) {
            minColumn = MapToCell(bounds.MinX, this._outlineBounds.MinX, this._outlineBounds.MaxX, this._width, this.ColumnCount);
            maxColumn = MapToCell(bounds.MaxX, this._outlineBounds.MinX, this._outlineBounds.MaxX, this._width, this.ColumnCount);
            minRow = MapToCell(bounds.MinY, this._outlineBounds.MinY, this._outlineBounds.MaxY, this._height, this.RowCount);
            maxRow = MapToCell(bounds.MaxY, this._outlineBounds.MinY, this._outlineBounds.MaxY, this._height, this.RowCount);
        }

        private static int MapToCell(
            float value,
            float minimum,
            float maximum,
            double extent,
            int cellCount) {
            if (cellCount == 1 || value <= minimum) {
                return 0;
            }

            if (value >= maximum) {
                return cellCount - 1;
            }

            int index = (int)(((double)value - minimum) / extent * cellCount);
            if (index < 0) {
                return 0;
            }

            return index >= cellCount ? cellCount - 1 : index;
        }
    }
}
