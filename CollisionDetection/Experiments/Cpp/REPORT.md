# Exact rectangle-perimeter versus outline-edge intersection

## Recommendation

Use `BvhAdaptiveExact` as the practical default for the intended many-query,
immutable-outline workload. It is exact, has linear storage, uses no per-query
allocation, handles spatially clustered edges much better than the grid, and
usually removes nearly all of the linear scan.

Use `UniformGridAdaptiveExact` after profiling when outline edges are spatially
uniform and queries are mostly small and local. It built 3.8–6.5 times faster
than the BVH in the measured cases and was usually the fastest regular-geometry
index, but a deliberately clustered distribution made its queries 12.9 times
slower than the BVH. Use `LinearAdaptiveExact` for small `N`, very few queries,
or when minimal memory and implementation overhead matter. Keep
`LinearAlwaysExact` as a simple oracle and audit path, not the normal runtime
choice.

All four implementations are exact. There is no tolerance-based or otherwise
approximate variant, so none has a tolerance-induced false-positive or
false-negative region.

## Input assumptions and returned predicate

The implementation assumes the validity contract from the task rather than
revalidating it: both outlines contain at least three finite binary32 vertices,
their segments are positive-length simple loops, `O2` is strictly inside `O1`,
and the four query vertices form a positive-edge, strictly convex cyclic loop.
Clockwise and counterclockwise queries are accepted.

Each index copies all `N = n1 + n2` closed outline edges. A query tests exactly
the four closed segments supplied in `R`; it never reconstructs an ideal
rectangle and never treats `R` as filled. Proper crossings, endpoint contact,
tangency, and collinear overlap return `true`. Containment with no edge contact
returns `false`.

## Numerical-robustness policy

Every input `float` is treated as the exact real represented by its IEEE 754
binary32 bit pattern. No geometric epsilon is used.

The common adaptive orientation predicate works as follows:

1. Binary32 values are converted to binary64 by integer rebiasing and
   normalization of their IEEE bit patterns. This is exact and remains correct
   when x86 DAZ (denormals-are-zero) is enabled.
2. It evaluates `(b-a) x (c-a)` in binary64. Under round-to-nearest it accepts a
   sign only when its magnitude exceeds the certified error bound
   `(3 + 16u)u (|left| + |right|)`, where `u = 2^-53`.
3. An uncertain result, including exact collinearity, falls back to fixed-width
   integer arithmetic. If the process uses any rounding mode other than
   round-to-nearest, the filter is skipped and the exact path is used.

Scaling any finite binary32 value by `2^149` produces an integer of at most 277
bits. The exact path therefore uses nine 32-bit limbs for signed coordinate
differences (at most 278 bits) and eighteen limbs for product magnitudes below
`2^556` (at most 556 bits). It compares the two signed products to obtain the determinant
sign; no division or rounded arithmetic is involved. `LinearAlwaysExact` uses
this path for every orientation. The adaptive variants use it only when the
filter cannot certify a sign.

The binary64 error bound is a certification threshold, not a geometric
tolerance: it only chooses between a proven sign and exact fallback. All
nonzero differences/products obtainable from finite binary32 inputs lie in the
normal binary64 range, so overflow and binary64 underflow do not invalidate the
bound.

Broad-phase coordinate ordering is also bitwise IEEE ordering, with `-0` and
`+0` normalized as the same real value. This prevents DAZ from changing AABB or
on-segment decisions. Inclusive AABBs can only retain extra candidates; every
retained candidate goes through the exact segment predicate. Build with strict
IEEE semantics (`/fp:strict`, as in `build.ps1`, or the non-MSVC CMake flags),
not a fast-math mode.

## Algorithms

### `LinearAlwaysExact`

The index stores each outline edge and its inclusive float-coordinate AABB,
plus one combined outline AABB. For each of the four query edges it first tests
the combined bound, scans all edge AABBs if necessary, and applies the
always-integer exact segment predicate to candidates. It stops at the first
hit.

Its objective is simplicity and independence from floating-predicate filtering.
It is the correctness oracle used by the tests and benchmark. Preprocessing is
`Theta(N)`, persistent storage is `Theta(N)`, and a query is best-case
`Theta(1)` after a global rejection or immediate hit and worst-case `Theta(N)`.
Binary32 fixes the integer limb counts, so the exact arithmetic is constant
work with respect to `N`.

