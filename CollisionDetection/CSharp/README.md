# Exact outline/query-perimeter collision detection

This directory contains three reusable C# algorithms, an independent exact oracle,
correctness/property tests, a benchmark runner, and the raw final measurements. All
three production algorithms return the exact closed-segment answer required by the
prompt. None uses a geometric tolerance.

The practical default is `MortonBvhIndex`. It has linear retained storage, needs no
scale-dependent tuning, and was the least workload-sensitive index. Use
`LinearScanIndex` for small outlines or very few queries. `SparseUniformGridIndex`
is worthwhile when outline sampling is fairly uniform and query perimeters are
usually small and local; it was the fastest option on those measured workloads but
lost badly on large enclosing queries.

The reusable library targets .NET Standard 2.1 and C# 8. It has no Unity dependency,
so callers can copy `UnityEngine.Vector2.x/y` into `CoordinateXY`. This target was chosen
from the ZoomTracks Unity 6000.3.22f1 project metadata. Validation and timings were run
with the standalone .NET 10 JIT, not inside Unity Mono or IL2CPP; target-player profiling
is still required before treating the measured ranking as final.

## API reference

All public types are in the `ZoomTracks.CollisionDetection` namespace.

### `CoordinateXY`

```csharp
public readonly struct CoordinateXY
{
    public CoordinateXY(float x, float y);
    public float X { get; }
    public float Y { get; }
}
```

Represents a two-dimensional point. Both coordinates must be finite; the constructor
throws `ArgumentOutOfRangeException` for `NaN` or positive/negative infinity.

### `ConvexQuadrilateralOutline`

```csharp
public readonly struct ConvexQuadrilateralOutline
{
    public ConvexQuadrilateralOutline(
        CoordinateXY p0,
        CoordinateXY p1,
        CoordinateXY p2,
        CoordinateXY p3);
    public CoordinateXY P0 { get; }
    public CoordinateXY P1 { get; }
    public CoordinateXY P2 { get; }
    public CoordinateXY P3 { get; }
    public CoordinateXY GetVertex(int index);
}
```

Represents the four edges `P0-P1`, `P1-P2`, `P2-P3`, and `P3-P0`. Supply the vertices
in cyclic order around a strictly convex perimeter. `GetVertex` accepts indices 0
through 3 and throws `ArgumentOutOfRangeException` for any other index.

### `ICollisionDetector`

```csharp
public interface ICollisionDetector
{
    bool IsColliding(ConvexQuadrilateralOutline outline);
}
```

- `IsColliding` returns `true` when any query-perimeter edge touches or crosses any
  outline edge. Endpoint contact, tangency, and collinear overlap count as intersections.
  It returns `false` when there is no edge contact, including when one shape merely
  contains another.

An index copies both outlines during construction. Later changes to the input lists do
not affect it, and the completed index can be queried concurrently from multiple threads.

### Index implementations

Each constructor takes two closed outlines as vertex lists. Do not repeat the first
vertex at the end: the closing edge from the last vertex to the first is added
automatically. Each outline must contain at least three vertices, and consecutive
vertices must be distinct. A null outline throws `ArgumentNullException`; an outline
that violates either of the other rules throws `ArgumentException`.

#### `MortonBvhIndex`

```csharp
public MortonBvhIndex(
    IReadOnlyList<CoordinateXY> outline1,
    IReadOnlyList<CoordinateXY> outline2,
    int leafSize = MortonBvhIndex.DefaultLeafSize);

public int LeafSize { get; }
public int NodeCount { get; }
```

The general-purpose default. `leafSize` controls how many outline segments are stored
in each BVH leaf and must be from 1 through 64. Its default value is 8. `NodeCount`
reports the number of nodes in the built hierarchy.

#### `LinearScanIndex`

```csharp
public LinearScanIndex(
    IReadOnlyList<CoordinateXY> outline1,
    IReadOnlyList<CoordinateXY> outline2);
```

Checks outline segments directly. Use it for small outlines, few queries, or as a
simple baseline.

#### `SparseUniformGridIndex`

