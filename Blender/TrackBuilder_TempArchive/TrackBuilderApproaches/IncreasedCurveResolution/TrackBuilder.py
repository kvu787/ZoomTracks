"""Experimental TrackBuilder using globally increased resolution for vanilla curves.

The public API is ``build_track(W, H, segment_length, material_names)``.
Mesh outlines retain the production behavior. Every smooth curve spline is
evaluated at sixteen times its authored U resolution for the away-facing offset;
the track-facing edge remains on the authored evaluated outline.
"""

from __future__ import annotations

import bisect
from fractions import Fraction
import math
import uuid
from dataclasses import dataclass

import bpy
from mathutils import Vector
from mathutils.geometry import delaunay_2d_cdt


ABSOLUTE_TOLERANCE_FACTOR = 1.0e-7
INTEGER_RATIO_TOLERANCE = 1.0e-10
MINIMUM_TURN_ANGLE_DEGREES = 0.01
MAX_SEGMENTS_PER_OUTLINE = 10_000
CURVE_RESOLUTION_MULTIPLIER = 16
CURVE_MAXIMUM_RESOLUTION = 1024
CURVE_MAXIMUM_EVALUATED_POINTS = 20_000


class TrackBuilderError(RuntimeError):
    """Base class for track-builder errors."""


class TrackBuilderValidationError(TrackBuilderError):
    """Raised when an input file or parameter violates the input contract."""


class TrackBuilderGeometryError(TrackBuilderError):
    """Raised when valid input cannot be represented by the requested geometry."""


@dataclass
class _RawOutline:
    object_name: str
    material: bpy.types.Material
    vertices: list[Vector]
    edges: list[tuple[int, int]]
    face_count: int
    is_curve: bool
    source_object: bpy.types.Object


@dataclass
class _Outline:
    object_name: str
    material: bpy.types.Material
    points: list[Vector]
    is_curve: bool
    source_object: bpy.types.Object
    sampling_method: str = "evaluated_input"
    offset_points: list[Vector] | None = None
    offset_source_fractions: list[float] | None = None


@dataclass
class _MeshPlan:
    name: str
    vertices: list[tuple[float, float, float]]
    faces: list[tuple[int, ...]]
    material: bpy.types.Material
    properties: dict[str, object]


def _cross_2d(a: Vector, b: Vector) -> float:
    return a.x * b.y - a.y * b.x


def _signed_area(points: list[Vector]) -> float:
    return 0.5 * sum(
        _cross_2d(points[index], points[(index + 1) % len(points)])
        for index in range(len(points))
    )


def _distance_point_to_segment(point: Vector, start: Vector, end: Vector) -> float:
    edge = end - start
    length_squared = edge.length_squared
    if length_squared == 0.0:
        return (point - start).length
    parameter = max(0.0, min(1.0, (point - start).dot(edge) / length_squared))
    return (point - (start + edge * parameter)).length


def _segments_are_close(
    a: Vector,
    b: Vector,
    c: Vector,
    d: Vector,
    epsilon: float,
) -> bool:
    ab = b - a
    cd = d - c
    side_c = _cross_2d(ab, c - a)
    side_d = _cross_2d(ab, d - a)
    side_a = _cross_2d(cd, a - c)
    side_b = _cross_2d(cd, b - c)
    if (
        ((side_c > 0.0 and side_d < 0.0) or (side_c < 0.0 and side_d > 0.0))
        and ((side_a > 0.0 and side_b < 0.0) or (side_a < 0.0 and side_b > 0.0))
    ):
        return True
    return min(
        _distance_point_to_segment(a, c, d),
        _distance_point_to_segment(b, c, d),
        _distance_point_to_segment(c, a, b),
        _distance_point_to_segment(d, a, b),
    ) <= epsilon


def _point_in_polygon(point: Vector, polygon: list[Vector]) -> bool:
    inside = False
    previous = polygon[-1]
    for current in polygon:
        if (current.y > point.y) != (previous.y > point.y):
            crossing_x = previous.x + (
                (point.y - previous.y)
                * (current.x - previous.x)
                / (current.y - previous.y)
            )
            if point.x < crossing_x:
                inside = not inside
        previous = current
    return inside


def _positive_finite_number(name: str, value: object) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise TrackBuilderValidationError(f"{name} must be a finite number greater than zero")
    result = float(value)
    if not math.isfinite(result) or result <= 0.0:
        raise TrackBuilderValidationError(f"{name} must be a finite number greater than zero")
    return result


def _validated_materials(material_names: object) -> list[bpy.types.Material]:
    if not isinstance(material_names, list) or len(material_names) < 2:
        raise TrackBuilderValidationError(
            "material_names must be a list containing at least two entries"
        )
    materials: list[bpy.types.Material] = []
    for index, name in enumerate(material_names):
        if not isinstance(name, str) or not name:
            raise TrackBuilderValidationError(
                f"material_names[{index}] must be a non-empty string"
            )
        material = bpy.data.materials.get(name)
        if material is None:
            raise TrackBuilderValidationError(
                f"material_names[{index}] names missing material {name!r}"
            )
        materials.append(material)
    return materials


def _unique_objects(collection: bpy.types.Collection) -> list[bpy.types.Object]:
    by_pointer = {obj.as_pointer(): obj for obj in collection.all_objects}
    return sorted(by_pointer.values(), key=lambda obj: obj.name)


def _curve_control_points(spline: bpy.types.Spline) -> list[object]:
    if spline.type == "BEZIER":
        return list(spline.bezier_points)
    return list(spline.points)


