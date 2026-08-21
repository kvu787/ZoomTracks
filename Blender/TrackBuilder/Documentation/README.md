# TrackBuilder

TrackBuilder converts closed 2D Blender outlines into a triangulated track,
surrounding ground, filled islands, and color segmented 3D barriers. It runs inside
Blender 4.5 and uses only Blender's bundled Python libraries.

## Files

| File | Purpose |
| --- | --- |
| [`TrackBuilder.py`](../TrackBuilder.py) | Track-building library and Python API |
| [`TrackBuilderCLI.py`](../TrackBuilderCLI.py) | Blender command-line wrapper |
| [`GenerateTrackBuilderSamples.py`](../GenerateTrackBuilderSamples.py) | Committed test-fixture generator |
| [`TestTrackBuilder.py`](../TestTrackBuilder.py) | Integration and geometry regression suite |
| [`GenerateExamples.py`](../GenerateExamples.py) | Regenerates the committed curve-sampling example |
| [`Examples`](../Examples) | Inspectable adaptive-sampling input and output |
| [`TEST.md`](TEST.md) | Test commands, fixtures, coverage, and artifacts |

## Input scene

The current `.blend` file must contain a collection named `Input`. That
collection recursively contains:

- Exactly one ground outline.
- Exactly one outer-track outline inside the ground outline.
- Zero or more inner-track outlines inside the outer-track outline.

TrackBuilder identifies these roles by determining how the outlines enclose one another,
so the input objects do not need special names.

Each outline must:

- Be a mesh or Curve object containing exactly one closed loop. Curves
  must have exactly one cyclic spline.
- Have no faces, loose vertices, branches, self-intersections, adjacent-edge
  backtracking, or zero-length edges in its normally evaluated geometry.
- Be flat on the global XY plane after its world transform is applied.
- Have exactly one material assigned.
- Not touch or intersect another outline.

Mesh outlines must have a turn angle of at least 0.01 degrees at every vertex.
TrackBuilder rejects smaller turns instead of merging nearly collinear vertices.
This authored-vertex rule is not applied to Curve objects because legitimate
curve evaluation can produce near-collinear samples.

Every object found recursively in `Input` must be a valid outline. Inner outlines
cannot contain one another. `Input` and `Output` cannot be nested inside each
other or share objects. An existing `Output` collection must be local, editable,
and contain no child collections.

Mesh objects are read from dependency-graph-evaluated geometry, including
modifiers, and converted to world space before validation. The `Input`
collection, its objects, and their datablocks are never modified.

### Curve contract

Curve objects may use cyclic `POLY`, Bézier, or NURBS splines. Both 2D and
default 3D curves are accepted when their evaluated loop is flat on global XY
and has no faces. Ordinary object transforms and NURBS weights are supported.
`POLY` splines remain linear and are not adaptively resampled.

Adaptive sampling supports ordinary local Curve objects. A Curve object is
rejected if it uses any of these features:

- Modifiers, constraints, or parenting.
- Object or data animation and drivers.
- Shape keys or linked-library data.
- A separate render resolution.
- Curve offset, extrusion, bevel geometry, a bevel object, or a taper object.
- Non-zero control-point tilt or non-default control-point radius.

The materials requested for barrier segments must already exist in the current
Blender file.

### Geometric tolerance

For distance-based validation, let `D` be the diagonal of the world-space XY
bounding box containing all normally evaluated input vertices. TrackBuilder
uses:

```text
epsilon = 1e-7 * max(1, D)
```

Distances at or below `epsilon` count as touching or equal. The mesh turn-angle
rule is independent of this distance tolerance.

## Python API

The public entry point is:

```python
build_track(W, H, segment_length, material_names)
```

Parameters:

- `W`: Barrier thickness.
- `H`: Barrier height along global +Z.
- `segment_length`: Target barrier segment length measured along an outline's
  normally evaluated contact boundary.
- `material_names`: Ordered Blender material names used repeatedly on barrier
  segments.

`W`, `H`, and `segment_length` must each be finite and greater than or equal to
`0.1`. At least two non-empty barrier-material names are required, every name
must resolve to an existing Blender material, and repeated names are allowed.

Example from Blender's Python console:

```python
import sys

sys.path.insert(0, r"C:\path\to\ZoomTracks\Blender\TrackBuilder")
from TrackBuilder import build_track

output = build_track(W=1, H=0.1, segment_length=5, material_names=["red", "white"])
```

The function returns the newly committed Blender `Output` collection.

## Command-line build

