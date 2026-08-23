"""Correctness tests for the immutable ordered-outline collision algorithms."""

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
        PreparedOutlines,
        SpatialChainBVHIndex,
        UniformGridIndex,
        segment_intersects_rectangle,
    )
    from .workloads import (
        generate_far_queries,
        generate_folded_outlines,
        generate_folded_queries,
        generate_lap_queries,
        generate_near_miss_queries,
        generate_track_outlines,
    )
else:
    from rectangle_segments import (
        ALGORITHM_TYPES,
        BVHIndex,
        CoherentBlockIndex,
        CoherentHierarchyIndex,
        LinearScanIndex,
        OrientedRectangle,
        PreparedOutlines,
        SpatialChainBVHIndex,
        UniformGridIndex,
        segment_intersects_rectangle,
    )
    from workloads import (
        generate_far_queries,
        generate_folded_outlines,
        generate_folded_queries,
        generate_lap_queries,
        generate_near_miss_queries,
        generate_track_outlines,
    )


FAR_LOOP = ((1000.0, 1000.0), (1002.0, 1000.0), (1001.0, 1002.0))


def _liang_barsky_reference(
    segment: tuple[float, float, float, float],
    rectangle: OrientedRectangle,
    padding: float = 0.0,
) -> bool:
    """Independent branchy endpoint-space clipping oracle."""

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


def _outline_reference(
    prepared: PreparedOutlines,
    rectangle: OrientedRectangle,
    padding: float = 0.0,
) -> bool:
    for loop in (prepared.outer, prepared.inner):
        for index, start in enumerate(loop):
            end = loop[(index + 1) % len(loop)]
            if _liang_barsky_reference((*start, *end), rectangle, padding):
                return True
    return False


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


def _build_all(prepared: PreparedOutlines):
    return (
        LinearScanIndex(prepared),
        CoherentBlockIndex(prepared, block_size=7),
        CoherentHierarchyIndex(prepared, leaf_size=7, branching_factor=2),
        CoherentHierarchyIndex(prepared, leaf_size=7, branching_factor=4),
        SpatialChainBVHIndex(prepared, chain_size=5),
        SpatialChainBVHIndex(prepared, chain_size=11),
        UniformGridIndex(prepared, cell_size=1.0),
        UniformGridIndex(prepared, cell_size=7.5),
        BVHIndex(prepared, leaf_size=3),
        BVHIndex(prepared, leaf_size=11),
    )


