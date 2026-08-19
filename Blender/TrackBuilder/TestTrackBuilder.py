"""Blender integration tests for TrackBuilder."""

from __future__ import annotations

import os
import sys
import unittest


sys.dont_write_bytecode = True
SCRIPT_DIRECTORY = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIRECTORY not in sys.path:
    sys.path.insert(0, SCRIPT_DIRECTORY)

import bpy

import GenerateTrackBuilderSamples as samples
import TrackBuilder


ORIGINAL_SAMPLE_PATH = os.path.join(SCRIPT_DIRECTORY, "SampleInput.blend")


def _output_signature(output: bpy.types.Collection) -> tuple[tuple[int, int, str], ...]:
    return tuple(
        sorted(
            (obj.as_pointer(), obj.data.as_pointer(), obj.name)
            for obj in output.all_objects
        )
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

    def test_synthetic_success_samples_build(self) -> None:
        for number in range(2, 9):
            with self.subTest(sample=number):
                parameters = samples.create_synthetic_sample(number)
                output = TrackBuilder.build_track(*parameters)
                self.assert_valid_output(output, has_inner=number != 2)

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
