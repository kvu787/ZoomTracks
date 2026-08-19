"""Blender integration tests for TrackBuilder."""

from __future__ import annotations

import hashlib
import json
import os
import sys
import unittest


sys.dont_write_bytecode = True
SCRIPT_DIRECTORY = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIRECTORY not in sys.path:
    sys.path.insert(0, SCRIPT_DIRECTORY)

import bpy

import GenerateTrackBuilderExamples as examples
import GenerateTrackBuilderSamples as samples
import TrackBuilder


ORIGINAL_SAMPLE_PATH = os.path.join(SCRIPT_DIRECTORY, "SampleInput.blend")
EXAMPLE_DIRECTORY = os.path.join(SCRIPT_DIRECTORY, "Examples")


def _output_signature(output: bpy.types.Collection) -> tuple[tuple[int, int, str], ...]:
    return tuple(
        sorted(
            (obj.as_pointer(), obj.data.as_pointer(), obj.name)
            for obj in output.all_objects
        )
    )


def _semantic_output_digest(output: bpy.types.Collection) -> str:
    object_signatures: list[object] = []
    for obj in output.all_objects:
        properties = tuple(
            sorted(
                (
                    key,
                    round(value, 12) if isinstance(value, float) else value,
                )
                for key, value in obj.items()
                if key.startswith("track_builder_")
            )
        )
        matrix = tuple(round(value, 10) for row in obj.matrix_world for value in row)
        vertices = tuple(
            tuple(round(component, 10) for component in vertex.co)
            for vertex in obj.data.vertices
        )
        polygons = tuple(tuple(polygon.vertices) for polygon in obj.data.polygons)
        materials = tuple(material.name for material in obj.data.materials)
        object_signatures.append(
            (obj.name, properties, matrix, vertices, polygons, materials)
        )
    encoded = repr(tuple(sorted(object_signatures))).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def _recorded_parameters() -> samples.BuildParameters:
    scene = bpy.context.scene
    return (
        float(scene["track_builder_W"]),
        float(scene["track_builder_H"]),
        float(scene["track_builder_segment_length"]),
        json.loads(scene["track_builder_material_names"]),
    )


class TrackBuilderTests(unittest.TestCase):
    def assert_valid_output(self, output: bpy.types.Collection, has_inner: bool) -> None:
        self.assertIs(output, bpy.data.collections.get("Output"))
        roles = {obj.get("track_builder_role") for obj in output.all_objects}
        self.assertIn("ground", roles)
        self.assertIn("track", roles)
        self.assertIn("outer_barrier", roles)
        if has_inner:
            self.assertIn("island", roles)
            self.assertIn("inner_barrier", roles)
        for obj in output.all_objects:
            self.assertEqual(obj.type, "MESH")
            self.assertGreater(len(obj.data.polygons), 0)

    def test_original_sample_builds(self) -> None:
        parameters = samples.load_sample_input(1, ORIGINAL_SAMPLE_PATH)
        output = TrackBuilder.build_track(*parameters)
        self.assert_valid_output(output, has_inner=True)

    def test_tracked_examples_match_current_builder(self) -> None:
        expected_names = set(examples.EXAMPLE_FILENAMES.values())
        actual_names = {
            filename
            for filename in os.listdir(EXAMPLE_DIRECTORY)
            if filename.startswith("TrackBuilderExample") and ".blend" in filename
        }
        self.assertEqual(
            actual_names,
            expected_names,
            "Tracked example set is stale; run GenerateTrackBuilderExamples.py",
        )

        for number, filename in examples.EXAMPLE_FILENAMES.items():
            with self.subTest(example=number):
                path = os.path.join(EXAMPLE_DIRECTORY, filename)
                bpy.ops.wm.open_mainfile(filepath=path)
                result = samples.expected_result(number)
                self.assertEqual(bpy.context.scene["track_builder_expected_result"], result)
                parameters = _recorded_parameters()

                if result == "success":
                    stored_output = bpy.data.collections.get("Output")
                    self.assertIsNotNone(stored_output)
                    stored_digest = _semantic_output_digest(stored_output)
                    rebuilt_output = TrackBuilder.build_track(*parameters)
                    self.assertEqual(
                        stored_digest,
                        _semantic_output_digest(rebuilt_output),
                        f"{filename} is stale; run GenerateTrackBuilderExamples.py",
                    )
                else:
                    self.assertIsNone(bpy.data.collections.get("Output"))
                    with self.assertRaises(examples.EXPECTED_EXCEPTIONS[result]):
                        TrackBuilder.build_track(*parameters)

    def test_synthetic_success_samples_build(self) -> None:
        for number in range(2, 9):
            with self.subTest(sample=number):
                parameters = samples.create_synthetic_sample(number)
                output = TrackBuilder.build_track(*parameters)
                self.assert_valid_output(output, has_inner=number != 2)

    def test_barriers_use_complete_material_sequences_without_short_segments(self) -> None:
        width, height, _, material_names = samples.create_synthetic_sample(3)
        blue = bpy.data.materials.new("BarrierBlue")
        blue.use_fake_user = True
        material_names.append(blue.name)
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
        width, height, target, material_names = samples.create_synthetic_sample(2)
        with self.assertRaisesRegex(
            TrackBuilder.TrackBuilderValidationError,
            "material_names must be a list containing at least two entries",
        ):
            TrackBuilder.build_track(width, height, target, material_names[:1])
        self.assertIsNone(bpy.data.collections.get("Output"))

    def test_small_turn_angle_sample_throws(self) -> None:
        parameters = samples.create_synthetic_sample(9)
        with self.assertRaisesRegex(
            TrackBuilder.TrackBuilderValidationError,
            "has a turn angle of .* degrees.*minimum is 0.01 degrees",
        ):
            TrackBuilder.build_track(*parameters)
        self.assertIsNone(bpy.data.collections.get("Output"))

    def test_single_segment_sample_throws(self) -> None:
        parameters = samples.create_synthetic_sample(10)
        with self.assertRaisesRegex(
            TrackBuilder.TrackBuilderGeometryError,
            "would produce only one barrier segment",
        ):
            TrackBuilder.build_track(*parameters)
        self.assertIsNone(bpy.data.collections.get("Output"))

    def test_single_segment_failure_preserves_previous_output(self) -> None:
        width, height, target, material_names = samples.create_synthetic_sample(3)
        output = TrackBuilder.build_track(width, height, target, material_names)
        signature = _output_signature(output)

        with self.assertRaises(TrackBuilder.TrackBuilderGeometryError):
            TrackBuilder.build_track(width, height, 1000.0, material_names)

        self.assertIs(output, bpy.data.collections.get("Output"))
        self.assertEqual(signature, _output_signature(output))


def _main() -> None:
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(TrackBuilderTests)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    if not result.wasSuccessful():
        raise SystemExit(1)


if __name__ == "__main__":
    _main()
