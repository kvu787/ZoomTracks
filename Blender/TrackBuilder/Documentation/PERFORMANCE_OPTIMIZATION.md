# TrackBuilder performance optimization hand-off

Date: 2026-08-22  
Branch: `TrackBuilder_PerformanceOptimization`  
Target Blender: 4.5.12 LTS, build `84afd5f785f7`

## Executive summary

This work preserves smooth offset barriers and the existing bounded-error visual
contract. It does **not** switch to authored-resolution offsets. Normally
evaluated contact points remain exact, while the dense away-facing offset stays
within `W * 0.001` chord error.

The representative existing-output build improved from 1.144392 seconds to
0.179479 seconds, about 6.38 times faster. The first two optimizations
were output-exact. The authored-interval simplifier then deliberately changed
only smooth-barrier offset topology while retaining the error bound.

| Stage | Median | Relative to baseline | Geometry |
| --- | ---: | ---: | --- |
| Before this work | 1.144392 s | 1.00x | Historical baseline |
| Native CDT provenance plus exact station merge | 0.415150 s | 2.76x faster | Exact historical hash |
| Add authored-interval offset simplification | 0.205954 s | 5.56x faster | New bounded-error topology |
| Add range, bounds, validation, and cleanup scaling fixes | 0.179479 s | 6.38x faster | Same new topology |

Representative output changes:

| Measurement | Before | After |
| --- | ---: | ---: |
| Objects | 180 | 180 |
| Vertices | 15,026 | 14,236 |
| Faces | 9,849 | 9,454 |
| Retained adaptive offset points | 2,851 | 2,456 |
| Geometry hash | `5a474887...aec` | `eebbde3b...8ed` |

Full hashes:

```text
Historical/output-exact stage:
5a4748879d84732cae2f25062f3ddcc7ededb7131f64880dd39da8a02ceb6aec

Final bounded-error topology:
eebbde3b05254530cf9a1d6a3902e485667896d025f2fdc2625ca42e61c0c8ed
```

The complete output, including its edge-only `OutlineMeshes`, has 183 objects,
16,220 vertices, 9,454 faces, and geometry hash
`ec42161fad649ff3367c47eb4bcce1440c661d2b4fc562f9acf8e72b93ca8649`.

## What changed

### 1. Constrained Delaunay provenance replaces Python face filtering

Previously TrackBuilder gave Blender's constrained Delaunay triangulator only
vertices and edges. Blender correctly produced a convex-hull triangulation, then
Python tested every triangle centroid against every boundary edge to rediscover
the desired outer-minus-holes region. The representative scene performed about
8.9 million Python polygon-edge tests.

TrackBuilder now supplies every normalized CCW loop as an input face, requests
CDT origin IDs, and retains triangles attributed exactly to outer face zero.
Triangles overlapping a supplied hole have additional origin IDs and are
discarded. Blender therefore performs both triangulation and region membership
in native code.

This retained exact vertices, ordered triangles, and geometry hashes for all
existing mesh fixtures and for the representative scene before the deliberate
adaptive topology change.

### 2. Exact integer two-pointer station merge

The former smooth-curve station merge allocated `Fraction` objects for every
authored and dense station, built maps and sets, and globally sorted their union.

Both inputs are already ordered. The new implementation walks them with two
cursors and compares cross-products:

```text
authored_index * dense_count
dense_index * authored_count
```

This retains exact rational ordering and interpolation while avoiding
`Fraction`, hash collections, and sorting. A test-only copy of the old rational
implementation remains as an independent exact oracle for focused regression
tests.

### 3. Adaptive offset simplification is local to authored intervals

The former simplifier globally processed both contact and offset reference
paths, then forced authored contact vertices afterward. Non-authored contact
samples were ultimately discarded even though they contributed substantial
distance-test work.

The new algorithm forces all normally evaluated contact stations first. It then
simplifies dense offset samples independently inside each adjacent authored
contact interval. The contact side is linear inside such an interval, so only
away-edge error is measured.

Important consequences:

- normally evaluated contact coordinates and topology remain unchanged;
- fill geometry remains unchanged by adaptive sampling;
- the away edge remains visually smooth to the same `W * 0.001` bound;
- fewer away-edge vertices are retained; and
- historical smooth-barrier topology is intentionally no longer an invariant.

The committed `ResolutionCurvatureIssue_Output.blend` was regenerated as a
deliberate canonical example update.

### 4. Barrier path slicing uses ordered ranges

Each material segment previously scanned every contact and offset point to find
interior vertices. The new implementation uses binary searches into the already
ordered cumulative-distance arrays and copies only the relevant slices.

This changes the lookup component from approximately
`segments * path_points` to `segments * log(path_points)` plus the points that
are actually emitted. Geometry order and strict boundary inclusion remain the
same.

### 5. Classification and segment validation reject work early