def _validate_vanilla_curve(obj: bpy.types.Object) -> None:
    """Reject curve features that the experimental sampler deliberately ignores."""

    curve = obj.data
    unsupported: list[str] = []
    if obj.library is not None or curve.library is not None:
        unsupported.append("linked library data")
    if obj.modifiers:
        unsupported.append("modifiers")
    if obj.constraints:
        unsupported.append("constraints")
    if obj.parent is not None:
        unsupported.append("parenting")
    if obj.animation_data is not None or curve.animation_data is not None:
        unsupported.append("animation data or drivers")
    if curve.shape_keys is not None:
        unsupported.append("shape keys")
    if curve.render_resolution_u != 0:
        unsupported.append("a separate render resolution")
    if curve.offset != 0.0:
        unsupported.append("curve geometry offset")
    if curve.extrude != 0.0:
        unsupported.append("curve extrusion")
    if curve.bevel_depth != 0.0 or curve.bevel_object is not None:
        unsupported.append("curve bevel geometry")
    if curve.taper_object is not None:
        unsupported.append("a taper object")

    if len(curve.splines) == 1:
        spline = curve.splines[0]
        for point in _curve_control_points(spline):
            if abs(float(point.tilt)) > 1.0e-12:
                unsupported.append("non-zero control-point tilt")
                break
        for point in _curve_control_points(spline):
            if abs(float(point.radius) - 1.0) > 1.0e-12:
                unsupported.append("non-default control-point radius")
                break

    if unsupported:
        features = ", ".join(dict.fromkeys(unsupported))
        raise TrackBuilderValidationError(
            f"Curve {obj.name!r} is not a vanilla curve; unsupported feature(s): {features}"
        )


def _read_raw_outlines(input_collection: bpy.types.Collection) -> list[_RawOutline]:
    objects = _unique_objects(input_collection)
    if len(objects) < 2:
        raise TrackBuilderValidationError(
            "Input must recursively contain at least two outline objects"
        )

    dependency_graph = bpy.context.evaluated_depsgraph_get()
    raw_outlines: list[_RawOutline] = []
    for obj in objects:
        if obj.type not in {"MESH", "CURVE"}:
            raise TrackBuilderValidationError(
                f"Input object {obj.name!r} has unsupported type {obj.type!r}; "
                "only MESH and CURVE are allowed"
            )
        if obj.type == "CURVE":
            _validate_vanilla_curve(obj)
            splines = obj.data.splines
            if len(splines) != 1 or not splines[0].use_cyclic_u:
                raise TrackBuilderValidationError(
                    f"Curve {obj.name!r} must contain exactly one cyclic spline"
                )

        slots = list(obj.material_slots)
        if len(slots) != 1 or slots[0].material is None:
            raise TrackBuilderValidationError(
                f"Outline {obj.name!r} must have exactly one non-empty material slot"
            )

        evaluated_object = obj.evaluated_get(dependency_graph)
        evaluated_mesh = evaluated_object.to_mesh()
        if evaluated_mesh is None:
            raise TrackBuilderValidationError(
                f"Outline {obj.name!r} could not be converted to evaluated mesh geometry"
            )
        try:
            world_matrix = evaluated_object.matrix_world.copy()
            vertices = [world_matrix @ vertex.co.copy() for vertex in evaluated_mesh.vertices]
            edges = [tuple(edge.vertices) for edge in evaluated_mesh.edges]
            face_count = len(evaluated_mesh.polygons)
        finally:
            evaluated_object.to_mesh_clear()

        raw_outlines.append(
            _RawOutline(
                object_name=obj.name,
                material=slots[0].material,
                vertices=vertices,
                edges=edges,
                face_count=face_count,
                is_curve=obj.type == "CURVE",
                source_object=obj,
            )
        )
    return raw_outlines


def _world_epsilon(raw_outlines: list[_RawOutline]) -> float:
    """Return the scale-aware distance tolerance used by geometry validation."""

    all_vertices = [vertex for outline in raw_outlines for vertex in outline.vertices]
    if not all_vertices:
        raise TrackBuilderValidationError("Input outlines contain no evaluated vertices")
    for vertex in all_vertices:
        if not all(math.isfinite(component) for component in vertex):
            raise TrackBuilderValidationError("Input contains a non-finite world-space coordinate")
    min_x = min(vertex.x for vertex in all_vertices)
    max_x = max(vertex.x for vertex in all_vertices)
    min_y = min(vertex.y for vertex in all_vertices)
    max_y = max(vertex.y for vertex in all_vertices)
    diagonal = math.hypot(max_x - min_x, max_y - min_y)
    return ABSOLUTE_TOLERANCE_FACTOR * max(1.0, diagonal)


