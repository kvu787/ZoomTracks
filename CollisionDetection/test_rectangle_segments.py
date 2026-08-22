"""Correctness tests for all rectangle/segment collision algorithms."""

from __future__ import annotations

import math
import random
import unittest
from fractions import Fraction

if __package__:
    from .rectangle_segments import (
        ALGORITHM_TYPES,
        BVHIndex,
        CoherentBlockIndex,
        CoherentHierarchyIndex,
        LinearScanIndex,
        OrientedRectangle,
        PreparedSegments,
        UniformGridIndex,
        segment_intersects_rectangle,
    )
    from .workloads import (
        generate_far_queries,
        generate_lap_queries,
        generate_near_miss_queries,
        generate_track_segments,
    )
else:
    from rectangle_segments import (
        ALGORITHM_TYPES,
        BVHIndex,
        CoherentBlockIndex,
        CoherentHierarchyIndex,
        LinearScanIndex,
        OrientedRectangle,
        PreparedSegments,
        UniformGridIndex,
        segment_intersects_rectangle,
    )
    from workloads import (
        generate_far_queries,
        generate_lap_queries,
        generate_near_miss_queries,
        generate_track_segments,
    )


def _liang_barsky_reference(
    segment: tuple[float, float, float, float],
    rectangle: OrientedRectangle,
    padding: float = 0.0,
) -> bool:
    """Independent, deliberately branchy local-space clipping oracle."""

    x0, y0, x1, y1 = segment
    relative_x0 = x0 - rectangle.center_x
    relative_y0 = y0 - rectangle.center_y
    relative_x1 = x1 - rectangle.center_x
    relative_y1 = y1 - rectangle.center_y
    local_x0 = relative_x0 * rectangle.axis_x + relative_y0 * rectangle.axis_y
    local_y0 = relative_y0 * rectangle.axis_x - relative_x0 * rectangle.axis_y
    local_x1 = relative_x1 * rectangle.axis_x + relative_y1 * rectangle.axis_y
    local_y1 = relative_y1 * rectangle.axis_x - relative_x1 * rectangle.axis_y
    minimum_t = 0.0
    maximum_t = 1.0

    for start, delta, extent in (
        (local_x0, local_x1 - local_x0, rectangle.half_x + padding),
        (local_y0, local_y1 - local_y0, rectangle.half_y + padding),
    ):
        if delta == 0.0:
            if start < -extent or start > extent:
                return False
            continue
        enter = (-extent - start) / delta
        leave = (extent - start) / delta
        if enter > leave:
            enter, leave = leave, enter
        minimum_t = max(minimum_t, enter)
        maximum_t = min(maximum_t, leave)
        if minimum_t > maximum_t:
            return False
    return True


def _orientation(
    a: tuple[Fraction, Fraction],
    b: tuple[Fraction, Fraction],
    c: tuple[Fraction, Fraction],
) -> Fraction:
    return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])


def _between(a: Fraction, b: Fraction, value: Fraction) -> bool:
    return min(a, b) <= value <= max(a, b)


def _exact_segments_intersect(
    a: tuple[Fraction, Fraction],
    b: tuple[Fraction, Fraction],
    c: tuple[Fraction, Fraction],
    d: tuple[Fraction, Fraction],
) -> bool:
    ab_c = _orientation(a, b, c)
    ab_d = _orientation(a, b, d)
    cd_a = _orientation(c, d, a)
    cd_b = _orientation(c, d, b)
    if ((ab_c > 0 > ab_d) or (ab_d > 0 > ab_c)) and (
        (cd_a > 0 > cd_b) or (cd_b > 0 > cd_a)
    ):
        return True
    for point, orientation, edge_a, edge_b in (
        (c, ab_c, a, b),
        (d, ab_d, a, b),
        (a, cd_a, c, d),
        (b, cd_b, c, d),
    ):
        if (
            orientation == 0
            and _between(edge_a[0], edge_b[0], point[0])
            and _between(edge_a[1], edge_b[1], point[1])
        ):
            return True
    return False


def _fraction_axis_aligned_oracle(
    segment: tuple[Fraction, Fraction, Fraction, Fraction],
    half_x: Fraction,
    half_y: Fraction,
) -> bool:
    start = segment[0], segment[1]
    end = segment[2], segment[3]
    if (
        -half_x <= start[0] <= half_x
        and -half_y <= start[1] <= half_y
    ) or (-half_x <= end[0] <= half_x and -half_y <= end[1] <= half_y):
        return True
    corners = (
        (-half_x, -half_y),
        (half_x, -half_y),
        (half_x, half_y),
        (-half_x, half_y),
    )
    return any(
        _exact_segments_intersect(start, end, corners[index], corners[(index + 1) % 4])
        for index in range(4)
    )


