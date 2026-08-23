# Exact rectangle-perimeter versus outline-edge collision detection

## Result and recommendation

Three worthwhile exact algorithms were implemented and compared:

1. `LinearScanCollisionDetector`: minimum preprocessing and auxiliary memory.
2. `BvhCollisionDetector`: a balanced static AABB hierarchy with eight edges per
   leaf by default.
3. `UniformGridCollisionDetector`: a bounded-replication static grid with an
   overflow list and a broad-query linear fallback.

For the intended workload—many localized rectangles against immutable,
track-like outlines—the uniform grid is the practical default. In the measured
localized workloads it took 0.086 microseconds/query at `N=12,288`, versus 0.113
for the BVH and 24.871 for the scan. It also built faster and allocated fewer
constructor bytes than the BVH. Its measured preprocessing break-even against a
scan was 11 queries in that workload.

No index wins everywhere. A scan was fastest when every large-outline query hit
an early-ordered edge (0.095 microseconds/query), and all approaches degraded to
roughly 245–266 microseconds for a rectangle AABB enclosing all 12,288 edges
without perimeter contact. Conversely, the BVH won the deliberately multiscale
grid-overflow workload by 23x over the grid (0.092 versus 2.098
microseconds/query). This supports the BVH when edge length or spatial-density
distributions are too irregular for a uniform grid.

All implementations are exact for the generated binary32 geometry. No
tolerance-based implementation is included: the filtered exact predicate was
fast enough, while an approximate API would add workload-dependent false
positives or false negatives without helping the exact interface contract.

## Prompt issue resolved

Finite bounds and pose values do not guarantee finite binary32 world corners.
For example, `position_x = float.MaxValue` plus a positive local contribution can
overflow when the final result is rounded to `float`. The prompt requires a
finite result but specifies neither an input precondition nor failure behavior.

The implementations evaluate the documented transform, cast each final
coordinate once, and throw `ArgumentOutOfRangeException` from `IsColliding` if a
rounded corner is nonfinite. Clamping was rejected because it would silently
invent different geometry. This behavior is tested.

Positive local extents can also collapse to zero-length world edges after float
rounding at a large translation. That case is valid and is handled exactly as a
closed point segment.

## Shared exact numerical policy

### Rectangle construction

Every detector uses the same policy:

- Convert the binary32 angle to binary64 and multiply by the binary64 constant
  `Math.PI / 180.0`.
- Evaluate `Math.Cos` and `Math.Sin` in binary64.
- Evaluate each documented coordinate expression with explicit binary64
  temporaries in the written order.
- Cast each final coordinate once to binary32. These eight rounded values define
  the rectangle used by the predicates.
- Reject a nonfinite rounded result as described above.

Huge finite angles are accepted. Trigonometric range reduction may differ across
runtimes, which the prompt permits; exactness begins with the binary32 vertices
that this policy generates.

### Segment predicate

There is no geometric epsilon or tolerance.

1. Inclusive binary32 AABBs reject definitely disjoint segment pairs.
2. The four orientation signs use a binary64 determinant and Shewchuk's certified
   first-stage error bound, `3.3306690738754716e-16 * detsum`.
3. An uncertain determinant falls back to exact integer arithmetic. Every finite
   binary32 coordinate is a signed integer times a power of two. The six
   orientation coordinates are aligned to a common power-of-two exponent and the
   determinant is evaluated with `BigInteger`; its sign is the exact real sign.
4. Any binary32 subnormal input goes directly to the bit-decoded integer path, so
   the filter does not depend on native float arithmetic preserving subnormals.
5. Zero orientations use inclusive coordinate bounds. Endpoint contact,
   tangency, T-junctions, point segments, and any collinear overlap therefore
   return true.

The fast filter assumes normal C#/Unity IEEE-754 binary64 round-to-nearest
semantics and no compiler mode that changes scalar double arithmetic. The exact
fallback itself works from float bit patterns and has no floating tolerance.

## Algorithms and complexity

Let `N = n1 + n2`. The transferred vertex lists account for `Theta(N)` storage in
every implementation and are retained directly; vertices are not copied. Index
structures contain only edge IDs and derived bounds.

### Linear scan

How it works: validate both outlines, then visit edges in list order. An edge
AABB is formed on demand; candidates are tested against the four rectangle sides
with the shared exact predicate. It returns immediately on a hit.

Objective: minimize construction latency and auxiliary storage; exploit small
`N` or hits near the beginning of the supplied ordering.

- Preprocessing: `Theta(N)` validation time.
- Per query: `O(N)` worst case, `O(1)` best case for an immediate hit.
- Auxiliary storage: `Theta(1)` beyond the transferred `Theta(N)` lists.

Preferred workloads: one or a few queries, small outlines, broad queries that
defeat spatial pruning, or strongly predictable early hits. It is poor for
localized misses as `N` grows.

