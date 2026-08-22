# ZoomTracks outline collision algorithms

This folder is a self-contained, standard-library Python implementation and
comparison of five algorithms for the requested collision contract:

> Given the vehicle's current oriented ground-plane rectangle and the authored,
> post-evaluation inner and outer outline line segments, return `True` if any
> segment intersects the rectangle. Touching counts. There is no swept test and
> no tunneling prevention.

Use Unity world **X** as the first Python coordinate and world **Z** as the
second. The algorithms use the authored segments directly; they do not consume
resolution-increased outline geometry.

## Recommendation

Start with `CoherentHierarchyIndex` when the two outlines retain their authored
edge order. It exploits information TrackBuilder already provides, builds in
linear time, needs no spatial tuning, and was essentially tied for the fastest
queries in the included benchmark. Give it separate inner/outer group sizes so
no hierarchy node spans the join between loops.

Use `UniformGridIndex` when segment order is unavailable or when a sparse grid
is a more natural runtime representation. Use `BVHIndex` for unordered geometry
with strongly varying segment lengths/densities or spatially close, nonadjacent
sections. `CoherentBlockIndex` is the smallest useful broad phase, while
`LinearScanIndex` is the reference and small-input implementation.

See [BENCHMARK_RESULTS.md](BENCHMARK_RESULTS.md) for reproducible measurements.
Python timings are not predictions of final Unity/C# timings; scaling behavior,
candidate structure, build cost, and crossover points are the useful results.

## Collision predicate and numerical policy

Every broad phase ultimately uses one shared rectangle/segment predicate. A
segment's world-space midpoint `m` and half-vector `d` are projected into the
rectangle frame. With rectangle center `c`, axes `u/v`, and half-extents
`ex/ey`:

```text
mx = dot(m - c, u)       my = dot(m - c, v)
dx = dot(d, u)           dy = dot(d, v)
```

The mathematical separating-axis conditions are:

```text
abs(mx) <= ex + abs(dx)
abs(my) <= ey + abs(dy)
abs(mx*dy - my*dx) <= ex*abs(dy) + ey*abs(dx)
```

The first two axes are the rectangle normals; the third is the segment normal.
They are sufficient for these two convex shapes. The normal fast path is
constant-time, division-free, and has no parallel-line branch.

Floating-point contact needs a deliberate policy. Strict comparisons produced
false negatives for authored endpoint and collinear-edge contacts after ordinary
translation/rotation. This implementation therefore:

- gives SAT comparisons a 16-ULP arithmetic guard;
- rounds broad-phase bounds outward so they cannot prune a narrow-phase contact;
- retains original endpoints and uses scaled endpoint-space Liang-Barsky clipping
  when the prepared midpoint test rejects an ambiguous case; and
- uses normalized SAT when intermediate cross products would overflow.

Consequently, geometry separated by only floating-point uncertainty (normally a
few ULPs at the input coordinate scale) may count as touching. This is not a
fixed world-unit collision skin. `padding` is the separate, explicit geometric
control: it expands both rectangle half-extents by that many world units and
defaults to zero. Very large world coordinates have large ULPs, so keeping track
coordinates near a local origin remains good practice.

The resulting semantics include:

- crossing one or more edges;
- either endpoint inside;
- a segment wholly inside the rectangle;
- edge or corner contact;
- collinear edge overlap;
- zero-length segments treated as points; and
- zero-width/height rectangles with their natural line/point meaning.

## Common preparation and usage

`PreparedSegments` validates finite source coordinates once. Each immutable
record retains midpoint, half-vector, AABB, and original endpoints (12 Python
floats). All indices can share one prepared object without copying it.

```python
from rectangle_segments import (
    CoherentHierarchyIndex,
    OrientedRectangle,
    PreparedSegments,
)

outer_segments = [
    # (x0, z0, x1, z1), including the closing edge exactly once
    (0.0, 0.0, 10.0, 0.0),
    (10.0, 0.0, 10.0, 10.0),
]
inner_segments = [
    (2.0, 2.0, 8.0, 2.0),
    (8.0, 2.0, 8.0, 8.0),
]

prepared = PreparedSegments([*outer_segments, *inner_segments])
detector = CoherentHierarchyIndex(
    prepared,
    group_sizes=(len(outer_segments), len(inner_segments)),
)

vehicle = OrientedRectangle.from_angle(
    center_x=9.5,
    center_y=5.0,       # Unity world Z
    half_x=2.2,
    half_y=1.0,
    angle_radians=0.0,
)

collided = detector.intersects(vehicle)
```

