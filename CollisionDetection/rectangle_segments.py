"""Fast 2D oriented-rectangle versus static line-segment collision queries.

The intended mapping for ZoomTracks is Unity's ground plane: use world X as the
first coordinate and world Z as the second coordinate.  Every implementation
uses the same inclusive, division-free separating-axis narrow phase.  They only
differ in their broad phase.

The module has no third-party dependencies.
"""

from __future__ import annotations

from dataclasses import dataclass
from math import cos, floor, hypot, inf, isfinite, nextafter, sin, sqrt, ulp
from statistics import median
from sys import float_info
from typing import Iterable, Iterator, Sequence, TypeAlias


Segment: TypeAlias = tuple[float, float, float, float]
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


class PreparedSegments:
    """Validated, immutable static segment data shared by every index.

    Each record stores world-space midpoint, half-vector, AABB, and the original
    endpoints. Preparing once avoids repeated midpoint work, while retaining the
    endpoints keeps grid construction robust at extreme floating-point scales.
    """

    __slots__ = ("_records", "_bounds")

    _records: tuple[_Record, ...]
    _bounds: tuple[float, float, float, float] | None

    def __init__(self, segments: Iterable[Sequence[float]]) -> None:
        records: list[_Record] = []
        all_min_x = inf
        all_min_y = inf
        all_max_x = -inf
        all_max_y = -inf

        for index, source in enumerate(segments):
            try:
                if len(source) != 4:
                    raise ValueError
                x0, y0, x1, y1 = (float(value) for value in source)
            except (TypeError, ValueError) as error:
                raise ValueError(
                    f"segment {index} must contain exactly four numeric values"
                ) from error

            if not all(isfinite(value) for value in (x0, y0, x1, y1)):
                raise ValueError(f"segment {index} values must all be finite")

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
            min_x = min(x0, x1)
            min_y = min(y0, y1)
            max_x = max(x0, x1)
            max_y = max(y0, y1)
            records.append(
                (
                    midpoint_x,
                    midpoint_y,
                    half_vector_x,
                    half_vector_y,
                    min_x,
                    min_y,
                    max_x,
                    max_y,
                    x0,
                    y0,
                    x1,
                    y1,
                )
            )
            all_min_x = min(all_min_x, min_x)
            all_min_y = min(all_min_y, min_y)
            all_max_x = max(all_max_x, max_x)
            all_max_y = max(all_max_y, max_y)

        self._records = tuple(records)
        self._bounds = (
            None
            if not records
            else (all_min_x, all_min_y, all_max_x, all_max_y)
        )

    @property
    def records(self) -> tuple[_Record, ...]:
        return self._records

    @property
    def bounds(self) -> tuple[float, float, float, float] | None:
        return self._bounds

    def __len__(self) -> int:
        return len(self.records)


def prepare_segments(
    segments: PreparedSegments | Iterable[Sequence[float]],
) -> PreparedSegments:
    """Return ``segments`` unchanged if it is already prepared."""

    if isinstance(segments, PreparedSegments):
        return segments
    return PreparedSegments(segments)


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
    """Test one segment with the shared division-free SAT predicate.

    ``padding`` expands each rectangle half-extent by that world-space amount.
    With zero padding, touching still counts because all comparisons are
    inclusive.
    """

    prepared = PreparedSegments((segment,))
    return _linear_records_intersect(
        prepared.records, rectangle, _validate_padding(padding, rectangle)
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

    for record in records:
        if (
            record[6] < query_min_x
            or record[4] > query_max_x
            or record[7] < query_min_y
            or record[5] > query_max_y
        ):
            continue
        relative_x = record[0] - center_x
        relative_y = record[1] - center_y
        center_local_x = relative_x * axis_x + relative_y * axis_y
        center_local_y = relative_y * axis_x - relative_x * axis_y
        half_local_x = record[2] * axis_x + record[3] * axis_y
        half_local_y = record[3] * axis_x - record[2] * axis_y
        absolute_half_x = absolute(half_local_x)
        absolute_half_y = absolute(half_local_y)

        left = absolute(center_local_x)
        right = extent_x + absolute_half_x
        if left > right and left - right > 16.0 * ulp(max(left, right)):
            if _endpoint_clipping_intersects(
                record,
                center_x,
                center_y,
                axis_x,
                axis_y,
                extent_x,
                extent_y,
            ):
                return True
            continue
        left = absolute(center_local_y)
        right = extent_y + absolute_half_y
        if left > right and left - right > 16.0 * ulp(max(left, right)):
            if _endpoint_clipping_intersects(
                record,
                center_x,
                center_y,
                axis_x,
                axis_y,
                extent_x,
                extent_y,
            ):
                return True
            continue
        left = absolute(
            center_local_x * half_local_y - center_local_y * half_local_x
        )
        right = extent_x * absolute_half_y + extent_y * absolute_half_x
        if not isfinite(left) or not isfinite(right):
            if _scaled_record_intersects(
                record,
                center_x,
                center_y,
                axis_x,
                axis_y,
                extent_x,
                extent_y,
            ):
                return True
            if _endpoint_clipping_intersects(
                record,
                center_x,
                center_y,
                axis_x,
                axis_y,
                extent_x,
                extent_y,
            ):
                return True
            continue
        if left <= right or left - right <= 16.0 * ulp(max(left, right)):
            return True
        if _endpoint_clipping_intersects(
            record,
            center_x,
            center_y,
            axis_x,
            axis_y,
            extent_x,
            extent_y,
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
) -> bool:
    """Narrow phase for broad phases that already reduced the candidates."""

    relative_x = record[0] - center_x
    relative_y = record[1] - center_y
    center_local_x = relative_x * axis_x + relative_y * axis_y
    center_local_y = relative_y * axis_x - relative_x * axis_y
    half_local_x = record[2] * axis_x + record[3] * axis_y
    half_local_y = record[3] * axis_x - record[2] * axis_y
    absolute_half_x = abs(half_local_x)
    absolute_half_y = abs(half_local_y)

    left = abs(center_local_x)
    right = extent_x + absolute_half_x
    if left > right and left - right > 16.0 * ulp(max(left, right)):
        return _endpoint_clipping_intersects(
            record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
        )
    left = abs(center_local_y)
    right = extent_y + absolute_half_y
    if left > right and left - right > 16.0 * ulp(max(left, right)):
        return _endpoint_clipping_intersects(
            record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
        )
    left = abs(center_local_x * half_local_y - center_local_y * half_local_x)
    right = extent_x * absolute_half_y + extent_y * absolute_half_x
    if not isfinite(left) or not isfinite(right):
        if _scaled_record_intersects(
            record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
        ):
            return True
        return _endpoint_clipping_intersects(
            record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
        )
    if left <= right or left - right <= 16.0 * ulp(max(left, right)):
        return True
    return _endpoint_clipping_intersects(
        record, center_x, center_y, axis_x, axis_y, extent_x, extent_y
    )


def _query_constants(
    rectangle: OrientedRectangle, padding: float
) -> tuple[float, float, float, float, float, float, float, float, float, float]:
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
    )


