# TrackBuilder

TrackBuilder converts closed 2D Blender outlines into a triangulated track,
surrounding ground, filled islands, and segmented 3D barriers.

It runs inside Blender 4.5 and uses only Blender's bundled Python libraries.

## Files

| File | Purpose |
| --- | --- |
| [`TrackBuilder.py`](TrackBuilder.py) | Production track-building API and command-line tool |
| [`GenerateTrackBuilderSamples.py`](GenerateTrackBuilderSamples.py) | Generates committed, input-only test fixtures |
| [`TestTrackBuilder.py`](TestTrackBuilder.py) | Runs Blender integration tests and writes temporary inspection artifacts |

## Input scene

The current `.blend` file must contain a collection named `Input`. That
collection recursively contains:

- Exactly one ground outline.
- Exactly one outer-track outline inside the ground outline.
- Zero or more inner-track outlines inside the outer-track outline.

TrackBuilder identifies these roles from geometric containment, so the input
objects do not need special names.

Each outline must:

- Be a mesh or curve object containing exactly one closed loop. Curves must have
  exactly one cyclic spline.
- Have no faces, loose vertices, branches, self-intersections, adjacent-edge
  backtracking, or zero-length edges.
- Have a turn angle of at least 0.01 degrees at every vertex. TrackBuilder rejects
  smaller turns instead of merging nearly collinear vertices.
- Be flat on the global XY plane after applying its world transform and
  evaluated modifiers.
- Have exactly one material assigned.
- Not touch or intersect another outline.

Every object found recursively in `Input` must be a valid outline. Inner outlines
cannot contain one another. `Input` and `Output` cannot be nested inside each
other or share objects.

TrackBuilder reads dependency-graph-evaluated geometry, including modifiers,
and converts it to world space before validation. The `Input` collection, its
objects, and their datablocks are never modified.

The materials requested for barrier segments must already exist in the current
Blender file.

### Geometric tolerance

For distance-based validation, let `D` be the diagonal of the world-space XY
bounding box containing all evaluated input vertices. TrackBuilder uses:

```text
epsilon = 1e-7 * max(1, D)
```

Distances at or below `epsilon` count as touching or equal. The turn-angle rule
is independent of this distance tolerance.

## Python API

The public entry point is:

```python
build_track(W, H, segment_length, material_names)
```

Parameters:

- `W`: Barrier thickness.
- `H`: Barrier height along global +Z.
- `segment_length`: Target barrier segment length measured along an outline.
- `material_names`: Ordered Blender material names used repeatedly on barrier
  segments.

All numeric parameters must be finite and greater than zero. At least two
non-empty barrier-material names are required, every name must resolve to an
existing Blender material, and repeated names are allowed.

Example from Blender's Python console:

```python
import sys

sys.path.insert(0, r"C:\path\to\ZoomTracks\Blender\TrackBuilder")
from TrackBuilder import build_track

output = build_track(0.3, 0.8, 2.5, ["red", "blue"])
```

The function returns the newly committed Blender `Output` collection.

## Command-line build

Using one of the committed test inputs, run a successful build from the
repository root in PowerShell:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" "Blender\TrackBuilder\TestInputs\TrackBuilderSampleInput02_NoInner.blend" --background --python-exit-code 1 --python "Blender\TrackBuilder\TrackBuilder.py" -- --build --w 0.35 --height 0.8 --segment-length 2.75 --materials BarrierRed BarrierWhite --save "Blender\TrackBuilder\BuiltTrack.blend"
```

`--save` is optional. Without it, TrackBuilder builds the current file in memory
and Blender exits without saving.

Arguments after `--` belong to TrackBuilder rather than Blender.

## Generated output

TrackBuilder accepts either input winding. Working loops are normalized to CCW
as viewed from global +Z, rotated to a deterministic starting vertex, and never
simplified by removing vertices.

All generated objects are meshes placed in the `Output` collection. Fill meshes
are triangulated, face global +Z, and retain the material from their source
outline. Every object has a `track_builder_role` custom property describing its
purpose:

| Role | Purpose |
| --- | --- |
| `ground` | Ground fill outside the outer track outline |
| `track` | Drivable track fill |
| `island` | Fill inside an inner track outline |
| `outer_barrier` | Segmented barrier outside the outer track outline |
| `inner_barrier` | Segmented barrier around an island |

Barrier objects also record their source outline, segment index, and adjusted
segment length as custom properties.

Barriers extend one-sided away from the track by `W` and upward by `H`. Adjacent
infinite offset lines define their miter points. Self-overlap, self-intersecting
barrier polygons, and barriers bleeding into the track are accepted; TrackBuilder
does not perform boolean cleanup.

For each outline, TrackBuilder divides its perimeter by `segment_length`, snaps
ratios within a relative tolerance of `1e-10` to an integer, and otherwise uses
the floored count. It then reduces that count by whole material sequences until
the count is a multiple of the number of barrier materials and the adjusted
segment length is at least `segment_length`. Every segment in a loop has equal
length, and segments remain gapless across outline corners. A build is rejected
if a loop cannot produce one complete material sequence, would produce more than
10,000 segments, or has a segment with fewer than three distinct vertices.

Barrier materials repeat in the supplied order, restarting from the first
material independently for every outer or inner barrier loop. Every loop
contains only complete material sequences.

## Failure and rollback behavior

TrackBuilder validates and plans the complete result before replacing an
existing `Output` collection. New datablocks are created in a temporary
collection. If validation or construction fails, temporary data is removed and
the previous output remains unchanged. A successful build commits the new
collection as `Output` and removes the replaced generated data.

The public exception hierarchy is:

- `TrackBuilderError`: Base exception.
- `TrackBuilderValidationError`: Invalid parameters, collections, or input
  outlines.
- `TrackBuilderGeometryError`: Valid input that cannot produce the requested
  output geometry.

## Generate committed test inputs

Run the sample generator in background Blender:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\GenerateTrackBuilderSamples.py"
```

By default, it writes ten files to `Blender\TrackBuilder\TestInputs`. Use
`--output-dir` and `--original-sample` after Blender's `--` separator to override
those paths:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\GenerateTrackBuilderSamples.py" -- --output-dir "C:\Temp\TrackBuilderSamples" --original-sample "Blender\TrackBuilder\TestInputs\TrackBuilderSampleInput01_Original.blend"
```

Each generated scene contains `Input` but no generated `Output`, and records its
build parameters and expected result in `track_builder_*` scene custom
properties. These inputs are test fixtures and are committed to the repository.

Samples 1 through 8 are successful build cases. Sample 9 contains a deliberate
0.005-degree turn and must be rejected. Sample 10 uses a segment length that
produces one barrier segment and must also be rejected.

## Run tests

From the repository root:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\TestTrackBuilder.py"
```

The suite reads only the committed fixtures in `TestInputs`; it never uses a
previous TrackBuilder output as an expected result. It tests successful inputs,
output roles and mesh validity, complete material sequences, segment-length
adjustment, material-list validation, minimum-turn-angle and one-segment
rejection, and preservation of an existing output after a rejected build.

Every run replaces `Blender\TrackBuilder\TestArtifacts\Outputs` with one
inspectable `.blend` file per committed input and writes the overall console
report to `Blender\TrackBuilder\TestArtifacts\TestReport.txt`. Expected-failure
artifacts contain their unchanged input and record the actual exception class;
successful artifacts contain the generated `Output`. The entire
`TestArtifacts` directory is gitignored and must not be committed.