### Balanced AABB BVH

How it works: cache each edge AABB and an edge-ID permutation. Recursively split
the longest centroid-spread axis at the median, producing a count-balanced flat
tree. A query prunes nodes using the AABB of the four rounded rectangle vertices;
leaf candidates receive exact edge tests.

Assumptions: no distribution assumption is required for correctness. AABB
overlap can nevertheless be loose for broad rectangles and rotated thin
rectangles.

Objective: robust localized-query acceleration with linear storage and no grid
resolution choice.

- Preprocessing: expected `O(N log N)` with median-of-three quickselect;
  adversarial worst case `O(N^2)` for construction.
- Per query: `O(V + K)`, where `V` is visited nodes and `K` is leaf candidates;
  typically `O(log N + K)`, worst case `Theta(N)`.
- Auxiliary storage: `Theta(N)` edge bounds, edge IDs, and nodes.

Preferred workloads: many localized queries, especially when edge sizes or
spatial density vary. Compared with the grid it is less sensitive to a chosen
cell scale, but costs more build time and pointer-free node storage in these
measurements.

### Bounded uniform grid

How it works: choose approximately `min(N, 65,536)` dense cells with dimensions
matching the combined outline aspect ratio. Insert an edge ID into every cell
overlapped by its AABB. If that would exceed 64 cells, place the ID once in an
overflow list. A localized query scans all cells overlapped by the rectangle
AABB, then performs exact tests. If the query spans more than one eighth of the
grid (with a minimum threshold of 16 cells), it scans all edges once instead;
this avoids repeated candidate work and requires no mutable deduplication stamp.

Assumptions: performance is best when outline edges are spatially distributed
and their AABBs are comparable to the grid cell scale. These assumptions do not
affect correctness.

Objective: minimum steady-state latency for many localized track queries, fast
construction, thread-safe immutable query state, and bounded edge replication.

Let `G` be the number of cells, `P` the configured replication cap (64 by
default), `M <= P*N` stored ordinary edge references, `H` overflow edges, `C`
queried cells, and `T` the cell references visited.

- Preprocessing: `Theta(N + G + M)` time.
- Per localized query: `O(C + T + H)`; broad-query fallback `Theta(N)`; worst
  case with fixed default `P` is `O(N + G)`.
- Auxiliary storage: `Theta(N + G + M + H)`, bounded by `Theta(N + G)` for fixed
  `P`.

Preferred workloads: many small or medium rectangles against track-like edges.
The BVH is preferable when a grid would have extreme occupancy skew. The scan is
preferable when query count cannot amortize the grid.

All three detectors are immutable and use no shared scratch markers, so concurrent
calls are safe provided the caller honors transferred-list ownership.

## Correctness validation

The final Release test run passed **688,831 assertions in 2.330 seconds**. The harness
contains an independent brute-force oracle: it rounds corners with a separately
written copy of the documented transform, decodes every float onto the common
`2^-149` integer lattice, and uses unconditional `BigInteger` orientation
determinants. It does not call production geometry or index helpers.

Coverage includes:

- required public namespace, types, two-list constructors, exceptions, invalid
  default bounds, valid default pose, list validation, and no mutation on a
  failed construction;
- proper crossings, both outline loops and closing edges, endpoint and corner
  contact, tangency, collinear overlap/disjointness, and AABB rejection;
- containment inside the inner loop, in the annulus, and enclosing both loops,
  all correctly false without edge contact;
- off-center local bounds, clockwise and counter-clockwise poses, huge finite
  angles, subnormal extents, one-ULP separations, large translations that collapse
  rectangle edges, and transformed-coordinate overflow;
- all 390,625 ordered pairs of point/segment endpoints on the integer lattice
  `[-2,2]^2`;
- 100,000 raw finite-bit orientation comparisons and 100,000 raw finite-bit
  segment comparisons against the exact oracle, plus 20,000 constructed exact
  large-translation collinear orientations and 20,000 matching ±1-ULP
  perturbations;
- randomized end-to-end queries at multiple `N`, reversed winding, elongated
  nonuniform loops, subdivided collinear loops, forced grid-overflow candidates,
  and custom BVH/grid setting validation.

Command:

```powershell
dotnet run --project "CollisionDetection.Harness\CollisionDetection.Harness.csproj" -c Release --no-build -- --test
```

## Benchmark methodology

Reported timings are measurements, not estimates.

- Reported run: 2026-08-23 21:23:46–21:25:07 UTC.
- Host: Intel Core Ultra 9 275HX, 24 logical processors, performance power plan;
  Windows build 10.0.26200 x64.
- Runtime: .NET 10.0.11 x64 JIT, workstation GC, Release optimization, no
  debugger, tiered compilation and tiered PGO disabled; `Stopwatch` frequency
  10,000,000 Hz.