```csharp
public SparseUniformGridIndex(
    IReadOnlyList<CoordinateXY> outline1,
    IReadOnlyList<CoordinateXY> outline2,
    int targetSegmentsPerCell = SparseUniformGridIndex.DefaultTargetSegmentsPerCell,
    int maxAxisCells = SparseUniformGridIndex.DefaultMaxAxisCells,
    int maxCellsPerSegment = SparseUniformGridIndex.DefaultMaxCellsPerSegment);

public int TargetSegmentsPerCell { get; }
public int MaxAxisCells { get; }
public int MaxCellsPerSegment { get; }
public int CellsX { get; }
public int CellsY { get; }
public int OccupiedCellCount { get; }
public int CellReferenceCount { get; }
public int OverflowSegmentCount { get; }
```

Optimized for many small, local queries against fairly uniformly sampled outlines.
Constructor limits are:

- `targetSegmentsPerCell`: 1 through 1,024; default 4.
- `maxAxisCells`: 1 through 65,536; default 4,096.
- `maxCellsPerSegment`: 1 through 1,048,576; default 64.

An out-of-range setting throws `ArgumentOutOfRangeException`. The remaining properties
describe the built grid and can be used when profiling its effectiveness. In particular,
a high `OverflowSegmentCount` means more segments must be checked outside the grid.

### `ExactSegmentPredicates`

```csharp
public static bool Intersects(
    CoordinateXY a, CoordinateXY b, CoordinateXY c, CoordinateXY d);

public static int OrientationSign(
    CoordinateXY a, CoordinateXY b, CoordinateXY c);
```

- `Intersects` tests the closed segments `a-b` and `c-d`, using the same exact policy
  as the indexes.
- `OrientationSign` returns `1` when `a`, `b`, `c` make a counterclockwise turn, `-1`
  for a clockwise turn, and `0` when they are collinear.

### Base class

`OutlineIndexBase` is the public abstract base of the three indexes. Most callers should
program against `ICollisionDetector` and construct one of the implementations
above.

## Contents and use

- `src/ZoomTracks.CollisionDetection`: production library.
- `runner/CollisionDetection.Runner`: package-free test and benchmark executable.
- `artifacts/benchmark-results.csv`: raw final benchmark medians, MADs, allocations,
  hashes, and index parameters.
- `artifacts/benchmark-environment.txt`: final machine/runtime/source manifest.
- `artifacts/correctness-results.txt`: final correctness-run summary.

Typical use:

```csharp
using ZoomTracks.CollisionDetection;

CoordinateXY[] o1 = GetOuterOutline();
CoordinateXY[] o2 = GetInnerOutline();

ICollisionDetector detector = new MortonBvhIndex(o1, o2);

var outline = new ConvexQuadrilateralOutline(p0, p1, p2, p3);
bool anyEdgeContact = detector.IsColliding(outline);
```

`p0..p3` are used exactly as supplied, in cyclic order. No ideal rectangle is
reconstructed. The indexes copy the outline segments during preprocessing, so later
mutation of the input lists cannot change the index.

Run the verification tools from this directory:

```powershell
dotnet build .\runner\CollisionDetection.Runner\CollisionDetection.Runner.csproj -c Release --ignore-failed-sources
dotnet run --project .\runner\CollisionDetection.Runner\CollisionDetection.Runner.csproj -c Release --no-build -- test
dotnet run --project .\runner\CollisionDetection.Runner\CollisionDetection.Runner.csproj -c Release --no-build -- bench
```

The library checks finite point construction, `n >= 3`, and positive outline-edge
length. It deliberately trusts the remaining input contract: simple/nested outlines
and a cyclic, strictly convex query perimeter. Those properties do not need to be used
by any algorithm; all `N = n1 + n2` outline edges are indexed as independent closed
segments.

## Shared exact numerical policy

Every broad phase ultimately calls `ExactSegmentPredicates.Intersects`. It first uses
inclusive float AABBs. `min`, `max`, and comparisons on finite input floats are exact
order operations, so this cannot reject endpoint contact, tangency, or collinear
overlap.

Each orientation sign then uses two stages:

1. Each binary32 coordinate converts exactly to binary64. A Shewchuk `orient2d`
   first-stage error bound,
   `3.3306690738754716e-16 * determinantSum`, certifies ordinary determinant signs.
   The float input range guarantees that the binary64 differences and products cannot
   overflow or underflow.
