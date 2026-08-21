# Historical TrackBuilder curve-sampling approaches

These folders retain the end-to-end experiments used to compare strategies for
improving smooth-curve barrier silhouettes. Offset-aware adaptive sampling was
selected and promoted to the canonical [`Blender/TrackBuilder`](../TrackBuilder)
tool. The canonical implementation and its documentation are authoritative.

| Folder | Supplied-example offset samples | Quality rule |
| --- | ---: | --- |
| `AdaptiveCurveSampling` | 351 outer / 327 inner | Offset-aware chord error |
| `UniformArcLengthSampling` | 1,928 outer / 1,149 inner | Source and offset edges at most 0.25 units |
| `IncreasedCurveResolution` | 2,304 per smooth curve | 16 times authored U resolution |

Each experiment is self-contained with its own builder, CLI, tests, fixture
copies, generator, documentation, and inspectable `.blend` pair. They remain
useful as implementation history and comparison baselines, but should not be
used as the project TrackBuilder entry point.

All three preserve Blender's normally evaluated contact boundary and derive the
away-facing edge from a denser evaluation of the mathematical spline. The
strategies primarily differ in how they reduce that dense representation:

- **Adaptive** measures the away-edge silhouette directly and retains only the
  stations required by its chord-error tolerance. This is now canonical.
- **Uniform arc length** provides a straightforward fixed-spacing quality
  baseline but spends geometry in low-curvature regions.
- **Increased resolution** follows Blender's tessellation controls most directly
  but has no geometric error guarantee and produces the densest result for the
  supplied issue scene.
