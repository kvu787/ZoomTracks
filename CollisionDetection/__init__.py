"""Public API for the rectangle-versus-outline collision prototypes."""

from .rectangle_segments import (
    ALGORITHM_TYPES,
    BVHIndex,
    CoherentBlockIndex,
    CoherentHierarchyIndex,
    GridQueryScratch,
    LinearScanIndex,
    OrientedRectangle,
    PreparedSegments,
    Segment,
    UniformGridIndex,
    prepare_segments,
    segment_intersects_rectangle,
)

__all__ = [
    "ALGORITHM_TYPES",
    "BVHIndex",
    "CoherentBlockIndex",
    "CoherentHierarchyIndex",
    "GridQueryScratch",
    "LinearScanIndex",
    "OrientedRectangle",
    "PreparedSegments",
    "Segment",
    "UniformGridIndex",
    "prepare_segments",
    "segment_intersects_rectangle",
]
