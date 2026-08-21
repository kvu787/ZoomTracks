# TrackBuilder testing

TrackBuilder tests keep committed inputs and examples separate from generated
test artifacts:

```text
Blender/TrackBuilder/
  Examples/                   committed curve-sampling input and output
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
| [`GenerateTrackBuilderSamples.py`](../GenerateTrackBuilderSamples.py) | Generates and synchronizes committed input fixtures without importing or running TrackBuilder |
| [`TestTrackBuilder.py`](../TestTrackBuilder.py) | Runs integration, regression, adaptive-quality, and rollback tests |
| [`TestInputs`](../TestInputs) | Ten committed `.blend` scenes containing `Input` but no generated `Output` |
| [`GenerateExamples.py`](../GenerateExamples.py) | Regenerates the committed adaptive-sampling example pair |
| [`Examples`](../Examples) | Input-only curvature issue scene and its inspectable canonical output |

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

## Regenerate the committed example

The example generator reads `Blender\ResolutionCurvatureIssue.blend`, removes its
existing output in memory, writes the input-only example, runs canonical
TrackBuilder, and writes the inspectable output:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\GenerateExamples.py"
```

The example parameters are `W=1`, `H=0.1`, `segment_length=5`, and materials
`red`, `white`.

## Run tests

From the repository root, run:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\TestTrackBuilder.py"
```

The suite covers:

- Every committed success and expected-failure fixture.
- Required output roles and non-empty mesh geometry.
- Golden geometry for every successful mesh-only fixture.
- Complete barrier-material sequences and adjusted segment lengths.
- `POLY` curve behavior.
- A generated cyclic Bézier outline.
- The committed NURBS curvature issue scene.
- Exact preservation of the normally evaluated contact topology.
- Adaptive away-edge chord-error bounds.
- Material-cut behavior with independently sampled ribbon sides.
- Preservation of input curve datablocks, control points, and resolutions.
- Rejection of unsupported curve features.
- Material-list, minimum-turn-angle, and one-segment validation.
- Existing-output editability and child-collection validation.
- Transactional preservation of an existing output after rejected builds.

## Inspect test artifacts

Every run replaces `Blender\TrackBuilder\TestArtifacts\Outputs` with one
inspectable `.blend` file per committed fixture and overwrites
`Blender\TrackBuilder\TestArtifacts\TestReport.txt` with the complete run and a
per-fixture expected/actual summary.

Successful artifacts contain the generated `Output`. Expected-failure artifacts
retain input geometry without an `Output` collection. Every fixture artifact
records `track_builder_actual_result` as a scene custom property.

The entire `TestArtifacts` directory is gitignored. Never commit its contents.