def _query_world_aabb_overlaps(
    bounds: tuple[float, float, float, float],
    constants: tuple[float, float, float, float, float, float, float, float, float, float],
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


class LinearScanIndex(_FrozenConfiguration):
    """Overall-bounds rejection followed by a branch-light segment scan."""

    __slots__ = ("segments",)

    def __init__(
        self, segments: PreparedSegments | Iterable[Sequence[float]]
    ) -> None:
        self.segments = prepare_segments(segments)

    def intersects(
        self, rectangle: OrientedRectangle, padding: float = 0.0
    ) -> bool:
        padding = _validate_padding(padding, rectangle)
        bounds = self.segments.bounds
        if bounds is None:
            return False
        constants = _query_constants(rectangle, padding)
        if not _query_world_aabb_overlaps(bounds, constants):
            return False
        return _linear_records_intersect_values(
            self.segments.records,
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

    __slots__ = ("segments", "block_size", "_blocks")

    def __init__(
        self,
        segments: PreparedSegments | Iterable[Sequence[float]],
        block_size: int = 16,
    ) -> None:
        if isinstance(block_size, bool) or not isinstance(block_size, int):
            raise TypeError("block_size must be an integer")
        if block_size < 1:
            raise ValueError("block_size must be positive")

        self.segments = prepare_segments(segments)
        self.block_size = block_size
        records = self.segments.records
        blocks: list[tuple[float, float, float, float, int, int]] = []

        for start in range(0, len(records), block_size):
            end = min(start + block_size, len(records))
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
        bounds = self.segments.bounds
        if bounds is None or not _query_world_aabb_overlaps(bounds, constants):
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
        records = self.segments.records

        for min_x, min_y, max_x, max_y, start, end in self._blocks:
            if (
                max_x < query_min_x
                or min_x > query_max_x
                or max_y < query_min_y
                or min_y > query_max_y
            ):
                continue
            for index in range(start, end):
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
                ):
                    return True
        return False


class CoherentHierarchyIndex(_FrozenConfiguration):
    """Linear-build AABB hierarchy over contiguous authored-outline ranges.

    This preserves the spatial coherence already present in ordered inner and
    outer loops instead of spending time rediscovering it with spatial sorting.
    Pass ``group_sizes`` to keep separate outline roots.
    """

    __slots__ = ("segments", "leaf_size", "group_sizes", "_nodes", "_roots")

    def __init__(
        self,
        segments: PreparedSegments | Iterable[Sequence[float]],
        leaf_size: int = 8,
        group_sizes: Sequence[int] | None = None,
    ) -> None:
        if isinstance(leaf_size, bool) or not isinstance(leaf_size, int):
            raise TypeError("leaf_size must be an integer")
        if leaf_size < 1:
            raise ValueError("leaf_size must be positive")
        self.segments = prepare_segments(segments)
        self.leaf_size = leaf_size
        segment_count = len(self.segments)

        if group_sizes is None:
            normalized_sizes = (segment_count,) if segment_count else ()
        else:
            normalized: list[int] = []
            for size in group_sizes:
                if isinstance(size, bool) or not isinstance(size, int) or size < 0:
                    raise ValueError("group sizes must be nonnegative integers")
                if size:
                    normalized.append(size)
            if sum(normalized) != segment_count:
                raise ValueError("group sizes must sum to the segment count")
            normalized_sizes = tuple(normalized)
        self.group_sizes = normalized_sizes
        self._nodes: list[
            tuple[float, float, float, float, int, int, int, int]
        ] = []
        roots: list[int] = []
        start = 0
        for size in normalized_sizes:
            end = start + size
            roots.append(self._build_range(start, end))
            start = end
        self._roots = tuple(roots)

    def _build_range(self, start: int, end: int) -> int:
        records = self.segments.records
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
            node_index = len(self._nodes)
            self._nodes.append((min_x, min_y, max_x, max_y, -1, -1, start, end))
            return node_index

        midpoint = (start + end) // 2
        left = self._build_range(start, midpoint)
        right = self._build_range(midpoint, end)
        left_node = self._nodes[left]
        right_node = self._nodes[right]
        node_index = len(self._nodes)
        self._nodes.append(
            (
                min(left_node[0], right_node[0]),
                min(left_node[1], right_node[1]),
                max(left_node[2], right_node[2]),
                max(left_node[3], right_node[3]),
                left,
                right,
                -1,
                -1,
            )
        )
        return node_index

    def intersects(
        self, rectangle: OrientedRectangle, padding: float = 0.0
    ) -> bool:
        padding = _validate_padding(padding, rectangle)
        if not self._roots:
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
        records = self.segments.records
        nodes = self._nodes
        stack = list(self._roots)

        while stack:
            node_index = stack.pop()
            min_x, min_y, max_x, max_y, left, right, start, end = nodes[node_index]
            if (
                max_x < query_min_x
                or min_x > query_max_x
                or max_y < query_min_y
                or min_y > query_max_y
            ):
                continue
            if left >= 0:
                stack.append(right)
                stack.append(left)
                continue
            for index in range(start, end):
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
                ):
                    return True
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