For a closed outline, include the last-to-first edge exactly once. All index
configuration is constructor-only/read-only because changing structural values
requires rebuilding the index.

## Algorithm 1: AABB-filtered linear scan

`LinearScanIndex` rejects queries outside the complete outline bounds. Inside
those bounds it scans the records, applies a cheap segment-AABB versus vehicle-
AABB test, and runs the shared predicate only for survivors. It returns on the
first hit.

### Performance characteristics

- Common preparation: `O(N)` time and memory.
- Additional build: `O(1)` time and memory.
- Query: `O(1)` for whole-outline rejection; otherwise `O(N)` worst case.
- Working memory: `O(1)`.
- The AABB filter makes its constant much lower than a raw SAT-per-segment scan.

### Tradeoffs

- Smallest implementation and almost no index build cost.
- Good for small outlines, far-away queries, and early hits.
- Misses inside the overall track bounds still inspect every segment AABB, so
  cost grows linearly.
- Serves as the differential correctness reference for the indexed algorithms.

## Algorithm 2: coherent fixed-size blocks

`CoherentBlockIndex` groups consecutive authored segments (16 by default),
stores one AABB per block, then filters both blocks and individual records by the
vehicle's conservative world AABB. Authored outline order normally keeps block
bounds tight.

### Performance characteristics

- Build: `O(N)`.
- Extra memory: `O(N / B)` for block size `B`.
- Coherent-input query: approximately `O(N / B + C*B)`, for `C` overlapping
  blocks.
- Worst-case query: `O(N)`.
- No candidate container proportional to `N`; query bookkeeping is fixed-size.

### Tradeoffs

- Very low construction and memory cost with no spatial tuning.
- Much faster than a scan while remaining easy to port.
- Still visits every block for in-bounds queries, so scaling remains linear.
- Randomly shuffled input makes block bounds loose.
- Measure block sizes from 8 through 32 on real output.

## Algorithm 3: ordered coherent hierarchy

`CoherentHierarchyIndex` builds a balanced binary AABB tree over contiguous edge
ranges. Leaves hold eight segments by default; parent bounds are aggregated
bottom-up. No sorting or spatial heuristic is required. `group_sizes` creates a
separate root for each outline.

### Performance characteristics

- Build: `O(N)`.
- Extra memory: `O(N / L)` nodes for leaf size `L`.
- Expected coherent-input query: `O(log N + K)`, for relevant leaf candidates
  `K`.
- Worst-case query: `O(N)` if ordering has no spatial coherence or bounds overlap
  heavily.
- Each segment occurs in exactly one leaf; no duplicate suppression is needed.

### Tradeoffs

- Best overall fit for ordered TrackBuilder loops in the included benchmark.
- Linear, inexpensive build and no cell-size/SAH tuning.
- Depends on preserving outline order. Shuffling segments can destroy pruning.
- Passing separate group sizes matters when concatenated loop counts differ; a
  single tree can otherwise form loose ranges across the loop join.
- It is less distribution-robust than a spatially built BVH.

## Algorithm 4: sparse DDA uniform grid

`UniformGridIndex` inserts segments into conservatively owned cells along their
path using half-open, tolerant-corner DDA traversal. It does not fill a diagonal
segment's entire AABB. Lines exactly on a cell boundary are owned by the `floor`
side; inclusive query cell ranges always include that owner.

A query enumerates cells in the vehicle's conservative world AABB, deduplicates
segment IDs with reusable generation stamps, AABB-filters them, and runs the
shared predicate. Queries covering more than `max_query_cells` fall back to the
linear loop.

Segments that would cross more than `max_cells_per_segment` cells (4,096 by
default), or whose scaled grid span is not representable, go into a small
always-tested overflow list instead of consuming huge memory or hanging grid
construction.

The default cell heuristic is:

```text
max(2 * sampled-median segment length, sqrt(outline AABB area / N))
```

The deterministic median sample is capped at 1,024 records, preserving linear
build complexity.

### Performance characteristics

Let `R` be segment-to-cell references, `Q` queried cells, `H` bucket entries,
and `L` overflow-list segments:

- Build: `O(N + R)`.
- Extra memory: `O(R + occupied_cells + N)`; `N` includes generation stamps.
- Query: `O(L + Q + H)` before narrow-phase candidate costs.
- Expected fixed-footprint query is near-constant when local density is stable.
- Worst case: `O(N)` for clustered geometry, many overflow records, or fallback.

### Tradeoffs

- Very fast queries without depending on segment order.
- Sparse storage avoids allocating the track interior.
- Cell size matters: too small increases lookups/references; too large increases
  candidates.
