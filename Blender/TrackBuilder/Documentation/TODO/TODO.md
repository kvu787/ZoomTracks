# TrackBuilder TODO

## Correct smooth-curve station phase alignment

`_contact_and_offset_reference` assumes the normally evaluated contact loop and
the dense reference loop have the same normalized station zero. Both loops are
canonicalized independently to their lowest sampled XY vertex, however, and the
dense loop is then rotated only to the dense vertex nearest the first contact
vertex. When the authored and reference evaluation counts are not integer
multiples, that nearest vertex is not the same curve parameter. The subsequent
exact rational merge therefore shifts every dense offset station along the
curve.

A formerly supported rotated cyclic Bézier with authored resolution 7 produced
56 contact points and 2,048 reference points. With `barrier_width=1`, the first
generated offset station differed by about `0.00691` from the dense offset
interpolated at the corresponding contact station, exceeding the documented
`0.001` adaptive-error limit. The same phase-alignment risk applies to supported
NURBS splines. The build otherwise completed successfully.

Preserve a shared spline-parameter origin through normal and dense evaluation,
or carry the measured fractional phase through the station merge. Add an
integration regression using a rotated NURBS curve whose authored resolution
does not divide the chosen reference resolution. The test must compare offset
stations to the corresponding dense source stations rather than to the current
legacy rational-union implementation, which has the same phase assumption.

## Make construction rollback remove every temporary datablock

`_instantiate_plans` creates its pending collections before entering its
`try` block, and creates each mesh before an object linked beneath the pending
hierarchy exists. If collection setup, `mesh.from_pydata`, material assignment,
mesh update, object creation, or object linking fails at the wrong point,
`_remove_collection_tree(pending)` cannot reach every newly created datablock.

An in-memory failure injected into `mesh.from_pydata` removed all pending
collections but left the new mesh as an orphan in `bpy.data.meshes`, contrary to
the documented rollback guarantee. Track the current mesh and object explicitly
until they are linked, cover collection setup with the same cleanup boundary,
and add failure-injection tests that assert no new collections, objects, meshes,
or curves remain after each construction-stage failure.

## Scope fixture cleanup to TrackBuilder/Output

`GenerateTrackBuilderSamples._remove_output_collection` looks up the file-wide
collection named `Output` instead of resolving the direct `Output` child of the
`TrackBuilder` collection. An original sample containing an unrelated top-level
`Output` collection therefore has that collection and its objects deleted even
when TrackBuilder has no generated output.

Resolve `TrackBuilder/Output` through the required hierarchy before removing
anything. Either preserve objects and datablocks linked outside that subtree as
the production removal path does, or reject such sharing explicitly. Add a test
with an unrelated `Output` collection and another with an output object linked
to an external collection.

## Include edge topology in geometry hashes

The geometry-hash implementations in `TestTrackBuilder.py` and
`BenchmarkTrackBuilder.py` hash object names, materials, custom properties,
vertices, and polygons, but not mesh edges. `OutlineMeshes` objects contain only
vertices and edges, so their connectivity is invisible to the golden and
benchmark hashes. Removing every edge from an outer outline mesh currently
leaves the reported hash unchanged.

Hash normalized edge endpoint pairs in both implementations and add a mutation
test proving that changing or removing an outline edge changes the hash. Keep
the existing explicit outline-connectivity test as a more diagnostic assertion.

## Add optional pairwise validation for untrusted inputs

If TrackBuilder gains more users, accepts imported or otherwise untrusted input,
runs unattended, or encounters failures caused by intersecting outlines,
consider validating:

- non-adjacent edges within each outline for self-intersection and
  self-touching; and
- edges belonging to different outlines for touching or intersection.

Use a spatial index, sweep-line algorithm, or another subquadratic broad phase
instead of exhaustive pair iteration.

## Revalidate contact geometry if refinement starts changing it

If refinement is expanded to modify contact points or containment roles,
revalidate and reclassify outlines after refinement. Offset-only refinement does
not need this additional pass.

## Reconsider the basis for material-segment cuts

Investigate whether smooth-curve material cuts should use distance along the
independently sampled offset boundary when it has higher resolution, rather than
distance along the normally evaluated contact boundary. Define the desired
visual behavior before changing the segmentation contract.

## Simplify or justify the curve reference-resolution settings

Investigate whether the fixed reference-resolution multiplier and minimum and
maximum reference resolutions can be derived more directly from the offset-error
contract and curve characteristics. If the fixed settings remain preferable,
document their rationale and supported range.

## Simplify segment-count failure handling

Consider whether the one-segment and incomplete-material-cycle failures in
`_segment_count` should share one branch without making the error less useful.
