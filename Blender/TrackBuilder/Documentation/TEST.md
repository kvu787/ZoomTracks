# TrackBuilder testing

TrackBuilder tests keep committed inputs separate from generated results:

```text
Blender/TrackBuilder/
  TestInputs/                 committed, input-only .blend fixtures
  TestArtifacts/              gitignored and replaced on each test run
    Outputs/                  inspectable result .blend files
    TestReport.txt            overall run report
```

The tests never use a previous TrackBuilder output as an expected result.
Expected behavior is committed in the test code and fixture metadata, while
actual `.blend` outputs and the run report remain temporary.

## Test files

| File | Purpose |
| --- | --- |
| [`GenerateTrackBuilderSamples.py`](../GenerateTrackBuilderSamples.py) | Generates and synchronizes committed input fixtures without importing or running TrackBuilder |
| [`TestTrackBuilder.py`](../TestTrackBuilder.py) | Runs the integration suite and writes temporary inspection artifacts |
| [`TestInputs`](../TestInputs) | Committed `.blend` scenes containing `Input` but no generated `Output` |

## Generate committed test inputs

Regenerate inputs only after a deliberate test-fixture change. From the
repository root, run:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\GenerateTrackBuilderSamples.py"
```

By default, the generator writes ten files to
`Blender\TrackBuilder\TestInputs`. Use `--output-dir` and `--original-sample`
after Blender's `--` separator to override those paths:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\GenerateTrackBuilderSamples.py" -- --output-dir "C:\Temp\TrackBuilderSamples" --original-sample "Blender\TrackBuilder\TestInputs\TrackBuilderSampleInput01_Original.blend"
```

The generator synchronizes fixture filenames and removes obsolete generated
fixtures and Blender backup files. Each scene records its build parameters and
expected result in `track_builder_*` scene custom properties.

Samples 1 through 8 are successful build cases. Sample 9 contains a deliberate
0.005-degree turn and must be rejected. Sample 10 uses a segment length that
produces one barrier segment and must also be rejected.

## Run tests

From the repository root, run:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\TestTrackBuilder.py"
```

The suite covers:

- Every committed success and expected-failure fixture.
- Required output roles and non-empty mesh geometry.
- Complete barrier-material sequences and adjusted segment lengths.
- Material-list validation.
- Minimum-turn-angle and one-segment rejection.
- Preservation of an existing output after a rejected build.

## Inspect test artifacts

Every run replaces `Blender\TrackBuilder\TestArtifacts\Outputs` with one
inspectable `.blend` file per committed fixture and overwrites
`Blender\TrackBuilder\TestArtifacts\TestReport.txt` with the complete test run
and a per-fixture expected/actual result summary.

Successful artifacts contain the generated `Output`. Expected-failure artifacts
retain the input geometry without an `Output` collection. Every artifact also
records `track_builder_actual_result` as a scene custom property.

The entire `TestArtifacts` directory is gitignored. Do not commit its contents.
