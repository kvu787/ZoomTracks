# TrackBuilder TODO

These possible validations, optimizations, and simplifications are intentionally
deferred.

## Revisit trusted pairwise edge relationships

TrackBuilder intentionally does not compare non-adjacent edges within an outline
or edges belonging to different outlines. Self-intersection, self-touching, and
cross-outline touching/intersection are trusted input preconditions. Violations
may produce a committed unexpected result without raising an exception.

The exhaustive checks were removed for the current single-user, trusted-input
workflow. On `TrackBuilderSandbox/TrackBuilder -- test -- perf issue.blend`, the
original implementation built in about 12.15 seconds, while removing pairwise
edge checks and redundant post-refinement validation reduced the median to about
1.19 seconds without changing the output geometry hash.

Revisit this decision when TrackBuilder gains another user, accepts imported or
otherwise untrusted input, runs unattended in batch workflows, or encounters a
real failure caused by intersecting outlines. Prefer a spatial index, sweep-line
algorithm, or similarly subquadratic broad phase. A simpler epsilon-expanded
axis-aligned bounding-box rejection preserved output and greatly reduced runtime
in a read-only prototype, but retaining all quadratic pair iteration was still
measurably slower than the trusted-input implementation.

## Revisit post-refinement validation if contact points can change

`_refine_classified_outlines` no longer revalidates or reclassifies outlines.
Adaptive refinement changes only `offset_points`; contact `points` and their
ground/outer/inner roles remain unchanged. Reintroduce appropriate validation if
future refinement starts modifying contact points or containment roles.

## Simplify adaptive selection by authored edge

Every authored contact vertex is forced into the final result, and the contact
path between adjacent authored vertices is linear. Consider simplifying each
authored interval independently and measuring only offset chord error. A
read-only prototype passed the complete suite while selecting fewer offset
samples on the curvature issue scene. This deliberately changes generated
geometry and would require regenerating the canonical example output.

## Lower-priority opportunities

- Slice barrier contact and offset paths with monotonic cursors or bisected
  ranges instead of scanning every retained point for every material segment.
- Use squared distances for comparisons that do not require an actual distance.
- Replace `_distinct_point_count` with an early-exit check that stops after three
  distinct points.
- Consider merging the one-segment and incomplete-material-cycle failures in
  `_segment_count`; the current separate branch provides a more specific error.