def _ordered_validated_loop(raw: _RawOutline, epsilon: float) -> list[Vector]:
    """Validate one evaluated outline and return its vertices in edge order."""

    if raw.face_count:
        raise TrackBuilderValidationError(
            f"Outline {raw.object_name!r} evaluates to {raw.face_count} face(s); faces are forbidden"
        )
    vertex_count = len(raw.vertices)
    if vertex_count < 3:
        raise TrackBuilderValidationError(
            f"Outline {raw.object_name!r} must contain at least three vertices"
        )
    if len(raw.edges) != vertex_count:
        raise TrackBuilderValidationError(
            f"Outline {raw.object_name!r} must have exactly one edge per vertex"
        )

    adjacency: list[list[int]] = [[] for _ in range(vertex_count)]
    unique_edges: set[tuple[int, int]] = set()
    for edge_index, (first, second) in enumerate(raw.edges):
        if not (0 <= first < vertex_count and 0 <= second < vertex_count):
            raise TrackBuilderValidationError(
                f"Outline {raw.object_name!r} edge {edge_index} has an invalid vertex index"
            )
        edge_key = (min(first, second), max(first, second))
        if first == second or edge_key in unique_edges:
            raise TrackBuilderValidationError(
                f"Outline {raw.object_name!r} contains a zero-length or duplicate edge"
            )
        first_xy = raw.vertices[first].xy
        second_xy = raw.vertices[second].xy
        if (second_xy - first_xy).length <= epsilon:
            raise TrackBuilderValidationError(
                f"Outline {raw.object_name!r} contains an edge no longer than epsilon"
            )
        unique_edges.add(edge_key)
        adjacency[first].append(second)
        adjacency[second].append(first)

    for vertex_index, neighbors in enumerate(adjacency):
        if len(neighbors) != 2:
            raise TrackBuilderValidationError(
                f"Outline {raw.object_name!r} vertex {vertex_index} has degree {len(neighbors)}; "
                "a closed loop requires degree two"
            )

    ordered_indices: list[int] = []
    visited: set[int] = set()
    previous = -1
    current = 0
    for _ in range(vertex_count):
        if current in visited:
            raise TrackBuilderValidationError(
                f"Outline {raw.object_name!r} contains more than one loop"
            )
        visited.add(current)
        ordered_indices.append(current)
        neighbors = adjacency[current]
        next_vertex = neighbors[0] if neighbors[0] != previous else neighbors[1]
        previous, current = current, next_vertex
    if current != ordered_indices[0] or len(visited) != vertex_count:
        raise TrackBuilderValidationError(
            f"Outline {raw.object_name!r} does not form exactly one closed loop"
        )

    points = [raw.vertices[index].xy.copy() for index in ordered_indices]
    for vertex in raw.vertices:
        if abs(vertex.z) > epsilon:
            raise TrackBuilderValidationError(
                f"Outline {raw.object_name!r} has world-space Z={vertex.z:g}, "
                f"outside tolerance {epsilon:g}"
            )

    point_count = len(points)
    for index in range(point_count):
        incoming = points[index] - points[index - 1]
        outgoing = points[(index + 1) % point_count] - points[index]
        cross = _cross_2d(incoming, outgoing)
        dot = incoming.dot(outgoing)
        scale = max(1.0, incoming.length, outgoing.length)
        if abs(cross) <= epsilon * scale and dot < 0.0:
            raise TrackBuilderValidationError(
                f"Outline {raw.object_name!r} backtracks at an adjacent collinear edge"
            )
        turn_angle = abs(math.atan2(cross, dot))
        if not raw.is_curve and turn_angle < math.radians(MINIMUM_TURN_ANGLE_DEGREES):
            raise TrackBuilderValidationError(
                f"Outline {raw.object_name!r} has a turn angle of "
                f"{math.degrees(turn_angle):g} degrees at vertex {ordered_indices[index]}; "
                f"the minimum is {MINIMUM_TURN_ANGLE_DEGREES:g} degrees"
            )

    for first_index in range(point_count):
        a = points[first_index]
        b = points[(first_index + 1) % point_count]
        for second_index in range(first_index + 1, point_count):
            if second_index == first_index + 1:
                continue
            if first_index == 0 and second_index == point_count - 1:
                continue
            c = points[second_index]
            d = points[(second_index + 1) % point_count]
            if _segments_are_close(a, b, c, d, epsilon):
                raise TrackBuilderValidationError(
                    f"Outline {raw.object_name!r} intersects or touches itself"
                )
    return points


def _validate_outline_separation(outlines: list[_Outline], epsilon: float) -> None:
    for first_index, first in enumerate(outlines):
        for second in outlines[first_index + 1 :]:
            for edge_a in range(len(first.points)):
                a = first.points[edge_a]
                b = first.points[(edge_a + 1) % len(first.points)]
                for edge_b in range(len(second.points)):
                    c = second.points[edge_b]
                    d = second.points[(edge_b + 1) % len(second.points)]
                    if _segments_are_close(a, b, c, d, epsilon):
                        raise TrackBuilderValidationError(
                            f"Outlines {first.object_name!r} and {second.object_name!r} "
                            "intersect or touch"
                        )


def _canonical_ccw(points: list[Vector], object_name: str, epsilon: float) -> list[Vector]:
    area = _signed_area(points)
    if abs(area) <= epsilon * epsilon:
        raise TrackBuilderValidationError(
            f"Outline {object_name!r} has negligible signed area"
        )
    result = [point.copy() for point in points]
    if area < 0.0:
        result.reverse()
    first_index = min(range(len(result)), key=lambda index: (result[index].x, result[index].y))
    return result[first_index:] + result[:first_index]


def _validated_outlines(raw_outlines: list[_RawOutline], epsilon: float) -> list[_Outline]:
    outlines: list[_Outline] = []
    for raw in raw_outlines:
        validated = _ordered_validated_loop(raw, epsilon)
        canonical = _canonical_ccw(validated, raw.object_name, epsilon)
        outlines.append(
            _Outline(
                raw.object_name,
                raw.material,
                canonical,
                raw.is_curve,
                raw.source_object,
            )
        )
    _validate_outline_separation(outlines, epsilon)
    return outlines


def _classify_outlines(
    outlines: list[_Outline],
) -> tuple[_Outline, _Outline, list[_Outline]]:
    depths: list[int] = []
    for index, outline in enumerate(outlines):
        representative = outline.points[0]
        depth = sum(
            1
            for other_index, other in enumerate(outlines)
            if other_index != index and _point_in_polygon(representative, other.points)
        )
        depths.append(depth)

    ground_candidates = [outline for outline, depth in zip(outlines, depths) if depth == 0]
    outer_candidates = [outline for outline, depth in zip(outlines, depths) if depth == 1]
    inner_candidates = [outline for outline, depth in zip(outlines, depths) if depth == 2]
    if len(ground_candidates) != 1:
        raise TrackBuilderValidationError(
            f"Geometry must identify exactly one ground outline; found {len(ground_candidates)}"
        )
    if len(outer_candidates) != 1:
        raise TrackBuilderValidationError(
            f"Geometry must identify exactly one outer-track outline; found {len(outer_candidates)}"
        )
    if len(inner_candidates) != len(outlines) - 2 or any(depth > 2 for depth in depths):
        raise TrackBuilderValidationError(
            "Outlines must have only ground, outer-track, and non-nested inner-track levels"
        )

    ground = ground_candidates[0]
    outer = outer_candidates[0]
    if not _point_in_polygon(outer.points[0], ground.points):
        raise TrackBuilderValidationError("Ground outline does not enclose the outer track")
    for inner in inner_candidates:
        if not _point_in_polygon(inner.points[0], outer.points):
            raise TrackBuilderValidationError(
                f"Inner outline {inner.object_name!r} is not enclosed by the outer track"
            )
    inner_candidates.sort(
        key=lambda outline: (
            outline.points[0].x,
            outline.points[0].y,
            outline.object_name,
        )
    )
    return ground, outer, inner_candidates