2. If the sign is not certified, each float is decoded bit-for-bit as the integer
   `Q = value * 2^149`. For a subnormal, `Q` is its signed fraction field. For a normal
   with raw exponent `E`, it is the signed
   `((1 << 23) | fraction) << (E - 1)`. The determinant is evaluated with
   `System.Numerics.BigInteger` and only its sign is used.

A scaled coordinate is at most 277 bits, a difference at most 278 bits, and determinant
products at most about 556 bits (the subtraction can need one more bit). These sizes
are bounded by the input format, so exact-predicate work is constant with respect to
`N`, although it is materially more expensive than the binary64 fast path.

This is an exact filter, not an epsilon test: uncertainty causes exact evaluation rather
than a guessed sign. The standard closed-segment orientation/on-box test then includes
proper crossings, endpoint contact, vertex tangency, and all collinear overlaps.
Containment without edge contact naturally returns false.

The certification assumes ordinary IEEE binary64 round-to-nearest evaluation without
unsafe reassociation, which is the normal managed .NET/Unity mode. Do not move it into
an unverified Burst/fast-math path. `BigInteger` also makes this implementation unsuitable
for Burst jobs as written. An exact fixed-limb or floating-expansion fallback would be
the appropriate follow-up if Burst compatibility or zero GC on degeneracies is required.

No approximate production variant was included. A world-space epsilon would have both
false-positive and false-negative regimes across the allowed binary32 exponent range,
while the filtered exact predicate makes ordinary cases cheap. Broad-phase AABBs and
grid buckets can admit false *candidates*, but the returned result has neither false
positives nor false negatives under the stated floating-point execution assumptions.

## Algorithms

Let:

- `N = n1 + n2`, also the number of indexed outline segments;
- `K` be exact segment/query-edge candidate tests;
- `V` be visited BVH nodes;
- `H` be grid overflow segments;
- `S` be total grid segment references;
- `G` be occupied grid cells;
- `B` be query cells visited; and
- `J` be bucket references read, including duplicates removed by stamps.

The four query edges are a constant factor. Big-integer bit complexity can be written
with multiplication cost `M(278)`, but binary32 bounds it independently of `N`.

### 1. `LinearScanIndex`

How it works: preprocessing flattens both loops into `N` immutable segments and stores
each endpoint AABB. A query globally rejects a disjoint perimeter AABB, then scans all
segments for every query edge whose AABB overlaps the combined outline bounds. It exits
on the first exact contact.

Numerical policy and assumptions: it uses only the shared inclusive AABB and exact
segment predicate. It assumes only valid segments and cyclic query edges; polygon
simplicity and nesting are irrelevant. There is no tolerance.

Objective: minimum implementation complexity and preprocessing latency, plus predictable
performance on workloads that defeat spatial indexes.

- Preprocessing time: `Theta(N)` in this implementation because it validates and copies
  the segments. A separate zero-copy view could be `Theta(1)`, but was not implemented.
- Per-query time: `Theta(N)` worst case; an overall-AABB miss is `Theta(1)` and a hit can
  exit early.
- Storage: `Theta(N)` for copied endpoints and AABBs.

Preferred workload: small `N`, a small number of queries, or broad/adversarial queries
where most index nodes or cells would be visited anyway. The trade-off is that it cannot
exploit the intended repeated-query workload on local queries.

### 2. `MortonBvhIndex` — practical default

How it works: segment-AABB centroids are computed in binary64, normalized, quantized to
16 bits per axis, and Morton-sorted. Adjacent entries become balanced leaves (eight
segments by default), and parent AABBs are unioned bottom-up. Each of the four supplied
query edges traverses the flat balanced hierarchy and exact-tests segments in overlapping
leaves.

Numerical policy and assumptions: centroid arithmetic, quantization, and sort order can
only change tree quality. Stored node bounds are inclusive unions of original float
bounds, so they cannot remove a real contact. Leaves use the shared exact predicate.
Duplicate Morton codes are broken deterministically by original segment index. There is
no geometric tolerance.

Objective: strong, workload-insensitive throughput for many queries while retaining
linear storage and avoiding grid-size tuning.