class PrimitiveTests(unittest.TestCase):
    def setUp(self) -> None:
        self.rectangle = OrientedRectangle.from_angle(0.0, 0.0, 2.0, 1.0, 0.0)

    def assert_hit(self, segment: tuple[float, float, float, float]) -> None:
        self.assertTrue(segment_intersects_rectangle(segment, self.rectangle), segment)

    def assert_miss(self, segment: tuple[float, float, float, float]) -> None:
        self.assertFalse(segment_intersects_rectangle(segment, self.rectangle), segment)

    def test_required_contact_semantics(self) -> None:
        hits = (
            (-3.0, 0.0, 3.0, 0.0),
            (-1.0, 0.0, 1.0, 0.0),
            (0.0, 0.0, 0.0, 0.0),
            (-3.0, 1.0, 3.0, 1.0),
            (2.0, 1.0, 3.0, 2.0),
            (2.0, -3.0, 2.0, 3.0),
            (2.0, 1.0, 2.0, 1.0),
            (-2.0, 1.0, 2.0, 1.0),
            (0.0, 0.0, 4.0, 0.0),
        )
        misses = (
            (-3.0, 1.00001, 3.0, 1.00001),
            (2.00001, -3.0, 2.00001, 3.0),
            (2.1, 1.1, 3.0, 2.0),
            (3.0, 0.0, 3.0, 0.0),
        )
        for segment in hits:
            self.assert_hit(segment)
            self.assert_hit((segment[2], segment[3], segment[0], segment[1]))
        for segment in misses:
            self.assert_miss(segment)
            self.assert_miss((segment[2], segment[3], segment[0], segment[1]))

    def test_roundoff_guard_preserves_authored_boundary_contacts(self) -> None:
        cases = (
            (
                OrientedRectangle.from_angle(-10.0, 0.0, 0.1, 1.0, 0.0),
                (-9.9, 0.0, -9.5, 0.0),
            ),
            (
                OrientedRectangle.from_angle(
                    -10.0, -10.0, 0.7, 0.01, math.pi / 4.0
                ),
                (
                    -9.505025253169416,
                    -9.505025253169416,
                    -8.161522368914977,
                    -8.161522368914977,
                ),
            ),
        )
        for rectangle, segment in cases:
            self.assertTrue(segment_intersects_rectangle(segment, rectangle))
            for algorithm_type in ALGORITHM_TYPES:
                self.assertTrue(
                    algorithm_type([segment]).intersects(rectangle),
                    algorithm_type.__name__,
                )

        safely_separated = (-9.899999, 0.0, -9.5, 0.0)
        rectangle = OrientedRectangle.from_angle(-10.0, 0.0, 0.1, 1.0, 0.0)
        self.assertFalse(segment_intersects_rectangle(safely_separated, rectangle))

    def test_clipping_fallback_handles_collinear_and_overflow_cases(self) -> None:
        edge_rectangle = OrientedRectangle.from_angle(
            -10.0, -10.0, 0.1, 0.1, 1.1
        )
        collinear_edge = (
            -10.00159848827897,
            -10.223601084154845,
            -9.82016003970874,
            -9.867118140130271,
        )
        self.assertTrue(
            segment_intersects_rectangle(collinear_edge, edge_rectangle)
        )

        unit_rectangle = OrientedRectangle.from_angle(
            0.0, 0.0, 1.0, 1.0, math.pi / 4.0
        )
        safely_distant = (-1.0e155, 1.0e155, 1.0e155, -5.0e154)
        self.assertFalse(
            segment_intersects_rectangle(safely_distant, unit_rectangle)
        )
        for algorithm_type in ALGORITHM_TYPES:
            self.assertTrue(
                algorithm_type([collinear_edge]).intersects(edge_rectangle),
                algorithm_type.__name__,
            )
            self.assertFalse(
                algorithm_type([safely_distant]).intersects(unit_rectangle),
                algorithm_type.__name__,
            )

    def test_padding_is_explicit_rectangle_expansion(self) -> None:
        segment = (-1.0, 1.05, 1.0, 1.05)
        self.assertFalse(segment_intersects_rectangle(segment, self.rectangle))
        self.assertTrue(segment_intersects_rectangle(segment, self.rectangle, 0.05))

    def test_zero_extent_rectangle_line_semantics(self) -> None:
        angle = 0.37
        axis_x = math.cos(angle)
        axis_y = math.sin(angle)
        rectangle = OrientedRectangle(3.0, -2.0, 0.0, 1.0, axis_x, axis_y)

        def world(local_x: float, local_y: float) -> tuple[float, float]:
            return (
                rectangle.center_x + local_x * axis_x - local_y * axis_y,
                rectangle.center_y + local_x * axis_y + local_y * axis_x,
            )

        cases = (
            (*world(0.0, -2.0), *world(0.0, 2.0)),
            (*world(-1.0, 0.0), *world(1.0, 0.0)),
            (*world(0.0, -0.5), *world(0.0, 0.5)),
            (*world(0.1, -2.0), *world(0.1, 2.0)),
        )
        for segment in cases:
            self.assertEqual(
                _liang_barsky_reference(segment, rectangle),
                segment_intersects_rectangle(segment, rectangle),
                segment,
            )

    def test_exhaustive_rational_lattice_against_exact_oracle(self) -> None:
        coordinates = tuple(Fraction(value, 2) for value in range(-4, 5))
        rectangle = OrientedRectangle.from_angle(0.0, 0.0, 1.0, 0.5, 0.0)
        for x0 in coordinates:
            for y0 in coordinates:
                for x1 in coordinates:
                    for y1 in coordinates:
                        rational_segment = x0, y0, x1, y1
                        expected = _fraction_axis_aligned_oracle(
                            rational_segment, Fraction(1), Fraction(1, 2)
                        )
                        actual = segment_intersects_rectangle(
                            tuple(float(value) for value in rational_segment), rectangle
                        )
                        self.assertEqual(expected, actual, rational_segment)

    def test_random_rotations_against_liang_barsky(self) -> None:
        random_source = random.Random(0x5A17)
        for _ in range(10_000):
            segment = tuple(random_source.uniform(-100.0, 100.0) for _ in range(4))
            rectangle = OrientedRectangle.from_angle(
                random_source.uniform(-30.0, 30.0),
                random_source.uniform(-30.0, 30.0),
                10.0 ** random_source.uniform(-2.0, 1.0),
                10.0 ** random_source.uniform(-2.0, 1.0),
                random_source.uniform(-math.pi, math.pi),
            )
            self.assertEqual(
                _liang_barsky_reference(segment, rectangle),
                segment_intersects_rectangle(segment, rectangle),
                (segment, rectangle),
            )