class PrimitiveTests(unittest.TestCase):
    def setUp(self) -> None:
        self.rectangle = OrientedRectangle.from_angle(0.0, 0.0, 2.0, 1.0, 0.0)

    def test_required_contact_semantics(self) -> None:
        hits = (
            (-3.0, 0.0, 3.0, 0.0),
            (-1.0, 0.0, 1.0, 0.0),
            (0.0, 0.0, 0.0, 0.0),
            (-3.0, 1.0, 3.0, 1.0),
            (2.0, 1.0, 3.0, 2.0),
            (2.0, -3.0, 2.0, 3.0),
            (-2.0, 1.0, 2.0, 1.0),
        )
        misses = (
            (-3.0, 1.00001, 3.0, 1.00001),
            (2.00001, -3.0, 2.00001, 3.0),
            (2.1, 1.1, 3.0, 2.0),
        )
        for segment in hits:
            self.assertTrue(segment_intersects_rectangle(segment, self.rectangle), segment)
            self.assertTrue(
                segment_intersects_rectangle(
                    (segment[2], segment[3], segment[0], segment[1]), self.rectangle
                ),
                segment,
            )
        for segment in misses:
            self.assertFalse(segment_intersects_rectangle(segment, self.rectangle), segment)

    def test_exhaustive_rational_lattice_against_exact_oracle(self) -> None:
        coordinates = tuple(Fraction(value, 2) for value in range(-4, 5))
        rectangle = OrientedRectangle.from_angle(0.0, 0.0, 1.0, 0.5, 0.0)
        for x0 in coordinates:
            for y0 in coordinates:
                for x1 in coordinates:
                    for y1 in coordinates:
                        rational = x0, y0, x1, y1
                        expected = _fraction_axis_aligned_oracle(
                            rational, Fraction(1), Fraction(1, 2)
                        )
                        actual = segment_intersects_rectangle(
                            tuple(float(value) for value in rational), rectangle
                        )
                        self.assertEqual(expected, actual, rational)

    def test_random_rotations_against_liang_barsky(self) -> None:
        source = random.Random(0x5A17)
        for _ in range(10_000):
            segment = tuple(source.uniform(-100.0, 100.0) for _ in range(4))
            rectangle = OrientedRectangle.from_angle(
                source.uniform(-30.0, 30.0),
                source.uniform(-30.0, 30.0),
                10.0 ** source.uniform(-2.0, 1.0),
                10.0 ** source.uniform(-2.0, 1.0),
                source.uniform(-math.pi, math.pi),
            )
            self.assertEqual(
                _liang_barsky_reference(segment, rectangle),
                segment_intersects_rectangle(segment, rectangle),
                (segment, rectangle),
            )

    def test_padding_and_extreme_coordinate_fallbacks(self) -> None:
        segment = (-1.0, 1.05, 1.0, 1.05)
        self.assertFalse(segment_intersects_rectangle(segment, self.rectangle))
        self.assertTrue(segment_intersects_rectangle(segment, self.rectangle, 0.05))
        edge_rectangle = OrientedRectangle.from_angle(-10.0, -10.0, 0.1, 0.1, 1.1)
        collinear = (
            -10.00159848827897,
            -10.223601084154845,
            -9.82016003970874,
            -9.867118140130271,
        )
        self.assertTrue(segment_intersects_rectangle(collinear, edge_rectangle))
        distant = (-1.0e155, 1.0e155, 1.0e155, -5.0e154)
        unit = OrientedRectangle.from_angle(0.0, 0.0, 1.0, 1.0, math.pi / 4.0)
        self.assertFalse(segment_intersects_rectangle(distant, unit))