- Compatibility target: the production assembly was separately compiled as C# 9
  `netstandard2.1`, matching ZoomTracks Unity 6000.3.22f1 / API compatibility
  level 6 / unsafe disabled.
- A Unity Editor was not installed on this machine. These are .NET JIT host
  timings, not Unity Mono or IL2CPP timings; an in-player benchmark should be run
  before choosing final game-specific thresholds.
- Fixed-seed outline and query generation. Smooth-loop edge length was about 12
  application units, within the prompt's typical range. `n1:n2` was 2:1.
- Every reported query for every algorithm was first compared with the
  independent exact oracle. Oracle work was outside timed regions.
- Build measurements: nine samples of batched constructors. Each batch covers
  approximately 262,144 total edges (21 to 4,096 constructors depending on `N`),
  and every constructor receives fresh transferred lists prepared before timing.
  Constructed detectors remain live through the batch. Tables report the median
  per-constructor time and MAD. Allocations exclude input lists and include
  constructor/index allocations.
- Query measurements: four warm-up passes, adaptive repetitions targeting at
  least 0.15 seconds/sample, eleven samples, median and MAD. The timed call is
  `ICollisionDetector.IsColliding`; bounds and poses were precreated, but all
  sine/cosine and corner construction remained inside each call. An
  order-sensitive checksum prevented dead-code elimination. Timings were not
  baseline-subtracted.
- Allocation measurement used one warmed pass and
  `GC.GetAllocatedBytesForCurrentThread`. Ordinary workloads allocated zero bytes
  per query. The exact-degeneracy stream averaged 0.984 bytes/query because rare
  filtered-predicate fallbacks allocate `BigInteger` data.

Workloads:

- **localized/mixed:** one quarter exact outer-edge hits (rectangle center sampled
  from an actual edge midpoint), plus inner containment, annulus misses, and far
  misses; randomized order, off-center bounds, and rotation;
- **enclosing miss:** rectangle AABB contains every edge while its perimeter is
  outside both loops, exposing broad-phase worst behavior;
- **early-edge hit:** every query is positioned near edge zero and intersects an
  early-ordered edge, favoring ordered scanning;
- **elongated nonuniform:** 8:1 aspect ratio and angularly nonuniform sampling;
- **exact collinear/1-ULP:** subdivided rectangular loops with alternating exact
  boundary overlap and a one-ULP disjoint perimeter.
- **multiscale grid-overflow miss:** a valid star-shaped outer loop alternates
  radii 100 and 900 while the inner loop has radius 30. Its roughly 800-unit outer
  edges force 1,880 IDs into the default grid overflow list; small rectangles
  remain inside the inner loop without perimeter contact.

Raw rows, full-precision medians/MADs, checksums, allocations, and index parameters
are in `TestArtifacts/benchmark-results.csv`. The UTC run timestamps, runtime
environment, and SHA-256 hashes of all executable source files are in
`TestArtifacts/benchmark-environment.txt`.

Benchmark command after the Release build:

```powershell
$env:DOTNET_TieredCompilation = "0"
$env:DOTNET_TieredPGO = "0"
dotnet run --project "CollisionDetection.Harness\CollisionDetection.Harness.csproj" -c Release --no-build -- --benchmark --output "TestArtifacts"
```

## Measured preprocessing

Times are per-constructor milliseconds from the batched measurements; allocations
are KiB and exclude transferred input lists. Sub-microsecond scan averages are
useful for amortization on this host but should not be treated as portable constants.

| Geometry | N | Algorithm | Build ms | MAD ms | Alloc KiB | Index details |
|---|---:|---|---:|---:|---:|---|
| small smooth | 48 | Linear | 0.0001 | 0.0000 | 0.04 | none |
|  |  | BVH-8 | 0.0027 | 0.0001 | 4.05 | 15 nodes |
|  |  | Grid | 0.0033 | 0.0001 | 5.20 | 7x7, 81 refs |
| medium smooth | 384 | Linear | 0.0003 | 0.0000 | 0.04 | none |
|  |  | BVH-8 | 0.0425 | 0.0010 | 31.61 | 127 nodes |
|  |  | Grid | 0.0214 | 0.0010 | 28.09 | 20x19, 491 refs |
| large smooth | 3,072 | Linear | 0.0019 | 0.0000 | 0.04 | none |
|  |  | BVH-8 | 0.5064 | 0.0095 | 252.11 | 1,023 nodes |
|  |  | Grid | 0.0707 | 0.0006 | 178.14 | 56x55, 3,379 refs |
| very-large smooth | 12,288 | Linear | 0.0075 | 0.0003 | 0.04 | none |
|  |  | BVH-8 | 2.4571 | 0.0489 | 1,008.11 | 4,095 nodes |
|  |  | Grid | 0.2777 | 0.0145 | 654.25 | 111x111, 12,892 refs |
| elongated nonuniform | 3,072 | Linear | 0.0019 | 0.0001 | 0.04 | none |
|  |  | BVH-8 | 0.5801 | 0.0101 | 252.11 | 1,023 nodes |
|  |  | Grid | 0.0732 | 0.0020 | 187.48 | 158x19, 3,557 refs |
| collinear rectangles | 1,024 | Linear | 0.0008 | 0.0000 | 0.04 | none |
|  |  | BVH-8 | 0.1482 | 0.0043 | 84.11 | 255 nodes |
|  |  | Grid | 0.0251 | 0.0008 | 68.34 | 32x32, 1,200 refs |
| multiscale overflow | 3,072 | Linear | 0.0019 | 0.0000 | 0.04 | none |
|  |  | BVH-8 | 0.4745 | 0.0087 | 252.11 | 1,023 nodes |
|  |  | Grid | 0.1106 | 0.0075 | 281.31 | 55x56, 7,680 refs, 1,880 overflow |

