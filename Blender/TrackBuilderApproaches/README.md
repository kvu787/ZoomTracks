# TrackBuilder curve-sampling approaches

These are three independent, end-to-end experiments for improving smooth-curve
barrier silhouettes without changing production `Blender/TrackBuilder`.

| Folder | Supplied-example source samples | Quality rule | Complete test time |
| --- | ---: | --- | ---: |
| `AdaptiveCurveSampling` | 364 outer / 338 inner | Offset-aware error plus 5-degree interval turn | about 5 seconds |
| `UniformArcLengthSampling` | 1,928 outer / 1,149 inner | Source and offset edges at most 0.25 units | about 28 seconds |
| `IncreasedCurveResolution` | 2,304 per smooth curve | 16 times authored U resolution | about 86 seconds |

Each folder contains its own builder, CLI, tests, copied test inputs, fixture
generator, example generator, README, and inspectable input/output `.blend` pair.
There is no shared runtime module between the approaches.

All three constrain the track-facing barrier edge to the authored evaluated input
polyline: every authored vertex is retained, and added source stations only
subdivide an existing authored segment. The generated fill uses the same contact
coordinates. Mesh-outline output remains golden-identical to production, and all
three enforce the same deliberately narrow vanilla-curve contract.

The away-facing edge is still computed from a dense evaluation of the mathematical
curve using the same infinite-line miter construction. Keeping that smooth offset
while fitting the visibly evaluated input polyline means the two sides are sampled
independently. Consequently, local ribbon thickness relative to a coarse authored
chord can differ by that curve evaluation's own chord error. Avoiding that tradeoff
would require changing the input curve's authored resolution or leaving the offset
as faceted as the authored polyline; these experiments leave the input untouched
and prioritize exact visible contact.

## Choosing among them

- **Adaptive** is the strongest production candidate for the stated priorities.
  It measures the silhouette that matters, keeps the existing miter-offset
  interpretation, and uses far less geometry than the other two on the issue
  scene. Its cost is the most complicated implementation and a tolerance policy
  that must eventually become a deliberate product setting.
- **Uniform arc length** is the clearest quality baseline. Its 0.25-unit edge
  ceiling is easy to inspect and reason about on both ribbon edges, but it spends
  many samples in low-curvature regions and assumes a particular world scale.
- **Increased resolution** is the lowest-conceptual-risk fallback. It stays close
  to Blender's normal tessellation path, but has no geometric error guarantee and
  is the slowest and densest result for this example.

The adaptive variant is therefore the sensible first one to evaluate visually;
the uniform variant is useful as a high-quality reference, while the global
variant provides a simple implementation baseline.