- Long-segment overflow bounds memory, but every overflow segment is tested on
  every in-bounds query. Increase cell size if `L` is not small.
- The built-in stamp scratch is for sequential calls. Concurrent callers need
  one reusable object each from `new_scratch()`.

Measure cell sizes around the vehicle's short side and median authored segment
length (`0.5x`, `1x`, and `2x` are useful starting points).

## Algorithm 5: query-aware binned-SAH BVH

`BVHIndex` builds a spatial binary hierarchy over segment AABBs. It tests 12
centroid bins on both axes and minimizes this query-aware score:

```text
measure(bounds) =
    (bounds_width  + expected_query_width) *
    (bounds_height + expected_query_height)

split_score = left_count * measure(left) + right_count * measure(right)
```

The expected query footprint makes the score meaningful for zero-area segment
bounds. Pathological or unrepresentable centroid spans use balanced median
fallbacks. Query traversal intentionally uses conservative world-AABB tests:
in pure Python, tighter OBB/AABB node SAT cost more than the extra nodes it
removed. Leaves apply per-segment AABB filtering and the shared predicate.

### Performance characteristics

- Expected build: `O(N log N)` with fixed-bin scans.
- Extra memory: `O(N)`; each segment is stored once.
- Expected query: `O(log N + K)`.
- Worst-case query: `O(N)` for heavily overlapping bounds.
- Segment length does not multiply storage references.

### Tradeoffs

- Most robust choice for unordered, nonuniform geometry and varying lengths.
- No grid cell-size dependency or duplicate candidates.
- Much more expensive to build than the ordered hierarchy or grid in Python.
- Conservative world-AABB traversal can admit extra nodes for highly elongated,
  rotated rectangles.
- Leaves of 4-8 and 12-16 bins are sensible. Expected query dimensions should
  approximate the vehicle's typical world-AABB footprint.

## Choosing an implementation

| Situation | Recommended implementation |
|---|---|
| Ordered inner/outer TrackBuilder loops | `CoherentHierarchyIndex` |
| Unordered, roughly uniform outline segments | `UniformGridIndex` |
| Unordered/nonuniform or widely varying lengths | `BVHIndex` |
| Minimal broad phase over ordered input | `CoherentBlockIndex` |
| Tiny input or reference implementation | `LinearScanIndex` |
| Guaranteed query outside complete track bounds | Any; all reject in `O(1)` |

All indices are static. Rebuild after outline or tuning changes; vehicle motion
does not require rebuilding.

## Validation and benchmarking

From the repository root:

```powershell
python -m unittest discover -s CollisionDetection -v
python -m CollisionDetection.benchmark
```

The tests include:

- exhaustive rational lattice comparison against an exact `Fraction` oracle;
- 10,000 randomized rotated comparisons with independent Liang-Barsky clipping;
- randomized differential queries across all five algorithms and tunings;
- required edge/corner/collinear/containment and degenerate semantics;
- mixed-sign grid corners, rounding-sensitive cells, giant segments, overflow
  routing, subnormals, large coordinates, and extreme BVH centroids;
- multi-outline hierarchy groups and reusable grid scratch; and
- checks that the benchmark lap/near/far workloads keep their intended hits.

Useful tuning commands:

```powershell
# Compare several grid sizes in one run.
python -m CollisionDetection.benchmark --cell-size 1 --cell-size 2 --cell-size 4

# Compare index granularities.
python -m CollisionDetection.benchmark --block-size 8 --hierarchy-leaf-size 4 --bvh-leaf-size 4 --bvh-bin-count 16
```

`benchmark.py --help` lists segment counts, query counts, repeats, workloads,
grid overflow/fallback caps, and all tuning flags. “Slowest mean” in its output
is the slowest complete-batch mean, not per-query p95 latency.

## Eventual Unity implementation

A Unity port should preserve these details:

- derive the current world-space oriented footprint from the vehicle
  `BoxCollider`, including collider center and object scale;
- project Unity `(x, z)` only and ignore height;
- retain separate ordered inner/outer segment ranges when available;
- prepare/index static authored outlines once;
- use conservative broad phases and robust inclusive narrow-phase comparisons;
- expose any geometric skin explicitly rather than folding it into tolerances;
- give each worker/vehicle separate grid scratch if queries can overlap; and
- retain current-pose-only semantics—do not add swept motion.

Python uses double-precision floats. If the Unity port uses 32-bit `float`, port
the boundary/translation regression cases and choose a corresponding ULP policy;
do not copy the literal double-precision tolerance blindly.
