# TrackBuilder

TrackBuilder converts closed 2D Blender outlines into a triangulated track,
surrounding ground, filled islands, and color segmented 3D barriers. It runs inside
Blender 4.5 and uses only Blender's bundled Python libraries.

## Files

| File | Purpose |
| --- | --- |
| [`TrackBuilder.py`](../TrackBuilder.py) | Track-building library and Python API |
| [`TrackBuilderCLI.py`](../TrackBuilderCLI.py) | Blender command-line wrapper |
| [`BenchmarkTrackBuilder.py`](../BenchmarkTrackBuilder.py) | Repeatable benchmark and geometry-hash reporter |
| [`GenerateTrackBuilderSamples.py`](../GenerateTrackBuilderSamples.py) | Committed test-fixture generator |
| [`TestTrackBuilder.py`](../TestTrackBuilder.py) | Integration and geometry regression suite |
| [`TEST.md`](TEST.md) | Test commands, fixtures, coverage, and artifacts |

## Collection structure

TrackBuilder requires this collection hierarchy:

```text
TrackBuilder/
  Input/
    Outlines/
  Output/
    Planes/
    BarrierSegments/
    OutlineMeshes/
```

`TrackBuilder`, `Input`, and `Outlines` must exist before a build. `Output` and
its children are generated and transactionally replaced by TrackBuilder. Every
temporary and generated object is linked somewhere beneath `TrackBuilder`.

## Input

### Description

- Input consists of outline objects with materials assigned beneath
  `TrackBuilder/Input/Outlines`.
- `build_track` treats every unique object found recursively in `Outlines` as
  an outline.
- TrackBuilder classifies outlines from their containment depths

### Input preconditions validated by the script

TrackBuilder rejects the build before replacing `TrackBuilder/Output` when any
of these checked preconditions fails:

- **Build arguments:**
  - `W`, `H`, and `segment_length` are Python `int` or
    `float` values (not booleans), are finite, and are at least `0.1`.
    `material_names` is a list with at least two entries; every entry is a
    non-empty string naming an existing Blender material. Repeated names are
    allowed.
- **Collections:**
  - a local, editable collection named `TrackBuilder` exists. It directly
    contains `Input`, which directly contains `Outlines`; `Outlines` recursively
    contains at least two unique objects. If `TrackBuilder/Output` already
    exists, it is local and editable, contains no objects directly, contains
    exactly the local editable leaf collections `Planes`, `BarrierSegments`,
    and `OutlineMeshes`, is not nested with `Outlines`, and shares no objects
    with `Outlines`. The generated collection names must not be occupied outside
    `TrackBuilder/Output`.
- **Current view layer:**
  - if `TrackBuilder/Input/Outlines` is present in the current view layer, its
    layer collection is not excluded. TrackBuilder rejects an explicitly
    excluded `Outlines` collection before reading its objects.
- **Object types and materials:**
  - every object found recursively in `TrackBuilder/Input/Outlines` is
    a Mesh or Curve, can be converted to normally evaluated mesh geometry, and
    has exactly one non-empty material slot.
- **Normally evaluated coordinates and topology:**
  - every world-space coordinate
    is finite; every outline has at least three evaluated vertices, has no faces,
    lies on the global XY plane within `epsilon`, and has no projected edge of
    length at or below `epsilon`. Edges have valid endpoints, no edge joins a
    vertex to itself, no undirected edge is duplicated, there is exactly one edge
    per vertex, every vertex has degree two, and traversing the edges visits every
    vertex in exactly one closed loop. These checks reject faces, loose vertices,
    branches, multiple loops, and adjacent zero-length edges.
- **Per-outline local shape:**
  - adjacent collinear edges do not reverse direction,
    and the absolute signed area is greater than `epsilon` squared. Every turn in
    an evaluated Mesh outline is at least `0.01` degrees. The turn-angle rule is
    not applied to Curve objects because legitimate curve evaluation can produce
    near-collinear samples.
- **Curve data:**
  - A Curve contains exactly one cyclic spline and uses only the
    supported feature set. TrackBuilder rejects linked-library Curve objects or
    datablocks; modifiers; constraints; parenting; object or data animation and
    drivers; shape keys; a separate render resolution; curve offset, extrusion,
    bevel geometry, bevel objects, or taper objects; non-zero control-point tilt;
    and non-default control-point radius. Cyclic `POLY`, Bézier, and NURBS splines,
    2D or 3D Curve dimensions, object transforms, and NURBS weights are otherwise
    supported. `POLY` splines remain linear and are not adaptively resampled.
- **Representative-point role classification:**
  - Testing one representative
    vertex from each outline against the other outlines produces exactly one
    depth-zero ground outline, exactly one depth-one outer-track outline, and only
    depth-two inner-track outlines after that. A depth greater than two or another
    candidate count is rejected. This check detects nested inner outlines when
    the trusted whole-boundary conditions in the next section hold.
