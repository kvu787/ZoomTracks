# ZoomTracks ordered-outline collision algorithms

This folder contains standard-library Python implementations and reproducible
comparisons for the current collision contract:

> Given the vehicle's current oriented ground-plane rectangle and two immutable
> authored, post-evaluation outline loops, return `True` if any outline segment
> intersects the rectangle.
>
> Each outline is represented by an ordered vertex sequence `v[0..n-1]`, with
> `n >= 3`, where segment `i` is `(v[i], v[(i+1) mod n])`. Each outline forms one
> connected, closed loop. All vertices are finite, expressed in the same
> two-dimensional track-local coordinate system, and consecutive vertices are
> distinct. The outlines remain unchanged for the lifetime of the loaded track.
>
> Touching counts as intersection. There is no swept test or tunneling
> prevention.

Use Unity ground-plane `(x, z)` as Python `(x, y)`. These algorithms consume the
authored post-evaluation vertices directly. They do not resample or increase the
outline resolution. The vehicle rectangle and both loops must already be in the
same track-local coordinate system.

## Recommendation

Use `CoherentHierarchyIndex` by default. The ordered-loop guarantee makes its
input assumption unconditional: it builds a separate four-way, stackless AABB
hierarchy over contiguous ranges of each loop. It has linear build cost, no
spatial tuning, and consistently provides the best build/query balance on the
track-shaped benchmark.

Use `SpatialChainBVHIndex` when real tracks contain folds, hairpins, or spatially
close nonadjacent portions that make contiguous-range bounds loose. It spatially
organizes small contiguous microchains instead of individual edges, approaching
the distribution robustness of a full spatial BVH at a fraction of its build
cost.

`UniformGridIndex` and the full per-edge `BVHIndex` remain useful experimental
comparisons for unusual real-track distributions. `CoherentBlockIndex` is a
minimal broad phase, and `LinearScanIndex` is the tiny-input/reference option.

See [BENCHMARK_RESULTS.md](BENCHMARK_RESULTS.md) for reproducible measurements.
Python timings are not predictions of Unity/C# timings; crossover behavior,
candidate counts, memory layout, and sensitivity to input shape are the useful
results.

## Input preparation and validation

`PreparedOutlines` is the only public input accepted by the indices. It:

- snapshots the outer and inner vertex sequences into immutable tuples;
- requires at least three vertices in each loop;
- converts coordinates to finite Python floats;
- rejects equal cyclic neighbors, including an explicitly repeated closing
  vertex;
- derives every edge, including each last-to-first edge, exactly once;
- retains the two loop ranges so broad phases never combine the loop join; and
- computes shared edge data and complete bounds once.

The contract does not require a winding direction, a canonical starting vertex,
simple polygons, nesting, or unique nonadjacent vertices, so preparation does
not invent those restrictions.

Each derived edge record stores its midpoint, half-vector, AABB, original
endpoints, and a coordinate-ULP bound. Original endpoints are used only for
numerically ambiguous cases and extreme-coordinate fallbacks. A production C#
port should use packed structures or structure-of-arrays rather than copying
CPython's tuple layout.

```python
from CollisionDetection import (
    CoherentHierarchyIndex,
    OrientedRectangle,
    PreparedOutlines,
)

outer_vertices = (
    (0.0, 0.0),
    (20.0, 0.0),
    (20.0, 20.0),
    (0.0, 20.0),
)
inner_vertices = (
    (6.0, 6.0),
    (14.0, 6.0),
    (14.0, 14.0),
    (6.0, 14.0),
)

outlines = PreparedOutlines(outer_vertices, inner_vertices)
detector = CoherentHierarchyIndex(outlines)

vehicle = OrientedRectangle.from_angle(
    center_x=19.0,
    center_y=10.0,       # Unity world Z
    half_x=2.2,
    half_y=1.0,
    angle_radians=0.0,
)

collided = detector.intersects(vehicle)
```

Do not repeat the first vertex at the end of a loop; closure is implicit.
Preparation and index settings are read-only. Rebuild the preparation/index if
the source outline or a tuning value changes.

`segment_intersects_rectangle` and the exported four-float `Segment` alias are
low-level single-edge predicate utilities. Public indices intentionally do not
accept arbitrary flattened segment lists because those would bypass the loop
contract.

## Shared narrow phase and numerical policy

For edge midpoint `m`, half-vector `d`, rectangle center `c`, unit axes `u/v`,
and half-extents `ex/ey`, the separating-axis test projects into the rectangle
frame:

```text
mx = dot(m - c, u)       my = dot(m - c, v)
dx = dot(d, u)           dy = dot(d, v)
```

The three sufficient conditions for intersection are:

```text
abs(mx*dy - my*dx) <= ex*abs(dy) + ey*abs(dx)  # segment normal
abs(mx)            <= ex + abs(dx)              # rectangle local X
abs(my)            <= ey + abs(dy)              # rectangle local Y
```

The fast path tests the segment-normal axis first because it rejects most
grazing misses. Clear separation returns immediately. Separations close enough
to floating-point uncertainty use endpoint-space Liang-Barsky clipping; scaled
SAT handles overflowing or underflowing intermediate products. Broad-phase
bounds are rounded outward so they cannot discard a narrow-phase contact.