def _increased_curve_resolution(obj: bpy.types.Object) -> int:
    curve = obj.data
    spline = curve.splines[0]
    base = max(int(curve.resolution_u), int(spline.resolution_u))
    requested = base * CURVE_RESOLUTION_MULTIPLIER
    if requested > CURVE_MAXIMUM_RESOLUTION:
        raise TrackBuilderGeometryError(
            f"Curve {obj.name!r} requests increased resolution {requested}; "
            f"the experimental maximum is {CURVE_MAXIMUM_RESOLUTION}"
        )
    return requested


def _evaluated_curve_loop(
    obj: bpy.types.Object,
    resolution: int,
    epsilon: float,
) -> list[Vector]:
    """Evaluate an unmodified temporary curve copy without touching the input."""

    temporary_data = obj.data.copy()
    temporary_object = obj.copy()
    temporary_object.data = temporary_data
    temporary_object.name = f"__TrackBuilderCurveSample_{uuid.uuid4().hex}"
    temporary_object.parent = None
    temporary_object.matrix_world = obj.matrix_world.copy()
    temporary_data.resolution_u = resolution
    temporary_data.render_resolution_u = 0
    if temporary_data.splines[0].type != "POLY":
        temporary_data.splines[0].resolution_u = resolution
    bpy.context.scene.collection.objects.link(temporary_object)
    evaluated_object = None
    evaluated_mesh = None
    try:
        bpy.context.view_layer.update()
        dependency_graph = bpy.context.evaluated_depsgraph_get()
        evaluated_object = temporary_object.evaluated_get(dependency_graph)
        evaluated_mesh = evaluated_object.to_mesh()
        if evaluated_mesh is None:
            raise TrackBuilderValidationError(
                f"Curve {obj.name!r} could not be evaluated at resolution {resolution}"
            )
        if evaluated_mesh.polygons:
            raise TrackBuilderValidationError(
                f"Vanilla curve {obj.name!r} unexpectedly evaluated to faces"
            )
        if len(evaluated_mesh.vertices) > CURVE_MAXIMUM_EVALUATED_POINTS:
            raise TrackBuilderGeometryError(
                f"Curve {obj.name!r} evaluated to {len(evaluated_mesh.vertices)} points; "
                f"the experimental maximum is {CURVE_MAXIMUM_EVALUATED_POINTS}"
            )

        matrix = evaluated_object.matrix_world.copy()
        vertices = [matrix @ vertex.co.copy() for vertex in evaluated_mesh.vertices]
        edges = [tuple(edge.vertices) for edge in evaluated_mesh.edges]
        if len(vertices) < 3 or len(edges) != len(vertices):
            raise TrackBuilderValidationError(
                f"Curve {obj.name!r} did not evaluate to one closed edge loop"
            )
        adjacency: list[list[int]] = [[] for _ in vertices]
        for first, second in edges:
            adjacency[first].append(second)
            adjacency[second].append(first)
        if any(len(neighbors) != 2 for neighbors in adjacency):
            raise TrackBuilderValidationError(
                f"Curve {obj.name!r} did not evaluate to one degree-two loop"
            )
        ordered_indices: list[int] = []
        previous = -1
        current = 0
        for _ in vertices:
            ordered_indices.append(current)
            neighbors = adjacency[current]
            next_index = neighbors[0] if neighbors[0] != previous else neighbors[1]
            previous, current = current, next_index
        if current != ordered_indices[0] or len(set(ordered_indices)) != len(vertices):
            raise TrackBuilderValidationError(
                f"Curve {obj.name!r} did not evaluate to exactly one closed loop"
            )
        points = [vertices[index].xy.copy() for index in ordered_indices]
        for vertex in vertices:
            if abs(vertex.z) > epsilon:
                raise TrackBuilderValidationError(
                    f"Curve {obj.name!r} has non-planar refined geometry"
                )
        for index, point in enumerate(points):
            if (points[(index + 1) % len(points)] - point).length <= epsilon:
                raise TrackBuilderValidationError(
                    f"Curve {obj.name!r} produces an edge no longer than epsilon when refined"
                )
        return _canonical_ccw(points, obj.name, epsilon)
    finally:
        if evaluated_object is not None and evaluated_mesh is not None:
            evaluated_object.to_mesh_clear()
        if temporary_object.name in bpy.data.objects:
            bpy.data.objects.remove(temporary_object, do_unlink=True)
        if temporary_data.name in bpy.data.curves and temporary_data.users == 0:
            bpy.data.curves.remove(temporary_data)


