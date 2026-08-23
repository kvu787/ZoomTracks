"""Fast 2D oriented-rectangle versus two immutable outline-loop queries.

The intended mapping for ZoomTracks is Unity's ground plane: use world X as the
first coordinate and world Z as the second coordinate. Inputs are two ordered,
closed vertex loops; their closing edges are implicit. Every implementation uses
the same inclusive separating-axis narrow phase and differs only in broad phase.

The module has no third-party dependencies.
"""

from __future__ import annotations

from dataclasses import dataclass
from math import cos, floor, hypot, inf, isfinite, nextafter, sin, sqrt, ulp
from statistics import median
from sys import float_info
from typing import Iterable, Iterator, Sequence, TypeAlias


Segment: TypeAlias = tuple[float, float, float, float]
Point: TypeAlias = tuple[float, float]
OutlineLoop: TypeAlias = tuple[Point, ...]
_Record: TypeAlias = tuple[
    float,
    float,
    float,
    float,
    float,
    float,
    float,
    float,
    float,
    float,
    float,
    float,
    float,
]


@dataclass(frozen=True, slots=True)
class OrientedRectangle:
    """An oriented rectangle on a 2D plane.

    ``axis_x, axis_y`` is the unit direction of the rectangle's local X axis.
    Its local Y axis is the perpendicular vector ``(-axis_y, axis_x)``.
    Half-extents may be zero, which gives the natural line/point semantics.
    """

    center_x: float
    center_y: float
    half_x: float
    half_y: float
    axis_x: float
    axis_y: float

    def __post_init__(self) -> None:
        values = (
            self.center_x,
            self.center_y,
            self.half_x,
            self.half_y,
            self.axis_x,
            self.axis_y,
        )
        if not all(isfinite(value) for value in values):
            raise ValueError("rectangle values must all be finite")
        if self.half_x < 0.0 or self.half_y < 0.0:
            raise ValueError("rectangle half-extents must be nonnegative")
        axis_length_squared = self.axis_x * self.axis_x + self.axis_y * self.axis_y
        if not 1.0 - 1.0e-12 <= axis_length_squared <= 1.0 + 1.0e-12:
            raise ValueError("rectangle axis must be unit length")

    @classmethod
    def from_angle(
        cls,
        center_x: float,
        center_y: float,
        half_x: float,
        half_y: float,
        angle_radians: float,
    ) -> OrientedRectangle:
        """Construct a rectangle whose local X axis has ``angle_radians``."""

        if not isfinite(angle_radians):
            raise ValueError("rectangle angle must be finite")
        return cls(
            float(center_x),
            float(center_y),
            float(half_x),
            float(half_y),
            cos(angle_radians),
            sin(angle_radians),
        )


def _make_record(x0: float, y0: float, x1: float, y1: float) -> _Record:
    """Prepare one already-validated edge without losing original endpoints."""

    difference_x = x1 - x0
    difference_y = y1 - y0
    if isfinite(difference_x):
        half_vector_x = difference_x * 0.5
        midpoint_x = x0 + half_vector_x
    else:
        midpoint_x = x0 * 0.5 + x1 * 0.5
        half_vector_x = x1 * 0.5 - x0 * 0.5
    if isfinite(difference_y):
        half_vector_y = difference_y * 0.5
        midpoint_y = y0 + half_vector_y
    else:
        midpoint_y = y0 * 0.5 + y1 * 0.5
        half_vector_y = y1 * 0.5 - y0 * 0.5
    return (
        midpoint_x,
        midpoint_y,
        half_vector_x,
        half_vector_y,
        min(x0, x1),
        min(y0, y1),
        max(x0, x1),
        max(y0, y1),
        x0,
        y0,
        x1,
        y1,
        max(ulp(abs(x0)), ulp(abs(y0)), ulp(abs(x1)), ulp(abs(y1))),
    )


class PreparedOutlines:
    """Validated immutable representation of the two contracted outline loops.

    ``outer_vertices`` and ``inner_vertices`` omit a repeated closing vertex.
    Each canonical tuple is snapshotted, and one prepared edge record is derived
    from every vertex to its cyclic successor. Records retain midpoint,
    half-vector, AABB, and exact source endpoints for robust narrow-phase work.
    """

    __slots__ = ("_outer", "_inner", "_records", "_bounds", "_loop_ranges")

    _outer: OutlineLoop
    _inner: OutlineLoop
    _records: tuple[_Record, ...]
    _bounds: tuple[float, float, float, float]
    _loop_ranges: tuple[tuple[int, int], tuple[int, int]]

    def __setattr__(self, name: str, value: object) -> None:
        try:
            object.__getattribute__(self, name)
        except AttributeError:
            object.__setattr__(self, name, value)
            return
        raise AttributeError(f"{name} is read-only")

    def __delattr__(self, name: str) -> None:
        raise AttributeError(f"{name} is read-only")

    def __init__(
        self,
        outer_vertices: Iterable[Sequence[float]],
        inner_vertices: Iterable[Sequence[float]],
    ) -> None:
        loops = (
            self._validate_loop(outer_vertices, "outer"),
            self._validate_loop(inner_vertices, "inner"),
        )
        records: list[_Record] = []
        all_min_x = inf
        all_min_y = inf
        all_max_x = -inf
        all_max_y = -inf

        for loop in loops:
            for index, (x0, y0) in enumerate(loop):
                x1, y1 = loop[(index + 1) % len(loop)]
                record = _make_record(x0, y0, x1, y1)
                records.append(record)
                all_min_x = min(all_min_x, record[4])
                all_min_y = min(all_min_y, record[5])
                all_max_x = max(all_max_x, record[6])
                all_max_y = max(all_max_y, record[7])

        outer_count = len(loops[0])
        self._outer = loops[0]
        self._inner = loops[1]
        self._records = tuple(records)
        self._bounds = (all_min_x, all_min_y, all_max_x, all_max_y)
        self._loop_ranges = ((0, outer_count), (outer_count, len(records)))

    @staticmethod
    def _validate_loop(
        source: Iterable[Sequence[float]], name: str
    ) -> OutlineLoop:
        try:
            source_vertices = tuple(source)
        except TypeError as error:
            raise ValueError(f"{name} outline must be an iterable of vertices") from error
        if len(source_vertices) < 3:
            raise ValueError(f"{name} outline must contain at least three vertices")

        vertices: list[Point] = []
        for index, source_vertex in enumerate(source_vertices):
            try:
                if len(source_vertex) != 2:
                    raise ValueError
                x, y = (float(value) for value in source_vertex)
            except (TypeError, ValueError, OverflowError) as error:
                raise ValueError(
                    f"{name} vertex {index} must contain exactly two numeric values"
                ) from error
            if not isfinite(x) or not isfinite(y):
                raise ValueError(f"{name} vertex {index} values must be finite")
            vertices.append((x, y))

        for index, vertex in enumerate(vertices):
            if vertex == vertices[(index + 1) % len(vertices)]:
                raise ValueError(
                    f"{name} vertices {index} and {(index + 1) % len(vertices)} "
                    "must be distinct"
                )
        return tuple(vertices)

    @property
    def outer(self) -> OutlineLoop:
        return self._outer

    @property
    def outer_vertices(self) -> OutlineLoop:
        """Canonical immutable outer vertex loop."""

        return self._outer

    @property
    def inner(self) -> OutlineLoop:
        return self._inner

    @property
    def inner_vertices(self) -> OutlineLoop:
        """Canonical immutable inner vertex loop."""

        return self._inner

    @property
    def records(self) -> tuple[_Record, ...]:
        return self._records

    @property
    def bounds(self) -> tuple[float, float, float, float]:
        return self._bounds

    @property
    def loop_ranges(self) -> tuple[tuple[int, int], tuple[int, int]]:
        return self._loop_ranges

    @property
    def edge_count(self) -> int:
        return len(self._records)

    def __len__(self) -> int:
        return len(self.records)


