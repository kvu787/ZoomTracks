# Benchmark results

These measurements compare the loop-native Python implementations directionally.
They are not predictions of Unity/C# timings and do not replace profiling real
TrackBuilder output on target hardware.

## Reproduction

- Date: 2026-08-22
- OS: Windows 11 (`10.0.26200`)
- Python: CPython 3.14.7, standard library only
- System state: idle except for the benchmark process
- Normal input: two equal-sized ordered closed vertex loops around one smooth
  synthetic centerline
- Derived edges: one edge per vertex, including each implicit closing edge
- Vehicle half-extents: `2.2 x 1.0`
- Queries per workload: 10,000
- Query samples: 15 complete batches after two warm-up batches
- Query GC: disabled during measured batches
- Query value: median complete-batch mean per query
- Parenthesized query value: slowest complete-batch mean, not a per-query
  percentile
- Build value: median of five builds with normal GC; excludes shared
  `PreparedOutlines` construction and teardown
- Preparation value: one timed validation/snapshot/edge-derivation pass

Index defaults:

- coherent blocks: 16 edges;
- ordered hierarchy: 8-edge leaves, branching factor 4;
- spatial microchains: 8 edges;
- uniform grid: automatic cell size, 256-cell query cap, 4,096-reference edge
  cap; and
- full edge BVH: 8-edge leaves, 12 SAH bins, expected query footprint `5 x 5`.

Exact normal-track command:

```powershell
python -m CollisionDetection.benchmark --vertices 1024,4096 --queries 10000 --repeats 15 --workload all
```

The normal workloads are deliberately different:

- **Lap:** smooth centerline lap with 5% outline contacts (500 hits).
- **Near:** every rectangle runs close to the outer outline without contact.
- **Far:** every rectangle lies outside the complete outline bounds.

The vertex-count variants increasingly sample the same fixed curve. They
increase local edge density; they do not hold density constant while making the
track longer. No total-track-length scaling claim is inferred from them.

## Normal track: 1,024 vertices / 1,024 edges

Shared `PreparedOutlines` construction took `1.04 ms`.

Times are median per query, with the slowest batch mean in parentheses.

| Algorithm | Index build | Lap | Grazing near miss | Far miss |
|---|---:|---:|---:|---:|
| Linear scan | 1.1 us | 16.68 us (16.93) | 18.77 us (19.08) | **0.430 us** (0.463) |
| Coherent blocks | 88.1 us | 2.43 us (2.47) | 4.33 us (4.37) | 0.437 us (0.467) |
| Ordered hierarchy | 168.8 us | 1.42 us (1.46) | **3.18 us** (3.28) | 0.630 us (0.645) |
| Spatial microchain BVH | 384.2 us | **1.41 us** (1.43) | 3.24 us (3.38) | 0.566 us (0.600) |
| Automatic uniform grid | 1.38 ms | 1.71 us (1.71) | 3.58 us (3.61) | 0.456 us (0.495) |
| Full edge SAH BVH | 8.14 ms | 1.65 us (1.67) | 3.51 us (3.56) | 0.612 us (0.629) |

The `0.01 us` lap difference between the ordered hierarchy and microchain BVH is
noise-scale. Build cost and behavior on folded geometry are more meaningful.

## Normal track: 4,096 vertices / 4,096 edges

Shared `PreparedOutlines` construction took `3.08 ms`.

| Algorithm | Index build | Lap | Grazing near miss | Far miss |
|---|---:|---:|---:|---:|
| Linear scan | 0.6 us | 62.97 us (63.95) | 69.76 us (70.11) | **0.428 us** (0.446) |
| Coherent blocks | 336.5 us | 6.27 us (6.45) | 11.38 us (11.52) | 0.447 us (0.469) |
| Ordered hierarchy | 757.3 us | **1.47 us** (1.52) | **6.40 us** (6.43) | 0.627 us (0.660) |
| Spatial microchain BVH | 1.82 ms | 1.58 us (1.62) | 6.57 us (6.68) | 0.579 us (0.603) |
| Automatic uniform grid | 4.74 ms | 1.81 us (1.84) | 7.43 us (7.55) | 0.459 us (0.481) |
| Full edge SAH BVH | 38.70 ms | 1.67 us (1.70) | 7.07 us (7.15) | 0.604 us (0.629) |

