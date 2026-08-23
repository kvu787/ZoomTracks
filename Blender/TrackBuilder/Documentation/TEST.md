# TrackBuilder testing

TrackBuilder tests keep committed inputs separate from generated test artifacts:

```text
Blender/TrackBuilder/
  TestInputs/                 committed, input-only regression fixtures
  TestArtifacts/              gitignored and replaced on each test run
    Outputs/                  inspectable fixture results
    TestReport.txt            overall run report
```

The integration suite does not use a previous generated output as the expected
result. Expected behavior is encoded in test assertions, fixture metadata, and
golden geometry hashes for successful mesh-only fixtures. Generated test outputs
and the report remain temporary.

## Test files

| File | Purpose |
| --- | --- |
| [`BenchmarkTrackBuilder.py`](../BenchmarkTrackBuilder.py) | Times repeated builds and reports geometry hashes and output counts |
| [`GenerateTrackBuilderSamples.py`](../GenerateTrackBuilderSamples.py) | Generates and synchronizes committed input fixtures without importing or running TrackBuilder |
| [`TestTrackBuilder.py`](../TestTrackBuilder.py) | Runs integration, regression, adaptive-quality, and rollback tests |
| [`TestInputs`](../TestInputs) | Ten committed `.blend` scenes containing `TrackBuilder/Input/Outlines` but no generated `TrackBuilder/Output` |

## Generate committed test inputs

Regenerate fixtures only after a deliberate test-fixture change. From the
repository root, run:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\GenerateTrackBuilderSamples.py"
```

By default, the generator writes ten files to `Blender\TrackBuilder\TestInputs`.
Use `--output-dir` and `--original-sample` after Blender's `--` separator to
override those paths:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\GenerateTrackBuilderSamples.py" -- --output-dir "C:\Temp\TrackBuilderSamples" --original-sample "Blender\TrackBuilder\TestInputs\TrackBuilderSampleInput01_Original.blend"
```

The generator synchronizes fixture filenames and removes obsolete generated
fixtures and Blender backup files. Each scene records its build parameters and
expected result in `track_builder_*` scene custom properties.

Samples 1 through 8 are successful build cases. Sample 9 contains a deliberate
0.005-degree mesh turn and must be rejected. Sample 10 uses a segment length that
would produce one barrier segment and must also be rejected.

## Run tests

From the repository root, run:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\TestTrackBuilder.py"
```

The suite covers:

- Every committed success and expected-failure fixture.
- Required output roles and non-empty mesh geometry.
- Golden geometry for every successful mesh-only fixture.
- The representative smooth-curve performance scene's bounded-error topology
  hash, object count, vertex count, and face count.
- Exact two-pointer station merging against the former rational-union reference,
  including coincident, non-coincident, coprime, authored-denser, and
  reference-denser station counts.
- Blender CDT input-face provenance for concave regions and multiple holes,
  without Python centroid-containment filtering.
- Complete barrier-material sequences and adjusted segment lengths.
- `POLY` curve behavior.
- A generated cyclic Bézier outline.
- The representative NURBS curvature scene.
- Exact preservation of the normally evaluated contact topology.
- Adaptive away-edge chord-error bounds.
- Material-cut behavior with independently sampled ribbon sides.
- Preservation of input curve datablocks, control points, and resolutions.
- Rejection of unsupported curve features.
- Trusted-input treatment of non-adjacent self-intersections, self-touching, and
  cross-outline edge relationships without pairwise edge-distance validation.
- Curve refinement without revalidating or reclassifying unchanged contact
  points.
- Numeric-parameter minimums, material-list, minimum-turn-angle, and one-segment
  validation.
- Required `TrackBuilder/Input/Outlines` discovery, generated output routing,
  existing-output editability, and child-collection validation.
- Transactional preservation of an existing output after rejected builds.
- Bounds-assisted classification of many disjoint inner loops.
- Early-exit distinct-point validation.
- Batched removal of exclusive generated objects, collections, and meshes while
  preserving objects linked outside the removed collection and meshes with
  external users.

## Run the representative performance benchmark

The benchmark opens the requested `.blend`, times only `build_track`, and prints
one machine-readable `TRACK_BUILDER_BENCHMARK=` JSON record. It does not save the
opened file.

From the repository root:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\BenchmarkTrackBuilder.py" -- --blend "Blender\TrackBuilderSandbox\TrackBuilder -- test -- perf issue.blend" --runs 9 --expected-hash ec42161fad649ff3367c47eb4bcce1440c661d2b4fc562f9acf8e72b93ca8649
```

The defaults match that representative scene: `W=1`, `H=0.1`,
`segment_length=5`, and materials `BarrierRed BarrierWhite`. Override them with
`--w`, `--height`, `--segment-length`, and `--materials`. Use `--warmup` only
when intentionally measuring warmed repeated builds.

Performance assertions are deliberately not part of the integration suite;
wall-clock thresholds are machine- and Blender-build-dependent. The suite
asserts the representative geometry oracle, while the benchmark reports timing.
For comparisons intended for publication, use a fresh Blender process per
sample and report the median and dispersion.

## Inspect test artifacts

Every run replaces `Blender\TrackBuilder\TestArtifacts\Outputs` with one
inspectable `.blend` file per committed fixture and overwrites
`Blender\TrackBuilder\TestArtifacts\TestReport.txt` with the complete run and a
per-fixture expected/actual summary.

Successful artifacts contain generated `TrackBuilder/Output/Planes`,
`TrackBuilder/Output/BarrierSegments`, and
`TrackBuilder/Output/OutlineMeshes`. Expected-failure artifacts retain input
geometry without a `TrackBuilder/Output` collection. Every fixture artifact
records `track_builder_actual_result` as a scene custom property.

The entire `TestArtifacts` directory is gitignored. Never commit its contents.