Run a build from the repository root in PowerShell:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" "C:\path\to\Input.blend" --background --python-exit-code 1 --python "Blender\TrackBuilder\TrackBuilderCLI.py" -- --build --w 0.35 --height 0.8 --segment-length 2.75 --materials BarrierRed BarrierWhite --save "C:\path\to\BuiltTrack.blend"
```

`--save` is optional. Without it, TrackBuilder builds the current file in memory
and Blender exits without saving. Arguments after `--` belong to TrackBuilder
rather than Blender.

## Generated output

TrackBuilder accepts either input winding. Normally evaluated working loops are
normalized to CCW as viewed from global +Z, rotated to a deterministic starting
vertex, and never simplified by removing vertices.

All generated objects are meshes placed in the `Output` collection. Fill meshes
are triangulated, face global +Z, and retain the material from their source
outline. Every object has a `track_builder_role` custom property:

| Role | Purpose |
| --- | --- |
| `ground` | Ground fill outside the outer track outline |
| `track` | Drivable track fill |
| `island` | Fill inside an inner track outline |
| `outer_barrier` | Segmented barrier outside the outer track outline |
| `inner_barrier` | Segmented barrier around an island |

Barrier objects also record their source outline, segment index, and adjusted
segment length. Objects sourced from curves record
`track_builder_curve_sampling` and `track_builder_curve_sample_count`; curve
barriers additionally record `track_builder_curve_offset_sample_count`.

Barriers extend one-sided away from the track by `W` and upward by `H`. Adjacent
infinite offset lines define their miter points. Self-overlap, self-intersecting
barrier polygons, and barriers bleeding into the track are accepted;
TrackBuilder does not perform boolean cleanup.

### Adaptive smooth-curve barriers

Smooth curve barriers use offset-aware adaptive sampling so their away-facing
silhouette can be smoother than Blender's normally evaluated outline. Blender's
evaluated points remain the exact track-facing contact boundary, but simply
offsetting those same points can make the exposed side of a wide barrier look
faceted, especially around tighter bends.

To avoid that faceting, TrackBuilder evaluates a temporary, higher-resolution
version of the spline and keeps additional points only where the generated
away-facing offset needs them to follow the curve smoothly. This does not add
points to the contact boundary, change the track or island fill, or modify the
input Curve object. In effect, the barrier is a ribbon whose track-facing edge
preserves Blender's normal evaluation while its away-facing edge is allowed to
use as much detail as its offset shape requires.

For smooth Bézier and NURBS barrier outlines, TrackBuilder treats the source
curve and its away-from-track offset as two independently sampled sides of a
ribbon:

1. Blender's normal curve evaluation defines the immutable contact boundary
   used by track/island fill and the track-facing barrier edge.
2. An unsaved temporary copy is evaluated at 32 times the authored resolution,
   with a minimum reference resolution of 256 and maximum of 1024.
3. The mathematical spline is authoritative for the away-facing shape. A dense
   miter offset is constructed to the right of the CCW outer outline or left of
   a CCW inner outline.
4. The offset is simplified adaptively until its maximum chord deviation is no
   greater than `W * 0.005`. Every normally evaluated contact vertex forces a
   corresponding offset station.
5. Adaptive stations add geometry only to the away edge. Material boundaries may
   independently add endpoints to either side when a segment cut falls inside an
   existing edge.

The contact and offset edges deliberately do not have to share the same discrete
shape, curvature, or resolution. Apparent local ribbon thickness can therefore
differ by the normally evaluated curve's chord error. The input curve's
datablock, control points, and authored resolutions remain unchanged.

TrackBuilder caps a dense curve evaluation at 20,000 points. A curve that exceeds
the cap is rejected with `TrackBuilderGeometryError`.

### Barrier segmentation and materials

For each barrier outline, TrackBuilder divides its contact perimeter by
`segment_length`, snaps ratios within a relative tolerance of `1e-10` to an
integer, and otherwise uses the floored count. It then reduces that count by
whole material sequences until the count is a multiple of the number of barrier
materials and the adjusted segment length is at least `segment_length`.

Every segment in a loop has equal contact-boundary length, and segments remain
gapless across outline corners. A build is rejected if a loop cannot produce one
complete material sequence, would produce more than 10,000 segments, or has a
segment with fewer than three distinct vertices.

Barrier materials repeat in the supplied order, restarting from the first
material independently for every outer or inner barrier loop. Every loop
contains only complete material sequences.

Open design note: material segment cuts currently use distance along the
normally evaluated contact boundary. Consider whether those cuts should instead
be based on the independently sampled offset boundary when it has higher
resolution.

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

## Example and testing

The committed adaptive-sampling example is in [`Examples`](../Examples). See
[`TEST.md`](TEST.md) for regeneration commands, fixture architecture, test
coverage, and inspection artifacts.