As a result, geometry separated by only floating-point uncertainty may count as
touching. This is not a fixed collision skin. The optional `padding` argument is
the explicit geometric control and expands both rectangle half-extents by a
world-space distance. Keeping track-local coordinates near the origin improves
both precision and fast-path frequency.

## Algorithm 1: AABB-filtered linear scan

### Description

`LinearScanIndex` first rejects a rectangle outside the combined outline AABB.
Otherwise it scans every derived edge, applies an edge-AABB versus conservative
rectangle-AABB filter, and runs the shared narrow phase only for survivors. It
returns on the first hit.

### Performance characteristics

- Shared preparation: `O(E)` time and memory for `E` derived edges.
- Additional build: `O(1)` time and memory.
- Query: `O(1)` for whole-outline rejection; `O(E)` worst case.
- Working memory: `O(1)`.

### Tradeoffs

- Lowest construction cost and smallest index.
- Good for tiny loops, far-away rectangles, or very early hits.
- In-bounds misses still inspect every edge AABB and scale linearly.
- Provides the simplest differential reference for the indexed algorithms.

## Algorithm 2: coherent fixed-size blocks

### Description

`CoherentBlockIndex` groups consecutive edges into blocks, restarting at every
loop boundary. Each block has one AABB. A query scans all block AABBs and applies
the fused edge-AABB/SAT loop only inside overlapping blocks.

### Performance characteristics

For block size `B` and `C` overlapping blocks:

- Build: `O(E)`.
- Extra memory: `O(E/B)`.
- Typical ordered query: `O(E/B + C*B)`.
- Worst-case query: `O(E)`.
- Default `B`: 16; measure 8, 16, and 32 on real output.

### Tradeoffs

- Very inexpensive build and straightforward Unity port.
- Exploits loop order without a tree or per-query scratch.
- Still visits every block, so the hierarchy overtakes it quickly as `E` grows.
- Primarily useful as a minimal broad phase and benchmark stepping stone.

## Algorithm 3: four-way stackless ordered hierarchy

### Description

`CoherentHierarchyIndex` recursively partitions each loop into contiguous edge
ranges. Its default four-way nodes reduce tree depth and node count compared with
a binary tree. Nodes are stored in preorder with an escape index: rejecting a
node jumps directly past its complete subtree without allocating a traversal
stack. Leaves use the fused contiguous-range narrow phase.

The two loop roots are derived automatically from `PreparedOutlines`; callers
cannot provide incorrect group sizes.

### Performance characteristics

For leaf size `L`:

- Build: `O(E)`.
- Extra memory: `O(E/L)` nodes.
- Expected coherent-loop query: `O(log E + K)`, where `K` is work in visited
  leaves.
- Worst-case query: `O(E)` when large contiguous ranges have heavily overlapping
  bounds.
- Defaults: `L=8`, branching factor 4.

### Tradeoffs

- Best default under the guaranteed ordered-loop contract.
- Linear, low-cost build; no cell size, sorting, candidate set, or scratch state.
- Excellent cache behavior because every leaf scans a contiguous edge range.
- A highly folded loop can put spatially distant/nearby portions in ranges whose
  bounds overlap, reducing pruning; use the spatial-chain BVH in that case.

## Algorithm 4: spatial microchain BVH

### Description

`SpatialChainBVHIndex` first partitions each loop into contiguous microchains
(`B=8` edges by default), never crossing a loop boundary. It computes one AABB
per chain, then builds a spatial median-split BVH over chain AABBs from both
loops. Each spatial leaf still scans its original contiguous edge range.

This hybrid retains ordered storage while allowing nonadjacent portions of a
folded track to become spatial neighbors in the index. Every edge occurs in one
chain, so no candidate deduplication is needed.

### Performance characteristics

For `C = ceil(E_outer/B) + ceil(E_inner/B)` chains:

- Chain construction: `O(E)`.
- Current Python median-tree build: `O(C log^2 C)` worst case because each level
  uses optimized built-in sorting; an `nth_element`/LBVH port can make it
  `O(C log C)`.
- Extra memory: `O(C)`.
- Expected query: `O(log C + K*B)` for `K` visited chains.
- Worst-case query: `O(E)`.
- Default `B`: 8; benchmark 4, 8, and 16.

### Tradeoffs

- Much cheaper to build than spatially partitioning every edge.
- More robust than the ordered hierarchy for folded/nonuniform layouts.
- Slightly more build work and tuning than the ordered hierarchy.
- A chain containing one very long edge can have a loose bound; smaller chains
  or adaptive chain construction may help real data with extreme edge lengths.

## Algorithm 5: sparse segment uniform grid

### Description

`UniformGridIndex` inserts derived edges into a sparse uniform grid using a
conservative DDA traversal. Queries enumerate the cells covered by the
rectangle's conservative world AABB and deduplicate edge IDs with reusable
generation stamps. Excessively long edges enter an always-tested overflow list;
very large query footprints fall back to the linear scan.