class BroadPhaseTests(unittest.TestCase):
    @staticmethod
    def _build_all(segments: list[tuple[float, float, float, float]]):
        prepared = PreparedSegments(segments)
        return (
            LinearScanIndex(prepared),
            CoherentBlockIndex(prepared, block_size=7),
            CoherentHierarchyIndex(prepared, leaf_size=7),
            UniformGridIndex(prepared, cell_size=1.0),
            UniformGridIndex(prepared, cell_size=7.5),
            BVHIndex(prepared, leaf_size=3),
            BVHIndex(prepared, leaf_size=11),
        )

    def test_empty_input(self) -> None:
        rectangle = OrientedRectangle.from_angle(0.0, 0.0, 1.0, 1.0, 0.4)
        for algorithm_type in ALGORITHM_TYPES:
            self.assertFalse(algorithm_type([]).intersects(rectangle))

    def test_long_segment_grid_corner_and_boundary_cases(self) -> None:
        segments = [
            (-100.0, -100.0, 100.0, 100.0),
            (-100.0, 1.0, 100.0, 1.0),
            (4.0, -8.0, 4.0, 8.0),
            (20.0, 20.0, 20.0, 20.0),
        ]
        detectors = self._build_all(segments)
        queries = (
            OrientedRectangle.from_angle(0.0, 0.0, 0.1, 0.1, 0.0),
            OrientedRectangle.from_angle(3.9, 6.0, 0.1, 0.1, 0.0),
            OrientedRectangle.from_angle(-30.0, 1.25, 0.2, 0.25, 0.0),
            OrientedRectangle.from_angle(20.0, 20.0, 0.0, 0.0, 0.0),
            OrientedRectangle.from_angle(20.1, 20.0, 0.01, 0.01, 0.0),
        )
        reference = detectors[0]
        for query in queries:
            expected = reference.intersects(query)
            for detector in detectors[1:]:
                self.assertEqual(expected, detector.intersects(query), (detector, query))

    def test_grid_mixed_sign_endpoint_corner_terminates_and_hits(self) -> None:
        segments = [
            (0.5, 0.5, 1.0, 0.0),
            (0.5, -0.5, 1.0, 0.0),
            (1.0, 0.0, 0.5, 0.5),
            (1.0, 0.0, 0.5, -0.5),
        ]
        grid = UniformGridIndex(segments, cell_size=1.0)
        for segment in segments:
            endpoint = OrientedRectangle.from_angle(
                segment[2], segment[3], 0.0, 0.0, 0.0
            )
            self.assertTrue(grid.intersects(endpoint), segment)

    def test_grid_roundoff_near_corner_remains_conservative(self) -> None:
        segment = (
            -0.5900000000000001,
            1.9500000000000002,
            0.5900000000000001,
            -0.35000000000000003,
        )
        point = OrientedRectangle.from_angle(0.0, 0.8, 0.0, 0.0, 0.0)
        self.assertTrue(LinearScanIndex([segment]).intersects(point))
        self.assertTrue(
            UniformGridIndex([segment], cell_size=0.1).intersects(point)
        )

    def test_large_finite_midpoint_does_not_overflow(self) -> None:
        segment = (1.0e308, 0.0, 1.0e308, 1.0)
        rectangle = OrientedRectangle.from_angle(1.0e308, 0.5, 0.0, 0.25, 0.0)
        prepared = PreparedSegments([segment])
        self.assertTrue(all(math.isfinite(value) for value in prepared.records[0]))
        for detector in self._build_all([segment]):
            self.assertTrue(detector.intersects(rectangle), type(detector).__name__)

        smallest = float.fromhex("0x0.0000000000001p-1022")
        point_segment = (smallest, 0.0, smallest, 0.0)
        point_rectangle = OrientedRectangle.from_angle(
            smallest, 0.0, 0.0, 0.0, 0.0
        )
        for detector in self._build_all([point_segment]):
            self.assertTrue(
                detector.intersects(point_rectangle), type(detector).__name__
            )

    def test_grid_routes_unrepresentably_long_segments_to_overflow_list(self) -> None:
        segment = (-1.0e308, 0.0, 1.0e308, 0.0)
        grid = UniformGridIndex(
            [segment], cell_size=1.0, max_cells_per_segment=32
        )
        on_segment = OrientedRectangle.from_angle(0.0, 0.0, 0.0, 0.0, 0.0)
        off_segment = OrientedRectangle.from_angle(0.0, 1.0, 0.0, 0.0, 0.0)
        self.assertTrue(grid.intersects(on_segment))
        self.assertFalse(grid.intersects(off_segment))

    def test_large_coordinate_endpoint_contact_uses_original_endpoint(self) -> None:
        segment = (1.0e16, 0.0, 1.0e16 + 2.0, 0.0)
        endpoint = OrientedRectangle.from_angle(
            1.0e16 + 2.0, 0.0, 0.0, 0.0, 0.0
        )
        for algorithm_type in ALGORITHM_TYPES:
            self.assertTrue(
                algorithm_type([segment]).intersects(endpoint),
                algorithm_type.__name__,
            )

    def test_random_broad_phases_match_linear_scan(self) -> None:
        random_source = random.Random(0xB04D)
        segments = [
            tuple(random_source.uniform(-60.0, 60.0) for _ in range(4))
            for _ in range(350)
        ]
        segments.extend(
            [
                (-200.0, 0.0, 200.0, 0.0),
                (5.0, 5.0, 5.0, 5.0),
                (10.0, -100.0, 10.0, 100.0),
            ]
        )
        detectors = self._build_all(segments)
        reference = detectors[0]
        for _ in range(2_000):
            query = OrientedRectangle.from_angle(
                random_source.uniform(-120.0, 120.0),
                random_source.uniform(-120.0, 120.0),
                random_source.uniform(0.0, 8.0),
                random_source.uniform(0.0, 5.0),
                random_source.uniform(-math.pi, math.pi),
            )
            padding = random_source.choice((0.0, 0.0, 0.01, 0.25))
            expected = reference.intersects(query, padding)
            for detector in detectors[1:]:
                self.assertEqual(
                    expected,
                    detector.intersects(query, padding),
                    (type(detector).__name__, query, padding),
                )

    def test_track_workload_and_grid_scratch_reuse(self) -> None:
        segments = generate_track_segments(512, seed=9)
        prepared = PreparedSegments(segments)
        reference = LinearScanIndex(prepared)
        grid = UniformGridIndex(prepared)
        block = CoherentBlockIndex(prepared)
        hierarchy = CoherentHierarchyIndex(prepared, group_sizes=(256, 256))
        bvh = BVHIndex(prepared)
        scratch = grid.new_scratch()
        for query in generate_lap_queries(1_000, seed=9):
            expected = reference.intersects(query)
            self.assertEqual(expected, grid.intersects(query, scratch=scratch))
            self.assertEqual(expected, block.intersects(query))
            self.assertEqual(expected, hierarchy.intersects(query))
            self.assertEqual(expected, bvh.intersects(query))

    def test_benchmark_workloads_keep_their_intended_hit_rates(self) -> None:
        detector = CoherentHierarchyIndex(
            generate_track_segments(1_024, seed=17), group_sizes=(512, 512)
        )
        lap = generate_lap_queries(1_000, seed=17)
        near = generate_near_miss_queries(1_000, seed=17)
        far = generate_far_queries(1_000, seed=17)
        self.assertEqual(50, sum(detector.intersects(query) for query in lap))
        self.assertFalse(any(detector.intersects(query) for query in near))
        self.assertFalse(any(detector.intersects(query) for query in far))