def _stable_curve_offset_points(
    points: list[Vector],
    width: float,
    offset_left: bool,
) -> list[Vector]:
    """Compute the same infinite-line miters with a stable straight-line case."""

    normals: list[Vector] = []
    for index, point in enumerate(points):
        direction = (points[(index + 1) % len(points)] - point).normalized()
        left_normal = Vector((-direction.y, direction.x))
        normals.append(left_normal if offset_left else -left_normal)

    result: list[Vector] = []
    for index, point in enumerate(points):
        previous = (index - 1) % len(points)
        normal_sum = normals[previous] + normals[index]
        if normal_sum.length <= 1.0e-14:
            raise TrackBuilderGeometryError(
                "A barrier miter is undefined at an antiparallel curve-sample edge pair"
            )
        bisector = normal_sum.normalized()
        denominator = bisector.dot(normals[index])
        if abs(denominator) <= 1.0e-14:
            raise TrackBuilderGeometryError(
                "A barrier miter is undefined at an antiparallel curve-sample edge pair"
            )
        miter = point + bisector * (width / denominator)
        if not all(math.isfinite(component) for component in miter):
            raise TrackBuilderGeometryError("A curve barrier miter produced a non-finite coordinate")
        result.append(miter)
    return result


def _closed_cumulative_lengths(points: list[Vector]) -> list[float]:
    cumulative = [0.0]
    for index, point in enumerate(points):
        cumulative.append(cumulative[-1] + (points[(index + 1) % len(points)] - point).length)
    return cumulative


def _cyclic_interpolate(points: list[Vector], station: Fraction) -> Vector:
    scaled = station * len(points)
    index = scaled.numerator // scaled.denominator
    remainder = scaled - index
    if remainder == 0:
        return points[index % len(points)].copy()
    return points[index % len(points)].lerp(
        points[(index + 1) % len(points)],
        float(remainder),
    )


def _global_contact_reference(
    authored_source: list[Vector],
    dense_source: list[Vector],
    dense_offset: list[Vector],
) -> tuple[list[Vector], list[Vector]]:
    """Globally subdivide while keeping every source point on the authored polyline."""

    start = min(
        range(len(dense_source)),
        key=lambda index: (dense_source[index] - authored_source[0]).length_squared,
    )
    aligned_offset = dense_offset[start:] + dense_offset[:start]
    authored_stations = {
        Fraction(index, len(authored_source)): index
        for index in range(len(authored_source))
    }
    dense_stations = {
        Fraction(index, len(aligned_offset)): index
        for index in range(len(aligned_offset))
    }
    stations = sorted(set(authored_stations) | set(dense_stations))
    contact: list[Vector] = []
    offset: list[Vector] = []
    for station in stations:
        authored_index = authored_stations.get(station)
        contact.append(
            authored_source[authored_index].copy()
            if authored_index is not None
            else _cyclic_interpolate(authored_source, station)
        )
        dense_index = dense_stations.get(station)
        offset.append(
            aligned_offset[dense_index].copy()
            if dense_index is not None
            else _cyclic_interpolate(aligned_offset, station)
        )
    return contact, offset


def _increased_resolution_curve_outline(
    outline: _Outline,
    width: float,
    offset_left: bool | None,
    epsilon: float,
) -> _Outline:
    spline = outline.source_object.data.splines[0]
    if spline.type == "POLY":
        return outline
    resolution = _increased_curve_resolution(outline.source_object)
    dense_source = _evaluated_curve_loop(outline.source_object, resolution, epsilon)
    dense_offset = (
        dense_source
        if offset_left is None
        else _stable_curve_offset_points(dense_source, width, offset_left)
    )
    points, offset_points = _global_contact_reference(
        outline.points,
        dense_source,
        dense_offset,
    )
    cumulative = _closed_cumulative_lengths(points)
    offset_fractions = (
        None
        if offset_left is None
        else [distance / cumulative[-1] for distance in cumulative[:-1]]
    )
    return _Outline(
        outline.object_name,
        outline.material,
        points,
        True,
        outline.source_object,
        sampling_method=(
            f"increased_contact=authored,resolution={resolution},"
            f"multiplier={CURVE_RESOLUTION_MULTIPLIER}"
        ),
        offset_points=None if offset_left is None else offset_points,
        offset_source_fractions=offset_fractions,
    )


def _validate_refined_outline(outline: _Outline, epsilon: float) -> None:
    points_3d = [Vector((point.x, point.y, 0.0)) for point in outline.points]
    raw = _RawOutline(
        outline.object_name,
        outline.material,
        points_3d,
        [(index, (index + 1) % len(points_3d)) for index in range(len(points_3d))],
        0,
        outline.is_curve,
        outline.source_object,
    )
    _ordered_validated_loop(raw, epsilon)


def _refine_classified_outlines(
    ground: _Outline,
    outer: _Outline,
    inner: list[_Outline],
    width: float,
    epsilon: float,
) -> tuple[_Outline, _Outline, list[_Outline]]:
    refined_ground = (
        _increased_resolution_curve_outline(ground, width, None, epsilon)
        if ground.is_curve
        else ground
    )
    refined_outer = (
        _increased_resolution_curve_outline(outer, width, False, epsilon)
        if outer.is_curve
        else outer
    )
    refined_inner = [
        _increased_resolution_curve_outline(outline, width, True, epsilon)
        if outline.is_curve
        else outline
        for outline in inner
    ]
    all_refined = [refined_ground, refined_outer, *refined_inner]
    for outline in all_refined:
        if outline.is_curve and outline.sampling_method != "evaluated_input":
            _validate_refined_outline(outline, epsilon)
    _validate_outline_separation(all_refined, epsilon)
    classified_ground, classified_outer, classified_inner = _classify_outlines(all_refined)
    if (
        classified_ground.object_name != ground.object_name
        or classified_outer.object_name != outer.object_name
        or {item.object_name for item in classified_inner} != {item.object_name for item in inner}
    ):
        raise TrackBuilderGeometryError("Curve refinement changed outline containment roles")
    return classified_ground, classified_outer, classified_inner


def _collection_contains(
    root: bpy.types.Collection,
    target: bpy.types.Collection,
) -> bool:
    if root == target:
        return True
    return any(_collection_contains(child, target) for child in root.children)