## Measured per-query time

Times and MAD are microseconds/query. `Q` is the distinct precreated query count.

| Workload | N | Q | Hits | Linear | BVH-8 | Grid | Fastest |
|---|---:|---:|---:|---:|---:|---:|---|
| small mixed | 48 | 8,192 | 2,138 | 0.2084 ± 0.0013 | **0.1245 ± 0.0031** | 0.1284 ± 0.0018 | BVH |
| medium localized | 384 | 4,096 | 1,024 | 0.9066 ± 0.0157 | 0.1069 ± 0.0016 | **0.0921 ± 0.0027** | Grid |
| large localized | 3,072 | 2,048 | 512 | 6.4147 ± 0.0164 | 0.1045 ± 0.0022 | **0.0864 ± 0.0009** | Grid |
| very-large localized | 12,288 | 2,048 | 512 | 24.8712 ± 0.2939 | 0.1133 ± 0.0023 | **0.0856 ± 0.0014** | Grid |
| very-large enclosing miss | 12,288 | 512 | 0 | 266.0515 ± 3.0022 | 257.2381 ± 3.0630 | **244.7388 ± 3.1485** | Grid fallback |
| very-large early-edge hit | 12,288 | 2,048 | 2,048 | **0.0952 ± 0.0013** | 0.1311 ± 0.0029 | 0.1170 ± 0.0017 | Linear |
| elongated nonuniform | 3,072 | 2,048 | 512 | 6.5221 ± 0.1019 | 0.1117 ± 0.0009 | **0.0831 ± 0.0007** | Grid |
| exact collinear/1-ULP | 1,024 | 2,048 | 1,024 | 1.6962 ± 0.0221 | 0.3840 ± 0.0064 | **0.3665 ± 0.0022** | Grid |
| multiscale grid-overflow miss | 3,072 | 2,048 | 0 | 7.6850 ± 0.1559 | **0.0921 ± 0.0013** | 2.0979 ± 0.0121 | BVH |

## Amortization

The following measured break-even query count is

`ceil((indexed build - scan build) / (scan query - indexed query))`.

It is meaningful only where the index query is faster.

| Workload | BVH break-even | Grid break-even |
|---|---:|---:|
| small mixed, N=48 | 32 | 41 |
| medium localized, N=384 | 53 | 26 |
| large localized, N=3,072 | 80 | 11 |
| very-large localized, N=12,288 | 99 | 11 |
| elongated nonuniform, N=3,072 | 91 | 12 |
| exact collinear/1-ULP, N=1,024 | 113 | 19 |
| multiscale grid-overflow miss, N=3,072 | 63 | 20 |
| very-large enclosing miss | 282 | 13 |
| very-large early-edge hit | not faster | not faster |

These are host/workload measurements rather than universal cutoffs. A practical
selection policy is:

- choose the default grid for dozens or more localized queries against ordinary
  track outlines;
- choose a scan for very few queries or known early-edge hits;
- choose the BVH when grid occupancy/edge-scale skew is unknown or observed to be
  poor—the measured overflow stress case is a concrete example—then tune the
  eight-edge leaf size on the target Unity backend.

## Limitations and next validation

- Measurements are synthetic, fixed-seed geometry rather than a captured
  ZoomTracks trace. Replay representative game poses before freezing a game-side
  crossover.
- The host lacks Unity, so neither Mono nor IL2CPP code generation was timed or
  compiled directly. The `netstandard2.1` build is a compatibility proxy.
- The grid's 65,536-cell default cap and 64-cell replication cap are practical
  constants, not analytically optimal for every outline. Its public overload
  allows controlled tuning up to 1,048,576 target cells.
- The BVH build's quickselect has an adversarial quadratic construction bound. A
  deterministic linear-time select or binned SAH builder is appropriate if
  untrusted inputs make preprocessing worst-case guarantees important.
