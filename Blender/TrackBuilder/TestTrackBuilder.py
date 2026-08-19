"""Blender integration tests for TrackBuilder.

The tests read committed, input-only .blend fixtures from TestInputs. Each run
writes inspectable .blend results and a complete text report to the gitignored
TestArtifacts directory.
"""

from __future__ import annotations

from datetime import datetime
import io
import json
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
TEST_ARTIFACT_RESULTS: list[tuple[str, str, str, str]] = []

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
        return _recorded_parameters()

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