def _validate_collection_separation(
    input_collection: bpy.types.Collection,
    output_collection: bpy.types.Collection | None,
) -> None:
    if output_collection is None:
        return
    if not output_collection.is_editable:
        raise TrackBuilderValidationError(
            "The existing Output collection must be local and editable"
        )
    if output_collection.children:
        raise TrackBuilderValidationError(
            "The existing Output collection must not contain child collections"
        )
    if _collection_contains(input_collection, output_collection) or _collection_contains(
        output_collection, input_collection
    ):
        raise TrackBuilderValidationError("Input and Output collections must not be nested")
    input_pointers = {obj.as_pointer() for obj in input_collection.all_objects}
    output_pointers = {obj.as_pointer() for obj in output_collection.all_objects}
    if input_pointers & output_pointers:
        raise TrackBuilderValidationError("Input and Output collections must not share objects")


def _triangulate_region(
    outer: _Outline,
    holes: list[_Outline],
    epsilon: float,
    name: str,
    material: bpy.types.Material,
    role: str,
) -> _MeshPlan:
    loops = [outer.points] + [hole.points for hole in holes]
    coordinates: list[Vector] = []
    constraint_edges: list[tuple[int, int]] = []
    for loop in loops:
        start = len(coordinates)
        coordinates.extend(point.copy() for point in loop)
        constraint_edges.extend(
            (start + index, start + (index + 1) % len(loop))
            for index in range(len(loop))
        )

    triangulated = delaunay_2d_cdt(
        coordinates,
        constraint_edges,
        [],
        0,
        epsilon,
        False,
    )
    output_coordinates, _, output_faces = triangulated[:3]
    kept_faces: list[tuple[int, int, int]] = []
    for face in output_faces:
        if len(face) != 3:
            raise TrackBuilderGeometryError(
                f"Blender CDT returned a non-triangle while constructing {name!r}"
            )
        centroid = sum((output_coordinates[index] for index in face), Vector((0.0, 0.0))) / 3.0
        if not _point_in_polygon(centroid, outer.points):
            continue
        if any(_point_in_polygon(centroid, hole.points) for hole in holes):
            continue
        face_tuple = tuple(face)
        triangle = [output_coordinates[index] for index in face_tuple]
        if _signed_area(triangle) < 0.0:
            face_tuple = (face_tuple[0], face_tuple[2], face_tuple[1])
        kept_faces.append(face_tuple)

    if not kept_faces:
        raise TrackBuilderGeometryError(f"Triangulation produced no faces for {name!r}")

    used_indices = sorted({index for face in kept_faces for index in face})
    remap = {old: new for new, old in enumerate(used_indices)}
    vertices = [
        (float(output_coordinates[index].x), float(output_coordinates[index].y), 0.0)
        for index in used_indices
    ]
    faces = [tuple(remap[index] for index in face) for face in kept_faces]
    properties: dict[str, object] = {
        "track_builder_role": role,
        "track_builder_source": outer.object_name,
    }
    if outer.is_curve:
        properties.update(
            {
                "track_builder_curve_sampling": outer.sampling_method,
                "track_builder_curve_sample_count": len(outer.points),
            }
        )
    return _MeshPlan(
        name=name,
        vertices=vertices,
        faces=faces,
        material=material,
        properties=properties,
    )


def _offset_miter_points(
    points: list[Vector],
    width: float,
    offset_left: bool,
) -> list[Vector]:
    """Intersect adjacent infinite offset lines without boolean cleanup."""

    directions: list[Vector] = []
    normals: list[Vector] = []
    for index, point in enumerate(points):
        direction = (points[(index + 1) % len(points)] - point).normalized()
        left_normal = Vector((-direction.y, direction.x))
        directions.append(direction)
        normals.append(left_normal if offset_left else -left_normal)

    miters: list[Vector] = []
    for index, point in enumerate(points):
        previous_index = (index - 1) % len(points)
        previous_direction = directions[previous_index]
        current_direction = directions[index]
        previous_origin = point + normals[previous_index] * width
        current_origin = point + normals[index] * width
        denominator = _cross_2d(previous_direction, current_direction)
        if abs(denominator) <= 1.0e-14:
            if previous_direction.dot(current_direction) <= 0.0:
                raise TrackBuilderGeometryError(
                    "A barrier miter is undefined at an antiparallel edge pair"
                )
            miter = current_origin
        else:
            parameter = _cross_2d(current_origin - previous_origin, current_direction) / denominator
            miter = previous_origin + previous_direction * parameter
        if not all(math.isfinite(component) for component in miter):
            raise TrackBuilderGeometryError("A barrier miter produced a non-finite coordinate")
        miters.append(miter)
    return miters


def _segment_count(
    perimeter: float,
    target: float,
    material_count: int,
    object_name: str,
) -> int:
    """Choose an adjusted segment count, rejecting unusable extremes."""

    ratio = perimeter / target
    nearest = round(ratio)
    if nearest >= 1 and abs(ratio - nearest) <= INTEGER_RATIO_TOLERANCE * max(1.0, ratio):
        count = nearest
    else:
        count = max(1, math.floor(ratio))
    if count == 1:
        raise TrackBuilderGeometryError(
            f"Outline {object_name!r} would produce only one barrier segment; "
            "decrease segment_length"
        )
    count -= count % material_count
    if count > 0 and perimeter / count < target:
        count -= material_count
    if count == 0:
        raise TrackBuilderGeometryError(
            f"Outline {object_name!r} cannot produce a complete sequence of "
            f"{material_count} barrier materials without making segments shorter than "
            "segment_length; decrease segment_length"
        )
    if count > MAX_SEGMENTS_PER_OUTLINE:
        raise TrackBuilderGeometryError(
            f"Outline {object_name!r} requires {count} barrier segments; "
            f"the maximum is {MAX_SEGMENTS_PER_OUTLINE}"
        )
    return count


