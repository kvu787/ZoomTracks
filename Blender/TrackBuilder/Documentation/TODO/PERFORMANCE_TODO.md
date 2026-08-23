# TrackBuilder performance history and remaining work

The major algorithmic performance work described by the former version of this
file was implemented on 2026-08-22. See
[`../PERFORMANCE_OPTIMIZATION.md`](../PERFORMANCE_OPTIMIZATION.md) for the final
design, measurements, compatibility notes, commands, and maintenance guidance.

This file now retains the useful historical context and the work that remains.

## Current representative result

- Input: `Blender/TrackBuilderSandbox/TrackBuilder -- test -- perf issue.blend`
- Blender: 4.5.12 LTS
- Parameters: width 1, height 0.1, segment length 5, `BarrierRed` and
  `BarrierWhite`
- Before optimization, nine-process existing-output median: 1.144392 seconds
- Final nine-process median: 0.179479 seconds
- Speedup: approximately 6.38 times
- Current complete-output geometry hash:
  `ec42161fad649ff3367c47eb4bcce1440c661d2b4fc562f9acf8e72b93ca8649`

Treat timings as historical measurements rather than permanent thresholds. Use
`BenchmarkTrackBuilder.py` to rerun them on the current machine and Blender
build.

## Completed work

### Exact integer station merge

`_contact_and_offset_reference` now merges authored and dense normalized
stations with two ordered cursors. Cross-products such as
`authored_index * dense_count` preserve exact rational ordering without
`Fraction` objects, dictionaries, sets, or a global sort.

The test suite retains the former `Fraction` implementation as a test-only
oracle and compares exact coordinates and forced indices for coincident,
non-coincident, coprime, authored-denser, and reference-denser counts.

### Native CDT face provenance

Every CCW boundary loop is now supplied to Blender's constrained Delaunay
triangulator as an input face. TrackBuilder requests origin IDs and retains only
triangles attributed to the outer input face and no overlapping hole face.

This replaces approximately 8.9 million representative-scene Python polygon
edge tests while preserving the former vertices and triangle ordering. The
previous Y-bucket containment prototype is no longer needed as the primary
implementation; it remains a possible fallback only if a future Blender version
breaks the focused CDT provenance tests.

### Authored-interval offset simplification

Every normally evaluated contact station is forced first. Dense away-edge
samples are then simplified independently inside each adjacent authored contact
interval. The contact path in such an interval is linear, so the simplifier
measures only offset chord error.

This deliberately changed smooth-barrier topology while preserving the visual
contract: maximum deviation from the dense away-edge reference remains no more
than `W * 0.001`. The representative curve golden hash was updated deliberately.

### Secondary scaling fixes

- Barrier material segments use bisected contact and offset ranges instead of
  scanning every retained point for every segment.
- Outline bounds reject disjoint containment candidates before ray crossing.
- Segment validation stops as soon as three epsilon-distinct points are found.
- Exclusive generated objects, collections, and meshes are removed in one
  `bpy.data.batch_remove` call. Objects linked outside the removed collection,
  meshes with external users, and fake-user meshes remain preserved.

## Remaining high-value opportunities

### Redesign the per-segment object model

TrackBuilder still creates one Blender Object and Mesh per barrier material
segment. This is inexpensive for the representative 176 barriers but dominates
at thousands of segments. Rebuilding an already-generated 8,910-barrier output
remains much slower than its geometry planning even after bulk deletion.

Any batching design must be coordinated with Unity, which currently discovers
each `Barrier*` GameObject and gives it an individual `BoxCollider`. The most
compatible likely design is one visual barrier mesh per loop plus lightweight
per-segment collider proxies or importer-generated colliders.

### Dependency-aware caching

There is still no unchanged-build or intermediate cache. Possible layers are:

- normally evaluated contact geometry and classification;
- fill triangulation;
- dense smooth-curve evaluation;
- width-dependent dense offsets;
- adaptive offset selection;
- material segmentation; and
- height-only extrusion.

Invalidation must include curve control data, handles, weights, resolution,
transforms, supported evaluation state, materials, numeric parameters, and the
Blender version. A whole-build fingerprint is the simplest useful first step.

### Object/datablock reuse

Repeated builds could create replacement meshes transactionally and then swap
them into stable existing objects. Segment-count changes, rollback, object-name
collisions, shared data, and outside links make this more complex than bulk
deletion.

### Batched temporary curve evaluation

Smooth curves currently create, link, update, evaluate, and remove one temporary
high-resolution copy at a time. Linking all copies before one view-layer update
may save some of the remaining dense evaluation cost. Profile before adding the
additional lifecycle complexity.

## Options tested and not recommended

- Lowering the global dense reference resolution exceeded the `W * 0.001`
  offset-error contract on the representative outer curve.
- Loosening the tolerance produced limited runtime savings for direct visual
  quality loss.
- Broad squared-distance substitutions were only about one percent faster and
  changed selected geometry.
- Python threading does not fit Blender data access and adds coordination cost.
- Uniform arc-length and globally increased curve resolution produce more
  geometry and are quality baselines, not performance improvements.
- Reintroducing exhaustive non-adjacent and cross-outline edge comparisons made
  the historical representative build roughly ten times slower. Keep those
  relationships as trusted input preconditions unless the usage model changes.

## Performance-change checklist

1. Pin the Blender version and record its build hash.
2. Run the complete suite in `TestTrackBuilder.py`.
3. Run `BenchmarkTrackBuilder.py` against the representative scene and assert
   the expected geometry hash.
4. For output-exact work, compare vertices, ordered faces, object metadata, and
   the representative hash.
5. For topology-changing work, preserve the dense-reference error bound, inspect
   the representative output, and update the explicit curve golden hash
   deliberately.
6. Exercise both ordinary and very high segment counts when changing Blender ID
   creation or removal.
7. Never commit `TestArtifacts`.