class ValidationTests(unittest.TestCase):
    def test_prepared_segment_public_data_is_read_only(self) -> None:
        prepared = PreparedSegments([(0.0, 0.0, 1.0, 1.0)])
        with self.assertRaises(AttributeError):
            setattr(prepared, "records", ())
        with self.assertRaises(AttributeError):
            setattr(prepared, "bounds", None)

        rectangle = OrientedRectangle.from_angle(0.0, 0.0, 1.0, 1.0, 0.0)
        for algorithm_type in ALGORITHM_TYPES:
            detector = algorithm_type(prepared)
            with self.assertRaises(AttributeError):
                setattr(detector, "segments", PreparedSegments([]))
            self.assertTrue(detector.intersects(rectangle))
        grid = UniformGridIndex(prepared)
        with self.assertRaises(AttributeError):
            grid.cell_size = grid.cell_size * 2.0

    def test_rejects_invalid_geometry(self) -> None:
        with self.assertRaises(ValueError):
            PreparedSegments([(0.0, 0.0, math.nan, 1.0)])
        with self.assertRaises(ValueError):
            PreparedSegments([(0.0, 1.0, 2.0)])
        with self.assertRaises(ValueError):
            OrientedRectangle(0.0, 0.0, -1.0, 1.0, 1.0, 0.0)
        with self.assertRaises(ValueError):
            OrientedRectangle(0.0, 0.0, 1.0, 1.0, 2.0, 0.0)
        rectangle = OrientedRectangle.from_angle(0.0, 0.0, 1.0, 1.0, 0.0)
        for algorithm_type in ALGORITHM_TYPES:
            with self.assertRaises(ValueError):
                algorithm_type([]).intersects(rectangle, -0.1)

    def test_hierarchy_group_sizes_are_validated(self) -> None:
        segments = [(0.0, 0.0, 1.0, 0.0), (1.0, 0.0, 2.0, 0.0)]
        with self.assertRaises(ValueError):
            CoherentHierarchyIndex(segments, group_sizes=(1,))
        with self.assertRaises(ValueError):
            CoherentHierarchyIndex(segments, group_sizes=(1, -1, 2))

    def test_extreme_finite_bvh_centroids_fall_back_safely(self) -> None:
        segments = [
            (x, float(index), x, float(index))
            for index, x in enumerate((-1.0e308, 1.0e308) * 5)
        ]
        detector = BVHIndex(segments)
        point = OrientedRectangle.from_angle(-1.0e308, 0.0, 0.0, 0.0, 0.0)
        self.assertTrue(detector.intersects(point))

    def test_padding_that_overflows_extents_is_rejected(self) -> None:
        rectangle = OrientedRectangle.from_angle(
            0.0, 0.0, 1.0e308, 1.0e308, 0.0
        )
        for algorithm_type in ALGORITHM_TYPES:
            with self.assertRaises(ValueError):
                algorithm_type([(0.0, 0.0, 0.0, 0.0)]).intersects(
                    rectangle, 1.0e308
                )


if __name__ == "__main__":
    unittest.main()