def _remove_consecutive_duplicates(points: list[Vector], epsilon: float) -> list[Vector]:
    result: list[Vector] = []
    for point in points:
        if not result or (point - result[-1]).length > epsilon:
            result.append(point)
    if len(result) > 1 and (result[0] - result[-1]).length <= epsilon:
        result.pop()
    return result


def _distinct_point_count(points: list[Vector], epsilon: float) -> int:
    unique: list[Vector] = []
    for point in points:
        if all((point - existing).length > epsilon for existing in unique):
            unique.append(point)
    return len(unique)


def _extruded_barrier_plan(
    name: str,
    polygon: list[Vector],
    height: float,
    material: bpy.types.Material,
    properties: dict[str, object],
) -> _MeshPlan:
    ordered = [point.copy() for point in polygon]
    if _signed_area(ordered) < 0.0:
        ordered.reverse()
    count = len(ordered)
    vertices = [(point.x, point.y, 0.0) for point in ordered]
    vertices.extend((point.x, point.y, height) for point in ordered)
    bottom = tuple(reversed(range(count)))
    top = tuple(range(count, count * 2))
    sides = [
        (
            index,
            (index + 1) % count,
            (index + 1) % count + count,
            index + count,
        )
        for index in range(count)
    ]
    return _MeshPlan(name, vertices, [bottom, top, *sides], material, properties)


def _barrier_plans(
    outline: _Outline,
    role: str,
    role_index: int,
    width: float,
    height: float,
    target_length: float,
    materials: list[bpy.types.Material],
    epsilon: float,
) -> list[_MeshPlan]:
    """Slice one offset outline into gapless, independently colored barriers."""

    points = outline.points
    offset_left = role == "inner_barrier"
    if outline.offset_points is not None:
        miters = outline.offset_points
    elif outline.is_curve:
        miters = _stable_curve_offset_points(points, width, offset_left)
    else:
        miters = _offset_miter_points(points, width, offset_left)
    edge_lengths = [
        (points[(index + 1) % len(points)] - points[index]).length
        for index in range(len(points))
    ]
    cumulative = [0.0]
    for length in edge_lengths:
        cumulative.append(cumulative[-1] + length)
    perimeter = cumulative[-1]
    if outline.offset_source_fractions is None:
        offset_distances = cumulative[:-1]
    else:
        if len(outline.offset_source_fractions) != len(miters):
            raise TrackBuilderGeometryError("Curve offset sampling metadata is inconsistent")
        offset_distances = [fraction * perimeter for fraction in outline.offset_source_fractions]
        if (
            not offset_distances
            or abs(offset_distances[0]) > epsilon
            or any(
                current <= previous
                for previous, current in zip(offset_distances, offset_distances[1:])
            )
            or offset_distances[-1] >= perimeter
        ):
            raise TrackBuilderGeometryError("Curve offset sampling distances are not cyclically ordered")
    count = _segment_count(perimeter, target_length, len(materials), outline.object_name)
    adjusted_length = perimeter / count

    def source_at(distance: float) -> Vector:
        if distance <= 0.0:
            return points[0].copy()
        if distance >= perimeter:
            return points[0].copy()
        edge_index = bisect.bisect_right(cumulative, distance) - 1
        edge_index = min(edge_index, len(points) - 1)
        parameter = (distance - cumulative[edge_index]) / edge_lengths[edge_index]
        return points[edge_index].lerp(points[(edge_index + 1) % len(points)], parameter)

    def offset_at(distance: float) -> Vector:
        if distance <= 0.0 or distance >= perimeter:
            return miters[0].copy()
        edge_index = bisect.bisect_right(offset_distances, distance) - 1
        edge_index = min(edge_index, len(miters) - 1)
        start_distance = offset_distances[edge_index]
        end_distance = (
            offset_distances[edge_index + 1]
            if edge_index + 1 < len(offset_distances)
            else perimeter
        )
        parameter = (distance - start_distance) / (end_distance - start_distance)
        return miters[edge_index].lerp(miters[(edge_index + 1) % len(miters)], parameter)

    plans: list[_MeshPlan] = []
    role_label = "Outer" if role == "outer_barrier" else f"Inner{role_index:02d}"
    for segment_index in range(count):
        start_distance = segment_index * adjusted_length
        end_distance = perimeter if segment_index == count - 1 else (segment_index + 1) * adjusted_length
        source_path = [source_at(start_distance)]
        offset_path = [offset_at(start_distance)]
        for vertex_index in range(1, len(points)):
            vertex_distance = cumulative[vertex_index]
            if start_distance < vertex_distance < end_distance:
                source_path.append(points[vertex_index].copy())
        for vertex_index in range(1, len(miters)):
            vertex_distance = offset_distances[vertex_index]
            if start_distance < vertex_distance < end_distance:
                offset_path.append(miters[vertex_index].copy())
        source_path.append(source_at(end_distance))
        offset_path.append(offset_at(end_distance))
        polygon = _remove_consecutive_duplicates(source_path + list(reversed(offset_path)), epsilon)
        if len(polygon) < 3 or _distinct_point_count(polygon, epsilon) < 3:
            raise TrackBuilderGeometryError(
                f"Barrier segment {segment_index} for outline {outline.object_name!r} "
                "has fewer than three distinct vertices"
            )
        material = materials[segment_index % len(materials)]
        plans.append(
            _extruded_barrier_plan(
                name=f"Barrier_{role_label}_{segment_index:04d}",
                polygon=polygon,
                height=height,
                material=material,
                properties={
                    "track_builder_role": role,
                    "track_builder_source": outline.object_name,
                    "track_builder_segment_index": segment_index,
                    "track_builder_adjusted_segment_length": adjusted_length,
                    **(
                        {
                            "track_builder_curve_sampling": outline.sampling_method,
                            "track_builder_curve_sample_count": len(outline.points),
                            "track_builder_curve_offset_sample_count": len(miters),
                        }
                        if outline.is_curve
                        else {}
                    ),
                },
            )
        )
    return plans