- **Smooth-curve reference geometry:**
  - A Bézier or NURBS outer or inner outline's
    denser temporary evaluation still has no faces, forms one closed degree-two
    loop, is planar, has no edge at or below `epsilon`, has non-negligible signed
    area, and has defined, finite barrier miters.
- **Requested output feasibility:**
  - Triangulation must return only triangles and
    retain at least one triangle for the ground, track, and every island. Every
    outer and inner loop must support at least one complete sequence of barrier
    materials without making adjusted segments shorter than `segment_length` and
    must give every segment at least three `epsilon`-distinct polygon vertices.

The final two groups are construction checks and can raise
`TrackBuilderGeometryError` rather than `TrackBuilderValidationError`; either
failure occurs before a new `TrackBuilder/Output` is committed. Mesh objects are checked from
dependency-graph-evaluated geometry, including modifiers, at the current scene
state. All evaluated objects are transformed to world space before validation.
The `TrackBuilder/Input` hierarchy, its objects, and their datablocks are never
modified.

### Input preconditions not validated by the script; user must ensure these

TrackBuilder deliberately performs no comparisons between non-adjacent boundary
edges. The user must ensure all of the following:

- **Every outline is geometrically simple.**
  - Non-adjacent edges of the same outline must not:
    - cross,
    - touch,
    - overlap,
    - retrace one another,
    - or share a repeated point.
- **Different outline boundaries are disjoint.**
  - Edges belonging to different outlines must not
    - cross,
    - touch tangentially,
    - share a point or edge,
    - or overlap.
  - Treat a separation at or below `epsilon` as touching.
    - I.e. non-adjacent edges of one outline and edges of distinct outlines must remain more than `epsilon` apart.
- **The outlines nest properly.**
  - The ground outline encloses all other outlines.
  - The outer-track boundary must lie wholly inside the ground boundary
  - Every inner-track boundary must lie wholly inside the outer-track boundary
  - Inner-track boundaries must be mutually disjoint and non-nested.
    - TrackBuilder's representative-point tests do not establish whole-boundary containment.
- **Curve paths satisfy those rules between control points.**
  - For Bézier and
    NURBS inputs, inspect the evaluated spline rather than only its control
    polygon. The normally evaluated path must remain simple and separated; for
    outer and inner barrier outlines, the denser reference path must do so as
    well.
- **Every valid object in `TrackBuilder/Input/Outlines` is intentional.**
  - TrackBuilder cannot
    distinguish an accidental but structurally valid outline from a desired one;
    it will classify such an object by containment and may generate another island.
- **Input and requested output sizes are practical.**
  - TrackBuilder does not impose a fixed maximum on dense evaluated curve points
    or generated barrier segments. Smooth curves create temporary dense geometry,
    and every barrier segment becomes a separate mesh object. The user must choose
    outline complexity, curve resolution, scale, and `segment_length` appropriate
    for the available processing time and memory.
- **`TrackBuilder/Input/Outlines` participates in the current evaluation context.**
  - TrackBuilder finds `TrackBuilder` in file-wide `bpy.data.collections` and
    resolves `Input` and `Outlines` through direct child links.
  - It rejects `Outlines` when that collection's own layer-collection entry is
    excluded, but does not otherwise verify that the hierarchy and its objects
    are linked and enabled in the current scene and view layer.
  - A detached Mesh, or one disabled elsewhere in the hierarchy, can be read
    without dependency-graph effects such as its modifiers.
  - Ensure the current scene, view layer, frame, and modifier state are the ones intended for the build.
- **An existing `TrackBuilder/Output` is disposable.**
  - TrackBuilder verifies its collection
    structure and editability, but not that it was produced by TrackBuilder or is
    safe to replace. After a successful build, the old collection is removed;
    objects not linked outside it are deleted, as are their mesh datablocks when
    left unused.

Violating a trusted precondition may be caught incidentally by triangulation or
barrier construction, but rejection is not guaranteed. The build can instead
succeed and commit unexpected geometry, so a successful build and transactional
rollback are not substitutes for these user checks.

TrackBuilder also does not require generated barrier polygons to be simple,
non-overlapping, or contained within the fill regions. Beyond finite miters and
three distinct points, it does not check their positive area or spatial
clearance. If the application requires those properties, the user must choose
the outlines and `W` accordingly.

### Geometric tolerance

For checked distance-based properties, let `D` be the diagonal of the
world-space XY bounding box containing all normally evaluated input vertices.
TrackBuilder uses:

```text
epsilon = 1e-7 * max(1, D)
```

Distances at or below `epsilon` count as equal for checked properties such as
edge length, planarity, and generated barrier-point distinctness. TrackBuilder
does not calculate pairwise edge clearance, so the user-enforced `epsilon`
clearance in the trusted preconditions is not checked by the script. The mesh
turn-angle rule is independent of this distance tolerance.

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

