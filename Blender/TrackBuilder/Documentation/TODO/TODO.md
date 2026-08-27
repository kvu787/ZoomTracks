# TrackBuilder TODO

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
