# TrackBuilder TODO

These possible optimizations and simplifications were investigated separately
from the minimum build-parameter contract. They are intentionally deferred.

## Geometry-validation broad phase

Add an epsilon-expanded axis-aligned bounding-box rejection before the exact
calculations in `_segments_are_close`. A read-only prototype preserved output,
passed the complete test suite, and substantially reduced validation time on the
representative mesh and curve scenes.

## Remove obsolete post-refinement validation

`_refine_classified_outlines` revalidates outline contact points, rechecks outline
separation, and reclassifies containment after adaptive curve refinement. The
adaptive implementation now changes only `offset_points`, leaving the already
validated contact points unchanged. Consider deleting `_validate_refined_outline`
and returning the original ground/outer/inner classification after refining the
outer and inner offsets.

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
