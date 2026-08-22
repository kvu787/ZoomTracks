# TrackBuilder TODO

These possible validations and architectural changes are intentionally deferred.

The completed 2026 performance work, measurements, and remaining performance
limits are documented in
[`../PERFORMANCE_OPTIMIZATION.md`](../PERFORMANCE_OPTIMIZATION.md). Historical
prototypes and rejected options remain in [`PERFORMANCE_TODO.md`](PERFORMANCE_TODO.md).

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

## Lower-priority opportunities

- Use squared distances for comparisons that do not require an actual distance.
- Consider merging the one-segment and incomplete-material-cycle failures in
  `_segment_count`; the current separate branch provides a more specific error.
