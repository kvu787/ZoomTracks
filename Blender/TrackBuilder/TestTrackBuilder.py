"""Blender integration tests for TrackBuilder.

The tests read committed, input-only .blend fixtures from TestInputs. Each run
writes inspectable .blend results and a complete text report to the gitignored
TestArtifacts directory.
"""

from __future__ import annotations

from datetime import datetime
from fractions import Fraction
import hashlib
import io
import json
import math
import os
import shutil
import sys
import time
import unittest
from unittest import mock


sys.dont_write_bytecode = True
SCRIPT_DIRECTORY = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIRECTORY not in sys.path:
    sys.path.insert(0, SCRIPT_DIRECTORY)

import bpy
from mathutils import Vector

import GenerateTrackBuilderSamples as samples
import TrackBuilder


TEST_INPUT_DIRECTORY = os.path.join(SCRIPT_DIRECTORY, "TestInputs")
TEST_ARTIFACT_DIRECTORY = os.path.join(SCRIPT_DIRECTORY, "TestArtifacts")
TEST_OUTPUT_DIRECTORY = os.path.join(TEST_ARTIFACT_DIRECTORY, "Outputs")
TEST_REPORT_PATH = os.path.join(TEST_ARTIFACT_DIRECTORY, "TestReport.txt")
PERFORMANCE_INPUT_PATH = os.path.abspath(
    os.path.join(
        SCRIPT_DIRECTORY,
        "..",
        "TrackBuilderSandbox",
        "TrackBuilder -- test -- perf issue.blend",
    )
)
PERFORMANCE_GEOMETRY_HASH = "ec42161fad649ff3367c47eb4bcce1440c661d2b4fc562f9acf8e72b93ca8649"
TEST_ARTIFACT_RESULTS: list[tuple[str, str, str, str]] = []
MESH_FIXTURE_GOLDEN_HASHES = {
    2: "1235733c39354afbf0468f7fa2b867bd7a4a5ca22815f8346bdad2912011c5d4",
    3: "faf67b0d0b522cc0ea9cccd0781725b705a324649e0af487adec9e7a6b998f97",
    4: "104d63c3301408995b9a8f2dda9f115ced22855e1f239699d3b3a2b9fab2c19a",
    5: "920f0a58ad4dae25fc0862d110e69de0f293b22cf95289019839ea5ea9167f26",
    6: "b183cec48af1d967316aae76558fb5d4eaa430e11870bdc51a6581006b512fe1",
    8: "051f06963361d59b372082f8131ca8029ffb0b8bc4a301228d3935c7808a9cad",
}

EXPECTED_EXCEPTIONS = {
    "minimum_turn_angle_error": TrackBuilder.TrackBuilderValidationError,
    "single_segment_error": TrackBuilder.TrackBuilderGeometryError,
}

EXPECTED_INNER_COUNTS = {
    1: 4,
    2: 0,
    3: 1,
    4: 2,
    5: 1,
    6: 1,
    7: 1,
    8: 1,
}


def _output_signature(output: bpy.types.Collection) -> tuple[tuple[int, int, str], ...]:
    return tuple(
        sorted(
            (obj.as_pointer(), obj.data.as_pointer(), obj.name)
            for obj in output.all_objects
        )
    )


def _output_geometry_hash(output: bpy.types.Collection) -> str:
    digest = hashlib.sha256()
    for obj in sorted(output.all_objects, key=lambda item: item.name):
        digest.update(obj.name.encode("utf-8"))
        material_name = obj.data.materials[0].name if obj.data.materials else ""
        digest.update(material_name.encode("utf-8"))
        for key in sorted(obj.keys()):
            digest.update(key.encode("utf-8"))
            digest.update(repr(obj[key]).encode("utf-8"))
        for vertex in obj.data.vertices:
            for component in vertex.co:
                digest.update(float(component).hex().encode("ascii"))
        for polygon in obj.data.polygons:
            digest.update(repr(tuple(polygon.vertices)).encode("ascii"))
    return digest.hexdigest()


def _ensure_material(name: str) -> bpy.types.Material:
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)


def _track_builder_collection() -> bpy.types.Collection | None:
    return bpy.data.collections.get("TrackBuilder")


def _outlines_collection() -> bpy.types.Collection | None:
    track_builder = _track_builder_collection()
    if track_builder is None:
        return None
    input_collection = TrackBuilder._direct_child(track_builder, "Input")
    if input_collection is None:
        return None
    return TrackBuilder._direct_child(input_collection, "Outlines")


def _output_collection() -> bpy.types.Collection | None:
    track_builder = _track_builder_collection()
    if track_builder is None:
        return None
    return TrackBuilder._direct_child(track_builder, "Output")


def _legacy_cyclic_interpolate(points: list[Vector], station: Fraction) -> Vector:
    scaled = station * len(points)
    index = scaled.numerator // scaled.denominator
    remainder = scaled - index
    if remainder == 0:
        return points[index % len(points)].copy()
    return points[index % len(points)].lerp(
        points[(index + 1) % len(points)],
        float(remainder),
    )


def _legacy_contact_and_offset_reference(
    authored_source: list[Vector],
    dense_source: list[Vector],
    dense_offset: list[Vector],
) -> tuple[list[Vector], list[Vector], set[int]]:
    """Reference implementation retained only for exact merge regression tests."""

    start = min(
        range(len(dense_source)),
        key=lambda index: (dense_source[index] - authored_source[0]).length_squared,
    )
    aligned_source = dense_source[start:] + dense_source[:start]
    aligned_offset = dense_offset[start:] + dense_offset[:start]
    authored_stations = {
        Fraction(index, len(authored_source)): index
        for index in range(len(authored_source))
    }
    dense_stations = {
        Fraction(index, len(aligned_source)): index
        for index in range(len(aligned_source))
    }
    stations = sorted(set(authored_stations) | set(dense_stations))
    forced_indices: set[int] = set()
    contact: list[Vector] = []
    offset: list[Vector] = []
    for candidate_index, station in enumerate(stations):
        authored_index = authored_stations.get(station)
        if authored_index is None:
            contact.append(_legacy_cyclic_interpolate(authored_source, station))
        else:
            contact.append(authored_source[authored_index].copy())
            forced_indices.add(candidate_index)
        dense_index = dense_stations.get(station)
        offset.append(
            aligned_offset[dense_index].copy()
            if dense_index is not None
            else _legacy_cyclic_interpolate(aligned_offset, station)
        )
    return contact, offset, forced_indices


