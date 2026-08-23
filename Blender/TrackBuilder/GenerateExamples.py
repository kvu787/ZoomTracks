"""Regenerate TrackBuilder's committed adaptive-sampling example output."""

from __future__ import annotations

import os
import sys

import bpy


sys.dont_write_bytecode = True
SCRIPT_DIRECTORY = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIRECTORY not in sys.path:
    sys.path.insert(0, SCRIPT_DIRECTORY)

import TrackBuilder
import GenerateTrackBuilderSamples as samples


EXAMPLE_DIRECTORY = os.path.join(SCRIPT_DIRECTORY, "Examples")
INPUT_PATH = os.path.join(EXAMPLE_DIRECTORY, "ResolutionCurvatureIssue_Input.blend")
OUTPUT_PATH = os.path.join(EXAMPLE_DIRECTORY, "ResolutionCurvatureIssue_Output.blend")
PARAMETERS = (1.0, 0.1, 5.0, ["red", "white"])


def _remove_output() -> None:
    output = bpy.data.collections.get("Output")
    if output is None:
        return
    outlines_collection = samples._ensure_input_collection_structure()
    input_objects = set(outlines_collection.all_objects)
    output_objects = set(output.all_objects)
    if input_objects & output_objects:
        raise RuntimeError(
            "Example TrackBuilder/Input/Outlines and TrackBuilder/Output "
            "unexpectedly share objects"
        )
    TrackBuilder._remove_collection_tree(output)


def main() -> None:
    os.makedirs(EXAMPLE_DIRECTORY, exist_ok=True)
    if not os.path.isfile(INPUT_PATH):
        raise FileNotFoundError(f"Committed example input does not exist: {INPUT_PATH}")
    bpy.ops.wm.open_mainfile(filepath=INPUT_PATH)
    samples._ensure_input_collection_structure()
    _remove_output()
    scene = bpy.context.scene
    scene["track_builder_W"] = PARAMETERS[0]
    scene["track_builder_H"] = PARAMETERS[1]
    scene["track_builder_segment_length"] = PARAMETERS[2]
    scene["track_builder_material_names"] = '["red", "white"]'
    scene["track_builder_approach"] = "offset-aware adaptive curve sampling"
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=INPUT_PATH, check_existing=False)
    TrackBuilder.build_track(*PARAMETERS)
    bpy.ops.wm.save_as_mainfile(filepath=OUTPUT_PATH, check_existing=False)
    print(f"TRACK_BUILDER_EXAMPLE_OUTPUT={OUTPUT_PATH}")


if __name__ == "__main__":
    main()
