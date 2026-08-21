# Uniform arc-length curve sampling

This folder is a self-contained experimental TrackBuilder implementation. It does
not import or modify the production implementation in `Blender/TrackBuilder`.

This approach gives the source edge and away-from-track offset edge independently
uniform spatial resolution. It intentionally spends geometry everywhere in order
to make its behavior easy to predict.

## How it works

For each smooth Bézier or NURBS outline:

1. TrackBuilder validates the authored curve at its normal Blender resolution.
2. It evaluates an unsaved temporary copy at 32 times the authored resolution,
   with a minimum reference resolution of 256 and maximum of 1024.
3. It uniformly subdivides the authored evaluated source polyline at intervals no
   longer than 0.25 world units, while retaining every authored vertex.
4. For a barrier outline, it constructs a dense miter offset on the correct side,
   then independently resamples that offset loop at equal arc-length intervals no
   longer than 0.25 world units.
5. Each offset sample retains its monotonically increasing parameter station,
   mapped onto distance along the authored source polyline. That lets material
   boundaries cut both independently sampled paths at the corresponding place.
6. The uniformly sampled source path is used by both fill triangulation and the
   track-facing barrier edge.

Independent sampling matters. Uniformly sampling only the source would allow a
tight bend to stretch the offset samples again. Uniformly sampling the already
coarse production offset would only subdivide existing chords. This implementation
first creates a dense approximation of the true offset, then resamples it.

The supplied issue scene produces 1,928 source and 1,811 offset samples for the
outer curve, and 1,149 source and 981 offset samples for the inner curve. Every
source and offset edge is at most approximately 0.25 units long.

## Why the barrier remains fitted to the outline

Every authored evaluated vertex is retained, and every additional source point is
interpolated on one authored segment. Those coordinates are shared verbatim by the
track/island fill and source-facing side of every barrier. The independently
sampled offset affects only the away-from-track side.

The saved-example maximum source deviation is `0.00000853` Blender units, below
the scene's `0.0000264` float/scale tolerance, with zero error at every authored
vertex. Tests also verify source samples in fill/barrier plans and unchanged input
curve datablocks and control points.

## Vanilla curve contract

Smooth sampling is deliberately limited to ordinary legacy Blender Curve objects.
A curve is rejected when it has any of the following:

- Modifiers, constraints, or parenting.
- Object/data animation or drivers.
- Shape keys or linked-library data.
- A separate render resolution.
- Curve offset, extrusion, bevel geometry, a bevel object, or a taper object.
- Non-zero control-point tilt or non-default control-point radius.
- More than one spline or a non-cyclic spline.
- Evaluated faces, branches, multiple loops, non-planar world geometry,
  intersections, touching outlines, or another normal TrackBuilder failure.

Both 2D and default 3D curves are accepted when their evaluated loop is flat on
global XY and has no faces. Ordinary object transforms and NURBS weights are
supported. `POLY` splines remain exactly linear and are not resampled.

The production 0.01-degree rule is unchanged for mesh outlines but is not applied
to smooth curve samples. Curve miters use a stable normal-bisector calculation;
mesh objects retain the production miter path and produce golden-identical output.

## Files

- `TrackBuilder.py` — complete uniform implementation.
- `TrackBuilderCLI.py` — standalone command-line wrapper for this implementation.
- `TestTrackBuilder.py` — integration, regression, quality, contact, and validation tests.
- `GenerateTrackBuilderSamples.py` — local copy of the committed-fixture generator.
- `GenerateExamples.py` — regenerates this folder's example pair.
- `TestInputs/` — private copy of all ten production regression inputs.
- `Examples/ResolutionCurvatureIssue_Input.blend` — input-only issue scene.
- `Examples/ResolutionCurvatureIssue_Output.blend` — output built by this approach.

Generated test artifacts go in `TestArtifacts/` and are ignored by Git.

## Inspect the result

Open `Examples/ResolutionCurvatureIssue_Output.blend` in Blender. Generated curve
objects record `track_builder_curve_sampling`, `track_builder_curve_sample_count`,
and, for barriers, `track_builder_curve_offset_sample_count`.

The example parameters are `W=1`, `H=0.1`, `segment_length=5`, and materials
`red`, `white`.

Regenerate the example pair:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilderApproaches\UniformArcLengthSampling\GenerateExamples.py"
```

## Build another file

The input file must follow the documented `Input`-collection and vanilla-curve
contract. From the repository root, this command leaves the input file alone and
saves a built copy:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background "C:\path\TrackInput.blend" --python "Blender\TrackBuilderApproaches\UniformArcLengthSampling\TrackBuilderCLI.py" -- --build --w 1 --height 0.1 --segment-length 5 --materials red white --save "C:\path\TrackOutput.blend"
```

## Run tests

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilderApproaches\UniformArcLengthSampling\TestTrackBuilder.py"
```

The suite covers all ten existing fixtures, production-golden geometry for every
successful mesh-only fixture,
POLY curves, a generated cyclic Bézier outline, the NURBS issue scene, exact
source contact, the 0.25-unit edge bound on both ribbon sides, material sequencing,
transactional rollback, and unsupported-feature rejection.

## Tradeoffs and known limits

- This is simpler to reason about than adaptive sampling but generates much more
  geometry in straight and gentle regions.
- `0.25` is an absolute world-space choice. A production version should probably
  expose it or derive it from project scale.
- The dense true-curve offset and authored contact polyline are independent. Local
  apparent thickness can therefore differ by the authored polyline's chord error;
  this preserves exact visible contact without mutating the input resolution.
- The dense reference remains finite and is capped at 1,024 spline resolution and
  20,000 samples per path.
- The independently sampled paths need source-distance metadata; losing or
  reordering that metadata would break material-boundary correspondence.
- Source subdivision preserves the authored perimeter and production segment-count
  meaning.
- Offset self-overlap remains allowed, matching production behavior.