- Preprocessing time: `Theta(N log N)` for the Morton sort; quantization and bottom-up
  tree construction are `Theta(N)`.
- Per-query time: `Theta(V + K)` and `Theta(N)` in the worst case. A good low-overlap
  hierarchy often behaves like `O(log N + K)`, but that is not guaranteed.
- Storage: `Theta(N)` retained nodes/segments and `Theta(log N)` traversal call stack.

Preferred workload: the requested immutable-outline/many-query case, especially when
edge density, edge length, and query sizes are mixed or not yet known. Its sort costs
asymptotically more than either linear-build alternative, although constants made the
measured grid build slightly slower at `N >= 1,024`. Large diagonal edge AABBs can still
visit many nodes.

### 3. `SparseUniformGridIndex`

How it works: the combined outline bounds are divided into a sparse grid. Default cell
size is four times the mean outline-edge length, capped at 4,096 cells per axis. Each
segment is inserted into all cells covered by its AABB. If that would exceed 64 cells,
the segment goes into an overflow list instead. A query enumerates each edge-AABB cell
range, deduplicates segment IDs with thread-local generation stamps, and exact-tests the
candidates. A query edge spanning more than `N` cells falls back to the direct scan.

Cell coordinates use binary64 only for partitioning. For a fixed built index, the
float-to-cell function is the same nondecreasing function for build and query. If two
closed one-dimensional AABB intervals overlap, monotonicity preserves
`cell(minA) <= cell(maxB)` and `cell(minB) <= cell(maxA)`, so their inclusive cell ranges
overlap. Applying this independently to X and Y proves that intersecting segment AABBs
share a bucket. Rounding can alter performance, never candidate completeness. The final
predicate is exact, and there is no geometric tolerance.

The packed `(x,y)` dictionary key uses a 64-bit avalanche comparer. This avoids the
structured `x XOR y` collisions that the default `long.GetHashCode()` would create for
diagonal cells.

Objective: lowest local-query latency and near-linear preprocessing when segments have
fairly uniform, local AABBs.

- Preprocessing time: expected `Theta(N + S)` hash-table work. With fixed per-segment
  cap `C`, `S <= C(N-H)`, so this is expected `Theta(N)`. Hash tables retain a theoretical
  collision worst case beyond that bound.
- Per-query time: expected `Theta(B + J + H + K)`; the broad-query fallback is
  `Theta(N)`. With fixed caps, worst geometric work is linear, apart from theoretical
  hash collision behavior.
- Index storage: `Theta(N + S + G)`, hence `Theta(N)` for a fixed insertion cap.
- Query scratch: one persistent `int[Nmax]` stamp array per querying thread, or
  `Theta(T * Nmax)` across `T` worker threads. Its first qualifying query allocates about
  `4N` bytes; subsequent nondegenerate queries reuse it.

Preferred workload: many small query perimeters near uniformly sampled outline edges.
Trade-offs: mixed edge scales can put many segments in `H`, which every local query must
scan; large rotated queries can cover many cells and trigger the scan fallback; sparse
dictionary storage has larger constants than arrays. The index remains safe for
concurrent calls because stamps are thread-local.

## Correctness testing

The final Release run reported:

```text
CORRECTNESS PASS assertions=1792702 orientations=250001 segment_pairs=1507988 outline_queries=34677 generator_rejections=0 elapsed_s=2.369
```

The test-only reference implementation decodes coordinates to the same mathematical
`2^-149` integer lattice but shares no production broad-phase helpers. It always uses
`BigInteger`. Its orientation-based closed-segment test is cross-checked against a
second exact parametric cross-product solver with different control flow.

Coverage includes:

- all ordered pairs of the 1,176 nonzero undirected segments on the integer grid
  `[-3,3]^2` (1,382,976 pairs);
- 250,000 raw-bit-weighted random and constructed-near-collinear orientations;
- 125,000 random finite-float segment pairs, with symmetry checks;
- proper crossings; endpoint/endpoint and endpoint/interior contact; tangency;
  partial/full/point collinear overlap; one-ULP separated parallel edges;
- signed zero, subnormals, mixed exponents, maximum finite
  values, and a determinant where a naive binary64 computation cancels to zero;
