# Globally increased curve resolution

This folder is a self-contained experimental TrackBuilder implementation. It does
not import or modify the production implementation in `Blender/TrackBuilder`.

This is the simplest of the three experiments: every smooth Bézier or NURBS
spline is evaluated at sixteen times its authored U resolution for the offset
edge, while matching parameter stations subdivide the authored contact polyline.

## How it works

1. TrackBuilder validates the curve at its authored Blender resolution.
2. It creates an unsaved temporary copy and multiplies both curve and spline U
   resolution by 16.
3. Blender evaluates that temporary curve. The input object and datablock are
   never changed.
4. Increased-resolution parameter stations subdivide the authored evaluated
   source segments; every authored vertex is retained.
5. One stable infinite-line miter is generated from the increased-resolution true
   curve at each station and used for the away-facing edge.
6. Barriers are sliced into material segments by source-outline distance as usual.

The implementation rejects a requested resolution above 1,024 or an evaluated
loop above 20,000 points instead of silently clamping quality.

The supplied issue curves use authored resolution 16, so each is evaluated at
resolution 256 and produces 2,304 samples. The largest measured offset turns are
under 8 degrees. This is smooth, but it demonstrates the cost of resolving two
small high-curvature regions by increasing resolution everywhere.

## Why the barrier remains fitted to the outline

Every source coordinate is either an authored evaluated vertex or a point on one
authored segment. The same coordinates are passed to track/island triangulation
and the barrier source edge; material cuts interpolate along those same segments.

The saved-example maximum source deviation is `0.00000853` Blender units, below
the scene's `0.0000264` float/scale tolerance, with zero error at authored vertices.
Tests verify source points in fill/barrier plans and unchanged input control points
and resolutions.

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
global XY and has no faces. Ordinary transforms and NURBS weights are supported.
`POLY` splines remain linear and are not affected by the resolution multiplier.

The 0.01-degree rule remains unchanged for mesh outlines. It is not applied to
smooth curve samples, because increasing resolution necessarily creates very
small turns in gentle regions. Curve miters use a stable normal-bisector form;
mesh outlines retain the production path and produce golden-identical output.

## Files

- `TrackBuilder.py` — complete increased-resolution implementation.
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
and `track_builder_curve_offset_sample_count` on barriers.

The example parameters are `W=1`, `H=0.1`, `segment_length=5`, and materials
`red`, `white`.

Regenerate the example pair:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilderApproaches\IncreasedCurveResolution\GenerateExamples.py"
```

## Build another file

The input file must follow the documented `Input`-collection and vanilla-curve
contract. From the repository root, this command leaves the input file alone and
saves a built copy:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background "C:\path\TrackInput.blend" --python "Blender\TrackBuilderApproaches\IncreasedCurveResolution\TrackBuilderCLI.py" -- --build --w 1 --height 0.1 --segment-length 5 --materials red white --save "C:\path\TrackOutput.blend"
```

## Run tests

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilderApproaches\IncreasedCurveResolution\TestTrackBuilder.py"
```

The suite covers all ten existing fixtures, production-golden geometry for every
successful mesh-only fixture,
POLY curves, a generated cyclic Bézier outline, the NURBS issue scene, exact
source contact, expected sample counts, the resulting turn bound, materials,
rollback, and unsupported-feature rejection.

## Tradeoffs and known limits

- This has the smallest conceptual and implementation risk, but the highest cost
  on the supplied example: 2,304 points are validated and triangulated per curve.
- Test time is correspondingly much higher than the other variants; the complete
  local suite takes roughly 80–90 seconds on the development machine.
- A fixed multiplier is not a geometric guarantee. A curve with still higher
  curvature or a wider barrier can require another multiplier increase.
- The increased-resolution true offset and subdivided authored contact polyline
  are independent. Local apparent thickness can differ by the authored chord
  error; this is the cost of exact contact without changing the input resolution.
- The requested increased evaluation can exceed the explicit cap even when most
  of the outline is straight.
- Source subdivision preserves the authored perimeter and production segment-count
  meaning.
- Offset self-overlap remains allowed, matching production behavior.