## Folded-range stress case

The separate folded case uses 20 radial oscillations and 4,096 total vertices.
It deliberately makes nonadjacent ranges spatially close. It is an index stress
case, not a physically representative road or a constant-normal track offset.
Its 10,000 queries produce 6,500 contacts.

Exact command:

```powershell
python -m CollisionDetection.benchmark --vertices 4096 --queries 10000 --repeats 15 --workload folded --folds 20
```

Shared preparation took `3.04 ms`.

| Algorithm | Index build | Folded query |
|---|---:|---:|
| Linear scan | 0.9 us | 41.60 us (42.68) |
| Coherent blocks | 337.3 us | 6.17 us (6.34) |
| Ordered hierarchy | 655.0 us | 3.01 us (3.06) |
| Spatial microchain BVH | 1.84 ms | 2.80 us (2.98) |
| Automatic uniform grid | 4.94 ms | 2.81 us (2.96) |
| Full edge SAH BVH | 38.33 ms | **2.77 us** (2.98) |

The microchain BVH is within `0.03 us` of the full edge BVH while building about
21 times faster. It also narrowly beats the ordered hierarchy here, demonstrating
the intended tradeoff without making it the normal-track default.

## Interpretation

- **The ordered hierarchy is the default.** On the normal 4,096-edge track it
  has both the fastest lap and grazing-miss medians. It builds about 2.4 times
  faster than the microchain BVH, 6.3 times faster than the grid, and 51 times
  faster than the full edge BVH.
- **Spatial microchains are the folded-track fallback.** They retain contiguous
  eight-edge leaf scans while spatially regrouping the leaf bounds. The folded
  stress result essentially matches the full spatial BVH at a small fraction of
  its build cost.
- **The full per-edge SAH BVH is not a production recommendation yet.** Neither
  normal nor folded synthetic input showed enough query benefit to justify its
  Python construction cost. It remains a useful benchmark/pathological-data
  comparison.
- **The grid remains experimental for real tracks.** It was competitive on the
  folded case, but microchains were equally fast with a cheaper build and no cell
  size, candidate stamps, or long-edge overflow policy.
- **Coherent blocks are a useful minimal baseline.** Their build is inexpensive,
  but every in-bounds query still scans all block bounds, so performance grows
  with edge count.
- **Grazing misses remain the hardest normal workload.** They overlap several
  candidate bounds and must prove separation for every candidate instead of
  returning on a hit.
- **Far misses exercise only common whole-outline/root rejection.** Sub-
  microsecond differences there do not characterize internal index quality.

## Build/query break-even versus linear scan

Using the normal 4,096-edge lap medians and excluding shared preparation:

```text
coherent blocks:       0.337 ms / (62.97 - 6.27 us) ~=   6 queries
ordered hierarchy:     0.757 ms / (62.97 - 1.47 us) ~=  13 queries
spatial microchain BVH: 1.82 ms / (62.97 - 1.58 us) ~=  30 queries
uniform grid:           4.74 ms / (62.97 - 1.81 us) ~=  78 queries
full edge SAH BVH:     38.70 ms / (62.97 - 1.67 us) ~= 632 queries
```

A static loaded track is queried for many thousands of frames, so every index
amortizes in normal play. The ordered hierarchy reaches its crossover quickly.

On the folded stress case, the microchain BVH saves about `0.21 us` per query
over the ordered hierarchy but costs about `1.19 ms` more to build, crossing over
after roughly 5,700 queries. This is why real-track measurement should decide
whether a folded layout warrants it.

## What to benchmark next

Before selecting the Unity implementation, repeat these categories on actual
TrackBuilder output and record candidate counts and memory as well as time:

- every real track at its authored post-evaluation vertex counts;
- unequal outer/inner counts and nonuniform evaluated spacing;
- separate hit, centerline miss, grazing miss, and far miss sequences;
- valid cyclic seam rotations and winding reversals;
- hairpins and spatially close nonadjacent portions;
- hierarchy leaf sizes 4/8/16 and branching factors 3/4;
- microchain sizes 4/8/16;
- grid sizes around vehicle width and median edge length; and
- the precision and packed data layout planned for Unity.