def prepare_outlines(
    outer_vertices: PreparedOutlines | Iterable[Sequence[float]],
    inner_vertices: Iterable[Sequence[float]] | None = None,
) -> PreparedOutlines:
    """Prepare two loops, or return an existing immutable preparation unchanged."""

    if isinstance(outer_vertices, PreparedOutlines):
        if inner_vertices is not None:
            raise TypeError("inner_vertices must be omitted with PreparedOutlines")
        return outer_vertices
    if inner_vertices is None:
        raise TypeError("inner_vertices is required when outlines are not prepared")
    return PreparedOutlines(outer_vertices, inner_vertices)


def _validate_padding(
    padding: float, rectangle: OrientedRectangle | None = None
) -> float:
    padding = float(padding)
    if not isfinite(padding) or padding < 0.0:
        raise ValueError("padding must be a finite nonnegative distance")
    if rectangle is not None and (
        not isfinite(rectangle.half_x + padding)
        or not isfinite(rectangle.half_y + padding)
    ):
        raise ValueError("padding makes the rectangle extents non-finite")
    return padding


def segment_intersects_rectangle(
    segment: Sequence[float],
    rectangle: OrientedRectangle,
    padding: float = 0.0,
) -> bool:
    """Test one segment with the shared SAT and robust clipping predicate.

    ``padding`` expands each rectangle half-extent by that world-space amount.
    With zero padding, touching still counts because all comparisons are
    inclusive.
    """

    try:
        if len(segment) != 4:
            raise ValueError
        x0, y0, x1, y1 = (float(value) for value in segment)
    except (TypeError, ValueError, OverflowError) as error:
        raise ValueError("segment must contain exactly four numeric values") from error
    if not all(isfinite(value) for value in (x0, y0, x1, y1)):
        raise ValueError("segment values must all be finite")
    return _linear_records_intersect(
        (_make_record(x0, y0, x1, y1),),
        rectangle,
        _validate_padding(padding, rectangle),
    )


def _endpoint_clipping_intersects(
    record: _Record,
    center_x: float,
    center_y: float,
    axis_x: float,
    axis_y: float,
    extent_x: float,
    extent_y: float,
) -> bool:
    """Robust endpoint-space Liang-Barsky fallback for failed fast SAT tests."""

    relative_x0 = record[8] - center_x
    relative_y0 = record[9] - center_y
    relative_x1 = record[10] - center_x
    relative_y1 = record[11] - center_y
    local_extent_x = extent_x
    local_extent_y = extent_y
    world_roundoff = 8.0 * max(
        ulp(abs(record[8])),
        ulp(abs(record[9])),
        ulp(abs(record[10])),
        ulp(abs(record[11])),
        ulp(abs(center_x)),
        ulp(abs(center_y)),
    )
    base_scale = 1.0
    if not all(
        isfinite(value)
        for value in (relative_x0, relative_y0, relative_x1, relative_y1)
    ):
        world_scale = max(
            abs(record[8]),
            abs(record[9]),
            abs(record[10]),
            abs(record[11]),
            abs(center_x),
            abs(center_y),
            extent_x,
            extent_y,
        )
        if world_scale == 0.0 or not isfinite(world_scale):
            return False
        relative_x0 = record[8] / world_scale - center_x / world_scale
        relative_y0 = record[9] / world_scale - center_y / world_scale
        relative_x1 = record[10] / world_scale - center_x / world_scale
        relative_y1 = record[11] / world_scale - center_y / world_scale
        local_extent_x /= world_scale
        local_extent_y /= world_scale
        base_scale = world_scale

    projection_scale = max(
        abs(relative_x0),
        abs(relative_y0),
        abs(relative_x1),
        abs(relative_y1),
        local_extent_x,
        local_extent_y,
    )
    if projection_scale == 0.0:
        return True
    relative_x0 /= projection_scale
    relative_y0 /= projection_scale
    relative_x1 /= projection_scale
    relative_y1 /= projection_scale
    local_extent_x /= projection_scale
    local_extent_y /= projection_scale
    input_roundoff = world_roundoff / base_scale / projection_scale

    start_x = relative_x0 * axis_x + relative_y0 * axis_y
    start_y = relative_y0 * axis_x - relative_x0 * axis_y
    end_x = relative_x1 * axis_x + relative_y1 * axis_y
    end_y = relative_y1 * axis_x - relative_x1 * axis_y
    minimum_t = 0.0
    maximum_t = 1.0

    for start, end, extent in (
        (start_x, end_x, local_extent_x),
        (start_y, end_y, local_extent_y),
    ):
        coordinate_tolerance = 16.0 * ulp(
            max(abs(start), abs(end), extent)
        ) + input_roundoff
        expanded_extent = extent + coordinate_tolerance
        delta = end - start
        if delta == 0.0:
            if start < -expanded_extent or start > expanded_extent:
                return False
            continue
        enter = (-expanded_extent - start) / delta
        leave = (expanded_extent - start) / delta
        if enter > leave:
            enter, leave = leave, enter
        minimum_t = max(minimum_t, enter)
        maximum_t = min(maximum_t, leave)
        if minimum_t > maximum_t and minimum_t - maximum_t > (
            16.0 * ulp(max(abs(minimum_t), abs(maximum_t)))
        ):
            return False
    return True


def _scaled_record_intersects(
    record: _Record,
    center_x: float,
    center_y: float,
    axis_x: float,
    axis_y: float,
    extent_x: float,
    extent_y: float,
) -> bool:
    """Overflow-safe SAT fallback using dimensionless world-space values."""

    relative_x = record[0] - center_x
    relative_y = record[1] - center_y
    scale = max(
        abs(relative_x),
        abs(relative_y),
        abs(record[2]),
        abs(record[3]),
        extent_x,
        extent_y,
    )
    if scale == 0.0:
        return True
    if not isfinite(scale):
        return _endpoint_clipping_intersects(
            record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
        )

    relative_x /= scale
    relative_y /= scale
    half_x = record[2] / scale
    half_y = record[3] / scale
    scaled_extent_x = extent_x / scale
    scaled_extent_y = extent_y / scale
    center_local_x = relative_x * axis_x + relative_y * axis_y
    center_local_y = relative_y * axis_x - relative_x * axis_y
    half_local_x = half_x * axis_x + half_y * axis_y
    half_local_y = half_y * axis_x - half_x * axis_y
    absolute_half_x = abs(half_local_x)
    absolute_half_y = abs(half_local_y)

    left = abs(center_local_x)
    right = scaled_extent_x + absolute_half_x
    if left > right and left - right > 16.0 * ulp(max(left, right)):
        return False
    left = abs(center_local_y)
    right = scaled_extent_y + absolute_half_y
    if left > right and left - right > 16.0 * ulp(max(left, right)):
        return False
    left = abs(center_local_x * half_local_y - center_local_y * half_local_x)
    right = (
        scaled_extent_x * abs(half_local_y)
        + scaled_extent_y * abs(half_local_x)
    )
    return left <= right or left - right <= 16.0 * ulp(max(left, right))


def _linear_records_intersect(
    records: Sequence[_Record],
    rectangle: OrientedRectangle,
    padding: float,
) -> bool:
    return _linear_records_intersect_values(
        records,
        rectangle.center_x,
        rectangle.center_y,
        rectangle.axis_x,
        rectangle.axis_y,
        rectangle.half_x + padding,
        rectangle.half_y + padding,
    )


def _linear_records_intersect_values(
    records: Sequence[_Record],
    center_x: float,
    center_y: float,
    axis_x: float,
    axis_y: float,
    extent_x: float,
    extent_y: float,
) -> bool:
    """Hot linear loop. Validation must already have happened."""

    absolute = abs
    absolute_axis_x = absolute(axis_x)
    absolute_axis_y = absolute(axis_y)
    world_half_x = nextafter(
        extent_x * absolute_axis_x + extent_y * absolute_axis_y, inf
    )
    world_half_y = nextafter(
        extent_x * absolute_axis_y + extent_y * absolute_axis_x, inf
    )
    query_min_x = nextafter(center_x - world_half_x, -inf)
    query_min_y = nextafter(center_y - world_half_y, -inf)
    query_max_x = nextafter(center_x + world_half_x, inf)
    query_max_y = nextafter(center_y + world_half_y, inf)
    query_coordinate_ulp = ulp(abs(center_x)) + ulp(abs(center_y))

    for record in records:
        if (
            record[6] < query_min_x
            or record[4] > query_max_x
            or record[7] < query_min_y
            or record[5] > query_max_y
        ):
            continue
        if _record_intersects(
            record,
            center_x,
            center_y,
            axis_x,
            axis_y,
            extent_x,
            extent_y,
            query_coordinate_ulp,
        ):
            return True

    return False