This is useful for small outlines, test or audit builds, and workloads in which
the global bound rejects nearly everything. It is poor when many edge AABBs are
candidates: at `N=32768`, the large rotated containment miss took 9.41 ms/query.

### `LinearAdaptiveExact`

This uses the same scan and storage but the certified binary64 filter before
integer fallback. It optimizes minimum preprocessing and storage while keeping
the exact answer. Its asymptotic preprocessing, query, and storage bounds are
the same as `LinearAlwaysExact`.

It is the preferred linear variant. The filter is especially valuable when
many nondegenerate candidate AABBs survive: the same `N=32768` containment miss
took 1.04 ms/query, 9.1 times faster than the always-integer oracle. When no
edge AABB survives, the two policies do equivalent broad-phase work.

### `BvhAdaptiveExact`

The BVH recursively splits edge records at the median centroid on the axis with
the larger centroid span. The default leaf size is `B=8`. Nodes and reordered
edges are stored in preorder; each interior node stores the index immediately
after its subtree, allowing stackless traversal. A query traverses each of its
four edge AABBs independently and runs the adaptive-exact predicate in
overlapping leaves.

Centroids are computed in binary64 only to choose a partition. Rounding a
centroid cannot change correctness because every node bound is the exact
inclusive union of its float endpoint AABBs. A nonoverlapping node therefore
cannot contain a real intersection.

Recursive `nth_element` selection gives expected preprocessing
`O(N * (1 + log(ceil(N/B))))`; this includes the root scan when `B >= N` and is
`O(N log N)` for the fixed default `B=8`. This is not claimed as a worst-case guarantee because
`nth_element` has average-linear selection complexity. There are
`Theta(ceil(N/B))` leaves and nodes, so persistent storage is `Theta(N)`. Query work
is proportional to visited nodes and leaf edges; well-separated local queries
often behave like `O(log N + k)`, but the guaranteed worst case is `O(N)`.
Traversal uses `Theta(1)` auxiliary query workspace.

The objectives are robust general query performance, cache-conscious storage,
and no query scratch allocation. It is preferred for unknown, clustered,
nonuniform, or long-edge distributions. Broad queries whose edge AABBs overlap
most of the outline can still degenerate to a near scan.

### `UniformGridAdaptiveExact`

The grid chooses an aspect-aware resolution with about one cell per edge:
`nx/ny` follows the domain aspect ratio and `nx*ny` approximates `N`, with each
axis capped at 4096 by default. An edge ID is stored in every grid cell touched
by its AABB. CSR arrays hold the cell offsets and IDs compactly. If one edge
would occupy more than the default 256 cells, it is put in an overflow list
instead, bounding index expansion.

Each of the four query-edge AABBs is clipped to the outline domain and maps to
an inclusive cell range. A thread-local generation-mark array deduplicates edge
IDs without an `O(N)` clear. Overflow edges are tested for every in-domain query
edge. Cell mapping uses a common monotone binary64 transform; overlapping exact
AABB intervals cannot map to disjoint integer ranges. Mapping arithmetic is
only a conservative candidate selector, and final tests are adaptive-exact.

Let `C=nx*ny`, `P` be stored cell-edge references, `H` overflow edges, `q` cells
visited by a query, and `p` references encountered in those cells.
Preprocessing is `Theta(N+C+P)`, and persistent index storage is
`Theta(N+C+P+H)`. With maximum references per edge `M`, `P <= NM`; the default
fixed cap and `C` near `N` make typical construction linear. A warm steady-state
query costs `O(q+p+H)` and has worst case `O(C+P+H)`. The first query on each
thread additionally allocates and zeros `Theta(N)` marks; a once-per-`2^64`
generation wrap also clears them.

The objectives are low preprocessing cost and maximum throughput for small
local queries on spatially uniform edges. Costs become sensitive to crowded
cells, long edge AABBs, overflow-list size, and large rotated query-edge AABBs.
The index remains exact when it degrades; only performance changes.

## Complexity summary

The four edges of `R` are a constant factor.