The function returns the newly committed Blender `TrackBuilder/Output`
collection. After a successful commit, TrackBuilder leaves
`TrackBuilder/Input/Outlines` enabled and excludes only
`TrackBuilder/Output/OutlineMeshes` in the active view layer using its Outliner
checkbox.

## Command-line build

Run a build from the repository root in PowerShell:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" "C:\path\to\Input.blend" --background --python-exit-code 1 --python "Blender\TrackBuilder\TrackBuilderCLI.py" -- --build --w 0.35 --height 0.8 --segment-length 2.75 --materials BarrierRed BarrierWhite --save "C:\path\to\BuiltTrack.blend"
```

`--save` is optional. Without it, TrackBuilder builds the current file in memory
and Blender exits without saving. The wrapper saves with Blender's overwrite
confirmation disabled, so the user must ensure that the `--save` path is safe to
replace. Arguments after `--` belong to TrackBuilder rather than Blender.

## Generated output

TrackBuilder accepts either input winding. Normally evaluated working loops are
normalized to CCW as viewed from global +Z, rotated to a deterministic starting
vertex, and never simplified by removing vertices.

All generated objects are meshes. Flat ground, track, and island meshes are
placed directly in `TrackBuilder/Output/Planes`; barrier segment objects are
placed directly in `TrackBuilder/Output/BarrierSegments`. The evaluated
track-facing outer outline and each evaluated track-facing inner outline are
closed edge-only meshes in `TrackBuilder/Output/OutlineMeshes`. Fill meshes are
triangulated, face global +Z, and retain the material from their source outline.
Every object has a `track_builder_role` custom property:

| Role | Purpose |
| --- | --- |
| `ground` | Ground fill outside the outer track outline |
| `track` | Drivable track fill |
| `island` | Fill inside an inner track outline |
| `outer_barrier` | Segmented barrier outside the outer track outline |
| `inner_barrier` | Segmented barrier around an island |
| `outer_outline` | Evaluated outer track-facing barrier boundary |
| `inner_outline` | Evaluated inner track-facing barrier boundary |

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
4. Normally evaluated contact stations and dense reference stations are paired
   by an exact integer two-pointer merge. Every normally evaluated contact
   vertex forces a corresponding offset station.
5. The dense offset is simplified independently between each pair of adjacent
   forced contact stations. Only away-edge chord error needs to be measured
   because the contact path inside an authored interval is linear. The maximum
   offset deviation remains no greater than `W * 0.001`.
6. Adaptive stations add geometry only to the away edge. Material boundaries may
   independently add endpoints to either side when a segment cut falls inside an
   existing edge.

The contact and offset edges deliberately do not have to share the same discrete
shape, curvature, or resolution. Apparent local ribbon thickness can therefore
differ by the normally evaluated curve's chord error. The input curve's
datablock, control points, and authored resolutions remain unchanged.

### Barrier segmentation and materials

For each barrier outline, TrackBuilder divides its contact perimeter by
`segment_length` and initially uses the floored count. If one additional segment
still has an adjusted length of at least `segment_length`, it uses that count to
recover from a division result rounded just below an integer. It then reduces
the count by whole material sequences until the count is a multiple of the
number of barrier materials and the adjusted segment length is at least
`segment_length`.

Every segment in a loop has equal contact-boundary length, and segments remain
gapless across outline corners. A build is rejected if a loop cannot produce one
complete material sequence or has a segment with fewer than three distinct
vertices.

Barrier materials repeat in the supplied order, restarting from the first
material independently for every outer or inner barrier loop. Every loop
contains only complete material sequences.

Open design note: material segment cuts currently use distance along the
normally evaluated contact boundary. Consider whether those cuts should instead
be based on the independently sampled offset boundary when it has higher
resolution.

## Failure and rollback behavior

TrackBuilder validates the checked input contract and plans the complete result
before replacing an existing `TrackBuilder/Output` collection. New datablocks
are created in a temporary hierarchy beneath `TrackBuilder`. If validation or
construction fails, temporary data is removed and the previous output remains
unchanged. A successful build commits the new hierarchy as
`TrackBuilder/Output`, removes the previous output data described above, leaves
`TrackBuilder/Input/Outlines` enabled, and excludes
`TrackBuilder/Output/OutlineMeshes` in the active view layer. A failed build does
not change those Outliner exclusion states. An unchecked self-intersection,
self-touch, or cross-outline intersection may not fail and can therefore produce
a committed unexpected result.

The public exception hierarchy is:

- `TrackBuilderError`: Base exception.
- `TrackBuilderValidationError`: Invalid parameters, collections, or input
  outlines.
- `TrackBuilderGeometryError`: Valid input that cannot produce the requested
  output geometry.

## Testing

See [`TEST.md`](TEST.md) for fixture generation, test commands, coverage, and
inspection artifacts.
