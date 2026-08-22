# Benchmark results

These measurements compare the Python implementations directionally. They are
not predictions of final Unity/C# timings and do not replace benchmarking real
TrackBuilder outlines on target hardware.

## Reproduction

- Date: 2026-08-22
- OS: Windows 11 (`10.0.26200`)
- Python: CPython 3.14.7, standard library only
- System state: idle except for the benchmark process
- Input: two ordered, closed, smooth nonuniform outlines
- Vehicle half-extents: `2.2 x 1.0`
- Queries per workload: 10,000
- Samples: 15 full batches after two warm-up batches
- GC: disabled during timed samples
- Build time: excludes shared `PreparedSegments` conversion and index teardown
- Query value: median complete-batch mean per query
- Slowest mean: slowest complete-batch mean, not per-query p95

Exact command:

```powershell
python -m CollisionDetection.benchmark --segments 1024,4096 --queries 10000 --repeats 15 --workload all
```

The workloads are deliberately different:

- **Lap:** smooth centerline lap with 5% outline contacts (500 hits).
- **Near:** every rectangle runs close to the outer outline without contact.
- **Far:** every rectangle is outside the complete outline bounds.

The segment-count variants increasingly resample the same fixed synthetic curve.
They increase local segment density; they do not hold density constant while
making the track longer. No total-track-length scaling claim is inferred from
these two sizes.

## 1,024 total outline segments

Shared segment preparation took `0.73 ms`.

| Algorithm | Index build | Lap median | Near-miss median | Far median | Lap queries/s |
|---|---:|---:|---:|---:|---:|
| AABB-filtered scan | 1.0 us | 16.30 us | 21.91 us | **0.37 us** | 61,344 |
| Coherent blocks | 92.1 us | 2.27 us | 7.59 us | 0.37 us | 440,470 |
| Ordered hierarchy | 122.9 us | **1.50 us** | 6.80 us | 0.57 us | **666,178** |
| Automatic uniform grid | 1.17 ms | 1.59 us | **6.74 us** | 0.39 us | 630,203 |
| Query-aware SAH BVH | 7.79 ms | 1.52 us | 6.76 us | 0.52 us | 659,235 |

## 4,096 total outline segments

Shared segment preparation took `2.77 ms`.

| Algorithm | Index build | Lap median | Near-miss median | Far median | Lap queries/s |
|---|---:|---:|---:|---:|---:|
| AABB-filtered scan | 0.6 us | 64.67 us | 82.93 us | **0.39 us** | 15,464 |
| Coherent blocks | 0.37 ms | 6.28 us | 22.82 us | 0.40 us | 159,358 |
| Ordered hierarchy | 0.52 ms | 1.68 us | **18.18 us** | 0.61 us | 593,948 |
| Automatic uniform grid | 4.46 ms | 1.81 us | 18.63 us | 0.42 us | 551,700 |
| Query-aware SAH BVH | 38.46 ms | **1.59 us** | 18.52 us | 0.57 us | **629,890** |

Differences of a few tenths of a microsecond should be treated as close, not as
a universal ranking. The ordered hierarchy, grid, and SAH BVH are effectively in
the same query-time class here; their build costs and input assumptions separate
them much more clearly. Across this longer idle-system run, each slowest batch
mean was close to its median, so the broad ranking was stable across samples.

## Interpretation

- **Ordered hierarchy is the best default for TrackBuilder-shaped input.** At
  4,096 segments it built about 8.5 times faster than the grid and 74 times
  faster than the SAH BVH, while remaining within `0.09 us` of the best lap time
  and producing the best near-miss time.
- **The grid remains the best order-independent uniform-density option.** It was
  close to the ordered hierarchy without relying on outline order, but costs
  more to build and needs cell-size/overflow tuning on real data.
- **The SAH BVH buys distribution robustness.** It narrowly won the dense lap
  query but its Python build was about 74 times more expensive than the
  hierarchy. It is appropriate when ordering is unavailable and geometry is
  nonuniform enough to justify that cost.
- **Coherent blocks are a strong minimal broad phase.** They cut the dense lap
  from `64.67 us` to `6.28 us` with a sub-half-millisecond build, but still scale
  linearly through the block list.
- **Near misses matter.** They visit many candidate bounds and trigger robust
  narrow-phase fallbacks, making every indexed algorithm substantially slower
  than the mostly empty-cell/empty-leaf lap workload.
- **Far measurements primarily test the common whole-outline rejection.** All
  algorithms reject them in under one microsecond; this workload does not
  distinguish their internal structures.

## Build/query break-even versus the scan

Using the 4,096-segment lap medians and excluding shared preparation:

```text
coherent blocks:   0.372 ms / (64.67 - 6.28 us)  ~=   7 queries
ordered hierarchy: 0.522 ms / (64.67 - 1.68 us)  ~=   9 queries
uniform grid:      4.46  ms / (64.67 - 1.81 us)  ~=  71 queries
SAH BVH:          38.46  ms / (64.67 - 1.59 us)  ~= 610 queries
```

A static track is queried for many thousands of frames, so every index amortizes
in normal play. The ordered hierarchy reaches break-even especially quickly.

## What to benchmark next

Before selecting the Unity implementation, run the same categories on actual
TrackBuilder output and record candidate counts/memory as well as time:

- each real track and authored evaluation density;
- separate hit, centerline miss, grazing miss, and far miss sequences;
- inner/outer group boundaries preserved versus shuffled input;
- grid sizes around vehicle width and median segment length;
- several hierarchy/BVH leaf sizes; and
- the precision and data layout planned for Unity.

The CLI exposes every current tuning parameter. For example:

```powershell
python -m CollisionDetection.benchmark --cell-size 1 --cell-size 2 --cell-size 4 --workload all
```