| Algorithm | Preprocessing | Worst query | Persistent storage | Query workspace |
|---|---:|---:|---:|---:|
| Linear, either predicate | `Theta(N)` | `O(N)` | `Theta(N)` | `Theta(1)` |
| Median BVH, leaf size `B` | expected `O(N * (1 + log(ceil(N/B))))` | `O(N)` | `Theta(N)` | `Theta(1)` |
| Capped uniform grid | `Theta(N+C+P)` | `O(C+P+H)` | `Theta(N+C+P+H)` | `Theta(N)` per thread after first use |

The grid also uses transient `Theta(C)` write-offset storage while building.

## Correctness testing

The final Release test run reported:

```text
PASS: 480225 checks
```

Permanent C++ coverage includes:

- proper crossings, separated segments, endpoint and corner contact, tangency,
  horizontal/vertical collinear overlap, and collinear separation;
- perimeter-only containment misses, crossing either outline, clockwise and
  counterclockwise queries, rotations, and deliberately non-ideal supplied
  quadrilaterals;
- subnormal-scale and near-maximum-coordinate cases, signed-zero-compatible
  ordering, alternate IEEE rounding modes, and x86 DAZ counterexamples;
- 100,000 orientations checked against an independent bounded-integer formula,
  200,000 full-finite-binary32 adaptive/always-exact orientation differentials,
  and 50,000 full-range segment differentials;
- 10,000 randomized rectangle-query differentials across unequal radial
  outlines;
- concave outlines, minimum three-edge outlines, BVH leaf sizes
  `{0,1,3,1000}`, forced grid overflow and one-cell settings;
- eight simultaneous threads querying one immutable grid index.

Every benchmark algorithm/result pair is also checked against
`LinearAlwaysExact` immediately before timing. A supplementary adversarial
audit ran 2,557,320 index comparisons over extreme grid/BVH configurations and
found no mismatch. A separate arbitrary-precision audit exercised 900,004
orientation triples under each IEEE rounding mode plus two million filter
trials. That audit found the original DAZ issue; bitwise ordering/conversion and
the permanent DAZ regressions are the implemented fix.

## Benchmark methodology

The benchmark was run on 2026-08-23 with:

- Windows 11, kernel build `10.0.26200.0`;
- Intel Core Ultra 9 275HX, 24 hardware threads;
- Microsoft x64 C/C++ 19.51.36256 (`_MSC_FULL_VER=195136256`);
- C++20 Release, `/O2 /DNDEBUG /fp:strict`.

It is a single-threaded, warm-cache microbenchmark. Each workload contains 512
deterministic queries. Results are medians of three timed batches after an
untimed correctness pass and 256-query warm-up. Calibration targets at least
80 ms per batch but caps repetitions at 1024, so the fastest global-rejection
samples run for less than the target. Query timing includes virtual dispatch,
the query loop, and a checksum, but excludes validation and statistics.
Preprocessing is a median of seven complete constructions, or three at
`N=32768`. No core affinity, cross-process repetitions, or confidence intervals
were used; sub-microsecond results should be treated as noisy. Algorithms use
paired query sets at a given `N`, while the deterministic seed changes across
`N`.

Regular geometry uses concentric regular loops of radii 1000 and 300 with
approximately two thirds of `N` edges in `O1`. The resulting edge-length ranges
are approximately 44.8–73.0 (`N=128`), 2.76–4.60 (`N=2048`), and
0.173–0.288 (`N=32768`), all within the typical guidance. Workloads are:

- `far_miss`: small queries outside the combined outline AABB;
- `annulus_local_miss`: small queries between the two loops;
- `mixed_local`: mixed sizes/locations, with 112, 92, and 101 hits of 512 as
  `N` increases;
- `large_rotated_containment_miss`: a rotated query enclosing both loops, with
  no perimeter contact;
- `vertex_touch`: exact contacts distributed uniformly around `O1`.

The second geometry has `N=2048` and places 80% of each loop's edges in a
0.2-radian arc. Its approximate edge-length range is 0.11–22.3, also within the
guidance. `clustered_cell_miss` places small no-hit queries just inside the
dense outer arc.

## Measured results

All query columns below are measured nanoseconds per query. `Build` is measured
microseconds. `Bytes` is the persistent index-payload estimate from object sizes
and retained vector capacities.

