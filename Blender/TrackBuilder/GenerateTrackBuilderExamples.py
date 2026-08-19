"""Regenerate and synchronize the tracked TrackBuilder example .blend files.

Successful examples contain the Output produced by the current TrackBuilder.
Expected-failure examples contain Input only. Files matching the TrackBuilder
example naming pattern but absent from the manifest are removed.
"""

from __future__ import annotations

import argparse
import os
import sys


sys.dont_write_bytecode = True
SCRIPT_DIRECTORY = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIRECTORY not in sys.path:
    sys.path.insert(0, SCRIPT_DIRECTORY)

import bpy

import GenerateTrackBuilderSamples as samples
import TrackBuilder


EXAMPLE_FILENAMES = {
    1: "TrackBuilderExample01_SampleInput.blend",
    2: "TrackBuilderExample02_NoInner.blend",
    3: "TrackBuilderExample03_OneInner.blend",
    4: "TrackBuilderExample04_MultipleInner.blend",
    5: "TrackBuilderExample05_Concave.blend",
    6: "TrackBuilderExample06_Transformed.blend",
    7: "TrackBuilderExample07_Curves.blend",
    8: "TrackBuilderExample08_ReversedWinding.blend",
    9: "TrackBuilderExample09_SmallTurnAngle.blend",
    10: "TrackBuilderExample10_SingleSegment.blend",
}

EXPECTED_EXCEPTIONS = {
    "minimum_turn_angle_error": TrackBuilder.TrackBuilderValidationError,
    "single_segment_error": TrackBuilder.TrackBuilderGeometryError,
}


def _build_or_verify_expected_failure(
    parameters: samples.BuildParameters,
    result: str,
) -> None:
    if result == "success":
        TrackBuilder.build_track(*parameters)
        return

    exception_type = EXPECTED_EXCEPTIONS[result]
    try:
        TrackBuilder.build_track(*parameters)
    except exception_type:
        pass
    except TrackBuilder.TrackBuilderError as error:
        raise RuntimeError(
            f"Expected {exception_type.__name__}, got {type(error).__name__}"
        ) from error
    else:
        raise RuntimeError(f"Expected {exception_type.__name__}, but the build succeeded")

    if bpy.data.collections.get("Output") is not None:
        raise RuntimeError("An expected-failure example unexpectedly contains Output")


def _prune_obsolete_examples(output_directory: str) -> list[str]:
    expected_names = set(EXAMPLE_FILENAMES.values())
    removed: list[str] = []
    for filename in os.listdir(output_directory):
        if not (
            filename.startswith("TrackBuilderExample")
            and ".blend" in filename
            and filename not in expected_names
        ):
            continue
        path = os.path.join(output_directory, filename)
        os.remove(path)
        removed.append(path)
    return removed


def generate_examples(output_directory: str, original_sample_path: str) -> list[str]:
    """Replace the tracked example set and return the written absolute paths."""

    output_directory = os.path.abspath(output_directory)
    original_sample_path = os.path.abspath(original_sample_path)
    os.makedirs(output_directory, exist_ok=True)

    for path in _prune_obsolete_examples(output_directory):
        print(f"TRACK_BUILDER_REMOVED_EXAMPLE={path}")

    written: list[str] = []
    for number, filename in EXAMPLE_FILENAMES.items():
        parameters = samples.load_sample_input(number, original_sample_path)
        result = samples.expected_result(number)
        _build_or_verify_expected_failure(parameters, result)
        samples.record_parameters(parameters, result)
        path = os.path.join(output_directory, filename)
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=path, check_existing=False)
        written.append(path)
    return written


def _script_arguments() -> list[str]:
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def _main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output-dir",
        default=os.path.join(SCRIPT_DIRECTORY, "Examples"),
    )
    parser.add_argument(
        "--original-sample",
        default=os.path.join(SCRIPT_DIRECTORY, "SampleInput.blend"),
    )
    arguments = parser.parse_args(_script_arguments())
    for path in generate_examples(arguments.output_dir, arguments.original_sample):
        print(f"TRACK_BUILDER_EXAMPLE={path}")


if __name__ == "__main__":
    _main()