def _record_intersects(
    record: _Record,
    center_x: float,
    center_y: float,
    axis_x: float,
    axis_y: float,
    extent_x: float,
    extent_y: float,
    query_coordinate_ulp: float | None = None,
) -> bool:
    """Fast SAT with clipping reserved for numerically ambiguous separation."""

    relative_x = record[0] - center_x
    relative_y = record[1] - center_y
    half_local_x = record[2] * axis_x + record[3] * axis_y
    half_local_y = record[3] * axis_x - record[2] * axis_y
    absolute_half_x = abs(half_local_x)
    absolute_half_y = abs(half_local_y)

    # The segment-normal determinant is rotation-invariant. Testing it directly
    # in world space defers midpoint projections for the common grazing miss.
    cross_a = relative_x * record[3]
    cross_b = relative_y * record[2]
    support_a = extent_x * absolute_half_y
    support_b = extent_y * absolute_half_x
    left = abs(cross_a - cross_b)
    right = support_a + support_b
    product_underflow = (left == 0.0 or right == 0.0) and (
        (cross_a == 0.0 and relative_x != 0.0 and record[3] != 0.0)
        or (cross_b == 0.0 and relative_y != 0.0 and record[2] != 0.0)
        or (support_a == 0.0 and extent_x != 0.0 and absolute_half_y != 0.0)
        or (support_b == 0.0 and extent_y != 0.0 and absolute_half_x != 0.0)
    )
    if product_underflow:
        if _scaled_record_intersects(
            record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
        ):
            return True
        return _endpoint_clipping_intersects(
            record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
        )
    if not isfinite(left) or not isfinite(right):
        if _scaled_record_intersects(
            record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
        ):
            return True
        return _endpoint_clipping_intersects(
            record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
        )
    if left > right:
        if query_coordinate_ulp is None:
            query_coordinate_ulp = ulp(abs(center_x)) + ulp(abs(center_y))
        coordinate_uncertainty = 64.0 * (record[12] + query_coordinate_ulp)
        cross_scale = (
            abs(relative_x)
            + abs(relative_y)
            + abs(record[2])
            + abs(record[3])
            + extent_x
            + extent_y
            + 1.0
        )
        tolerance = coordinate_uncertainty * cross_scale * 16.0 + (
            64.0 * float_info.epsilon * cross_scale * cross_scale
        )
        if left - right > tolerance:
            return False
        return _endpoint_clipping_intersects(
            record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
        )

    center_local_x = relative_x * axis_x + relative_y * axis_y
    center_local_y = relative_y * axis_x - relative_x * axis_y
    left = abs(center_local_x)
    right = extent_x + absolute_half_x
    if left > right:
        if query_coordinate_ulp is None:
            query_coordinate_ulp = ulp(abs(center_x)) + ulp(abs(center_y))
        coordinate_uncertainty = 64.0 * (record[12] + query_coordinate_ulp)
        if left - right > coordinate_uncertainty + 64.0 * float_info.epsilon * (
            left + right + 1.0
        ):
            return False
        return _endpoint_clipping_intersects(
            record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
        )
    left = abs(center_local_y)
    right = extent_y + absolute_half_y
    if left > right:
        if query_coordinate_ulp is None:
            query_coordinate_ulp = ulp(abs(center_x)) + ulp(abs(center_y))
        coordinate_uncertainty = 64.0 * (record[12] + query_coordinate_ulp)
        if left - right > coordinate_uncertainty + 64.0 * float_info.epsilon * (
            left + right + 1.0
        ):
            return False
        return _endpoint_clipping_intersects(
            record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
        )
    return True


def _range_records_intersect(
    records: Sequence[_Record],
    start: int,
    end: int,
    query_min_x: float,
    query_min_y: float,
    query_max_x: float,
    query_max_y: float,
    constants: tuple[
        float,
        float,
        float,
        float,
        float,
        float,
        float,
        float,
        float,
        float,
        float,
    ],
) -> bool:
    """Fused leaf-range AABB filtering and SAT narrow phase."""

    center_x = constants[0]
    center_y = constants[1]
    axis_x = constants[2]
    axis_y = constants[3]
    extent_x = constants[4]
    extent_y = constants[5]
    query_coordinate_ulp = constants[10]
    absolute = abs
    for index in range(start, end):
        record = records[index]
        if (
            record[6] < query_min_x
            or record[4] > query_max_x
            or record[7] < query_min_y
            or record[5] > query_max_y
        ):
            continue
        relative_x = record[0] - center_x
        relative_y = record[1] - center_y
        half_local_x = record[2] * axis_x + record[3] * axis_y
        half_local_y = record[3] * axis_x - record[2] * axis_y
        absolute_half_x = absolute(half_local_x)
        absolute_half_y = absolute(half_local_y)

        cross_a = relative_x * record[3]
        cross_b = relative_y * record[2]
        support_a = extent_x * absolute_half_y
        support_b = extent_y * absolute_half_x
        left = absolute(cross_a - cross_b)
        right = support_a + support_b
        product_underflow = (left == 0.0 or right == 0.0) and (
            (cross_a == 0.0 and relative_x != 0.0 and record[3] != 0.0)
            or (cross_b == 0.0 and relative_y != 0.0 and record[2] != 0.0)
            or (support_a == 0.0 and extent_x != 0.0 and absolute_half_y != 0.0)
            or (support_b == 0.0 and extent_y != 0.0 and absolute_half_x != 0.0)
        )
        if product_underflow:
            if _scaled_record_intersects(
                record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
            ) or _endpoint_clipping_intersects(
                record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
            ):
                return True
            continue
        if not isfinite(left) or not isfinite(right):
            if _scaled_record_intersects(
                record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
            ) or _endpoint_clipping_intersects(
                record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
            ):
                return True
            continue
        if left > right:
            coordinate_uncertainty = 64.0 * (
                record[12] + query_coordinate_ulp
            )
            cross_scale = (
                absolute(relative_x)
                + absolute(relative_y)
                + absolute(record[2])
                + absolute(record[3])
                + extent_x
                + extent_y
                + 1.0
            )
            tolerance = coordinate_uncertainty * cross_scale * 16.0 + (
                64.0 * float_info.epsilon * cross_scale * cross_scale
            )
            if left - right > tolerance:
                continue
            if _endpoint_clipping_intersects(
                record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
            ):
                return True
            continue

        center_local_x = relative_x * axis_x + relative_y * axis_y
        center_local_y = relative_y * axis_x - relative_x * axis_y
        left = absolute(center_local_x)
        right = extent_x + absolute_half_x
        if left > right:
            coordinate_uncertainty = 64.0 * (
                record[12] + query_coordinate_ulp
            )
            if left - right > coordinate_uncertainty + 64.0 * float_info.epsilon * (
                left + right + 1.0
            ):
                continue
            if _endpoint_clipping_intersects(
                record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
            ):
                return True
            continue
        left = absolute(center_local_y)
        right = extent_y + absolute_half_y
        if left > right:
            coordinate_uncertainty = 64.0 * (
                record[12] + query_coordinate_ulp
            )
            if left - right > coordinate_uncertainty + 64.0 * float_info.epsilon * (
                left + right + 1.0
            ):
                continue
            if _endpoint_clipping_intersects(
                record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
            ):
                return True
            continue
        return True
    return False