def _build_plans(
    ground: _Outline,
    outer: _Outline,
    inner: list[_Outline],
    width: float,
    height: float,
    target_length: float,
    barrier_materials: list[bpy.types.Material],
    epsilon: float,
) -> list[_MeshPlan]:
    plans = [
        _triangulate_region(ground, [outer], epsilon, "Ground", ground.material, "ground"),
        _triangulate_region(outer, inner, epsilon, "Track", outer.material, "track"),
    ]
    for index, island in enumerate(inner):
        plans.append(
            _triangulate_region(
                island,
                [],
                epsilon,
                f"Island_{index:02d}",
                island.material,
                "island",
            )
        )
    plans.extend(
        _barrier_plans(
            outer,
            "outer_barrier",
            0,
            width,
            height,
            target_length,
            barrier_materials,
            epsilon,
        )
    )
    for index, island in enumerate(inner):
        plans.extend(
            _barrier_plans(
                island,
                "inner_barrier",
                index,
                width,
                height,
                target_length,
                barrier_materials,
                epsilon,
            )
        )
    return plans


def _collection_tree(root: bpy.types.Collection) -> list[bpy.types.Collection]:
    result = [root]
    for child in root.children:
        result.extend(_collection_tree(child))
    return result


def _remove_collection_tree(root: bpy.types.Collection) -> None:
    collections = _collection_tree(root)
    collection_pointers = {collection.as_pointer() for collection in collections}
    objects = {
        obj.as_pointer(): obj
        for collection in collections
        for obj in collection.objects
    }
    meshes_to_check: list[bpy.types.Mesh] = []
    for obj in objects.values():
        linked_outside = any(
            collection.as_pointer() not in collection_pointers
            for collection in obj.users_collection
        )
        if linked_outside:
            continue
        if isinstance(obj.data, bpy.types.Mesh):
            meshes_to_check.append(obj.data)
        bpy.data.objects.remove(obj, do_unlink=True)
    for collection in reversed(collections):
        if collection.name in bpy.data.collections:
            bpy.data.collections.remove(collection, do_unlink=True)
    for mesh in meshes_to_check:
        if mesh.name in bpy.data.meshes and mesh.users == 0:
            bpy.data.meshes.remove(mesh)


def _instantiate_plans(
    plans: list[_MeshPlan],
) -> tuple[bpy.types.Collection, list[tuple[bpy.types.Object, str]]]:
    pending_name = f"__TrackBuilderPending_{uuid.uuid4().hex}"
    pending = bpy.data.collections.new(pending_name)
    bpy.context.scene.collection.children.link(pending)
    created: list[tuple[bpy.types.Object, str]] = []
    try:
        for index, plan in enumerate(plans):
            temporary_name = f"{pending_name}_{index:05d}"
            mesh = bpy.data.meshes.new(f"{temporary_name}Mesh")
            mesh.from_pydata(plan.vertices, [], plan.faces)
            mesh.materials.append(plan.material)
            mesh.validate(clean_customdata=False)
            mesh.update()
            obj = bpy.data.objects.new(temporary_name, mesh)
            pending.objects.link(obj)
            for key, value in plan.properties.items():
                obj[key] = value
            created.append((obj, plan.name))
    except Exception:
        _remove_collection_tree(pending)
        raise
    return pending, created


def _commit_output(
    pending: bpy.types.Collection,
    created: list[tuple[bpy.types.Object, str]],
    previous: bpy.types.Collection | None,
) -> bpy.types.Collection:
    previous_temporary_name = None
    if previous is not None:
        previous_temporary_name = f"__TrackBuilderPrevious_{uuid.uuid4().hex}"
        previous.name = previous_temporary_name
    try:
        pending.name = "Output"
    except Exception:
        if previous is not None:
            previous.name = "Output"
        _remove_collection_tree(pending)
        raise

    if previous is not None:
        _remove_collection_tree(previous)
    for obj, desired_name in created:
        obj.name = desired_name
        if isinstance(obj.data, bpy.types.Mesh):
            obj.data.name = f"{desired_name}Mesh"
    return pending


def build_track(
    W: float,
    H: float,
    segment_length: float,
    material_names: list[str],
) -> bpy.types.Collection:
    """Validate the current file and transactionally rebuild its Output collection.

    ``W``, ``H``, and ``segment_length`` must be finite and positive.
    ``material_names`` must be a list of at least two existing Blender materials.
    The current file must satisfy the input contract documented in
    ``Documentation/README.md``.

    Returns the newly committed ``Output`` collection. Validation or geometry
    failures preserve the existing output and raise a ``TrackBuilderError``.
    """

    width = _positive_finite_number("W", W)
    height = _positive_finite_number("H", H)
    target_length = _positive_finite_number("segment_length", segment_length)
    barrier_materials = _validated_materials(material_names)

    input_collection = bpy.data.collections.get("Input")
    if input_collection is None:
        raise TrackBuilderValidationError("The file does not contain a collection named 'Input'")
    previous_output = bpy.data.collections.get("Output")
    _validate_collection_separation(input_collection, previous_output)

    raw_outlines = _read_raw_outlines(input_collection)
    epsilon = _world_epsilon(raw_outlines)
    outlines = _validated_outlines(raw_outlines, epsilon)
    ground, outer, inner = _classify_outlines(outlines)
    ground, outer, inner = _refine_classified_outlines(
        ground,
        outer,
        inner,
        width,
        epsilon,
    )
    plans = _build_plans(
        ground,
        outer,
        inner,
        width,
        height,
        target_length,
        barrier_materials,
        epsilon,
    )
    pending, created = _instantiate_plans(plans)
    return _commit_output(pending, created, previous_output)
