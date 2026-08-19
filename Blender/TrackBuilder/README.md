# TrackBuilder

TrackBuilder converts closed 2D Blender outlines into a triangulated track,
surrounding ground, filled islands, and segmented 3D barriers.

It runs inside Blender 4.5 and uses only Blender's bundled Python libraries.

## Files

| File | Purpose |
| --- | --- |
| [`TrackBuilder.py`](TrackBuilder.py) | Production track-building API and command-line tool |
| [`GenerateTrackBuilderSamples.py`](GenerateTrackBuilderSamples.py) | Generates sample-input `.blend` files |
| [`TestTrackBuilder.py`](TestTrackBuilder.py) | Blender integration tests |
| [`TrackBuilderDesign_request.md`](TrackBuilderDesign_request.md) | Complete input and geometry specification |

## Input scene

The current `.blend` file must contain a collection named `Input`. That
collection recursively contains:

- Exactly one ground outline.
- Exactly one outer-track outline inside the ground outline.
- Zero or more inner-track outlines inside the outer-track outline.

TrackBuilder identifies these roles from geometric containment, so the input
objects do not need special names.

Each outline must:

- Be a mesh or curve object containing one closed loop.
- Have no faces, loose vertices, branches, self-intersections, or zero-length
  edges.
- Be flat on the global XY plane after applying its world transform and
  evaluated modifiers.
- Have exactly one material assigned.
- Not touch or intersect another outline.

The materials requested for barrier segments must already exist in the current
Blender file.

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

All numeric parameters must be finite and greater than zero. At least one
barrier material is required.

Example from Blender's Python console:

```python
import sys

sys.path.insert(0, r"C:\path\to\ZoomTracks\Blender\TrackBuilder")
from TrackBuilder import build_track

output = build_track(0.3, 0.8, 2.5, ["red", "blue"])
```

The function returns the newly committed Blender `Output` collection.

## Command-line build

From the repository root in PowerShell:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" "Blender\TrackBuilder\SampleInput.blend" --background --python-exit-code 1 --python "Blender\TrackBuilder\TrackBuilder.py" -- --build --w 0.3 --height 0.8 --segment-length 2.5 --materials red blue --save "Blender\TrackBuilder\BuiltTrack.blend"
```

`--save` is optional. Without it, TrackBuilder builds the current file in memory
and Blender exits without saving.

Arguments after `--` belong to TrackBuilder rather than Blender.

## Generated output

All generated objects are meshes placed in the `Output` collection. Every
object has a `track_builder_role` custom property describing its purpose:

| Role | Purpose |
| --- | --- |
| `ground` | Ground fill outside the outer track outline |
| `track` | Drivable track fill |
| `island` | Fill inside an inner track outline |
| `outer_barrier` | Segmented barrier outside the outer track outline |
| `inner_barrier` | Segmented barrier around an island |

Barrier objects also record their source outline, segment index, and adjusted
segment length as custom properties.

The requested segment length is adjusted so all segments around an outline have
equal length. A build is rejected if an outline would produce only one segment
or more than 10,000 segments.

## Failure and rollback behavior

TrackBuilder validates and plans the complete result before replacing an
existing `Output` collection. If validation or construction fails, the previous
output remains unchanged.

The public exception hierarchy is:

- `TrackBuilderError`: Base exception.
- `TrackBuilderValidationError`: Invalid parameters, collections, or input
  outlines.
- `TrackBuilderGeometryError`: Valid input that cannot produce the requested
  output geometry.

## Generate sample inputs

Run the sample generator in background Blender:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\GenerateTrackBuilderSamples.py"
```

By default, it writes ten files to `Blender\TrackBuilder\SampleInputs`. Use
`--output-dir` and `--original-sample` after Blender's `--` separator to override
those paths:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\GenerateTrackBuilderSamples.py" -- --output-dir "C:\Temp\TrackBuilderSamples" --original-sample "Blender\TrackBuilder\SampleInput.blend"
```

Samples 1 through 9 are successful build cases. Sample 10 deliberately uses a
segment length that produces one barrier segment and must be rejected.

## Run tests

From the repository root:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\TestTrackBuilder.py"
```

The suite tests the original input, successful synthetic inputs, one-segment
rejection, and preservation of an existing output after a rejected build.