def _create_mesh_loop(
    collection: bpy.types.Collection,
    name: str,
    points: list[tuple[float, float]],
    material: bpy.types.Material,
) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    vertices = [(x, y, 0.0) for x, y in points]
    edges = [(index, (index + 1) % len(vertices)) for index in range(len(vertices))]
    mesh.from_pydata(vertices, edges, [])
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    return obj


def _create_bezier_loop(
    collection: bpy.types.Collection,
    name: str,
    radius_x: float,
    radius_y: float,
    material: bpy.types.Material,
) -> bpy.types.Object:
    curve = bpy.data.curves.new(f"{name}Curve", type="CURVE")
    curve.dimensions = "2D"
    curve.fill_mode = "NONE"
    curve.resolution_u = 2
    spline = curve.splines.new(type="BEZIER")
    spline.bezier_points.add(7)
    spline.resolution_u = 2
    spline.use_cyclic_u = True
    for index, point in enumerate(spline.bezier_points):
        angle = math.tau * index / len(spline.bezier_points)
        point.co = (radius_x * math.cos(angle), radius_y * math.sin(angle), 0.0)
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    curve.materials.append(material)
    obj = bpy.data.objects.new(name, curve)
    collection.objects.link(obj)
    return obj


def _recorded_parameters() -> samples.BuildParameters:
    scene = bpy.context.scene
    return (
        float(scene["track_builder_W"]),
        float(scene["track_builder_H"]),
        float(scene["track_builder_segment_length"]),
        json.loads(scene["track_builder_material_names"]),
    )


def _artifact_filename(input_filename: str) -> str:
    return input_filename.replace("SampleInput", "TestOutput", 1)


def _save_test_artifact(
    input_filename: str,
    expected_result: str,
    actual_result: str,
) -> str:
    bpy.context.scene["track_builder_actual_result"] = actual_result
    path = os.path.join(TEST_OUTPUT_DIRECTORY, _artifact_filename(input_filename))
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=path, check_existing=False)
    TEST_ARTIFACT_RESULTS.append(
        (input_filename, expected_result, actual_result, path)
    )
    print(f"TRACK_BUILDER_TEST_OUTPUT={path}")
    return path


def _prepare_artifact_directory() -> None:
    TEST_ARTIFACT_RESULTS.clear()
    os.makedirs(TEST_ARTIFACT_DIRECTORY, exist_ok=True)
    if os.path.isdir(TEST_OUTPUT_DIRECTORY):
        shutil.rmtree(TEST_OUTPUT_DIRECTORY)
    os.makedirs(TEST_OUTPUT_DIRECTORY)
    if os.path.isfile(TEST_REPORT_PATH):
        os.remove(TEST_REPORT_PATH)


class _TeeStream:
    def __init__(self, *streams: object) -> None:
        self.streams = streams

    def write(self, text: str) -> None:
        for stream in self.streams:
            stream.write(text)

    def flush(self) -> None:
        for stream in self.streams:
            stream.flush()


