"""Blender integration tests for TrackBuilder.

The tests read committed, input-only .blend fixtures from TestInputs. Each run
writes inspectable .blend results and a complete text report to the gitignored
TestArtifacts directory.
"""

from __future__ import annotations

from datetime import datetime
import hashlib
import io
import json
import math
import os
import shutil
import sys
import time
import unittest


sys.dont_write_bytecode = True
SCRIPT_DIRECTORY = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIRECTORY not in sys.path:
    sys.path.insert(0, SCRIPT_DIRECTORY)

import bpy

import GenerateTrackBuilderSamples as samples
import TrackBuilder


TEST_INPUT_DIRECTORY = os.path.join(SCRIPT_DIRECTORY, "TestInputs")
TEST_ARTIFACT_DIRECTORY = os.path.join(SCRIPT_DIRECTORY, "TestArtifacts")
TEST_OUTPUT_DIRECTORY = os.path.join(TEST_ARTIFACT_DIRECTORY, "Outputs")
TEST_REPORT_PATH = os.path.join(TEST_ARTIFACT_DIRECTORY, "TestReport.txt")
EXAMPLE_INPUT_PATH = os.path.join(
    SCRIPT_DIRECTORY,
    "Examples",
    "ResolutionCurvatureIssue_Input.blend",
)
TEST_ARTIFACT_RESULTS: list[tuple[str, str, str, str]] = []
MESH_FIXTURE_GOLDEN_HASHES = {
    2: "40513fa0f4866aeec1bf53741a8dbfc708cab7622f875d541bf1b197aba9517e",
    3: "02c19b4c5fc5a0e955427b42278064beb2b1c3c0df056bb0f23e9b539a2728ff",
    4: "52ba61001a8729afc63cd0b8466da15ed2e4b78867d95a2c6e32edb69fb4881f",
    5: "c755438e54571cc4a1eb9de32a9674377c196a079487d8c61490bd698080f112",
    6: "14c86821945e524ae4b01fb7585d7b84e8d60410ec38d864c7f24fda74b3a459",
    8: "75e282043def010bafc3f82f5fa80bfb4030134b6bf43d2eb1f7d1ef98d9a3e4",
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
        self.assertIsNotNone(bpy.data.collections.get("Input"))
        self.assertIsNone(
            bpy.data.collections.get("Output"),
            f"Committed test input contains generated Output: {filename}",
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
        self.assertIs(output, bpy.data.collections.get("Output"))
        role_counts: dict[str, int] = {}
        for obj in output.all_objects:
            role = obj.get("track_builder_role")
            role_counts[role] = role_counts.get(role, 0) + 1
            self.assertEqual(obj.type, "MESH")
            self.assertGreater(len(obj.data.polygons), 0)

        self.assertEqual(role_counts.get("ground"), 1)
        self.assertEqual(role_counts.get("track"), 1)
        self.assertEqual(role_counts.get("island", 0), expected_inner_count)
        self.assertGreater(role_counts.get("outer_barrier", 0), 0)
        if expected_inner_count:
            self.assertGreater(role_counts.get("inner_barrier", 0), 0)
        else:
            self.assertEqual(role_counts.get("inner_barrier", 0), 0)
        self.assertEqual(
            set(role_counts),
            {"ground", "track", "island", "outer_barrier", "inner_barrier"}
            if expected_inner_count
            else {"ground", "track", "outer_barrier"},
        )

    def load_example_input(self) -> tuple[float, float, float, list[str]]:
        self.assertTrue(os.path.isfile(EXAMPLE_INPUT_PATH), f"Missing example: {EXAMPLE_INPUT_PATH}")
        bpy.ops.wm.open_mainfile(filepath=EXAMPLE_INPUT_PATH)
        self.assertIsNotNone(bpy.data.collections.get("Input"))
        self.assertIsNone(bpy.data.collections.get("Output"))
        return (1.0, 0.1, 5.0, ["red", "white"])

    def prepared_example_outlines(
        self,
        width: float,
    ) -> tuple[float, list[TrackBuilder._Outline], TrackBuilder._Outline, list[TrackBuilder._Outline]]:
        raw = TrackBuilder._read_raw_outlines(bpy.data.collections["Input"])
        epsilon = TrackBuilder._world_epsilon(raw)
        base = TrackBuilder._validated_outlines(raw, epsilon)
        ground, outer, inner = TrackBuilder._classify_outlines(base)
        ground, outer, inner = TrackBuilder._refine_classified_outlines(
            ground,
            outer,
            inner,
            width,
            epsilon,
        )
        return epsilon, base, outer, inner

    def test_resolution_issue_example_uses_increased_resolution_and_contacts_source_exactly(self) -> None:
        width, height, target, material_names = self.load_example_input()
        original_state = {
            obj.name: (
                obj.data.as_pointer(),
                int(obj.data.resolution_u),
                int(obj.data.splines[0].resolution_u),
                tuple(tuple(float(value) for value in point.co) for point in obj.data.splines[0].points),
            )
            for obj in bpy.data.collections["Input"].all_objects
            if obj.type == "CURVE"
        }
        epsilon, base, outer, inner = self.prepared_example_outlines(width)
        base_counts = {outline.object_name: len(outline.points) for outline in base}
        base_by_name = {outline.object_name: outline for outline in base}
        self.assertGreater(len(outer.points), base_counts[outer.object_name])
        self.assertGreater(len(inner[0].points), base_counts[inner[0].object_name])
        self.assertEqual(len(outer.points), 2304)
        self.assertEqual(len(inner[0].points), 2304)
        for outline in [outer, inner[0]]:
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
            self.assertTrue(authored_keys <= refined_keys)
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
            offset_points = outline.offset_points
            maximum_turn = 0.0
            for index, point in enumerate(offset_points):
                incoming = point - offset_points[index - 1]
                outgoing = offset_points[(index + 1) % len(offset_points)] - point
                maximum_turn = max(
                    maximum_turn,
                    abs(
                        math.atan2(
                            TrackBuilder._cross_2d(incoming, outgoing),
                            incoming.dot(outgoing),
                        )
                    ),
                )
            self.assertLessEqual(math.degrees(maximum_turn), 8.0)

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
            fill_keys: set[tuple[float, float]] = set()
            for plan in plans:
                if plan.properties.get("track_builder_role") == barrier_role:
                    bottom_count = len(plan.vertices) // 2
                    barrier_keys.update(
                        (round(float(x), 7), round(float(y), 7))
                        for x, y, _ in plan.vertices[:bottom_count]
                    )
                if plan.name in fill_names:
                    fill_keys.update(
                        (round(float(x), 7), round(float(y), 7))
                        for x, y, _ in plan.vertices
                    )
            self.assertTrue(outline_keys <= barrier_keys)
            self.assertTrue(outline_keys <= fill_keys)

        output = TrackBuilder.build_track(width, height, target, material_names)
        self.assert_valid_output(output, expected_inner_count=1)
        curve_objects = [
            obj
            for obj in output.all_objects
            if obj.get("track_builder_curve_sampling") is not None
        ]
        self.assertGreater(len(curve_objects), 0)
        self.assertTrue(
            all(
                str(obj["track_builder_curve_sampling"]).startswith(
                    "increased_contact=authored,"
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
            for obj in bpy.data.collections["Input"].all_objects
            if obj.type == "CURVE"
        }
        self.assertEqual(original_state, current_state)

    def test_all_successful_mesh_outputs_match_production_golden_geometry(self) -> None:
        for number, expected_hash in MESH_FIXTURE_GOLDEN_HASHES.items():
            with self.subTest(fixture=number):
                parameters = self.load_test_input(number)
                self.assertTrue(
                    all(obj.type == "MESH" for obj in bpy.data.collections["Input"].all_objects)
                )
                output = TrackBuilder.build_track(*parameters)
                self.assertEqual(_output_geometry_hash(output), expected_hash)

    def test_poly_curve_remains_linear_and_unresampled(self) -> None:
        parameters = self.load_test_input(7)
        input_counts = {
            obj.name: len(obj.data.splines[0].points)
            for obj in bpy.data.collections["Input"].all_objects
        }
        output = TrackBuilder.build_track(*parameters)
        for obj in output.all_objects:
            source = obj.get("track_builder_source")
            if source not in input_counts or obj.get("track_builder_curve_sampling") is None:
                continue
            self.assertEqual(obj["track_builder_curve_sampling"], "evaluated_input")
            self.assertEqual(obj["track_builder_curve_sample_count"], input_counts[source])

    def test_vanilla_curve_contract_rejects_unsupported_features(self) -> None:
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
                    "is not a vanilla curve",
                ):
                    TrackBuilder.build_track(*parameters)
                self.assertIsNone(bpy.data.collections.get("Output"))

    def test_vanilla_curve_rejection_preserves_previous_output(self) -> None:
        parameters = self.load_test_input(7)
        output = TrackBuilder.build_track(*parameters)
        signature = _output_signature(output)
        bpy.data.objects["OuterTrackCurve"].modifiers.new("UnsupportedModifier", type="NODES")
        with self.assertRaisesRegex(
            TrackBuilder.TrackBuilderValidationError,
            "is not a vanilla curve",
        ):
            TrackBuilder.build_track(*parameters)
        self.assertIs(output, bpy.data.collections.get("Output"))
        self.assertEqual(signature, _output_signature(output))

    def test_vanilla_bezier_curve_is_sampled_without_modifying_input(self) -> None:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        input_collection = bpy.data.collections.new("Input")
        bpy.context.scene.collection.children.link(input_collection)
        ground_material = _ensure_material("GroundMaterial")
        track_material = _ensure_material("TrackMaterial")
        _ensure_material("BarrierRed")
        _ensure_material("BarrierWhite")
        _create_mesh_loop(
            input_collection,
            "GroundOutline",
            [(-15.0, -10.0), (15.0, -10.0), (15.0, 10.0), (-15.0, 10.0)],
            ground_material,
        )
        curve = _create_bezier_loop(
            input_collection,
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
        self.assertTrue(
            str(track["track_builder_curve_sampling"]).startswith(
                "increased_contact=authored,"
            )
        )
        self.assertGreater(track["track_builder_curve_sample_count"], 16)
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
                        self.assertIsNone(bpy.data.collections.get("Output"))
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
        self.assertIsNone(bpy.data.collections.get("Output"))

    def test_small_turn_angle_sample_throws(self) -> None:
        parameters = self.load_test_input(9)
        with self.assertRaisesRegex(
            TrackBuilder.TrackBuilderValidationError,
            "has a turn angle of .* degrees.*minimum is 0.01 degrees",
        ):
            TrackBuilder.build_track(*parameters)
        self.assertIsNone(bpy.data.collections.get("Output"))

    def test_single_segment_sample_throws(self) -> None:
        parameters = self.load_test_input(10)
        with self.assertRaisesRegex(
            TrackBuilder.TrackBuilderGeometryError,
            "would produce only one barrier segment",
        ):
            TrackBuilder.build_track(*parameters)
        self.assertIsNone(bpy.data.collections.get("Output"))

    def test_single_segment_failure_preserves_previous_output(self) -> None:
        width, height, target, material_names = self.load_test_input(3)
        output = TrackBuilder.build_track(width, height, target, material_names)
        signature = _output_signature(output)

        with self.assertRaises(TrackBuilder.TrackBuilderGeometryError):
            TrackBuilder.build_track(width, height, 1000.0, material_names)

        self.assertIs(output, bpy.data.collections.get("Output"))
        self.assertEqual(signature, _output_signature(output))

    def test_existing_output_with_child_collection_is_rejected(self) -> None:
        width, height, target, material_names = self.load_test_input(3)
        output = TrackBuilder.build_track(width, height, target, material_names)
        signature = _output_signature(output)
        shared_child = bpy.data.collections.new("SharedOutputChild")
        output.children.link(shared_child)
        bpy.data.collections["Input"].children.link(shared_child)

        with self.assertRaisesRegex(
            TrackBuilder.TrackBuilderValidationError,
            "existing Output collection must not contain child collections",
        ):
            TrackBuilder.build_track(width, height, target, material_names)

        self.assertIs(output, bpy.data.collections.get("Output"))
        self.assertEqual(signature, _output_signature(output))
        self.assertIs(shared_child, bpy.data.collections.get("SharedOutputChild"))
        self.assertIn(shared_child, output.children[:])
        self.assertIn(shared_child, bpy.data.collections["Input"].children[:])
        self.assertFalse(
            any(
                collection.name.startswith("__TrackBuilderPending_")
                for collection in bpy.data.collections
            )
        )

    def test_library_linked_output_is_rejected(self) -> None:
        width, height, target, material_names = self.load_test_input(3)
        library_path = os.path.join(TEST_ARTIFACT_DIRECTORY, "LinkedOutput.blend")
        source_output = bpy.data.collections.new("Output")
        bpy.data.libraries.write(library_path, {source_output})
        bpy.data.collections.remove(source_output)

        try:
            with bpy.data.libraries.load(library_path, link=True) as (_, library_data):
                library_data.collections = ["Output"]
            linked_output = library_data.collections[0]
            bpy.context.scene.collection.children.link(linked_output)
            self.assertFalse(linked_output.is_editable)

            with self.assertRaisesRegex(
                TrackBuilder.TrackBuilderValidationError,
                "existing Output collection must be local and editable",
            ):
                TrackBuilder.build_track(width, height, target, material_names)

            self.assertIs(linked_output, bpy.data.collections.get("Output"))
            self.assertFalse(
                any(
                    collection.name.startswith("__TrackBuilderPending_")
                    for collection in bpy.data.collections
                )
            )
        finally:
            linked_output = bpy.data.collections.get("Output")
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
