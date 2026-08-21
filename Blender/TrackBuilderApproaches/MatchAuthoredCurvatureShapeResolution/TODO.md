# Match authored curvature, shape, and resolution

Status: design exploration only; no implementation exists in this folder yet.

Recorded against repository commit `043c6e89ae9d3b931604e782d7a455a2a7910d44`
on 2026-08-21.

## Clarified goal

The generated away-from-track barrier offset should visually correspond well to
the normally evaluated authored outline in all three respects:

- Shape: it should not silently reconstruct a materially different silhouette.
- Curvature: authored sharpness or softness should remain recognizable rather
  than being spread across substantially more samples.
- Resolution: the output should have a visibly comparable sampling density to
  the authored Blender evaluation.

The authored outline is therefore the visual authority. This differs from the
current `AdaptiveCurveSampling` premise, where the underlying mathematical
spline is the authority for the away edge and only the contact edge is kept on
the authored evaluated polyline.

An important question to settle before implementation is whether "correspond
well" means exact discrete correspondence or permits bounded visual departure.
Start with exact correspondence as the baseline; do not invent a departure
budget implicitly.

## Why the current adaptive result looks too smooth

`AdaptiveCurveSampling/TrackBuilder.py` currently:

1. Evaluates the authored curve normally for the contact edge.
2. Evaluates a temporary copy at 32 times the authored U resolution, with a
   minimum reference resolution of 256 and maximum of 1024.
3. Computes a dense miter offset from that high-resolution mathematical curve.
4. Simplifies the dense offset to a chord error of
   `max(0.0001, barrier_width * 0.005)`.
5. Forces each authored parameter station into the result, but uses the
   high-resolution offset position at that station.

Consequently, the high-resolution curve is a new shape reference rather than
just a source of harmless subdivisions. Raising the chord-error tolerance can
reduce the point count, but it does not restore the authored offset shape because
forced authored stations still receive high-resolution offset coordinates.

Relevant locations in the current experiment:

- Constants: `AdaptiveCurveSampling/TrackBuilder.py:27-32`
- Reference resolution: `AdaptiveCurveSampling/TrackBuilder.py:505-512`
- Dense offset and adaptive selection: `AdaptiveCurveSampling/TrackBuilder.py:751-800`
- Existing authored-point fallback in barrier planning:
  `AdaptiveCurveSampling/TrackBuilder.py:1091-1096`
- Tests that explicitly judge quality against the high-resolution offset:
  `AdaptiveCurveSampling/TestTrackBuilder.py:335-365`

## Measurements from the supplied issue example

Input:
`AdaptiveCurveSampling/Examples/ResolutionCurvatureIssue_Input.blend`

Parameters: barrier width 1, height 0.1, segment length 5, materials red/white.
Each smooth authored outline has U resolution 16 and evaluates to 144 points.
The current reference resolution is 512 and evaluates to 4,608 points.

| Measurement | Outer | Inner |
| --- | ---: | ---: |
| Authored evaluated samples | 144 | 144 |
| Current adaptive offset samples | 351 | 327 |
| Maximum authored-source turn | 85.562 deg | 89.940 deg |
| Maximum authored-resolution offset turn | 85.562 deg | 89.940 deg |
| Maximum current adaptive offset turn | 9.744 deg | 10.473 deg |
| Maximum dense-offset deviation from authored-resolution offset | 0.2612 | 0.2758 |
| Current maximum dense-reference approximation error | 0.0050 | 0.0049 |

The normal miter offset of the authored polyline has effectively the same
discrete turn sequence as its source because corresponding offset edges remain
parallel to the authored edges. The adaptive result instead spreads the sharp
authored regions over many lower-angle turns.

A read-only resolution sweep using the existing algorithm and the same 0.005
error factor produced:

| Reference multiplier | Outer samples / max turn | Inner samples / max turn |
| ---: | ---: | ---: |
| 1x | 144 / 85.6 deg | 144 / 89.9 deg |
| 2x | 259 / 50.4 deg | 272 / 54.2 deg |
| 4x | 338 / 26.6 deg | 316 / 29.0 deg |
| 8x | 345 / 13.5 deg | 325 / 14.8 deg |
| 16x | 353 / 8.8 deg | 329 / 9.7 deg |
| 32x (current) | 351 / 9.7 deg | 327 / 10.5 deg |

This sweep is useful for visual comparison, but selecting a smaller multiplier
alone does not establish the new semantic contract.

## Recommended first baseline

Implement and inspect a strict authored-resolution baseline before designing a
new adaptive rule:

- Treat `outline.points` (the normal evaluated Blender curve) as the complete
  source of offset stations.
- Generate one stable infinite-line miter per authored point using
  `_stable_curve_offset_points(outline.points, width, offset_left)`.
- Keep the source and offset paths in one-to-one cyclic correspondence.
- Permit only material-boundary slicing to add vertices after this step.
- Do not use a hidden high-resolution curve evaluation for the offset.
- Do not change the input curve datablock or its authored resolution.

The barrier planner already follows this path when `outline.offset_points` is
`None`, so much of the current outline validation, classification, material
slicing, extrusion, and transactional output logic can be reused. The main
behavioral change is localized to how smooth-curve `offset_points` are produced.

Under the strict interpretation, the adaptive feature intentionally disappears:
subdividing the authored miter edges without moving them adds topology but cannot
improve the visible silhouette. Any visually meaningful smoothing necessarily
changes authored shape or curvature and therefore requires an explicit bounded
departure in the specification.

## If bounded adaptation is still desired

Do not resume the existing 32x rule without first choosing measurable limits.
Potential limits to explore include:

- Maximum geometric displacement from the authored-resolution miter outline.
- Maximum output-to-authored sample-count ratio.
- Maximum change in local turn-angle distribution.
- A ban on smoothing across authored stations that represent visible sharp
  features.
- A rule derived from authored chord error or authored local angular resolution,
  rather than solely from barrier width.

Evaluate any such design against the strict baseline. A useful adaptive rule
must explain both where added samples are allowed and how far their positions may
depart from the authored-resolution offset.

## Tentative validation criteria

For the strict baseline:

- Offset sample count equals the authored evaluated sample count.
- Each offset edge is parallel to its corresponding authored edge within a
  scale-aware tolerance.
- The cyclic discrete turn-angle sequence matches the authored outline.
- All authored contact vertices remain present in track/island fill and barrier
  contact plans.
- Material cuts remain the only permitted extra contact or offset vertices.
- Changing the authored curve resolution predictably changes output resolution.
- Input curve datablocks, control points, transforms, and resolutions remain
  unchanged.
- Mesh-only fixtures remain golden-identical to production behavior.
- Existing validation and transactional rollback behavior remains intact.

For a future bounded-adaptive variant, add explicit assertions for each chosen
departure budget rather than testing against an effectively arbitrary dense
reference.

## Suggested next session

1. Reconfirm whether exact authored correspondence or bounded departure is the
   intended product behavior.
2. Generate a strict authored-resolution output beside the existing adaptive
   example and compare them in Blender from the same views.
3. If strict correspondence is visually acceptable, implement that smaller
   design and remove unused dense-reference machinery from this approach.
4. If it is not acceptable, identify the specific objectionable regions and
   define numerical departure limits before writing another adaptive selector.
5. Add focused tests for shape, turn sequence, and sample-count correspondence.
6. After any implementation affecting TrackBuilder output or tests, run this
   approach's test suite and the production TrackBuilder validation required by
   `Blender/TrackBuilder/Documentation/TEST.md`. Keep generated test artifacts
   uncommitted.

## Documentation housekeeping noticed during investigation

`Blender/TrackBuilderApproaches/README.md` still describes the removed
five-degree accumulated-turn rule and old adaptive sample counts. The current
`AdaptiveCurveSampling/README.md` reports the newer 351/327 counts. Update the
overview when these experiments are next documented.