class TrackBuilderTests(unittest.TestCase):
    def load_test_input(self, number: int) -> samples.BuildParameters:
        filename = samples.SAMPLE_FILENAMES[number]
        path = os.path.join(TEST_INPUT_DIRECTORY, filename)
        self.assertTrue(os.path.isfile(path), f"Missing committed test input: {path}")
        bpy.ops.wm.open_mainfile(filepath=path)
        track_builder = _track_builder_collection()
        self.assertIsNotNone(track_builder)
        self.assertIsNotNone(_outlines_collection())
        self.assertIsNone(
            _output_collection(),
            f"Committed test input contains generated TrackBuilder/Output: {filename}",
        )
        self.assertEqual(
            bpy.context.scene["track_builder_expected_result"],
            samples.expected_result(number),
        )
        parameters = _recorded_parameters()
        self.assertEqual(
            parameters[3],
            list(samples.SAMPLE_MATERIAL_NAMES[number]),
            f"Committed test input has a stale material list: {filename}",
        )
        return parameters

    def assert_valid_output(
        self,
        output: bpy.types.Collection,
        expected_inner_count: int,
    ) -> None:
        track_builder = _track_builder_collection()
        self.assertIsNotNone(track_builder)
        self.assertIs(output, _output_collection())
        self.assertIn(output, track_builder.children[:])
        self.assertEqual(len(output.objects), 0)
        planes = TrackBuilder._direct_child(output, "Planes")
        barrier_segments = TrackBuilder._direct_child(output, "BarrierSegments")
        outline_meshes = TrackBuilder._direct_child(output, "OutlineMeshes")
        self.assertIsNotNone(planes)
        self.assertIsNotNone(barrier_segments)
        self.assertIsNotNone(outline_meshes)
        self.assertEqual(
            {child.name for child in output.children},
            {"Planes", "BarrierSegments", "OutlineMeshes"},
        )
        role_counts: dict[str, int] = {}
        for obj in output.all_objects:
            role = obj.get("track_builder_role")
            role_counts[role] = role_counts.get(role, 0) + 1
            self.assertEqual(obj.type, "MESH")
            if role in {"outer_outline", "inner_outline"}:
                self.assertEqual(len(obj.data.polygons), 0)
                self.assertEqual(len(obj.data.edges), len(obj.data.vertices))
            else:
                self.assertGreater(len(obj.data.polygons), 0)
            expected_collection = (
                barrier_segments
                if role in {"outer_barrier", "inner_barrier"}
                else outline_meshes
                if role in {"outer_outline", "inner_outline"}
                else planes
            )
            self.assertEqual(obj.users_collection[:], (expected_collection,))

        self.assertEqual(
            {obj.get("track_builder_role") for obj in planes.objects},
            {"ground", "track", "island"}
            if expected_inner_count
            else {"ground", "track"},
        )
        self.assertEqual(
            {obj.get("track_builder_role") for obj in barrier_segments.objects},
            {"outer_barrier", "inner_barrier"}
            if expected_inner_count
            else {"outer_barrier"},
        )
        self.assertEqual(
            {obj.get("track_builder_role") for obj in outline_meshes.objects},
            {"outer_outline", "inner_outline"}
            if expected_inner_count
            else {"outer_outline"},
        )

        self.assertEqual(role_counts.get("ground"), 1)
        self.assertEqual(role_counts.get("track"), 1)
        self.assertEqual(role_counts.get("island", 0), expected_inner_count)
        self.assertGreater(role_counts.get("outer_barrier", 0), 0)
        self.assertEqual(role_counts.get("outer_outline"), 1)
        self.assertEqual(role_counts.get("inner_outline", 0), expected_inner_count)
        if expected_inner_count:
            self.assertGreater(role_counts.get("inner_barrier", 0), 0)
        else:
            self.assertEqual(role_counts.get("inner_barrier", 0), 0)
        self.assertEqual(
            set(role_counts),
            {
                "ground",
                "track",
                "island",
                "outer_barrier",
                "inner_barrier",
                "outer_outline",
                "inner_outline",
            }
            if expected_inner_count
            else {"ground", "track", "outer_barrier", "outer_outline"},
        )

    def load_representative_curve_input(self) -> tuple[float, float, float, list[str]]:
        self.assertTrue(
            os.path.isfile(PERFORMANCE_INPUT_PATH),
            f"Missing representative performance input: {PERFORMANCE_INPUT_PATH}",
        )
        bpy.ops.wm.open_mainfile(filepath=PERFORMANCE_INPUT_PATH)
        self.assertIsNotNone(_outlines_collection())
        previous_output = _output_collection()
        if previous_output is not None:
            TrackBuilder._remove_collection_tree(previous_output)
        self.assertIsNone(_output_collection())
        return (1.0, 0.1, 5.0, ["BarrierRed", "BarrierWhite"])

    def prepared_curve_outlines(
        self,
        width: float,
    ) -> tuple[float, list[TrackBuilder._Outline], TrackBuilder._Outline, list[TrackBuilder._Outline]]:
        raw = TrackBuilder._read_raw_outlines(_outlines_collection())
        epsilon = TrackBuilder._world_epsilon(raw)
        base = TrackBuilder._validated_outlines(raw, epsilon)
        ground, outer, inner = TrackBuilder._classify_outlines(base)
        ground, outer, inner = TrackBuilder._refine_classified_outlines(
            ground,
            outer,
            inner,
            width,
            epsilon,
            _track_builder_collection(),
        )
        return epsilon, base, outer, inner

    def test_adaptive_offset_indices_respect_authored_intervals(self) -> None:
        reference = [
            Vector((0.0, 0.0)),
            Vector((1.0, 0.01)),
            Vector((2.0, -0.01)),
            Vector((3.0, 0.01)),
            Vector((4.0, 0.0)),
            Vector((3.0, 1.0)),
            Vector((2.0, 1.0)),
            Vector((1.0, 1.0)),
        ]

        selected = TrackBuilder._adaptive_offset_indices(reference, {0, 4}, 0.011)

        self.assertTrue({0, 4} <= set(selected))
        self.assertTrue({1, 2, 3}.isdisjoint(selected))

    def test_contact_and_offset_reference_matches_exact_rational_union(self) -> None:
        for authored_count, dense_count in [(4, 8), (3, 5), (5, 3), (7, 11)]:
            with self.subTest(authored_count=authored_count, dense_count=dense_count):
                authored = [
                    Vector(
                        (
                            math.cos(math.tau * index / authored_count),
                            math.sin(math.tau * index / authored_count),
                        )
                    )
                    for index in range(authored_count)
                ]
                dense_source = [
                    Vector(
                        (
                            math.cos(math.tau * index / dense_count),
                            math.sin(math.tau * index / dense_count),
                        )
                    )
                    for index in range(dense_count)
                ]
                dense_offset = [
                    Vector((100.0 + index * index, -50.0 + index * 0.25))
                    for index in range(dense_count)
                ]
                shift = min(2, dense_count - 1)
                dense_source = dense_source[shift:] + dense_source[:shift]
                dense_offset = dense_offset[shift:] + dense_offset[:shift]

                expected = _legacy_contact_and_offset_reference(
                    authored,
                    dense_source,
                    dense_offset,
                )
                actual = TrackBuilder._contact_and_offset_reference(
                    authored,
                    dense_source,
                    dense_offset,
                )

                self.assertEqual(actual[2], expected[2])
                self.assertEqual(
                    [tuple(float(value) for value in point) for point in actual[0]],
                    [tuple(float(value) for value in point) for point in expected[0]],
                )
                self.assertEqual(
                    [tuple(float(value) for value in point) for point in actual[1]],
                    [tuple(float(value) for value in point) for point in expected[1]],
                )

    def test_triangulation_uses_cdt_face_provenance(self) -> None:
        material = _ensure_material("TriangulationTestMaterial")

        def outline(name: str, coordinates: list[tuple[float, float]]) -> TrackBuilder._Outline:
            return TrackBuilder._Outline(
                name,
                material,
                [Vector(coordinate) for coordinate in coordinates],
                False,
                None,
            )

        cases = [
            (
                outline(
                    "ConcaveOuter",
                    [(0.0, 0.0), (8.0, 0.0), (8.0, 8.0), (4.0, 4.0), (0.0, 8.0)],
                ),
                [],
            ),
            (
                outline("OuterWithHoles", [(0.0, 0.0), (10.0, 0.0), (10.0, 10.0), (0.0, 10.0)]),
                [
                    outline("HoleA", [(2.0, 2.0), (4.0, 2.0), (4.0, 4.0), (2.0, 4.0)]),
                    outline("HoleB", [(6.0, 6.0), (8.0, 6.0), (8.0, 8.0), (6.0, 8.0)]),
                ],
            ),
        ]
        for outer, holes in cases:
            with self.subTest(case=outer.object_name), mock.patch.object(
                TrackBuilder,
                "_point_in_polygon",
                side_effect=AssertionError("triangulation unexpectedly used Python containment"),
            ):
                plan = TrackBuilder._triangulate_region(
                    outer,
                    holes,
                    1.0e-7,
                    "TriangulationTest",
                    material,
                    "test",
                )

            triangles = [
                [Vector(plan.vertices[index][:2]) for index in face]
                for face in plan.faces
            ]
            self.assertTrue(all(len(face) == 3 for face in plan.faces))
            self.assertTrue(all(TrackBuilder._signed_area(triangle) > 0.0 for triangle in triangles))
            triangulated_area = sum(TrackBuilder._signed_area(triangle) for triangle in triangles)
            expected_area = TrackBuilder._signed_area(outer.points) - sum(
                TrackBuilder._signed_area(hole.points) for hole in holes
            )
            self.assertAlmostEqual(triangulated_area, expected_area, places=6)

    def test_pairwise_edge_relationships_are_trusted_input_preconditions(self) -> None:
        def raw_outline(
            name: str,
            coordinates: list[tuple[float, float]],
        ) -> TrackBuilder._RawOutline:
            vertices = [Vector((x, y, 0.0)) for x, y in coordinates]
            return TrackBuilder._RawOutline(
                name,
                None,
                vertices,
                [
                    (index, (index + 1) % len(vertices))
                    for index in range(len(vertices))
                ],
                0,
                False,
                None,
            )

        star = raw_outline(
            "SelfIntersectingStar",
            [
                (
                    math.cos(math.tau * ((index * 2) % 5) / 5),
                    math.sin(math.tau * ((index * 2) % 5) / 5),
                )
                for index in range(5)
            ],
        )
        overlapping = [
            raw_outline(
                "OverlappingA",
                [(-2.0, -2.0), (2.0, -2.0), (2.0, 2.0), (-2.0, 2.0)],
            ),
            raw_outline(
                "OverlappingB",
                [(0.0, -3.0), (3.0, -3.0), (3.0, 0.0), (0.0, 0.0)],
            ),
        ]

        with mock.patch.object(
            TrackBuilder,
            "_distance_point_to_segment",
            side_effect=AssertionError("pairwise edge-distance validation unexpectedly ran"),
        ):
            self.assertEqual(len(TrackBuilder._ordered_validated_loop(star, 1.0e-7)), 5)
            self.assertEqual(len(TrackBuilder._validated_outlines(overlapping, 1.0e-7)), 2)

    def test_outline_classification_bounds_skip_disjoint_loops(self) -> None:
        def square(name: str, minimum_x: float, minimum_y: float, size: float) -> TrackBuilder._Outline:
            return TrackBuilder._Outline(
                name,
                None,
                [
                    Vector((minimum_x, minimum_y)),
                    Vector((minimum_x + size, minimum_y)),
                    Vector((minimum_x + size, minimum_y + size)),
                    Vector((minimum_x, minimum_y + size)),
                ],
                False,
                None,
            )

        ground = square("Ground", -100.0, -100.0, 200.0)
        outer = square("Outer", -90.0, -90.0, 180.0)
        expected_inner = [
            square(
                f"Inner{index:03d}",
                -40.0 + (index % 10) * 8.0,
                -40.0 + (index // 10) * 8.0,
                2.0,
            )
            for index in range(100)
        ]
        containment = TrackBuilder._point_in_polygon
        with mock.patch.object(
            TrackBuilder,
            "_point_in_polygon",
            side_effect=containment,
        ) as containment_mock:
            actual_ground, actual_outer, actual_inner = TrackBuilder._classify_outlines(
                [ground, outer, *expected_inner]
            )

        self.assertIs(actual_ground, ground)
        self.assertIs(actual_outer, outer)
        self.assertEqual(set(map(id, actual_inner)), set(map(id, expected_inner)))
        self.assertLessEqual(containment_mock.call_count, 3 * len(expected_inner) + 2)

    def test_distinct_point_check_stops_after_three_points(self) -> None:
        epsilon = 1.0e-7
        self.assertFalse(
            TrackBuilder._has_at_least_three_distinct_points(
                [Vector((0.0, 0.0)), Vector((0.0, 0.0)), Vector((1.0, 0.0))],
                epsilon,
            )
        )
        self.assertTrue(
            TrackBuilder._has_at_least_three_distinct_points(
                [
                    Vector((0.0, 0.0)),
                    Vector((1.0, 0.0)),
                    Vector((2.0, 0.0)),
                    *[Vector((float(index), 1.0)) for index in range(10_000)],
                ],
                epsilon,
            )
        )

    def test_curve_refinement_does_not_revalidate_unchanged_contact_points(self) -> None:
        width, _, _, _ = self.load_test_input(7)
        raw = TrackBuilder._read_raw_outlines(_outlines_collection())
        epsilon = TrackBuilder._world_epsilon(raw)
        base = TrackBuilder._validated_outlines(raw, epsilon)
        ground, outer, inner = TrackBuilder._classify_outlines(base)
        contact_points = {
            outline.object_name: [tuple(float(value) for value in point) for point in outline.points]
            for outline in [ground, outer, *inner]
        }

        with mock.patch.object(
            TrackBuilder,
            "_ordered_validated_loop",
            side_effect=AssertionError("curve refinement unexpectedly revalidated contact points"),
        ), mock.patch.object(
            TrackBuilder,
            "_classify_outlines",
            side_effect=AssertionError("curve refinement unexpectedly reclassified outlines"),
        ):
            refined_ground, refined_outer, refined_inner = TrackBuilder._refine_classified_outlines(
                ground,
                outer,
                inner,
                width,
                epsilon,
                _track_builder_collection(),
            )

        self.assertIs(refined_ground, ground)
        for outline in [refined_ground, refined_outer, *refined_inner]:
            self.assertEqual(
                [tuple(float(value) for value in point) for point in outline.points],
                contact_points[outline.object_name],
            )

    def test_outline_meshes_match_evaluated_barrier_contact_loops(self) -> None:
        width, height, target, material_names = self.load_test_input(4)
        raw = TrackBuilder._read_raw_outlines(_outlines_collection())
        epsilon = TrackBuilder._world_epsilon(raw)
        ground, outer, inner = TrackBuilder._classify_outlines(
            TrackBuilder._validated_outlines(raw, epsilon)
        )
        _, outer, inner = TrackBuilder._refine_classified_outlines(
            ground,
            outer,
            inner,
            width,
            epsilon,
            _track_builder_collection(),
        )

        output = TrackBuilder.build_track(
            width,
            height,
            target,
            material_names,
        )
        outline_meshes = TrackBuilder._direct_child(output, "OutlineMeshes")
        generated_by_source = {
            obj["track_builder_source"]: obj for obj in outline_meshes.objects
        }

        expected_outlines = [outer, *inner]
        self.assertEqual(
            set(generated_by_source),
            {outline.object_name for outline in expected_outlines},
        )
        for index, outline in enumerate(expected_outlines):
            obj = generated_by_source[outline.object_name]
            expected_role = "outer_outline" if index == 0 else "inner_outline"
            expected_name = "Outline_Outer" if index == 0 else f"Outline_Inner_{index - 1:02d}"
            self.assertEqual(obj.name, expected_name)
            self.assertEqual(obj["track_builder_role"], expected_role)
            self.assertIs(obj.data.materials[0], outline.material)
            self.assertEqual(
                [tuple(float(component) for component in vertex.co) for vertex in obj.data.vertices],
                [(float(point.x), float(point.y), 0.0) for point in outline.points],
            )
            self.assertEqual(
                {
                    tuple(sorted((int(edge.vertices[0]), int(edge.vertices[1]))))
                    for edge in obj.data.edges
                },
                {
                    tuple(sorted((vertex_index, (vertex_index + 1) % len(outline.points))))
                    for vertex_index in range(len(outline.points))
                },
            )
            self.assertEqual(len(obj.data.polygons), 0)

    def test_representative_curve_adapts_only_offset_and_preserves_contact_topology(self) -> None:
        width, height, target, material_names = self.load_representative_curve_input()
        original_state = {
            obj.name: (
                obj.data.as_pointer(),
                int(obj.data.resolution_u),
                int(obj.data.splines[0].resolution_u),
                tuple(tuple(float(value) for value in point.co) for point in obj.data.splines[0].points),
            )
            for obj in _outlines_collection().all_objects
            if obj.type == "CURVE"
        }
        epsilon, base, outer, inner = self.prepared_curve_outlines(width)
        base_counts = {outline.object_name: len(outline.points) for outline in base}
        base_by_name = {outline.object_name: outline for outline in base}
        self.assertEqual(len(outer.points), base_counts[outer.object_name])
        for outline in inner:
            self.assertEqual(len(outline.points), base_counts[outline.object_name])
        adaptively_refined_count = 0
        for outline in [outer, *inner]:
            self.assertIsNotNone(outline.offset_points)
            authored = base_by_name[outline.object_name]
            authored_keys = {
                (round(float(point.x), 7), round(float(point.y), 7))
                for point in authored.points
            }
            refined_keys = {
                (round(float(point.x), 7), round(float(point.y), 7))
                for point in outline.points
            }
            self.assertEqual(refined_keys, authored_keys)
            self.assertEqual(
                [tuple(float(value) for value in point) for point in outline.points],
                [tuple(float(value) for value in point) for point in authored.points],
            )
            self.assertGreaterEqual(len(outline.offset_points), len(outline.points))
            if len(outline.offset_points) > len(outline.points):
                adaptively_refined_count += 1
            for point in outline.points:
                self.assertLessEqual(
                    min(
                        TrackBuilder._distance_point_to_segment(
                            point,
                            authored.points[index],
                            authored.points[(index + 1) % len(authored.points)],
                        )
                        for index in range(len(authored.points))
                    ),
                    epsilon,
                )
            resolution = TrackBuilder._curve_reference_resolution(outline.source_object)
            dense_source = TrackBuilder._evaluated_curve_loop(
                outline.source_object,
                resolution,
                epsilon,
                _track_builder_collection(),
            )
            dense_offset = TrackBuilder._stable_curve_offset_points(
                dense_source,
                width,
                outline is not outer,
            )
            _, offset_reference, _ = TrackBuilder._contact_and_offset_reference(
                authored.points,
                dense_source,
                dense_offset,
            )
            maximum_error = width * TrackBuilder.ADAPTIVE_OFFSET_ERROR_FACTOR
            for point in offset_reference:
                self.assertLessEqual(
                    min(
                        TrackBuilder._distance_point_to_segment(
                            point,
                            outline.offset_points[index],
                            outline.offset_points[(index + 1) % len(outline.offset_points)],
                        )
                        for index in range(len(outline.offset_points))
                    ),
                    maximum_error * 1.001,
                )
        self.assertGreater(adaptively_refined_count, 0)

        ground = next(item for item in base if not item.is_curve)
        plans = TrackBuilder._build_plans(
            ground,
            outer,
            inner,
            width,
            height,
            target,
            [bpy.data.materials[name] for name in material_names],
            epsilon,
        )
        for outline, barrier_role, fill_names in [
            (outer, "outer_barrier", {"Track"}),
            (inner[0], "inner_barrier", {"Track", "Island_00"}),
        ]:
            outline_keys = {
                (round(float(point.x), 7), round(float(point.y), 7))
                for point in outline.points
            }
            barrier_keys: set[tuple[float, float]] = set()
            contact_keys: set[tuple[float, float]] = set()
            fill_keys: set[tuple[float, float]] = set()
            barrier_plan_count = 0
            for plan in plans:
                if (
                    plan.properties.get("track_builder_role") == barrier_role
                    and plan.properties.get("track_builder_source") == outline.object_name
                ):
                    barrier_plan_count += 1
                    bottom_count = len(plan.vertices) // 2
                    barrier_keys.update(
                        (round(float(x), 7), round(float(y), 7))
                        for x, y, _ in plan.vertices[:bottom_count]
                    )
                    contact_keys.update(
                        (round(float(x), 7), round(float(y), 7))
                        for x, y, _ in plan.vertices[:bottom_count]
                        if min(
                            TrackBuilder._distance_point_to_segment(
                                Vector((x, y)),
                                outline.points[index],
                                outline.points[(index + 1) % len(outline.points)],
                            )
                            for index in range(len(outline.points))
                        )
                        <= epsilon
                    )
                if plan.name in fill_names:
                    fill_keys.update(
                        (round(float(x), 7), round(float(y), 7))
                        for x, y, _ in plan.vertices
                    )
            self.assertTrue(outline_keys <= barrier_keys)
            self.assertTrue(outline_keys <= contact_keys)
            self.assertEqual(len(contact_keys - outline_keys), barrier_plan_count - 1)
            self.assertTrue(outline_keys <= fill_keys)

        output = TrackBuilder.build_track(width, height, target, material_names)
        self.assert_valid_output(output, expected_inner_count=2)
        curve_objects = [
            obj
            for obj in output.all_objects
            if obj.get("track_builder_curve_sampling") is not None
        ]
        self.assertGreater(len(curve_objects), 0)
        self.assertTrue(
            all(
                str(obj["track_builder_curve_sampling"]).startswith(
                    "contact=authored_evaluated,offset=adaptive,"
                )
                for obj in curve_objects
            )
        )
        current_state = {
            obj.name: (
                obj.data.as_pointer(),
                int(obj.data.resolution_u),
                int(obj.data.splines[0].resolution_u),
                tuple(tuple(float(value) for value in point.co) for point in obj.data.splines[0].points),
            )
            for obj in _outlines_collection().all_objects
            if obj.type == "CURVE"
        }
        self.assertEqual(original_state, current_state)

    def test_all_successful_mesh_outputs_match_regression_golden_geometry(self) -> None:
        for number, expected_hash in MESH_FIXTURE_GOLDEN_HASHES.items():
            with self.subTest(fixture=number):
                parameters = self.load_test_input(number)
                self.assertTrue(
                    all(obj.type == "MESH" for obj in _outlines_collection().all_objects)
                )
                output = TrackBuilder.build_track(*parameters)
                self.assertEqual(_output_geometry_hash(output), expected_hash)

    def test_representative_curve_output_matches_bounded_error_golden_geometry(self) -> None:
        output = TrackBuilder.build_track(*self.load_representative_curve_input())

        self.assertEqual(_output_geometry_hash(output), PERFORMANCE_GEOMETRY_HASH)
        self.assertEqual(len(output.all_objects), 183)
        self.assertEqual(sum(len(obj.data.vertices) for obj in output.all_objects), 16_220)
        self.assertEqual(sum(len(obj.data.polygons) for obj in output.all_objects), 9_454)

    def test_poly_curve_remains_linear_and_unresampled(self) -> None:
        parameters = self.load_test_input(7)
        input_counts = {
            obj.name: len(obj.data.splines[0].points)
            for obj in _outlines_collection().all_objects
        }
        output = TrackBuilder.build_track(*parameters)
        for obj in output.all_objects:
            source = obj.get("track_builder_source")
            if source not in input_counts or obj.get("track_builder_curve_sampling") is None:
                continue
            self.assertEqual(obj["track_builder_curve_sampling"], "evaluated_input")
            self.assertEqual(obj["track_builder_curve_sample_count"], input_counts[source])

    def test_unsupported_curve_features_are_rejected(self) -> None:
        def add_modifier(obj: bpy.types.Object) -> None:
            obj.modifiers.new("UnsupportedModifier", type="NODES")

        def add_constraint(obj: bpy.types.Object) -> None:
            obj.constraints.new(type="COPY_LOCATION")

        def add_parent(obj: bpy.types.Object) -> None:
            parent = bpy.data.objects.new("UnsupportedParent", None)
            bpy.context.scene.collection.objects.link(parent)
            obj.parent = parent

        def add_taper(obj: bpy.types.Object) -> None:
            taper_data = bpy.data.curves.new("UnsupportedTaperCurve", type="CURVE")
            taper = bpy.data.objects.new("UnsupportedTaper", taper_data)
            bpy.context.scene.collection.objects.link(taper)
            obj.data.taper_object = taper

        mutations = {
            "modifier": add_modifier,
            "constraint": add_constraint,
            "parent": add_parent,
            "extrude": lambda obj: setattr(obj.data, "extrude", 0.1),
            "bevel": lambda obj: setattr(obj.data, "bevel_depth", 0.1),
            "offset": lambda obj: setattr(obj.data, "offset", 0.1),
            "taper": add_taper,
            "render resolution": lambda obj: setattr(obj.data, "render_resolution_u", 24),
            "tilt": lambda obj: setattr(obj.data.splines[0].points[0], "tilt", 0.1),
            "radius": lambda obj: setattr(obj.data.splines[0].points[0], "radius", 0.75),
            "animation": lambda obj: obj.keyframe_insert(data_path="location", frame=1),
            "shape key": lambda obj: obj.shape_key_add(name="Basis"),
        }
        for label, mutate in mutations.items():
            with self.subTest(feature=label):
                parameters = self.load_test_input(7)
                curve = bpy.data.objects["OuterTrackCurve"]
                mutate(curve)
                with self.assertRaisesRegex(
                    TrackBuilder.TrackBuilderValidationError,
                    "uses unsupported feature",
                ):
                    TrackBuilder.build_track(*parameters)
                self.assertIsNone(_output_collection())

    def test_unsupported_curve_feature_rejection_preserves_previous_output(self) -> None:
        parameters = self.load_test_input(7)
        output = TrackBuilder.build_track(*parameters)
        signature = _output_signature(output)
        bpy.data.objects["OuterTrackCurve"].modifiers.new("UnsupportedModifier", type="NODES")
        with self.assertRaisesRegex(
            TrackBuilder.TrackBuilderValidationError,
            "uses unsupported feature",
        ):
            TrackBuilder.build_track(*parameters)
        self.assertIs(output, _output_collection())
        self.assertEqual(signature, _output_signature(output))

    def test_supported_bezier_curve_is_sampled_without_modifying_input(self) -> None:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        track_builder_collection = bpy.data.collections.new("TrackBuilder")
        input_collection = bpy.data.collections.new("Input")
        outlines_collection = bpy.data.collections.new("Outlines")
        bpy.context.scene.collection.children.link(track_builder_collection)
        track_builder_collection.children.link(input_collection)
        input_collection.children.link(outlines_collection)
        ground_material = _ensure_material("GroundMaterial")
        track_material = _ensure_material("TrackMaterial")
        _ensure_material("BarrierRed")
        _ensure_material("BarrierWhite")
        _create_mesh_loop(
            outlines_collection,
            "GroundOutline",
            [(-15.0, -10.0), (15.0, -10.0), (15.0, 10.0), (-15.0, 10.0)],
            ground_material,
        )
        curve = _create_bezier_loop(
            outlines_collection,
            "OuterBezier",
            10.0,
            6.0,
            track_material,
        )
        original_resolution = curve.data.splines[0].resolution_u
        output = TrackBuilder.build_track(
            0.5,
            0.7,
            2.0,
            ["BarrierRed", "BarrierWhite"],
        )
        track = next(obj for obj in output.all_objects if obj.get("track_builder_role") == "track")
        barrier = next(
            obj for obj in output.all_objects if obj.get("track_builder_role") == "outer_barrier"
        )
        self.assertTrue(
            str(track["track_builder_curve_sampling"]).startswith(
                "contact=authored_evaluated,offset=adaptive,"
            )
        )
        self.assertEqual(track["track_builder_curve_sample_count"], 16)
        self.assertEqual(barrier["track_builder_curve_sample_count"], 16)
        self.assertGreater(
            barrier["track_builder_curve_offset_sample_count"],
            barrier["track_builder_curve_sample_count"],
        )
        self.assertEqual(curve.data.splines[0].resolution_u, original_resolution)

    def test_committed_inputs_build_and_write_artifacts(self) -> None:
        expected_names = set(samples.SAMPLE_FILENAMES.values())
        actual_names = {
            entry.name
            for entry in os.scandir(TEST_INPUT_DIRECTORY)
            if entry.is_file()
            and entry.name.startswith("TrackBuilderSampleInput")
            and entry.name.endswith(".blend")
        }
        self.assertEqual(
            actual_names,
            expected_names,
            "Committed test input set is stale; run GenerateTrackBuilderSamples.py",
        )

        for number, filename in samples.SAMPLE_FILENAMES.items():
            with self.subTest(input=number):
                parameters = self.load_test_input(number)
                expected_result = samples.expected_result(number)
                actual_result = "test_aborted"
                try:
                    if expected_result == "success":
                        output = TrackBuilder.build_track(*parameters)
                        actual_result = "success"
                        self.assert_valid_output(
                            output,
                            expected_inner_count=EXPECTED_INNER_COUNTS[number],
                        )
                    else:
                        expected_exception = EXPECTED_EXCEPTIONS[expected_result]
                        try:
                            TrackBuilder.build_track(*parameters)
                        except TrackBuilder.TrackBuilderError as error:
                            actual_result = type(error).__name__
                            self.assertIsInstance(error, expected_exception)
                        else:
                            actual_result = "unexpected_success"
                            self.fail(f"Expected {expected_exception.__name__}")
                        self.assertIsNone(_output_collection())
                finally:
                    _save_test_artifact(filename, expected_result, actual_result)

    def test_barriers_use_complete_material_sequences_without_short_segments(self) -> None:
        width, height, _, material_names = self.load_test_input(3)
        self.assertEqual(len(material_names), 3)
        target = 4.0

        output = TrackBuilder.build_track(width, height, target, material_names)
        barrier_groups: dict[tuple[str, str], list[bpy.types.Object]] = {}
        for obj in output.all_objects:
            role = obj.get("track_builder_role")
            if role not in {"outer_barrier", "inner_barrier"}:
                continue
            key = (role, obj["track_builder_source"])
            barrier_groups.setdefault(key, []).append(obj)

        self.assertGreater(len(barrier_groups), 0)
        for barriers in barrier_groups.values():
            barriers.sort(key=lambda obj: obj["track_builder_segment_index"])
            self.assertEqual(len(barriers) % len(material_names), 0)
            self.assertEqual(
                [obj.data.materials[0].name for obj in barriers],
                [
                    material_names[index % len(material_names)]
                    for index in range(len(barriers))
                ],
            )
            for obj in barriers:
                self.assertGreaterEqual(
                    obj["track_builder_adjusted_segment_length"], target
                )

    def test_equal_segment_length_is_accepted_without_allowing_shorter_segments(self) -> None:
        self.assertEqual(TrackBuilder._segment_count(90.0, 10.0, 3, "TestOutline"), 9)
        count = TrackBuilder._segment_count(89.999999999, 10.0, 3, "TestOutline")
        self.assertGreaterEqual(89.999999999 / count, 10.0)

    def test_single_material_throws(self) -> None:
        width, height, target, material_names = self.load_test_input(2)
        with self.assertRaisesRegex(
            TrackBuilder.TrackBuilderValidationError,
            "material_names must be a list containing at least two entries",
        ):
            TrackBuilder.build_track(width, height, target, material_names[:1])
        self.assertIsNone(_output_collection())

    def test_track_builder_collection_and_nested_outlines_are_required(self) -> None:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        legacy_input = bpy.data.collections.new("Input")
        bpy.context.scene.collection.children.link(legacy_input)
        _ensure_material("BarrierRed")
        _ensure_material("BarrierWhite")

        with self.assertRaisesRegex(
            TrackBuilder.TrackBuilderValidationError,
            "does not contain a collection named 'TrackBuilder'",
        ):
            TrackBuilder.build_track(
                0.5,
                0.5,
                2.0,
                ["BarrierRed", "BarrierWhite"],
            )

        track_builder = bpy.data.collections.new("TrackBuilder")
        bpy.context.scene.collection.children.link(track_builder)
        track_builder.children.link(legacy_input)
        with self.assertRaisesRegex(
            TrackBuilder.TrackBuilderValidationError,
            "TrackBuilder/Input does not contain a direct child collection named 'Outlines'",
        ):
            TrackBuilder.build_track(
                0.5,
                0.5,
                2.0,
                ["BarrierRed", "BarrierWhite"],
            )

    def test_numeric_parameters_accept_point_one_and_reject_smaller_values(self) -> None:
        _, _, _, material_names = self.load_test_input(2)
        output = TrackBuilder.build_track(0.1, 0.1, 0.1, material_names)
        self.assert_valid_output(output, expected_inner_count=0)

        signature = _output_signature(output)
        invalid_parameters = {
            "W": (math.nextafter(0.1, 0.0), 0.1, 0.1, material_names),
            "H": (0.1, math.nextafter(0.1, 0.0), 0.1, material_names),
            "segment_length": (0.1, 0.1, math.nextafter(0.1, 0.0), material_names),
        }
        for name, parameters in invalid_parameters.items():
            with self.subTest(parameter=name):
                with self.assertRaisesRegex(
                    TrackBuilder.TrackBuilderValidationError,
                    rf"^{name} must be a finite number greater than or equal to 0\.1$",
                ):
                    TrackBuilder.build_track(*parameters)
                self.assertIs(output, _output_collection())
                self.assertEqual(signature, _output_signature(output))

    def test_small_turn_angle_sample_throws(self) -> None:
        parameters = self.load_test_input(9)
        with self.assertRaisesRegex(
            TrackBuilder.TrackBuilderValidationError,
            "has a turn angle of .* degrees.*minimum is 0.01 degrees",
        ):
            TrackBuilder.build_track(*parameters)
        self.assertIsNone(_output_collection())

    def test_single_segment_sample_throws(self) -> None:
        parameters = self.load_test_input(10)
        with self.assertRaisesRegex(
            TrackBuilder.TrackBuilderGeometryError,
            "would produce only one barrier segment",
        ):
            TrackBuilder.build_track(*parameters)
        self.assertIsNone(_output_collection())

    def test_single_segment_failure_preserves_previous_output(self) -> None:
        width, height, target, material_names = self.load_test_input(3)
        output = TrackBuilder.build_track(width, height, target, material_names)
        signature = _output_signature(output)

        with self.assertRaises(TrackBuilder.TrackBuilderGeometryError):
            TrackBuilder.build_track(width, height, 1000.0, material_names)

        self.assertIs(output, _output_collection())
        self.assertEqual(signature, _output_signature(output))

    def test_existing_output_with_extra_child_collection_is_rejected(self) -> None:
        width, height, target, material_names = self.load_test_input(3)
        output = TrackBuilder.build_track(width, height, target, material_names)
        signature = _output_signature(output)
        shared_child = bpy.data.collections.new("SharedOutputChild")
        output.children.link(shared_child)
        _outlines_collection().children.link(shared_child)

        with self.assertRaisesRegex(
            TrackBuilder.TrackBuilderValidationError,
            "existing TrackBuilder/Output collection must contain exactly",
        ):
            TrackBuilder.build_track(width, height, target, material_names)

        self.assertIs(output, _output_collection())
        self.assertEqual(signature, _output_signature(output))
        self.assertIs(shared_child, bpy.data.collections.get("SharedOutputChild"))
        self.assertIn(shared_child, output.children[:])
        self.assertIn(shared_child, _outlines_collection().children[:])
        self.assertFalse(
            any(
                collection.name.startswith("__TrackBuilderPending_")
                for collection in bpy.data.collections
            )
        )

    def test_existing_output_missing_outline_meshes_is_rejected(self) -> None:
        width, height, target, material_names = self.load_test_input(3)
        output = bpy.data.collections.new("Output")
        output.children.link(bpy.data.collections.new("Planes"))
        output.children.link(bpy.data.collections.new("BarrierSegments"))
        _track_builder_collection().children.link(output)

        with self.assertRaisesRegex(
            TrackBuilder.TrackBuilderValidationError,
            "existing TrackBuilder/Output collection must contain exactly",
        ):
            TrackBuilder.build_track(width, height, target, material_names)

        self.assertIs(output, _output_collection())
        self.assertEqual(
            {child.name for child in output.children},
            {"Planes", "BarrierSegments"},
        )
        self.assertFalse(
            any(
                collection.name.startswith("__TrackBuilderPending_")
                for collection in bpy.data.collections
            )
        )

    def test_collection_removal_batches_exclusive_data_and_preserves_shared_objects(self) -> None:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        root = bpy.data.collections.new("RemovalRoot")
        child = bpy.data.collections.new("RemovalChild")
        external = bpy.data.collections.new("RemovalExternal")
        bpy.context.scene.collection.children.link(root)
        bpy.context.scene.collection.children.link(external)
        root.children.link(child)

        def mesh_object(name: str, collection: bpy.types.Collection) -> bpy.types.Object:
            mesh = bpy.data.meshes.new(f"{name}Mesh")
            mesh.from_pydata(
                [(0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0)],
                [],
                [(0, 1, 2)],
            )
            obj = bpy.data.objects.new(name, mesh)
            collection.objects.link(obj)
            return obj

        exclusive_root = mesh_object("ExclusiveRoot", root)
        exclusive_child = mesh_object("ExclusiveChild", child)
        shared = mesh_object("SharedOutside", root)
        external.objects.link(shared)
        exclusive_with_shared_mesh = mesh_object("ExclusiveWithSharedMesh", root)
        external_mesh_user = bpy.data.objects.new(
            "ExternalMeshUser",
            exclusive_with_shared_mesh.data,
        )
        external.objects.link(external_mesh_user)
        exclusive_mesh_names = {exclusive_root.data.name, exclusive_child.data.name}
        shared_mesh = shared.data
        externally_used_mesh = exclusive_with_shared_mesh.data

        TrackBuilder._remove_collection_tree(root)

        self.assertIsNone(bpy.data.collections.get("RemovalRoot"))
        self.assertIsNone(bpy.data.collections.get("RemovalChild"))
        self.assertIs(external, bpy.data.collections.get("RemovalExternal"))
        self.assertIs(shared, bpy.data.objects.get("SharedOutside"))
        self.assertIn(shared, external.objects[:])
        self.assertIs(shared_mesh, bpy.data.meshes.get(shared_mesh.name))
        self.assertIs(external_mesh_user, bpy.data.objects.get("ExternalMeshUser"))
        self.assertIs(externally_used_mesh, bpy.data.meshes.get(externally_used_mesh.name))
        for name in ["ExclusiveRoot", "ExclusiveChild", "ExclusiveWithSharedMesh"]:
            self.assertIsNone(bpy.data.objects.get(name))
        for name in exclusive_mesh_names:
            self.assertIsNone(bpy.data.meshes.get(name))

    def test_library_linked_output_is_rejected(self) -> None:
        width, height, target, material_names = self.load_test_input(3)
        library_path = os.path.join(TEST_ARTIFACT_DIRECTORY, "LinkedOutput.blend")
        source_output = bpy.data.collections.new("Output")
        source_output.children.link(bpy.data.collections.new("Planes"))
        source_output.children.link(bpy.data.collections.new("BarrierSegments"))
        source_output.children.link(bpy.data.collections.new("OutlineMeshes"))
        bpy.data.libraries.write(library_path, {source_output})
        TrackBuilder._remove_collection_tree(source_output)

        try:
            with bpy.data.libraries.load(library_path, link=True) as (_, library_data):
                library_data.collections = ["Output"]
            linked_output = library_data.collections[0]
            _track_builder_collection().children.link(linked_output)
            self.assertFalse(linked_output.is_editable)

            with self.assertRaisesRegex(
                TrackBuilder.TrackBuilderValidationError,
                "existing TrackBuilder/Output collection must be local and editable",
            ):
                TrackBuilder.build_track(width, height, target, material_names)

            self.assertIs(linked_output, _output_collection())
            self.assertFalse(
                any(
                    collection.name.startswith("__TrackBuilderPending_")
                    for collection in bpy.data.collections
                )
            )
        finally:
            linked_output = _output_collection()
            if linked_output is not None:
                bpy.data.collections.remove(linked_output, do_unlink=True)
            if os.path.isfile(library_path):
                os.remove(library_path)


def _main() -> None:
    _prepare_artifact_directory()
    started_at = datetime.now().astimezone()
    started_timer = time.perf_counter()
    captured = io.StringIO()
    stream = _TeeStream(sys.stdout, captured)
    stream.write("TrackBuilder test run\n")
    stream.write(f"Started: {started_at.isoformat()}\n")
    stream.write(f"Blender: {bpy.app.version_string}\n")
    stream.write(f"Inputs: {TEST_INPUT_DIRECTORY}\n")
    stream.write(f"Artifacts: {TEST_ARTIFACT_DIRECTORY}\n\n")

    suite = unittest.defaultTestLoader.loadTestsFromTestCase(TrackBuilderTests)
    result = unittest.TextTestRunner(stream=stream, verbosity=2).run(suite)
    elapsed = time.perf_counter() - started_timer
    stream.write(f"\nElapsed: {elapsed:.3f} seconds\n")
    stream.write(f"Inspectable outputs: {TEST_OUTPUT_DIRECTORY}\n")
    stream.write("\nFixture artifacts:\n")
    for input_filename, expected, actual, path in TEST_ARTIFACT_RESULTS:
        stream.write(
            f"  {input_filename}: expected={expected}, actual={actual}\n"
            f"    {path}\n"
        )
    stream.write(f"Overall result: {'PASS' if result.wasSuccessful() else 'FAIL'}\n")

    with open(TEST_REPORT_PATH, "w", encoding="utf-8", newline="\n") as report:
        report.write(captured.getvalue())
    print(f"TRACK_BUILDER_TEST_REPORT={TEST_REPORT_PATH}")

    if not result.wasSuccessful():
        raise SystemExit(1)


if __name__ == "__main__":
    _main()