def _auto_grid_cell_size(segments: PreparedSegments) -> float:
    records = segments.records
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
    assert segments.bounds is not None
    min_x, min_y, max_x, max_y = segments.bounds
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
        "segments",
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
        segments: PreparedSegments | Iterable[Sequence[float]],
        cell_size: float | None = None,
        max_query_cells: int = 256,
        max_cells_per_segment: int = 4_096,
    ) -> None:
        self.segments = prepare_segments(segments)
        if cell_size is None:
            cell_size = _auto_grid_cell_size(self.segments)
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
        for segment_index, record in enumerate(self.segments.records):
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
            # A tolerant diagonal-corner step can emit both side neighbors and
            # the diagonal cell. This is a conservative cap on actual references,
            # including the explicitly retained endpoint cell.
            reference_bound = (
                2 + cell_span_x + cell_span_y + min(cell_span_x, cell_span_y)
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
        records = self.segments.records
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
        self._scratch = GridQueryScratch(len(self.segments))

    def new_scratch(self) -> GridQueryScratch:
        return GridQueryScratch(len(self.segments))

    def intersects(
        self,
        rectangle: OrientedRectangle,
        padding: float = 0.0,
        scratch: GridQueryScratch | None = None,
    ) -> bool:
        padding = _validate_padding(padding, rectangle)
        constants = _query_constants(rectangle, padding)
        bounds = self.segments.bounds
        if bounds is None or not _query_world_aabb_overlaps(bounds, constants):
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
        ) = constants
        query_min_x = nextafter(center_x - world_half_x, -inf)
        query_max_x = nextafter(center_x + world_half_x, inf)
        query_min_y = nextafter(center_y - world_half_y, -inf)
        query_max_y = nextafter(center_y + world_half_y, inf)
        records = self.segments.records

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
                self.segments.records,
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
        generation = scratch.begin(len(self.segments))
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
                    ):
                        return True
        return False


class BVHIndex(_FrozenConfiguration):
    """Packed binned-SAH hierarchy with conservative world-AABB node pruning.

    The split score includes a representative query footprint.  Unlike ordinary
    surface-area heuristics, this remains meaningful for zero-area line-segment
    bounds and approximates the region of vehicle centers that visits a node.
    """

    __slots__ = (
        "segments",
        "leaf_size",
        "bin_count",
        "expected_query_width",
        "expected_query_height",
        "_nodes",
        "_root",
    )

    def __init__(
        self,
        segments: PreparedSegments | Iterable[Sequence[float]],
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
        self.segments = prepare_segments(segments)
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
        indices = list(range(len(self.segments)))
        self._root = self._build(indices) if indices else -1

    def _build(self, indices: list[int]) -> int:
        records = self.segments.records
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

        records = self.segments.records
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
        records = self.segments.records
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
    "PreparedSegments",
    "Segment",
    "UniformGridIndex",
    "prepare_segments",
    "segment_intersects_rectangle",
]