The automatic cell heuristic combines sampled median edge length with outline
AABB density. `new_scratch()` supplies independent reusable stamp storage for
concurrent callers.

### Performance characteristics

For `R` edge-to-cell references, `Q` queried cells, `H` bucket entries, and `G`
overflow edges:

- Build: `O(E + R)`.
- Extra memory: `O(R + occupied_cells + E)`.
- Expected query: `O(G + Q + H)` plus surviving narrow phases.
- Worst-case query: `O(E)` for clustered input, a large overflow list, or linear
  fallback.

### Tradeoffs

- Query time can be close to constant when local density and cell size align.
- Does not depend on contiguous-range bounds, which may help severely folded
  tracks.
- Cell size is data/query dependent; duplicate stamps and buckets add memory.
- Long edges and large rectangles require explicit caps/fallback behavior.
- The built-in scratch is sequential; concurrent callers need separate scratch.

## Algorithm 6: full query-aware edge SAH BVH

### Description

`BVHIndex` builds a binned surface-area-heuristic tree over every edge AABB. Its
split score expands node bounds by an expected vehicle footprint, making the
heuristic meaningful for zero-area line bounds. Query traversal uses inexpensive
world-AABB tests; leaves apply per-edge filtering and the shared predicate.

### Performance characteristics

- Expected build: `O(E log E)` with fixed-bin scans; repeated sorted fallbacks
  can reach `O(E log^2 E)` in the current Python implementation.
- Extra memory: `O(E)`.
- Expected query: `O(log E + K)`.
- Worst-case query: `O(E)` for strongly overlapping edge bounds.
- Query working memory: expected `O(log E)`, worst-case `O(E)`, for its traversal
  stack.
- Defaults: 8-edge leaves and 12 bins.
- Expected query width/height default to `5.0 x 5.0`; these influence tree
  quality, never correctness.

### Tradeoffs

- Most distribution-robust index in the comparison and insensitive to authored
  starting vertex or winding.
- Expensive Python build and many nodes compared with the microchain BVH.
- Current measurements show little query benefit over spatial microchains for
  track-shaped data; retain it as a comparison and pathological-data fallback.

## Choosing an implementation

| Situation | Recommended implementation |
|---|---|
| Normal ordered TrackBuilder outlines | `CoherentHierarchyIndex` |
| Folded/hairpin layout with loose contiguous bounds | `SpatialChainBVHIndex` |
| Real data empirically favors spatial hashing | `UniformGridIndex` |
| Extreme distribution where microchains remain loose | `BVHIndex` |
| Minimal low-build broad phase | `CoherentBlockIndex` |
| Tiny input or correctness reference | `LinearScanIndex` |

Geometry and structural index configuration are static/read-only. Vehicle motion
changes only the query; outline motion or tuning changes require rebuilding. The
grid's built-in generation-stamp scratch is intentionally mutable query state.

## Validation and benchmarking

From the repository root:

```powershell
python -m unittest discover -s CollisionDetection -v
python -m CollisionDetection.benchmark
```

Tests cover:

- exact rational-lattice comparison for the primitive predicate;
- randomized rotated comparison against independent Liang-Barsky clipping;
- direct independent-oracle comparison across every index;
- implicit outer/inner closing edges and unequal loop sizes;
- cyclic rotation, reversal, and loop swapping;
- boundary-only semantics, touching, collinearity, and containment cases;
- immutable snapshots and all contracted validation failures;
- grid boundary/overflow and extreme finite-coordinate fallbacks; and
- deterministic lap, grazing near-miss, and far-miss workloads.

Useful tuning commands:

```powershell
# Compare hierarchy topology.
python -m CollisionDetection.benchmark --hierarchy-leaf-size 8 --hierarchy-branching-factor 4

# Compare microchain granularity.
python -m CollisionDetection.benchmark --chain-size 4
python -m CollisionDetection.benchmark --chain-size 16

# Compare grid sizes.
python -m CollisionDetection.benchmark --cell-size 1 --cell-size 2 --cell-size 4

# Stress spatially folded contiguous ranges (synthetic index stress, not a road).
python -m CollisionDetection.benchmark --vertices 4096 --workload folded --folds 20
```

`benchmark.py --help` lists total vertex counts, query counts, repeats, workloads,
and every tuning option. “Slowest mean” is the slowest complete-batch mean, not a
per-query percentile.

## Eventual Unity implementation

A Unity port should:

- export the immutable post-evaluation outer and inner vertex arrays, without a
  duplicated closing vertex or runtime resampling;
- project track-local `(x, z)` once and prepare/index the arrays at track load;
- derive the current oriented footprint from the vehicle `BoxCollider`, including
  collider center, transform, and scale;
- use packed contiguous arrays/structs and inline the leaf narrow-phase loop;
- preserve outward-rounded conservative broad-phase bounds;
- keep the adaptive numerical fallback for ambiguous contacts;
- provide per-worker scratch only for the grid implementation; and
- retain current-pose-only semantics without adding swept collision.

Python uses double-precision floats. If Unity uses 32-bit `float`, port the
boundary and translation regressions and select error bounds for binary32 rather
than copying the literal binary64 thresholds.