def _query_constants(
    rectangle: OrientedRectangle, padding: float
) -> tuple[
    float,
    float,
    float,
    float,
    float,
    float,
    float,
    float,
    float,
    float,
    float,
]:
    """Values shared by AABB broad-phase tests for one query."""

    center_x = rectangle.center_x
    center_y = rectangle.center_y
    axis_x = rectangle.axis_x
    axis_y = rectangle.axis_y
    extent_x = rectangle.half_x + padding
    extent_y = rectangle.half_y + padding
    absolute_axis_x = abs(axis_x)
    absolute_axis_y = abs(axis_y)
    # Broad phases must never reject a narrow-phase contact due to a rounded-in
    # extent, so their world-space half-extents are rounded outward by one ULP.
    world_half_x = nextafter(
        extent_x * absolute_axis_x + extent_y * absolute_axis_y, inf
    )
    world_half_y = nextafter(
        extent_x * absolute_axis_y + extent_y * absolute_axis_x, inf
    )
    return (
        center_x,
        center_y,
        axis_x,
        axis_y,
        extent_x,
        extent_y,
        absolute_axis_x,
        absolute_axis_y,
        world_half_x,
        world_half_y,
        ulp(abs(center_x)) + ulp(abs(center_y)),
    )


def _query_world_aabb_overlaps(
    bounds: tuple[float, float, float, float],
    constants: tuple[
        float,
        float,
        float,
        float,
        float,
        float,
        float,
        float,
        float,
        float,
        float,
    ],
) -> bool:
    """Very cheap conservative test used only for whole-index rejection."""

    center_x = constants[0]
    center_y = constants[1]
    world_half_x = constants[8]
    world_half_y = constants[9]
    min_x, min_y, max_x, max_y = bounds
    return not (
        max_x < nextafter(center_x - world_half_x, -inf)
        or min_x > nextafter(center_x + world_half_x, inf)
        or max_y < nextafter(center_y - world_half_y, -inf)
        or min_y > nextafter(center_y + world_half_y, inf)
    )


class _FrozenConfiguration:
    """Prevent structural index settings from becoming silently inconsistent."""

    __slots__ = ()

    def __setattr__(self, name: str, value: object) -> None:
        try:
            object.__getattribute__(self, name)
        except AttributeError:
            object.__setattr__(self, name, value)
            return
        raise AttributeError(f"{name} is read-only; rebuild the index to retune it")

    def __delattr__(self, name: str) -> None:
        raise AttributeError(f"{name} is read-only; rebuild the index to retune it")


class LinearScanIndex(_FrozenConfiguration):
    """Overall-bounds rejection followed by a branch-light segment scan."""

    __slots__ = ("outlines",)

    def __init__(self, outlines: PreparedOutlines) -> None:
        self.outlines = prepare_outlines(outlines)

    def intersects(
        self, rectangle: OrientedRectangle, padding: float = 0.0
    ) -> bool:
        padding = _validate_padding(padding, rectangle)
        bounds = self.outlines.bounds
        constants = _query_constants(rectangle, padding)
        if not _query_world_aabb_overlaps(bounds, constants):
            return False
        return _linear_records_intersect_values(
            self.outlines.records,
            constants[0],
            constants[1],
            constants[2],
            constants[3],
            constants[4],
            constants[5],
        )


class CoherentBlockIndex(_FrozenConfiguration):
    """A low-build-cost broad phase for spatially ordered outline segments.

    Consecutive segments are grouped into small blocks.  Each query first tests
    a block AABB and only scans the block's segments when it overlaps.  Authored
    outline order is normally spatially coherent, which makes those AABBs tight.
    """

    __slots__ = ("outlines", "block_size", "_blocks")

    def __init__(
        self,
        outlines: PreparedOutlines,
        block_size: int = 16,
    ) -> None:
        if isinstance(block_size, bool) or not isinstance(block_size, int):
            raise TypeError("block_size must be an integer")
        if block_size < 1:
            raise ValueError("block_size must be positive")

        self.outlines = prepare_outlines(outlines)
        self.block_size = block_size
        records = self.outlines.records
        blocks: list[tuple[float, float, float, float, int, int]] = []

        # Restart at each loop boundary. A block spanning the unrelated inner/
        # outer join would have a needlessly loose AABB.
        for loop_start, loop_end in self.outlines.loop_ranges:
            for start in range(loop_start, loop_end, block_size):
                end = min(start + block_size, loop_end)
                first = records[start]
                min_x = first[4]
                min_y = first[5]
                max_x = first[6]
                max_y = first[7]
                for index in range(start + 1, end):
                    record = records[index]
                    min_x = min(min_x, record[4])
                    min_y = min(min_y, record[5])
                    max_x = max(max_x, record[6])
                    max_y = max(max_y, record[7])
                blocks.append((min_x, min_y, max_x, max_y, start, end))

        self._blocks = tuple(blocks)

    def intersects(
        self, rectangle: OrientedRectangle, padding: float = 0.0
    ) -> bool:
        padding = _validate_padding(padding, rectangle)
        constants = _query_constants(rectangle, padding)
        bounds = self.outlines.bounds
        if not _query_world_aabb_overlaps(bounds, constants):
            return False
        center_x = constants[0]
        center_y = constants[1]
        axis_x = constants[2]
        axis_y = constants[3]
        extent_x = constants[4]
        extent_y = constants[5]
        query_min_x = nextafter(center_x - constants[8], -inf)
        query_min_y = nextafter(center_y - constants[9], -inf)
        query_max_x = nextafter(center_x + constants[8], inf)
        query_max_y = nextafter(center_y + constants[9], inf)
        records = self.outlines.records

        for min_x, min_y, max_x, max_y, start, end in self._blocks:
            if (
                max_x < query_min_x
                or min_x > query_max_x
                or max_y < query_min_y
                or min_y > query_max_y
            ):
                continue
            if _range_records_intersect(
                records,
                start,
                end,
                query_min_x,
                query_min_y,
                query_max_x,
                query_max_y,
                constants,
            ):
                return True
        return False