- Precomputed outline bounds reject disjoint representative-point containment
  candidates before ray crossing. This removes the many-island quadratic
  polygon-scan cliff without changing containment semantics.
- Barrier validation now stops after finding three epsilon-distinct points. The
  caller only needs that Boolean and no longer computes a full quadratic unique
  count.

### 6. Generated Blender IDs are removed in one batch

Exclusive generated Objects, Collections, and Meshes are collected and passed
to one `bpy.data.batch_remove` call. The ownership rules remain conservative:

- an object linked to any collection outside the removed tree is preserved;
- a mesh with an external user is preserved;
- a fake-user mesh is preserved; and
- only exclusively owned generated IDs are batched for deletion.

This matters mainly at extreme segment counts. In a direct rebuild comparison
with an existing 8,910-barrier output, the final bulk-removal path completed the
whole rebuild in about 22.8 seconds; restoring the former one-ID-at-a-time
removal made the same rebuild take about 124.9 seconds.

Thousands of independent Blender objects remain intrinsically expensive. The
first build of that stress output was about 3.3 seconds, while replacing an
already existing copy was much slower even with batched cleanup.

## Tests and benchmark tooling

`TestTrackBuilder.py` now additionally covers:

- exact integer station merging against the former rational implementation;
- coincident, non-coincident, coprime, authored-denser, and reference-denser
  station counts;
- CDT provenance on concavity and multiple holes without Python containment;
- the representative smooth-curve output hash and counts;
- interval-local adaptive selection and the existing dense-reference error
  checks;
- bounds-assisted classification of 100 disjoint inner loops;
- early-exit distinct-point validation; and
- batched cleanup with outside-linked objects and externally shared meshes.

`BenchmarkTrackBuilder.py` was added to time `build_track` without file loading,
report every sample plus median/dispersion, compute the output hash, and report
object/vertex/face/role counts. `--expected-hash` turns topology drift into a
failing benchmark run.

Final recorded validation:

- 25 tests passed in 3.682 seconds;
- nine fresh Blender processes produced a 0.179479-second median;
- fresh-process range was 0.176761 to 0.182958 seconds;
- population standard deviation was 0.001832 seconds;
- every benchmark process matched the final geometry hash and output counts; and
- the regenerated example was visually inspected in a colored top-down render,
  including its tightest inner and outer turns.

Correctness test:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\TestTrackBuilder.py"
```

Representative benchmark:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\BenchmarkTrackBuilder.py" -- --blend "Blender\TrackBuilderSandbox\TrackBuilder -- test -- perf issue.blend" --runs 9 --expected-hash ec42161fad649ff3367c47eb4bcce1440c661d2b4fc562f9acf8e72b93ca8649
```

Regenerate the canonical smooth example after a deliberate future topology
change:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\GenerateExamples.py"
```

## Commit organization

The implementation was split into reviewable stages:

1. `488a115 Optimize TrackBuilder exact geometry paths`
   - native CDT provenance;
   - exact integer station merge;
   - focused tests; and
   - benchmark tool.
2. `075c5be Simplify smooth offsets within authored intervals`
   - bounded-error topology redesign;
   - adaptive test update; and
   - regenerated canonical example.
3. `ae97f78 Remove TrackBuilder scaling cliffs`
   - bisected barrier ranges;
   - bounds-assisted classification;
   - early-exit validation;
   - batch ID cleanup; and
   - ownership/scaling tests.
4. Documentation and final representative golden coverage are kept in the final
   hand-off commit.

## Compatibility and maintenance notes

- Keep Blender 4.5.12 pinned when comparing hashes. Curve evaluation and CDT
  behavior can vary across Blender releases.
- The old smooth-curve hash is still useful for identifying the output-exact
  intermediate stage, but it is no longer the final oracle.
- Do not replace exact cross-product station comparisons with approximate
  floating-point station sorting.
- CDT face-origin behavior has focused coverage. If a Blender upgrade breaks it,
  investigate that API change before restoring Python centroid filtering.
- Do not simplify across authored contact intervals; doing so weakens the reason
  contact-side error can be omitted.
- The adaptive-quality test checks all dense reference samples against the
  retained away edge. Preserve that test for any future simplifier rewrite.
- Input curve data and authored resolutions remain untouched.
- Unity-side object/collider behavior is unchanged by this work.

## Deliberately not implemented

- Authored-resolution-only offsets: rejected because the requested visual
  contract requires a smooth away-facing silhouette.
- Geometry or output caching: useful for repeated identical/parameter-only
  builds, but invalidation requires a separate design.
- Barrier visual-object batching: likely the next large scaling win, but it must
  be coordinated with Unity's current one-GameObject/one-BoxCollider-per-segment
  workflow.
- Geometry Nodes, compiled extension, or Unity-side replacement: unnecessary
  for the current approximately 6.38-times algorithmic win.
- Changes outside `Blender/TrackBuilder`: none were made.