class ContractTests(unittest.TestCase):
    def test_preparation_derives_each_closing_edge_once(self) -> None:
        outer = ((0.0, 0.0), (4.0, 0.0), (2.0, 3.0))
        inner = ((1.0, 1.0), (2.0, 1.0), (1.5, 2.0))
        prepared = PreparedOutlines(outer, inner)
        self.assertEqual(6, len(prepared))
        self.assertEqual(((0, 3), (3, 6)), prepared.loop_ranges)
        self.assertEqual((2.0, 3.0, 0.0, 0.0), prepared.records[2][8:12])
        self.assertEqual((1.5, 2.0, 1.0, 1.0), prepared.records[5][8:12])

    def test_rejects_invalid_loops_and_vertices(self) -> None:
        valid = ((0.0, 0.0), (1.0, 0.0), (0.0, 1.0))
        invalid_loops = (
            (),
            ((0.0, 0.0),),
            ((0.0, 0.0), (1.0, 0.0)),
            ((0.0, 0.0), (0.0, 0.0), (1.0, 0.0)),
            ((0.0, 0.0), (1.0, 0.0), (0.0, 0.0)),
            ((0.0, 0.0), (1.0, 0.0), (math.nan, 1.0)),
            ((0.0, 0.0), (1.0, 0.0), (math.inf, 1.0)),
            ((0.0, 0.0), (1.0, 0.0), (2.0,)),
            ((0.0, 0.0), (1.0, 0.0), ("bad", 1.0)),
            ((0.0, 0.0), (1.0, 0.0), (10**10000, 1.0)),
        )
        for invalid in invalid_loops:
            with self.assertRaises(ValueError):
                PreparedOutlines(invalid, valid)
            with self.assertRaises(ValueError):
                PreparedOutlines(valid, invalid)

    def test_inputs_are_snapshotted_and_configuration_is_read_only(self) -> None:
        outer = [[0.0, 0.0], [4.0, 0.0], [2.0, 3.0]]
        inner = [[1.0, 1.0], [2.0, 1.0], [1.5, 2.0]]
        prepared = PreparedOutlines(outer, inner)
        outer[0][0] = 999.0
        inner.append([3.0, 3.0])
        self.assertEqual((0.0, 0.0), prepared.outer[0])
        self.assertEqual(3, len(prepared.inner))
        with self.assertRaises(AttributeError):
            prepared.outer = ()
        with self.assertRaises(AttributeError):
            del prepared._records
        for algorithm_type in ALGORITHM_TYPES:
            detector = algorithm_type(prepared)
            with self.assertRaises(AttributeError):
                detector.outlines = PreparedOutlines(FAR_LOOP, FAR_LOOP)
            with self.assertRaises(AttributeError):
                del detector.outlines

    def test_contract_does_not_add_unstated_topology_rules(self) -> None:
        self_crossing = ((0.0, 0.0), (2.0, 2.0), (0.0, 2.0), (2.0, 0.0))
        zero_area = ((0.0, 0.0), (1.0, 0.0), (2.0, 0.0))
        repeated_nonconsecutive = (
            (0.0, 0.0),
            (2.0, 0.0),
            (0.0, 0.0),
            (0.0, 2.0),
        )
        for loop in (self_crossing, zero_area, repeated_nonconsecutive):
            self.assertEqual(len(loop) + 3, len(PreparedOutlines(loop, FAR_LOOP)))