class CoherentHierarchyIndex(_FrozenConfiguration):
    """Stackless AABB hierarchy over contiguous ranges of each outline loop.

    Nodes are laid out in preorder with an escape index. A rejected subtree can
    therefore be skipped without allocating or mutating a query-time stack.
    Loop ranges come from ``PreparedOutlines`` and always have separate roots.
    """

    __slots__ = ("outlines", "leaf_size", "branching_factor", "_nodes")

    def __init__(
        self,
        outlines: PreparedOutlines,
        leaf_size: int = 8,
        branching_factor: int = 4,
    ) -> None:
        if isinstance(leaf_size, bool) or not isinstance(leaf_size, int):
            raise TypeError("leaf_size must be an integer")
        if leaf_size < 1:
            raise ValueError("leaf_size must be positive")
        if (
            isinstance(branching_factor, bool)
            or not isinstance(branching_factor, int)
            or branching_factor < 2
        ):
            raise ValueError("branching_factor must be an integer of at least two")
        self.outlines = prepare_outlines(outlines)
        self.leaf_size = leaf_size
        self.branching_factor = branching_factor
        nodes: list[tuple[float, float, float, float, int, int, int]] = []
        self._nodes = nodes
        for start, end in self.outlines.loop_ranges:
            self._build_range(start, end)
        object.__setattr__(self, "_nodes", tuple(nodes))

    def _build_range(
        self, start: int, end: int
    ) -> tuple[float, float, float, float]:
        records = self.outlines.records
        nodes = self._nodes
        node_index = len(nodes)
        nodes.append((0.0, 0.0, 0.0, 0.0, 0, -1, -1))
        if end - start <= self.leaf_size:
            first = records[start]
            min_x = first[4]
            min_y = first[5]
            max_x = first[6]
            max_y = first[7]
            for index in range(start + 1, end):
                record = records[index]
                min_x = min(min_x, record[4])
                min_y = min(min_y, record[5])
                max_x = max(max_x, record[6])
                max_y = max(max_y, record[7])
            nodes[node_index] = (
                min_x,
                min_y,
                max_x,
                max_y,
                node_index + 1,
                start,
                end,
            )
            return min_x, min_y, max_x, max_y

        edge_count = end - start
        child_count = min(
            self.branching_factor,
            (edge_count + self.leaf_size - 1) // self.leaf_size,
        )
        child_bounds: list[tuple[float, float, float, float]] = []
        for child in range(child_count):
            child_start = start + edge_count * child // child_count
            child_end = start + edge_count * (child + 1) // child_count
            child_bounds.append(self._build_range(child_start, child_end))
        bounds = (
            min(bound[0] for bound in child_bounds),
            min(bound[1] for bound in child_bounds),
            max(bound[2] for bound in child_bounds),
            max(bound[3] for bound in child_bounds),
        )
        nodes[node_index] = (*bounds, len(nodes), -1, -1)
        return bounds

    def intersects(
        self, rectangle: OrientedRectangle, padding: float = 0.0
    ) -> bool:
        padding = _validate_padding(padding, rectangle)
        constants = _query_constants(rectangle, padding)
        center_x = constants[0]
        center_y = constants[1]
        axis_x = constants[2]
        axis_y = constants[3]
        extent_x = constants[4]
        extent_y = constants[5]
        query_min_x = nextafter(center_x - constants[8], -inf)
        query_min_y = nextafter(center_y - constants[9], -inf)
        query_max_x = nextafter(center_x + constants[8], inf)
        query_max_y = nextafter(center_y + constants[9], inf)
        records = self.outlines.records
        nodes = self._nodes
        node_index = 0
        node_count = len(nodes)

        while node_index < node_count:
            min_x, min_y, max_x, max_y, escape, start, end = nodes[node_index]
            if (
                max_x < query_min_x
                or min_x > query_max_x
                or max_y < query_min_y
                or min_y > query_max_y
            ):
                node_index = escape
                continue
            if start < 0:
                node_index += 1
                continue
            if _range_records_intersect(
                records,
                start,
                end,
                query_min_x,
                query_min_y,
                query_max_x,
                query_max_y,
                constants,
            ):
                return True
            node_index = escape
        return False


class GridQueryScratch:
    """Reusable candidate-deduplication state for ``UniformGridIndex``.

    The grid has a built-in scratch object for normal sequential queries.  Give
    each concurrent caller its own object from ``new_scratch``.
    """

    __slots__ = ("marks", "generation")

    def __init__(self, segment_count: int) -> None:
        self.marks = [0] * segment_count
        self.generation = 0

    def begin(self, segment_count: int) -> int:
        if len(self.marks) != segment_count:
            self.marks = [0] * segment_count
            self.generation = 0
        self.generation += 1
        return self.generation


