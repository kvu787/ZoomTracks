# TrackBuilder performance TODO

This work is intentionally deferred. The current implementation is correct and
well tested; do not take on the output or maintenance risk until performance is
again a priority.

## Benchmark context

- Representative input:
  `Blender/TrackBuilderSandbox/TrackBuilder -- test -- perf issue.blend`
- Blender: 4.5.12, background mode
- Track parameters: width 1, height 0.1, segment length 5, red and white
  barriers
- Fresh `build_track` median: approximately 1.221 seconds
- Representative geometry hash:
  `5a4748879d84732cae2f25062f3ddcc7ededb7131f64880dd39da8a02ceb6aec`

Treat these timings as historical measurements, not permanent targets. Rerun
the baseline before making changes because Blender, Python, hardware, and the
input file may have changed.

## Priority 1: replace Fraction-based station merging

Risk: low. Expected output change: none.

`_contact_and_offset_reference` currently creates `Fraction` objects, builds
maps and sets, and globally sorts the union of authored and dense stations. On
the representative file, the authored stations already coincide with dense
stations because the dense count is 16 times the authored count, so the union
adds no stations.

Historical breakdown of adaptive refinement:

| Work | Time | Share |
| --- | ---: | ---: |
| Dense curve evaluation | 0.075 s | 12.9% |
| Dense offset/miter generation | 0.040 s | 6.9% |
| Station merge | 0.223 s | 38.4% |
| Adaptive selection | 0.238 s | 41.0% |
| Other | 0.005 s | 0.8% |

Replace the union with an exact rational two-pointer merge. Compare
`authored_index * dense_count` with `dense_index * authored_count`, avoiding
floating-point comparisons as well as `Fraction` allocation and global sorting.

Prototype result:

- station merge: 0.222 to 0.020 seconds
- adaptive refinement: 0.579 to 0.369 seconds
- full build: 1.210 to 1.014 seconds
- identical representative geometry hash
- all 19 TrackBuilder tests passed

Acceptance criteria:

- Preserve the representative geometry hash.
- Pass the complete TrackBuilder test suite.
- Add focused tests for coincident and non-coincident station counts, including
  coprime counts and both possible ordering directions.
- Keep exact integer comparisons; do not substitute approximate floats.

## Priority 2: spatially index polygon edges for face filtering

Risk: moderate engineering and edge-case risk. Expected output change: none.

Explicit triangulation should remain. The expensive part is the subsequent
centroid-in-outline filtering: every candidate triangle repeatedly scans the
entire outline in `_point_in_polygon`.

Historical breakdown:

- complete triangulation and filtering phase: approximately 0.562 seconds
- Track region: 0.390 seconds
- Ground region: 0.134 seconds
- two island regions: 0.019 and 0.017 seconds
- `_point_in_polygon` calls: 14,271

Prototype an exact vertical Y-bucket index. Insert every polygon edge into each
bucket overlapped by its Y range, then test a triangle centroid only against the
edges in its bucket while retaining the existing ray-crossing formula.

Prototype result:

- triangulation and filtering: 0.562 to 0.028 seconds
- full build with this change alone: 1.215 to 0.676 seconds
- full build with Priority 1 as well: 1.215 to 0.468 seconds
- identical representative geometry hash
- all 19 TrackBuilder tests passed

Design and acceptance requirements:

- Keep the existing full scan as a clear reference implementation and fallback.
- Use the index only above a measured polygon-size threshold.
- Fall back when bucket density makes the index unhelpful.
- Preserve the exact crossing and boundary semantics of the current code.
- Test points on and immediately around bucket boundaries, polygon min/max Y,
  horizontal edges, vertices, long edges spanning many buckets, narrow shapes,
  and degenerate input.
- Measure index construction and memory, not just query time.
- Preserve the representative geometry hash and pass the complete suite.

The main downsides to manage are added implementation complexity, more boundary
cases, overhead on small polygons, memory duplication for edges spanning many
buckets, and poor performance on pathological shapes. A small-input fallback
and a density fallback keep those costs bounded.

## Priority 3: simplify independently within authored intervals

Risk: higher. Expected output change: yes, while remaining within the current
error tolerance.

Current adaptive selection globally simplifies the contact and offset paths and
then unions the forced authored vertices into the result. The representative
build performed about 610,374 point-to-segment distance calculations.

A more direct algorithm would force every authored station first, then simplify
only the offset samples inside each interval between adjacent authored contact
vertices. The contact path inside such an interval is linear, so only the offset
chord error needs to be measured.

Prototype result when combined with Priority 1:

- adaptive refinement: approximately 0.586 to 0.155 seconds
- retained offset points: 2,851 to 2,456
- output vertices: 15,026 to 14,236
- output faces: 9,849 to 9,454
- changed geometry hash:
  `eebbde3b05254530cf9a1d6a3902e485667896d025f2fdc2625ca42e61c0c8ed`
- all 19 tests passed

Measured maximum deviation from the dense reference remained within the 0.001
tolerance:

| Curve | Current | Prototype |
| --- | ---: | ---: |
| Outer NURBS curve | 0.00099824 | 0.00099505 |
| Inner NURBS curve | 0.00099738 | 0.00099797 |
| Circle | 0.00018882 | 0.00018882 |

Because this changes topology, implementation requires deliberate visual review
in material preview and rendered viewport modes, output comparison, and explicit
regeneration of any canonical example output. Keep it separate from the two
output-exact optimizations above.

## Longer-term possibilities

- Cache dense curve evaluation, potentially avoiding about 0.075 seconds on a
  repeated unchanged build. Define reliable invalidation for curve geometry and
  relevant settings first.
- Cache dense offset/miter results when width and curve data are unchanged,
  potentially avoiding about 0.040 seconds. Invalidation complexity is the main
  concern.
- Investigate a per-curve adaptive reference-resolution strategy. A single lower
  global reference resolution is not safe.
- Slice barrier paths using cursors or binary search rather than repeatedly
  scanning them.
- Add an early exit to `_distinct_point_count` and merge segment-failure checks
  where profiling shows value.

## Options already tested and not recommended

- Lowering the global dense reference resolution: resolutions below 1,024
  exceeded the current 0.001 error tolerance on the representative outer curve.
- Loosening the tolerance: even a tenfold change produced only a small runtime
  improvement while directly reducing output quality.
- Replacing distance comparisons with squared-distance comparisons: the
  prototype was only about 1% faster and changed two vertices and one face.
- Threading this Python work: coordination and Blender data-access constraints
  are unlikely to repay the added complexity before the algorithmic work above.

## Resume checklist

1. Confirm the representative input and its expected output are still valid.
2. Record a fresh median baseline and phase-level profile.
3. Implement one optimization at a time, in the priority order above.
4. For output-exact work, compare the representative geometry hash before and
   after the change.
5. Run the complete TrackBuilder suite after every behavior-affecting change:

   ```powershell
   & "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\TestTrackBuilder.py"
   ```

6. Inspect the generated results in `Blender/TrackBuilder/TestArtifacts`; never
   commit that gitignored directory.
7. For topology-changing work, review the representative result visually and
   regenerate canonical output only as a deliberate fixture change.