| `N` | Algorithm | Build us | Bytes | Far | Annulus | Mixed | Large contain | Vertex touch |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 128 | LinearAlwaysExact | 1.5 | 4,152 | 26.5 | 917.4 | 985.1 | 40,652.3 | 551.4 |
| 128 | LinearAdaptiveExact | 1.2 | 4,152 | 27.8 | 1,158.8 | 1,045.9 | 6,881.7 | 425.9 |
| 128 | BvhAdaptiveExact | 23.1 | 5,152 | 45.5 | 272.3 | 306.7 | 6,398.2 | 322.6 |
| 128 | UniformGridAdaptiveExact | 6.0 | 6,896 | 45.1 | 140.0 | 205.4 | 6,695.2 | 365.1 |
| 2,048 | LinearAlwaysExact | 31.0 | 65,592 | 39.6 | 19,414.4 | 14,420.0 | 629,521.7 | 2,088.9 |
| 2,048 | LinearAdaptiveExact | 33.1 | 65,592 | 41.7 | 19,537.6 | 13,491.8 | 105,258.2 | 1,958.1 |
| 2,048 | BvhAdaptiveExact | 527.7 | 81,952 | 44.5 | 346.1 | 448.8 | 90,146.8 | 371.2 |
| 2,048 | UniformGridAdaptiveExact | 115.9 | 100,912 | 46.3 | 143.8 | 338.3 | 89,760.7 | 370.7 |
| 32,768 | LinearAlwaysExact | 381.9 | 1,048,632 | 44.1 | 299,511.3 | 227,755.1 | 9,407,015.2 | 14,634.1 |
| 32,768 | LinearAdaptiveExact | 367.6 | 1,048,632 | 23.4 | 168,567.8 | 122,382.0 | 1,037,739.1 | 14,310.5 |
| 32,768 | BvhAdaptiveExact | 7,972.3 | 1,310,752 | 25.7 | 219.8 | 2,406.5 | 931,489.1 | 337.4 |
| 32,768 | UniformGridAdaptiveExact | 1,235.8 | 1,583,520 | 28.3 | 104.9 | 2,474.3 | 934,594.5 | 387.6 |

The global AABB makes `far_miss` constant work for every approach; these rows do
not demonstrate a spatial-index advantage. The local annulus does: at
`N=32768`, BVH and grid took 219.8 ns and 104.9 ns versus 168.6 us for adaptive
linear. The mixed workload retained about 40 exact candidates per query and
measured about 2.4 us for either spatial index versus 122.4 us linear.

The large containment miss retained 17,253.9 candidates per query at
`N=32768` for all approaches. BVH/grid therefore approached the same 0.93 ms
cost, demonstrating their `O(N)` worst case. Exact vertex contacts caused two
integer fallbacks per query; the BVH still reduced a uniformly positioned
linear early-exit scan from 14.3 us to 337 ns.

Clustered-geometry results are:

| Algorithm | Build us | Bytes | Clustered-cell miss ns/query |
|---|---:|---:|---:|
| LinearAlwaysExact | 17.1 | 65,592 | 10,725.5 |
| LinearAdaptiveExact | 17.1 | 65,592 | 10,788.1 |
| BvhAdaptiveExact | 357.9 | 81,952 | 173.9 |
| UniformGridAdaptiveExact | 94.0 | 101,008 | 2,250.7 |

Here the grid performed 943.7 edge-AABB tests per query from crowded cells after
deduplicating separately within each query edge, while the BVH performed 56.2
total node/edge AABB tests. The BVH was 12.9 times faster per query; its extra
263.9 us of construction versus the grid amortizes after about 127 such queries.

At regular `N=32768`, the BVH's extra preprocessing over adaptive linear
amortized after roughly 45 annulus misses or 63 mixed queries in this run. The
grid's extra preprocessing amortized after roughly 6 and 8 respectively. These
are measured-workload illustrations, not universal crossover guarantees.

The CSV `storage_bytes` values exclude allocator metadata, process RSS,
transient build allocations, and grid query scratch. After first use, the grid
adds approximately `8N` bytes per querying thread (1,024; 16,384; and 262,144
bytes at the three regular sizes), retained according to the largest grid seen
by that thread. CSV statistics are gathered outside timing. Their counters are
algorithm-specific: BVH broad-phase counts include node tests, grid cell scans
have separate counters, and `exact_orientation_fallbacks=0` on
`LinearAlwaysExact` means exact work is its primary path rather than a fallback.

Raw values and counters are in `results/benchmark.csv` and can be regenerated
with the commands in `README.md`.