def _auto_grid_cell_size(outlines: PreparedOutlines) -> float:
    records = outlines.records
    if not records:
        return 1.0

    # A bounded deterministic sample keeps automatic tuning linear-time while
    # remaining stable for naturally ordered outline data.
    sample_limit = 1_024
    sample_stride = max(1, (len(records) + sample_limit - 1) // sample_limit)
    positive_half_lengths = [
        hypot(records[index][2], records[index][3])
        for index in range(0, len(records), sample_stride)
        if records[index][2] != 0.0 or records[index][3] != 0.0
    ]
    if not positive_half_lengths:
        positive_half_lengths = [
            hypot(record[2], record[3])
            for record in records
            if record[2] != 0.0 or record[3] != 0.0
        ][:1]
    typical_half_length = (
        median(positive_half_lengths) if positive_half_lengths else 0.5
    )
    typical_scale = (
        typical_half_length * 4.0
        if typical_half_length <= float_info.max / 4.0
        else float_info.max
    )
    min_x, min_y, max_x, max_y = outlines.bounds
    width = max_x - min_x
    height = max_y - min_y
    if width > 0.0 and height > 0.0:
        density_scale = sqrt(width / len(records)) * sqrt(height)
        if not isfinite(density_scale):
            density_scale = float_info.max
    else:
        density_scale = 0.0
    return max(typical_scale, density_scale, 1.0e-12)


def _segment_grid_cells(record: _Record, cell_size: float) -> Iterator[tuple[int, int]]:
    """Conservative half-open DDA traversal with corner-neighbor coverage."""

    inverse_cell_size = 1.0 / cell_size
    x0 = record[8] * inverse_cell_size
    y0 = record[9] * inverse_cell_size
    x1 = record[10] * inverse_cell_size
    y1 = record[11] * inverse_cell_size
    cell_x = floor(x0)
    cell_y = floor(y0)
    end_x = floor(x1)
    end_y = floor(y1)
    yield cell_x, cell_y
    if cell_x != end_x or cell_y != end_y:
        # Guarantee endpoint inclusion even when a computed crossing time lands
        # one ULP above 1.0. A duplicate reference is harmless: query stamps
        # suppress duplicate narrow-phase tests.
        yield end_x, end_y

    delta_x = x1 - x0
    delta_y = y1 - y0

    if delta_x > 0.0:
        step_x = 1
        next_x = (cell_x + 1 - x0) / delta_x
        step_time_x = 1.0 / delta_x
    elif delta_x < 0.0:
        step_x = -1
        next_x = (cell_x - x0) / delta_x
        step_time_x = -1.0 / delta_x
    else:
        step_x = 0
        next_x = inf
        step_time_x = inf

    if delta_y > 0.0:
        step_y = 1
        next_y = (cell_y + 1 - y0) / delta_y
        step_time_y = 1.0 / delta_y
    elif delta_y < 0.0:
        step_y = -1
        next_y = (cell_y - y0) / delta_y
        step_time_y = -1.0 / delta_y
    else:
        step_y = 0
        next_y = inf
        step_time_y = inf

    # Cell equality is not a valid termination condition at a corner: with
    # mixed-sign motion, floor(endpoint) is a side cell rather than the diagonal
    # cell entered by a simultaneous X/Y step. Traverse parametrically instead.
    while next_x <= 1.0 or next_y <= 1.0:
        if next_y == inf:
            cell_x += step_x
            next_x += step_time_x
            yield cell_x, cell_y
            continue
        if next_x == inf:
            cell_y += step_y
            next_y += step_time_y
            yield cell_x, cell_y
            continue

        crossing_tolerance = 8.0 * ulp(max(abs(next_x), abs(next_y), 1.0))
        if next_x + crossing_tolerance < next_y:
            cell_x += step_x
            next_x += step_time_x
            yield cell_x, cell_y
        elif next_y + crossing_tolerance < next_x:
            cell_y += step_y
            next_y += step_time_y
            yield cell_x, cell_y
        else:
            # At an exact grid corner, all four adjoining cells count as touched.
            yield cell_x + step_x, cell_y
            yield cell_x, cell_y + step_y
            cell_x += step_x
            cell_y += step_y
            next_x += step_time_x
            next_y += step_time_y
            yield cell_x, cell_y


class UniformGridIndex(_FrozenConfiguration):
    """Static sparse uniform grid with shared narrow-phase candidate testing.

    Segment insertion uses conservative half-open DDA traversal, so a long
    diagonal costs cells proportional to its length rather than every cell in its
    AABB. Query-time generation marks remove duplicate candidates without a set.
    """

    __slots__ = (
        "outlines",
        "cell_size",
        "max_query_cells",
        "max_cells_per_segment",
        "_inverse_cell_size",
        "_cells",
        "_long_segments",
        "_scratch",
        "_origin_cell_x",
        "_origin_cell_y",
        "_max_cell_x",
        "_max_cell_y",
        "_column_count",
    )

    def __init__(
        self,
        outlines: PreparedOutlines,
        cell_size: float | None = None,
        max_query_cells: int = 256,
        max_cells_per_segment: int = 4_096,
    ) -> None:
        self.outlines = prepare_outlines(outlines)
        if cell_size is None:
            cell_size = _auto_grid_cell_size(self.outlines)
        cell_size = float(cell_size)
        if not isfinite(cell_size) or cell_size <= 0.0:
            raise ValueError("cell_size must be finite and positive")
        if (
            isinstance(max_query_cells, bool)
            or not isinstance(max_query_cells, int)
            or max_query_cells < 1
        ):
            raise ValueError("max_query_cells must be a positive integer")
        if (
            isinstance(max_cells_per_segment, bool)
            or not isinstance(max_cells_per_segment, int)
            or max_cells_per_segment < 1
        ):
            raise ValueError("max_cells_per_segment must be a positive integer")

        self.cell_size = cell_size
        self.max_query_cells = max_query_cells
        self.max_cells_per_segment = max_cells_per_segment
        inverse_cell_size = 1.0 / cell_size
        if not isfinite(inverse_cell_size):
            raise ValueError("cell_size is too small for finite grid coordinates")
        self._inverse_cell_size = inverse_cell_size

        grid_segments: list[tuple[int, int, int, int, int]] = []
        long_segments: list[int] = []
        for segment_index, record in enumerate(self.outlines.records):
            scaled = (
                record[8] * inverse_cell_size,
                record[9] * inverse_cell_size,
                record[10] * inverse_cell_size,
                record[11] * inverse_cell_size,
            )
            if not all(isfinite(value) for value in scaled):
                long_segments.append(segment_index)
                continue
            if not isfinite(scaled[2] - scaled[0]) or not isfinite(
                scaled[3] - scaled[1]
            ):
                long_segments.append(segment_index)
                continue
            start_x, start_y, end_x, end_y = (
                floor(scaled[0]),
                floor(scaled[1]),
                floor(scaled[2]),
                floor(scaled[3]),
            )
            cell_span_x = abs(end_x - start_x)
            cell_span_y = abs(end_y - start_y)
            # A negative-moving endpoint exactly on a cell boundary has a
            # half-open owner on the floor side, while DDA also reaches the cell
            # immediately below it at t=1. Account for that extra crossing in
            # the reference cap.
            effective_span_x = cell_span_x + int(
                scaled[2] == end_x and scaled[2] < scaled[0]
            )
            effective_span_y = cell_span_y + int(
                scaled[3] == end_y and scaled[3] < scaled[1]
            )
            # A tolerant diagonal-corner step can emit both side neighbors and
            # the diagonal cell. This is a conservative cap on actual references,
            # including the explicitly retained endpoint cell.
            reference_bound = (
                2
                + effective_span_x
                + effective_span_y
                + min(effective_span_x, effective_span_y)
            )
            if reference_bound > max_cells_per_segment:
                long_segments.append(segment_index)
            else:
                grid_segments.append(
                    (segment_index, start_x, start_y, end_x, end_y)
                )

        self._long_segments = tuple(long_segments)
        if not grid_segments:
            self._origin_cell_x = 0
            self._origin_cell_y = 0
            self._max_cell_x = -1
            self._max_cell_y = -1
            self._column_count = 0
        else:
            # One conservative guard cell covers DDA corner neighbors.
            self._origin_cell_x = min(
                min(item[1], item[3]) for item in grid_segments
            ) - 1
            self._origin_cell_y = min(
                min(item[2], item[4]) for item in grid_segments
            ) - 1
            self._max_cell_x = max(
                max(item[1], item[3]) for item in grid_segments
            ) + 1
            self._max_cell_y = max(
                max(item[2], item[4]) for item in grid_segments
            ) + 1
            self._column_count = self._max_cell_x - self._origin_cell_x + 1

        cells: dict[int, list[int]] = {}
        records = self.outlines.records
        for segment_index, _, _, _, _ in grid_segments:
            record = records[segment_index]
            for cell_x, cell_y in _segment_grid_cells(record, cell_size):
                key = (
                    (cell_y - self._origin_cell_y) * self._column_count
                    + cell_x
                    - self._origin_cell_x
                )
                cells.setdefault(key, []).append(segment_index)
        self._cells = {cell: tuple(indices) for cell, indices in cells.items()}
        self._scratch = GridQueryScratch(len(self.outlines))

    def new_scratch(self) -> GridQueryScratch:
        return GridQueryScratch(len(self.outlines))

    def intersects(
        self,
        rectangle: OrientedRectangle,
        padding: float = 0.0,
        scratch: GridQueryScratch | None = None,
    ) -> bool:
        padding = _validate_padding(padding, rectangle)
        constants = _query_constants(rectangle, padding)
        bounds = self.outlines.bounds
        if not _query_world_aabb_overlaps(bounds, constants):
            return False
        (
            center_x,
            center_y,
            axis_x,
            axis_y,
            extent_x,
            extent_y,
            _,
            _,
            world_half_x,
            world_half_y,
            _,
        ) = constants
        query_min_x = nextafter(center_x - world_half_x, -inf)
        query_max_x = nextafter(center_x + world_half_x, inf)
        query_min_y = nextafter(center_y - world_half_y, -inf)
        query_max_y = nextafter(center_y + world_half_y, inf)
        records = self.outlines.records

        # Segments that would require an excessive number of cell references are
        # cheaper and safer as a tiny always-tested overflow list.
        for index in self._long_segments:
            record = records[index]
            if (
                record[6] < query_min_x
                or record[4] > query_max_x
                or record[7] < query_min_y
                or record[5] > query_max_y
            ):
                continue
            if _record_intersects(
                record,
                center_x,
                center_y,
                axis_x,
                axis_y,
                extent_x,
                extent_y,
                constants[10],
            ):
                return True

        if not self._cells:
            return False
        inverse_cell_size = self._inverse_cell_size
        scaled_query_bounds = (
            query_min_x * inverse_cell_size,
            query_min_y * inverse_cell_size,
            query_max_x * inverse_cell_size,
            query_max_y * inverse_cell_size,
        )
        if not all(isfinite(value) for value in scaled_query_bounds):
            return _linear_records_intersect_values(
                records,
                center_x,
                center_y,
                axis_x,
                axis_y,
                extent_x,
                extent_y,
            )
        min_cell_x = floor(scaled_query_bounds[0])
        min_cell_y = floor(scaled_query_bounds[1])
        max_cell_x = floor(scaled_query_bounds[2])
        max_cell_y = floor(scaled_query_bounds[3])
        query_cell_count = (max_cell_x - min_cell_x + 1) * (
            max_cell_y - min_cell_y + 1
        )

        # A huge query defeats a fine grid.  The flat scan has lower overhead and
        # commonly returns early when such a rectangle covers much of the track.
        if query_cell_count > self.max_query_cells:
            return _linear_records_intersect_values(
                self.outlines.records,
                center_x,
                center_y,
                axis_x,
                axis_y,
                extent_x,
                extent_y,
            )

        min_cell_x = max(min_cell_x, self._origin_cell_x)
        max_cell_x = min(max_cell_x, self._max_cell_x)
        min_cell_y = max(min_cell_y, self._origin_cell_y)
        max_cell_y = min(max_cell_y, self._max_cell_y)
        if min_cell_x > max_cell_x or min_cell_y > max_cell_y:
            return False

        if scratch is None:
            scratch = self._scratch
        generation = scratch.begin(len(self.outlines))
        marks = scratch.marks
        cells = self._cells
        origin_cell_x = self._origin_cell_x
        origin_cell_y = self._origin_cell_y
        column_count = self._column_count

        for cell_y in range(min_cell_y, max_cell_y + 1):
            key = (
                (cell_y - origin_cell_y) * column_count
                + min_cell_x
                - origin_cell_x
            )
            for _ in range(min_cell_x, max_cell_x + 1):
                candidates = cells.get(key)
                key += 1
                if candidates is None:
                    continue
                for index in candidates:
                    if marks[index] == generation:
                        continue
                    marks[index] = generation
                    record = records[index]
                    if (
                        record[6] < query_min_x
                        or record[4] > query_max_x
                        or record[7] < query_min_y
                        or record[5] > query_max_y
                    ):
                        continue
                    if _record_intersects(
                        record,
                        center_x,
                        center_y,
                        axis_x,
                        axis_y,
                        extent_x,
                        extent_y,
                        constants[10],
                    ):
                        return True
        return False


class SpatialChainBVHIndex(_FrozenConfiguration):
    """Spatial BVH over small contiguous chains from the ordered loops.

    The loop contract lets one inexpensive AABB represent several neighboring
    edges. Only those chain AABBs are spatially partitioned, avoiding the full
    per-edge SAH build while retaining tight pruning when distant portions of a
    folded track are adjacent in world space.
    """

    __slots__ = ("outlines", "chain_size", "_chains", "_nodes")

    def __init__(self, outlines: PreparedOutlines, chain_size: int = 8) -> None:
        if isinstance(chain_size, bool) or not isinstance(chain_size, int):
            raise TypeError("chain_size must be an integer")
        if chain_size < 1:
            raise ValueError("chain_size must be positive")
        self.outlines = prepare_outlines(outlines)
        self.chain_size = chain_size
        records = self.outlines.records
        chains: list[tuple[float, float, float, float, int, int, float, float]] = []
        for loop_start, loop_end in self.outlines.loop_ranges:
            for start in range(loop_start, loop_end, chain_size):
                end = min(start + chain_size, loop_end)
                first = records[start]
                min_x = first[4]
                min_y = first[5]
                max_x = first[6]
                max_y = first[7]
                for index in range(start + 1, end):
                    record = records[index]
                    min_x = min(min_x, record[4])
                    min_y = min(min_y, record[5])
                    max_x = max(max_x, record[6])
                    max_y = max(max_y, record[7])
                center_x = min_x * 0.5 + max_x * 0.5
                center_y = min_y * 0.5 + max_y * 0.5
                chains.append(
                    (min_x, min_y, max_x, max_y, start, end, center_x, center_y)
                )
        self._chains = tuple(chains)
        nodes: list[tuple[float, float, float, float, int, int]] = []
        self._nodes = nodes
        self._build(list(range(len(chains))))
        object.__setattr__(self, "_nodes", tuple(nodes))

    def _build(self, chain_indices: list[int]) -> tuple[float, float, float, float]:
        chains = self._chains
        nodes = self._nodes
        node_index = len(nodes)
        nodes.append((0.0, 0.0, 0.0, 0.0, 0, -1))
        first = chains[chain_indices[0]]
        min_x = first[0]
        min_y = first[1]
        max_x = first[2]
        max_y = first[3]
        centroid_min_x = centroid_max_x = first[6]
        centroid_min_y = centroid_max_y = first[7]
        for chain_index in chain_indices[1:]:
            chain = chains[chain_index]
            min_x = min(min_x, chain[0])
            min_y = min(min_y, chain[1])
            max_x = max(max_x, chain[2])
            max_y = max(max_y, chain[3])
            centroid_min_x = min(centroid_min_x, chain[6])
            centroid_max_x = max(centroid_max_x, chain[6])
            centroid_min_y = min(centroid_min_y, chain[7])
            centroid_max_y = max(centroid_max_y, chain[7])
        bounds = min_x, min_y, max_x, max_y
        if len(chain_indices) == 1:
            nodes[node_index] = (*bounds, node_index + 1, chain_indices[0])
            return bounds

        axis = (
            0
            if centroid_max_x - centroid_min_x >= centroid_max_y - centroid_min_y
            else 1
        )
        coordinate = 6 + axis
        chain_indices.sort(key=lambda index: chains[index][coordinate])
        midpoint = len(chain_indices) // 2
        self._build(chain_indices[:midpoint])
        self._build(chain_indices[midpoint:])
        nodes[node_index] = (*bounds, len(nodes), -1)
        return bounds

    def intersects(
        self, rectangle: OrientedRectangle, padding: float = 0.0
    ) -> bool:
        padding = _validate_padding(padding, rectangle)
        constants = _query_constants(rectangle, padding)
        center_x = constants[0]
        center_y = constants[1]
        axis_x = constants[2]
        axis_y = constants[3]
        extent_x = constants[4]
        extent_y = constants[5]
        query_min_x = nextafter(center_x - constants[8], -inf)
        query_min_y = nextafter(center_y - constants[9], -inf)
        query_max_x = nextafter(center_x + constants[8], inf)
        query_max_y = nextafter(center_y + constants[9], inf)
        records = self.outlines.records
        chains = self._chains
        nodes = self._nodes
        node_index = 0
        node_count = len(nodes)

        while node_index < node_count:
            min_x, min_y, max_x, max_y, escape, chain_index = nodes[node_index]
            if (
                max_x < query_min_x
                or min_x > query_max_x
                or max_y < query_min_y
                or min_y > query_max_y
            ):
                node_index = escape
                continue
            if chain_index < 0:
                node_index += 1
                continue
            chain = chains[chain_index]
            if _range_records_intersect(
                records,
                chain[4],
                chain[5],
                query_min_x,
                query_min_y,
                query_max_x,
                query_max_y,
                constants,
            ):
                return True
            node_index = escape
        return False


class BVHIndex(_FrozenConfiguration):
    """Packed binned-SAH hierarchy with conservative world-AABB node pruning.

    The split score includes a representative query footprint.  Unlike ordinary
    surface-area heuristics, this remains meaningful for zero-area line-segment
    bounds and approximates the region of vehicle centers that visits a node.
    """

    __slots__ = (
        "outlines",
        "leaf_size",
        "bin_count",
        "expected_query_width",
        "expected_query_height",
        "_nodes",
        "_root",
    )

    def __init__(
        self,
        outlines: PreparedOutlines,
        leaf_size: int = 8,
        bin_count: int = 12,
        expected_query_width: float = 5.0,
        expected_query_height: float = 5.0,
    ) -> None:
        if isinstance(leaf_size, bool) or not isinstance(leaf_size, int):
            raise TypeError("leaf_size must be an integer")
        if leaf_size < 1:
            raise ValueError("leaf_size must be positive")
        if (
            isinstance(bin_count, bool)
            or not isinstance(bin_count, int)
            or bin_count < 2
        ):
            raise ValueError("bin_count must be an integer of at least two")
        expected_query_width = float(expected_query_width)
        expected_query_height = float(expected_query_height)
        if (
            not isfinite(expected_query_width)
            or not isfinite(expected_query_height)
            or expected_query_width <= 0.0
            or expected_query_height <= 0.0
        ):
            raise ValueError("expected query dimensions must be finite and positive")
        self.outlines = prepare_outlines(outlines)
        self.leaf_size = leaf_size
        self.bin_count = bin_count
        self.expected_query_width = expected_query_width
        self.expected_query_height = expected_query_height
        self._nodes: list[
            tuple[
                float,
                float,
                float,
                float,
                int,
                int,
                tuple[int, ...] | None,
                int,
                float,
            ]
        ] = []
        indices = list(range(len(self.outlines)))
        self._root = self._build(indices)

    def _build(self, indices: list[int]) -> int:
        records = self.outlines.records
        first = records[indices[0]]
        min_x = first[4]
        min_y = first[5]
        max_x = first[6]
        max_y = first[7]
        centroid_min_x = centroid_max_x = first[0]
        centroid_min_y = centroid_max_y = first[1]
        for index in indices[1:]:
            record = records[index]
            min_x = min(min_x, record[4])
            min_y = min(min_y, record[5])
            max_x = max(max_x, record[6])
            max_y = max(max_y, record[7])
            centroid_min_x = min(centroid_min_x, record[0])
            centroid_max_x = max(centroid_max_x, record[0])
            centroid_min_y = min(centroid_min_y, record[1])
            centroid_max_y = max(centroid_max_y, record[1])

        node_index = len(self._nodes)
        self._nodes.append((min_x, min_y, max_x, max_y, -1, -1, None, -1, 0.0))
        if len(indices) <= self.leaf_size:
            self._nodes[node_index] = (
                min_x,
                min_y,
                max_x,
                max_y,
                -1,
                -1,
                tuple(indices),
                -1,
                0.0,
            )
            return node_index

        left_indices, right_indices, axis, split = self._partition(
            indices,
            centroid_min_x,
            centroid_max_x,
            centroid_min_y,
            centroid_max_y,
        )
        left = self._build(left_indices)
        right = self._build(right_indices)
        self._nodes[node_index] = (
            min_x,
            min_y,
            max_x,
            max_y,
            left,
            right,
            None,
            axis,
            split,
        )
        return node_index

    def _partition(
        self,
        indices: list[int],
        centroid_min_x: float,
        centroid_max_x: float,
        centroid_min_y: float,
        centroid_max_y: float,
    ) -> tuple[list[int], list[int], int, float]:
        """Choose a query-aware binned-SAH split, with a balanced fallback."""

        records = self.outlines.records
        bin_count = self.bin_count
        centroid_minimums = centroid_min_x, centroid_min_y
        centroid_maximums = centroid_max_x, centroid_max_y
        best_score = inf
        best_axis = -1
        best_split_bin = -1

        for axis in (0, 1):
            centroid_minimum = centroid_minimums[axis]
            span = centroid_maximums[axis] - centroid_minimum
            if span == 0.0 or not isfinite(span):
                continue
            scale = bin_count / span
            if scale == 0.0 or not isfinite(scale):
                continue
            counts = [0] * bin_count
            bounds = [[inf, inf, -inf, -inf] for _ in range(bin_count)]
            for index in indices:
                record = records[index]
                bin_index = int((record[axis] - centroid_minimum) * scale)
                bin_index = min(bin_count - 1, max(0, bin_index))
                counts[bin_index] += 1
                bound = bounds[bin_index]
                bound[0] = min(bound[0], record[4])
                bound[1] = min(bound[1], record[5])
                bound[2] = max(bound[2], record[6])
                bound[3] = max(bound[3], record[7])

            prefix_counts = [0] * bin_count
            prefix_bounds = [[inf, inf, -inf, -inf] for _ in range(bin_count)]
            count = 0
            running = [inf, inf, -inf, -inf]
            for bin_index in range(bin_count):
                if counts[bin_index]:
                    bound = bounds[bin_index]
                    running[0] = min(running[0], bound[0])
                    running[1] = min(running[1], bound[1])
                    running[2] = max(running[2], bound[2])
                    running[3] = max(running[3], bound[3])
                count += counts[bin_index]
                prefix_counts[bin_index] = count
                prefix_bounds[bin_index] = running.copy()

            suffix_counts = [0] * bin_count
            suffix_bounds = [[inf, inf, -inf, -inf] for _ in range(bin_count)]
            count = 0
            running = [inf, inf, -inf, -inf]
            for bin_index in range(bin_count - 1, -1, -1):
                if counts[bin_index]:
                    bound = bounds[bin_index]
                    running[0] = min(running[0], bound[0])
                    running[1] = min(running[1], bound[1])
                    running[2] = max(running[2], bound[2])
                    running[3] = max(running[3], bound[3])
                count += counts[bin_index]
                suffix_counts[bin_index] = count
                suffix_bounds[bin_index] = running.copy()

            for split_bin in range(bin_count - 1):
                left_count = prefix_counts[split_bin]
                right_count = suffix_counts[split_bin + 1]
                if not left_count or not right_count:
                    continue
                left_bound = prefix_bounds[split_bin]
                right_bound = suffix_bounds[split_bin + 1]
                left_measure = (
                    left_bound[2] - left_bound[0] + self.expected_query_width
                ) * (left_bound[3] - left_bound[1] + self.expected_query_height)
                right_measure = (
                    right_bound[2] - right_bound[0] + self.expected_query_width
                ) * (right_bound[3] - right_bound[1] + self.expected_query_height)
                score = left_count * left_measure + right_count * right_measure
                if score < best_score:
                    best_score = score
                    best_axis = axis
                    best_split_bin = split_bin

        if best_axis >= 0:
            centroid_minimum = centroid_minimums[best_axis]
            span = centroid_maximums[best_axis] - centroid_minimum
            scale = bin_count / span
            left_indices: list[int] = []
            right_indices: list[int] = []
            for index in indices:
                bin_index = int(
                    (records[index][best_axis] - centroid_minimum) * scale
                )
                bin_index = min(bin_count - 1, max(0, bin_index))
                (left_indices if bin_index <= best_split_bin else right_indices).append(
                    index
                )

            # Keep depth logarithmic when the SAH favors a pathological sliver.
            minimum_partition = max(1, len(indices) // 16)
            if (
                len(left_indices) >= minimum_partition
                and len(right_indices) >= minimum_partition
            ):
                split = centroid_minimum + (best_split_bin + 1) / scale
                return left_indices, right_indices, best_axis, split

        fallback_axis = (
            0
            if centroid_max_x - centroid_min_x >= centroid_max_y - centroid_min_y
            else 1
        )
        indices.sort(key=lambda index: records[index][fallback_axis])
        midpoint = len(indices) // 2
        left_indices = indices[:midpoint]
        right_indices = indices[midpoint:]
        left_centroid = records[left_indices[-1]][fallback_axis]
        right_centroid = records[right_indices[0]][fallback_axis]
        difference = right_centroid - left_centroid
        split = (
            left_centroid + difference * 0.5
            if isfinite(difference)
            else left_centroid * 0.5 + right_centroid * 0.5
        )
        return left_indices, right_indices, fallback_axis, split

    def intersects(
        self, rectangle: OrientedRectangle, padding: float = 0.0
    ) -> bool:
        padding = _validate_padding(padding, rectangle)
        if self._root < 0:
            return False
        constants = _query_constants(rectangle, padding)
        center_x = constants[0]
        center_y = constants[1]
        axis_x = constants[2]
        axis_y = constants[3]
        extent_x = constants[4]
        extent_y = constants[5]
        query_min_x = nextafter(center_x - constants[8], -inf)
        query_min_y = nextafter(center_y - constants[9], -inf)
        query_max_x = nextafter(center_x + constants[8], inf)
        query_max_y = nextafter(center_y + constants[9], inf)
        records = self.outlines.records
        nodes = self._nodes
        stack = [self._root]

        while stack:
            node_index = stack.pop()
            (
                min_x,
                min_y,
                max_x,
                max_y,
                left,
                right,
                members,
                split_axis,
                split,
            ) = nodes[node_index]
            # In pure Python the tighter four-axis OBB/AABB node test costs more
            # than the extra traversal it saves for a vehicle-sized rectangle.
            # This world-AABB test is conservative; leaves use the shared narrow phase.
            if (
                max_x < query_min_x
                or min_x > query_max_x
                or max_y < query_min_y
                or min_y > query_max_y
            ):
                continue
            if members is not None:
                for index in members:
                    record = records[index]
                    if (
                        record[6] < query_min_x
                        or record[4] > query_max_x
                        or record[7] < query_min_y
                        or record[5] > query_max_y
                    ):
                        continue
                    if _record_intersects(
                        record,
                        center_x,
                        center_y,
                        axis_x,
                        axis_y,
                        extent_x,
                        extent_y,
                        constants[10],
                    ):
                        return True
                continue

            query_coordinate = center_x if split_axis == 0 else center_y
            if query_coordinate <= split:
                stack.append(right)
                stack.append(left)
            else:
                stack.append(left)
                stack.append(right)

        return False


ALGORITHM_TYPES = (
    LinearScanIndex,
    CoherentBlockIndex,
    CoherentHierarchyIndex,
    SpatialChainBVHIndex,
    UniformGridIndex,
    BVHIndex,
)


__all__ = [
    "ALGORITHM_TYPES",
    "BVHIndex",
    "CoherentBlockIndex",
    "CoherentHierarchyIndex",
    "GridQueryScratch",
    "LinearScanIndex",
    "OrientedRectangle",
    "OutlineLoop",
    "Point",
    "PreparedOutlines",
    "Segment",
    "SpatialChainBVHIndex",
    "UniformGridIndex",
    "prepare_outlines",
    "segment_intersects_rectangle",
]