- query containment inside `O2`, in the annulus, and around both outlines with no edge
  contact;
- clockwise/counterclockwise and cyclic-order transformations, redundant collinear
  outline vertices, `n1 = n2 = 3`, closing edges, and exact contact fixtures;
- 5,000 randomly rounded rotated rectangles over ten unequal-size wobbly outline pairs,
  reverse-order replay, and forced-contact queries;
- BVH leaf sizes 1, 8, and 64; forced grid overflow; extreme grid bounds; and 512
  concurrent queries per algorithm. The verified enclosing benchmark corpus also
  exercises the grid's broad-query scan behavior.

All exact comparisons had zero mismatches. Generated valid rectangles had zero rejected
cases in the final run. Invalid NaN/infinite, self-intersecting, or nonconvex geometry is
outside the requested contract and was not treated as a geometry-validation project.

The randomized tests use fixed seeds `731991`, `918273`, and `20260823`. A limitation is
that the oracle and production code use the same documented float-bit decoding identity;
manual signed-zero/subnormal/maximum/mixed-exponent fixtures reduce, but do not entirely
remove, that shared-concept risk.

## Performance methodology and environment

Final results are from source hash
`c285544bb82ddfc49107958f03851ed00724237d123ff950e9b6fb3be7691ad6`.
The machine was an Intel Core Ultra 9 275HX, Windows 10.0.26200, x64 .NET 10.0.11,
24 visible logical processors, workstation GC in Interactive mode. Process priority and
affinity were left at their defaults; power mode and background load were uncontrolled.
`Stopwatch.Frequency` was 10 MHz. No debugger was attached.

For each `N`, `n1 = n2 = N/2`. Both outlines were immutable, smooth wobbly ellipses; edge
lengths for all three sizes were within the typical `0.1..1000` guidance. Query arrays
were materialized before timing using seed `31415926` plus dataset-specific offsets:

- `boundary-50pct`: half the queries share an exact outline edge (guaranteed contact and
  exact-fallback stress); half are nearby small misses.
- `annulus-miss`: small rotated queries between `O1` and `O2`; global bounds overlap but
  the perimeters do not.
- `enclosing-miss`: large rotated rectangles contain both outlines without perimeter
  contact, stressing broad phases.
- `far-miss`: disjoint overall AABBs, measuring the common constant-time reject.

There were 2,048 fixed queries per workload for `N=64` and `1,024`, and 768 for
`N=16,384`. Every benchmark query was labeled per-query with the exact linear
implementation. The independent `BigInteger` oracle checked every query at `N=64` and
`1,024`, plus 16 evenly spaced queries per large workload. All implementations were
verified per-query before timing; timed repetitions used a position-sensitive checksum.

Queries were warmed, then measured in 11 batches targeting about 0.20 seconds each.
Tables report median and median absolute deviation (MAD), not estimates. Algorithm order
was rotated between workloads. Preprocessing was separately batched to roughly 20 ms,
subject to a 64 MiB allocation cap per sample. Query generation, oracle work, hashing,
and correctness comparisons were outside timed regions.

Retained byte counts are intentionally not inferred from noisy `GC.GetTotalMemory`
deltas; use the structural storage bounds above. `build_allocated_bytes` and steady-state
per-query allocations come from thread allocation counters. The grid's per-thread stamp
array is reported separately because it is created on first qualifying query, not during
index construction.

### Measured preprocessing

Median milliseconds; parentheses contain allocated KiB per constructed index.

| N | Linear | Morton BVH | Sparse grid |
|---:|---:|---:|---:|
| 64 | 0.00256 (2.1) | 0.01895 (5.7) | 0.01377 (7.7) |
| 1,024 | 0.00872 (32.1) | 0.05720 (88.2) | 0.05753 (109.7) |
| 16,384 | 0.14014 (512.1) | 0.87875 (1,408.2) | 1.07553 (1,850.1) |

At `N=16,384`, the grid additionally needs a 65,536-byte stamp array per querying
thread after its first qualifying query.

### Measured query time

Median microseconds per query; the CSV contains MADs and full precision.