class OutlineCollisionTests(unittest.TestCase):
    def test_closing_edge_of_each_loop_is_queryable(self) -> None:
        closing_hit = ((-2.0, 0.0), (10.0, 10.0), (2.0, 0.0))
        query = OrientedRectangle.from_angle(0.0, 0.0, 0.1, 0.1, 0.37)
        for prepared in (
            PreparedOutlines(closing_hit, FAR_LOOP),
            PreparedOutlines(FAR_LOOP, closing_hit),
        ):
            for detector in _build_all(prepared):
                self.assertTrue(detector.intersects(query), type(detector).__name__)

    def test_boundary_only_semantics(self) -> None:
        outer = ((-10.0, -10.0), (10.0, -10.0), (10.0, 10.0), (-10.0, 10.0))
        inner = ((-3.0, -3.0), (3.0, -3.0), (3.0, 3.0), (-3.0, 3.0))
        prepared = PreparedOutlines(outer, inner)
        inside_without_touching = OrientedRectangle.from_angle(0.0, 0.0, 1.0, 1.0, 0.2)
        enclosing = OrientedRectangle.from_angle(0.0, 0.0, 20.0, 20.0, 0.0)
        for detector in _build_all(prepared):
            self.assertFalse(detector.intersects(inside_without_touching))
            self.assertTrue(detector.intersects(enclosing))

    def test_rotation_reversal_and_loop_swap_do_not_change_result(self) -> None:
        outer, inner = generate_track_outlines(128, seed=4)
        variants = (
            PreparedOutlines(outer, inner),
            PreparedOutlines(outer[17:] + outer[:17], inner[9:] + inner[:9]),
            PreparedOutlines(tuple(reversed(outer)), tuple(reversed(inner))),
            PreparedOutlines(inner, outer),
        )
        queries = generate_lap_queries(300, seed=4) + generate_near_miss_queries(300, seed=4)
        expected = tuple(_outline_reference(variants[0], query) for query in queries)
        for prepared in variants:
            for detector in _build_all(prepared):
                self.assertEqual(
                    expected,
                    tuple(detector.intersects(query) for query in queries),
                    type(detector).__name__,
                )

    def test_random_algorithms_match_independent_loop_oracle(self) -> None:
        source = random.Random(0xB04D)
        outer = tuple((source.uniform(-60, 60), source.uniform(-60, 60)) for _ in range(83))
        inner = tuple((source.uniform(-40, 40), source.uniform(-40, 40)) for _ in range(67))
        prepared = PreparedOutlines(outer, inner)
        detectors = _build_all(prepared)
        for _ in range(1_500):
            query = OrientedRectangle.from_angle(
                source.uniform(-100.0, 100.0),
                source.uniform(-100.0, 100.0),
                source.uniform(0.0, 8.0),
                source.uniform(0.0, 5.0),
                source.uniform(-math.pi, math.pi),
            )
            padding = source.choice((0.0, 0.0, 0.01, 0.25))
            expected = _outline_reference(prepared, query, padding)
            for detector in detectors:
                self.assertEqual(
                    expected,
                    detector.intersects(query, padding),
                    (type(detector).__name__, query, padding),
                )

    def test_track_workload_grid_scratch_and_hit_rates(self) -> None:
        outer, inner = generate_track_outlines(512, seed=9)
        prepared = PreparedOutlines(outer, inner)
        reference = LinearScanIndex(prepared)
        grid = UniformGridIndex(prepared)
        scratch = grid.new_scratch()
        hierarchy = CoherentHierarchyIndex(prepared)
        chain = SpatialChainBVHIndex(prepared)
        for query in generate_lap_queries(1_000, seed=9):
            expected = reference.intersects(query)
            self.assertEqual(expected, grid.intersects(query, scratch=scratch))
            self.assertEqual(expected, hierarchy.intersects(query))
            self.assertEqual(expected, chain.intersects(query))

        benchmark_prepared = PreparedOutlines(*generate_track_outlines(1_024, seed=17))
        detector = CoherentHierarchyIndex(benchmark_prepared)
        self.assertEqual(50, sum(detector.intersects(q) for q in generate_lap_queries(1_000, seed=17)))
        self.assertFalse(any(detector.intersects(q) for q in generate_near_miss_queries(1_000, seed=17)))
        self.assertFalse(any(detector.intersects(q) for q in generate_far_queries(1_000, seed=17)))

        folded = PreparedOutlines(*generate_folded_outlines(4_096, folds=20))
        folded_detector = CoherentHierarchyIndex(folded)
        self.assertEqual(
            6_500,
            sum(
                folded_detector.intersects(query)
                for query in generate_folded_queries(10_000, folds=20)
            ),
        )

    def test_extreme_finite_edges_and_grid_overflow(self) -> None:
        outer = (
            (-1.0e308, 0.0),
            (1.0e308, 0.0),
            (1.0e308, 100.0),
            (-1.0e308, 100.0),
        )
        prepared = PreparedOutlines(outer, FAR_LOOP)
        on_edge = OrientedRectangle.from_angle(0.0, 0.0, 0.0, 0.0, 0.0)
        off_edge = OrientedRectangle.from_angle(0.0, 1.0, 0.0, 0.0, 0.0)
        for detector in _build_all(prepared):
            self.assertTrue(detector.intersects(on_edge), type(detector).__name__)
            self.assertFalse(detector.intersects(off_edge), type(detector).__name__)
        overflow_grid = UniformGridIndex(
            prepared, cell_size=1.0, max_cells_per_segment=32
        )
        self.assertTrue(overflow_grid.intersects(on_edge))
        self.assertFalse(overflow_grid.intersects(off_edge))

    def test_large_coordinate_endpoint_contact(self) -> None:
        outer = ((1.0e16, 0.0), (1.0e16 + 2.0, 0.0), (1.0e16, 100.0))
        prepared = PreparedOutlines(outer, FAR_LOOP)
        endpoint = OrientedRectangle.from_angle(1.0e16 + 2.0, 0.0, 0.0, 0.0, 0.0)
        for detector in _build_all(prepared):
            self.assertTrue(detector.intersects(endpoint), type(detector).__name__)

    def test_long_rotated_tangent_uses_scaled_uncertainty(self) -> None:
        rectangle = OrientedRectangle.from_angle(0.0, 0.0, 1.0, 1.0, 1.1)

        def world(local_x: float, local_y: float) -> tuple[float, float]:
            return (
                local_x * rectangle.axis_x - local_y * rectangle.axis_y,
                local_x * rectangle.axis_y + local_y * rectangle.axis_x,
            )

        start = world(-1.0e10, 1.0)
        end = world(1.0e10, 1.0)
        outer = (start, end, world(0.0, 2.0))
        self.assertTrue(segment_intersects_rectangle((*start, *end), rectangle))
        prepared = PreparedOutlines(outer, FAR_LOOP)
        for detector in _build_all(prepared):
            self.assertTrue(detector.intersects(rectangle), type(detector).__name__)

    def test_subnormal_product_underflow_uses_scaled_predicate(self) -> None:
        start = (-1.0e-250, 1.0e-250)
        end = (1.0e-250, -5.0e-251)
        point = OrientedRectangle.from_angle(0.0, 0.0, 0.0, 0.0, 0.0)
        self.assertFalse(segment_intersects_rectangle((*start, *end), point))
        outer = (start, end, (2.0e-250, 2.0e-250))
        prepared = PreparedOutlines(outer, FAR_LOOP)
        for detector in _build_all(prepared):
            self.assertFalse(detector.intersects(point), type(detector).__name__)

    def test_grid_reference_cap_counts_negative_boundary_endpoint(self) -> None:
        outer = ((2.1, 0.1), (1.0, 0.1), (1.0, 2.0), (3.0, 2.0))
        prepared = PreparedOutlines(outer, FAR_LOOP)
        grid = UniformGridIndex(
            prepared, cell_size=1.0, max_cells_per_segment=3
        )
        self.assertIn(0, grid._long_segments)


class ConfigurationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.prepared = PreparedOutlines(
            ((0.0, 0.0), (2.0, 0.0), (1.0, 2.0)), FAR_LOOP
        )

    def test_rejects_invalid_tuning_and_padding(self) -> None:
        with self.assertRaises(ValueError):
            CoherentBlockIndex(self.prepared, block_size=0)
        with self.assertRaises(ValueError):
            CoherentHierarchyIndex(self.prepared, leaf_size=0)
        with self.assertRaises(ValueError):
            CoherentHierarchyIndex(self.prepared, branching_factor=1)
        with self.assertRaises(ValueError):
            SpatialChainBVHIndex(self.prepared, chain_size=0)
        with self.assertRaises(ValueError):
            UniformGridIndex(self.prepared, cell_size=0.0)
        with self.assertRaises(ValueError):
            BVHIndex(self.prepared, leaf_size=0)
        rectangle = OrientedRectangle.from_angle(0.0, 0.0, 1.0, 1.0, 0.0)
        for algorithm_type in ALGORITHM_TYPES:
            with self.assertRaises(ValueError):
                algorithm_type(self.prepared).intersects(rectangle, -0.1)

    def test_rectangle_validation_and_padding_overflow(self) -> None:
        with self.assertRaises(ValueError):
            OrientedRectangle(0.0, 0.0, -1.0, 1.0, 1.0, 0.0)
        with self.assertRaises(ValueError):
            OrientedRectangle(0.0, 0.0, 1.0, 1.0, 2.0, 0.0)
        rectangle = OrientedRectangle.from_angle(0.0, 0.0, 1.0e308, 1.0e308, 0.0)
        for algorithm_type in ALGORITHM_TYPES:
            with self.assertRaises(ValueError):
                algorithm_type(self.prepared).intersects(rectangle, 1.0e308)


if __name__ == "__main__":
    unittest.main()
