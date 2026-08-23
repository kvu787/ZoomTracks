"""Deterministic track-shaped data used by tests and benchmarks."""

from __future__ import annotations

from math import atan2, cos, pi, sin

if __package__:
    from .rectangle_segments import OrientedRectangle, OutlineLoop
else:
    from rectangle_segments import OrientedRectangle, OutlineLoop


def _centerline(angle: float, phase: float) -> tuple[float, float]:
    radius = 100.0 + 11.0 * sin(3.0 * angle + phase) + 4.0 * sin(
        7.0 * angle - 0.5 * phase
    )
    return radius * cos(angle), 0.62 * radius * sin(angle)


def _normal_and_tangent(angle: float, phase: float) -> tuple[float, float, float, float]:
    step = 1.0e-4
    before_x, before_y = _centerline(angle - step, phase)
    after_x, after_y = _centerline(angle + step, phase)
    tangent_x = after_x - before_x
    tangent_y = after_y - before_y
    inverse_length = (tangent_x * tangent_x + tangent_y * tangent_y) ** -0.5
    tangent_x *= inverse_length
    tangent_y *= inverse_length
    normal_x = tangent_y
    normal_y = -tangent_x
    center_x, center_y = _centerline(angle, phase)
    if normal_x * center_x + normal_y * center_y < 0.0:
        normal_x = -normal_x
        normal_y = -normal_y
    return normal_x, normal_y, tangent_x, tangent_y


def generate_track_outlines(
    total_vertices: int, seed: int = 1
) -> tuple[OutlineLoop, OutlineLoop]:
    """Generate two ordered loops without duplicated closing vertices."""

    if total_vertices < 8 or total_vertices % 2:
        raise ValueError("total_vertices must be an even integer of at least 8")
    count_per_outline = total_vertices // 2
    phase = (seed * 0.6180339887498949 % 1.0) * 2.0 * pi
    outlines: list[list[tuple[float, float]]] = [[], []]

    for index in range(count_per_outline):
        angle = 2.0 * pi * index / count_per_outline
        center_x, center_y = _centerline(angle, phase)
        normal_x, normal_y, _, _ = _normal_and_tangent(angle, phase)
        outlines[0].append((center_x + 8.0 * normal_x, center_y + 8.0 * normal_y))
        outlines[1].append((center_x - 8.0 * normal_x, center_y - 8.0 * normal_y))

    return tuple(outlines[0]), tuple(outlines[1])


def generate_folded_outlines(
    total_vertices: int, folds: int = 20
) -> tuple[OutlineLoop, OutlineLoop]:
    """Generate a radial folding stress case, not a representative road."""

    if total_vertices < 8 or total_vertices % 2:
        raise ValueError("total_vertices must be an even integer of at least 8")
    if folds < 1:
        raise ValueError("folds must be positive")
    count_per_outline = total_vertices // 2
    outlines: list[list[tuple[float, float]]] = [[], []]
    for index in range(count_per_outline):
        angle = 2.0 * pi * index / count_per_outline
        base_radius = 100.0 + 35.0 * sin(folds * angle)
        for loop, offset in zip(outlines, (6.0, -6.0)):
            radius = base_radius + offset
            loop.append((radius * cos(angle), radius * sin(angle)))
    return tuple(outlines[0]), tuple(outlines[1])


def generate_folded_queries(
    query_count: int, folds: int = 20
) -> list[OrientedRectangle]:
    """Queries for ``generate_folded_outlines`` with deterministic contacts."""

    if query_count < 1:
        raise ValueError("query_count must be positive")
    if folds < 1:
        raise ValueError("folds must be positive")
    step = 1.0e-5
    queries: list[OrientedRectangle] = []
    for index in range(query_count):
        angle = 2.0 * pi * index / query_count
        radius = 100.0 + 35.0 * sin(folds * angle)
        before_angle = angle - step
        after_angle = angle + step
        before_radius = 100.0 + 35.0 * sin(folds * before_angle)
        after_radius = 100.0 + 35.0 * sin(folds * after_angle)
        before_x = before_radius * cos(before_angle)
        before_y = before_radius * sin(before_angle)
        after_x = after_radius * cos(after_angle)
        after_y = after_radius * sin(after_angle)
        tangent_x = after_x - before_x
        tangent_y = after_y - before_y
        inverse_length = (tangent_x * tangent_x + tangent_y * tangent_y) ** -0.5
        queries.append(
            OrientedRectangle(
                radius * cos(angle),
                radius * sin(angle),
                2.2,
                1.0,
                tangent_x * inverse_length,
                tangent_y * inverse_length,
            )
        )
    return queries


def generate_lap_queries(
    query_count: int,
    seed: int = 1,
    impact_every: int = 20,
) -> list[OrientedRectangle]:
    """Generate a smooth lap with occasional contacts against the outer outline."""

    if query_count < 1:
        raise ValueError("query_count must be positive")
    phase = (seed * 0.6180339887498949 % 1.0) * 2.0 * pi
    queries: list[OrientedRectangle] = []
    for index in range(query_count):
        angle = 2.0 * pi * index / query_count
        center_x, center_y = _centerline(angle, phase)
        normal_x, normal_y, tangent_x, tangent_y = _normal_and_tangent(angle, phase)
        if impact_every > 0 and index % impact_every == 0:
            center_x += 7.35 * normal_x
            center_y += 7.35 * normal_y
        queries.append(
            OrientedRectangle(
                center_x,
                center_y,
                2.2,
                1.0,
                tangent_x,
                tangent_y,
            )
        )
    return queries


def generate_far_queries(query_count: int, seed: int = 1) -> list[OrientedRectangle]:
    """Generate guaranteed misses that exercise broad-phase rejection."""

    if query_count < 1:
        raise ValueError("query_count must be positive")
    queries: list[OrientedRectangle] = []
    for index in range(query_count):
        angle = 2.0 * pi * index / query_count
        center_x = 300.0 + 30.0 * cos(angle)
        center_y = -250.0 + 30.0 * sin(angle)
        heading = angle + seed * 0.01
        queries.append(
            OrientedRectangle(
                center_x,
                center_y,
                2.2,
                1.0,
                cos(heading),
                sin(heading),
            )
        )
    return queries


def generate_near_miss_queries(
    query_count: int, seed: int = 1
) -> list[OrientedRectangle]:
    """Generate noncontacts that run close to the outer outline."""

    if query_count < 1:
        raise ValueError("query_count must be positive")
    phase = (seed * 0.6180339887498949 % 1.0) * 2.0 * pi
    queries: list[OrientedRectangle] = []
    for index in range(query_count):
        angle = 2.0 * pi * index / query_count
        center_x, center_y = _centerline(angle, phase)
        normal_x, normal_y, tangent_x, tangent_y = _normal_and_tangent(angle, phase)
        center_x += 6.5 * normal_x
        center_y += 6.5 * normal_y
        queries.append(
            OrientedRectangle(
                center_x,
                center_y,
                2.2,
                1.0,
                tangent_x,
                tangent_y,
            )
        )
    return queries


def rectangle_heading(rectangle: OrientedRectangle) -> float:
    """Return a query heading; useful in diagnostics."""

    return atan2(rectangle.axis_y, rectangle.axis_x)


__all__ = [
    "generate_far_queries",
    "generate_folded_outlines",
    "generate_folded_queries",
    "generate_lap_queries",
    "generate_near_miss_queries",
    "generate_track_outlines",
    "rectangle_heading",
]
