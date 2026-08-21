# Offset-aware adaptive curve sampling

This folder is a self-contained experimental TrackBuilder implementation. It does
not import or modify the production implementation in `Blender/TrackBuilder`.

The approach treats the source curve and the away-from-track offset as a ribbon,
then retains samples only where either side needs them. It is intended to preserve
the current infinite-offset-line/miter meaning while putting geometry in the
regions where the generated silhouette would otherwise become faceted.

## How it works

For each smooth Bézier or NURBS outline:

1. TrackBuilder validates the curve at its normal Blender resolution and records
   that evaluated polyline as the immutable contact boundary.
2. It evaluates an unsaved temporary copy at 32 times the authored resolution,
   with a minimum reference resolution of 256 and maximum of 1024.
3. For a barrier outline, it constructs a dense miter offset on the correct side:
   right of the CCW outer outline and left of each CCW inner outline.
4. Dense parameter stations are paired with points that lie on the authored
   contact segments. Every authored contact vertex is forced into the result.
5. A closed, paired simplifier checks the constrained source path and dense offset.
   It subdivides until the maximum chord deviation is no greater than
   `max(0.0001, W * 0.005)` and the accumulated turn per retained interval is no
   greater than 5 degrees.
6. The retained source path is used by both the fill triangulation and the
   track-facing side of every barrier. The retained offset path is used only for
   the away-from-track side.
7. Material segments are sliced after sampling, using distance along the retained
   source outline exactly as production TrackBuilder does.

The supplied issue scene produces 364 samples on the outer curve and 338 on the
inner curve, from 4,608-point dense references. The measured largest turn on the
retained offset paths is approximately 6.8 degrees because two accepted intervals
meet at a retained station.

## Why the barrier remains fitted to the outline

The source path contains every authored evaluated vertex, plus optional points
interpolated on individual authored segments. No result edge can skip an authored
corner, and no source point comes from the higher-resolution mathematical curve.
The track/island triangulation and barrier source edge then consume this same list.

The saved-example audit measures a maximum source-edge deviation of
`0.00000853` Blender units, below the scene's `0.0000264` float/scale tolerance;
every authored vertex is present with zero measured error. Tests also check every
sampled source point is present in both fill and barrier plans and that input curve
datablocks, resolutions, and control points remain unchanged.

## Vanilla curve contract

Smooth sampling is deliberately limited to ordinary legacy Blender Curve objects.
A curve is rejected when it has any of the following:

- Modifiers, constraints, or parenting.
- Object/data animation or drivers.
- Shape keys or linked-library data.
- A separate render resolution.
- Curve offset, extrusion, bevel geometry, a bevel object, or a taper object.
- Non-zero control-point tilt or non-default control-point radius.
- More than one spline or a non-cyclic spline (the normal TrackBuilder rule).
- Evaluated faces, branches, multiple loops, non-planar world geometry,
  intersections, touching outlines, or the other normal TrackBuilder failures.

Both 2D and default 3D curves are accepted as long as the evaluated loop is flat
on global XY and has no faces. Ordinary object transforms and NURBS weights are
supported. `POLY` splines remain exactly linear and are not resampled.

The 0.01-degree authored-vertex rule remains unchanged for mesh objects. It is not
applied to smooth curve samples because legitimate refinement necessarily creates
near-collinear points. Curve miters use a numerically stable normal-bisector form;
mesh miters retain the production code path.

## Files

- `TrackBuilder.py` — complete adaptive implementation.
- `TrackBuilderCLI.py` — standalone command-line wrapper for this implementation.
- `TestTrackBuilder.py` — integration, regression, quality, contact, and validation tests.
- `GenerateTrackBuilderSamples.py` — local copy of the committed-fixture generator.
- `GenerateExamples.py` — regenerates this folder's inspectable example pair.
- `TestInputs/` — private copy of all ten production regression inputs.
- `Examples/ResolutionCurvatureIssue_Input.blend` — input-only issue scene.
- `Examples/ResolutionCurvatureIssue_Output.blend` — output built by this approach.

Generated test artifacts go in `TestArtifacts/` and are ignored by Git.

## Inspect the result

Open `Examples/ResolutionCurvatureIssue_Output.blend` in Blender. The generated
objects retain the original Input collection and record these custom properties:

- `track_builder_curve_sampling`
- `track_builder_curve_sample_count`
- `track_builder_curve_offset_sample_count` on barriers

The example parameters are `W=1`, `H=0.1`, `segment_length=5`, and materials
`red`, `white`.

Regenerate the input/output pair from the repository root:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilderApproaches\AdaptiveCurveSampling\GenerateExamples.py"
```

## Build another file

The input file must follow the documented `Input`-collection and vanilla-curve
contract. From the repository root, this command leaves the input file alone and
saves a built copy:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background "C:\path\TrackInput.blend" --python "Blender\TrackBuilderApproaches\AdaptiveCurveSampling\TrackBuilderCLI.py" -- --build --w 1 --height 0.1 --segment-length 5 --materials red white --save "C:\path\TrackOutput.blend"
```

## Run tests

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilderApproaches\AdaptiveCurveSampling\TestTrackBuilder.py"
```

The suite covers all ten existing fixtures, production-golden geometry for every
successful mesh-only fixture,
POLY curves, a generated cyclic Bézier outline, the NURBS issue scene, source
contact, adaptive quality bounds, material sequencing, transactional rollback,
and rejection of the unsupported curve features listed above. Each run writes a
report and inspectable fixture outputs under `TestArtifacts/`.

## Tradeoffs and known limits

- This is the most geometry-efficient of the three experiments, but also the most
  algorithmically complex.
- The quality tolerance is derived from barrier width rather than exposed through
  the public API. A production version should decide whether to expose it.
- The away edge retains the high-resolution true-curve miter. Because the contact
  edge is constrained to Blender's lower-resolution authored polyline, apparent
  local thickness can differ by the authored curve's chord error. This is the
  explicit cost of exact visible contact without modifying the input curve.
- The dense reference is still a finite Blender evaluation. The hard resolution
  and 20,000-point limits turn pathological inputs into explicit errors.
- Subdivision preserves the authored source perimeter, so material segment counts
  retain production's source-outline meaning.
- Offset self-overlap remains allowed, matching production semantics. This is not
  a polygon-buffer/boolean cleanup implementation.