| N | Workload | Linear | Morton BVH | Sparse grid |
|---:|---|---:|---:|---:|
| 64 | boundary-50pct | 0.467 | 0.449 | 0.449 |
| 64 | annulus-miss | 0.351 | 0.248 | 0.158 |
| 64 | enclosing-miss | 0.314 | 0.375 | 0.426 |
| 64 | far-miss | 0.012 | 0.010 | 0.011 |
| 1,024 | boundary-50pct | 2.917 | 0.586 | 0.434 |
| 1,024 | annulus-miss | 4.607 | 0.422 | 0.102 |
| 1,024 | enclosing-miss | 5.210 | 3.668 | 14.956 |
| 1,024 | far-miss | 0.012 | 0.010 | 0.011 |
| 16,384 | boundary-50pct | 40.169 | 0.720 | 0.440 |
| 16,384 | annulus-miss | 70.563 | 0.552 | 0.165 |
| 16,384 | enclosing-miss | 71.818 | 54.128 | 83.819 |
| 16,384 | far-miss | 0.012 | 0.010 | 0.010 |

Ordinary miss workloads allocated 0 bytes per steady-state query for every algorithm.
The deliberately collinear 50%-contact workload triggered exact `BigInteger` fallback:
at `N=16,384`, measured allocations were 496 bytes/query (linear), 549 (BVH), and 570
(grid). The grid's one-time/per-thread stamp allocation was consumed during warmup and
is reported separately above.

The grid was 1.64x faster than the BVH on the large local contact/miss mix and 3.35x
faster on the large annulus misses. The BVH was 1.55x faster than the grid on the large
enclosing adversary. This inversion is why the BVH is the general default and the grid
is a workload-specific option.

### Derived amortization points

For an indexed method that measured faster per query than linear, the runner reports

```text
Q* = (indexed preprocessing - linear preprocessing)
     / (linear query time - indexed query time)
```

These are derived from measured medians, not separately timed end-to-end experiments:

| N | Workload | BVH Q* | Grid Q* |
|---:|---|---:|---:|
| 64 | boundary-50pct | 899 | 602 |
| 64 | annulus-miss | 159 | 58 |
| 1,024 | boundary-50pct | 21 | 20 |
| 1,024 | annulus-miss | 12 | 11 |
| 1,024 | enclosing-miss | 31 | never (slower query) |
| 16,384 | boundary-50pct | 19 | 24 |
| 16,384 | annulus-miss | 11 | 13 |
| 16,384 | enclosing-miss | 42 | never (slower query) |

Far-miss timings differ by less than one nanosecond and all use the same global AABB
reject; their very large computed break-even counts are not practically meaningful.

## Recommendation and limits

Start with `MortonBvhIndex` for ZoomTracks. It remains fast for local queries, handles
the enclosing-query adversary better, has no spatial-resolution knobs, and typically
amortized its measured build cost within a few dozen nontrivial queries for `N >= 1,024`.

Choose `SparseUniformGridIndex` after profiling demonstrates mostly small/local queries,
roughly uniform edge lengths, and a small overflow count. Its measured local advantage
is real, but inspect `OverflowSegmentCount`, `CellReferenceCount`, and the grid dimensions;
large `H` or frequent scan fallback removes that advantage. Account for one `4N`-byte
stamp array on every worker thread that queries it.

Choose `LinearScanIndex` for few queries, small outlines, or a known stream of overall-
AABB misses. It is also the simplest operational fallback when a spatial index is
consistently defeated.

The benchmark deliberately covers several query shapes but only one smooth, balanced
outline family. It does not model a real ZoomTracks frame trace, unequal benchmark
`n1:n2`, highly clustered edges, many grid-overflow segments, Mono, IL2CPP, Burst, or
mobile/console hardware. The correctness suite covers unequal counts and forced overflow,
but performance conclusions for those cases require new measurements. Feed captured game
queries into the same runner before freezing cutoffs or grid parameters.

If collinear/tangent contacts are common enough for the measured `BigInteger` allocation
to matter, retain the same binary64 filter and replace only its fallback with a reviewed
fixed-size floating expansion or custom fixed-limb integer determinant. That can preserve
the exact predicate while removing degeneracy-path GC; a tolerance-based shortcut is not
required.
