"""Public API for the rectangle-versus-outline collision prototypes."""

from .rectangle_segments import (
    ALGORITHM_TYPES,
    BVHIndex,
    CoherentBlockIndex,
    CoherentHierarchyIndex,
    GridQueryScratch,
    LinearScanIndex,
    OrientedRectangle,
    OutlineLoop,
    Point,
    PreparedOutlines,
    Segment,
    SpatialChainBVHIndex,
    UniformGridIndex,
    prepare_outlines,
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
    "OutlineLoop",
    "Point",
    "PreparedOutlines",
    "Segment",
    "SpatialChainBVHIndex",
    "UniformGridIndex",
    "prepare_outlines",
    "segment_intersects_rectangle",
]
