"""Regenerate the committed input/output pair for this experimental approach."""

from __future__ import annotations

import os
import sys

import bpy


sys.dont_write_bytecode = True
SCRIPT_DIRECTORY = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIRECTORY not in sys.path:
    sys.path.insert(0, SCRIPT_DIRECTORY)

import TrackBuilder


EXAMPLE_DIRECTORY = os.path.join(SCRIPT_DIRECTORY, "Examples")
SOURCE_PATH = os.path.abspath(
    os.path.join(SCRIPT_DIRECTORY, "..", "..", "TrackBuilder", "ResolutionCurvatureIssue.blend")
)
INPUT_PATH = os.path.join(EXAMPLE_DIRECTORY, "ResolutionCurvatureIssue_Input.blend")
OUTPUT_PATH = os.path.join(EXAMPLE_DIRECTORY, "ResolutionCurvatureIssue_Output.blend")
PARAMETERS = (1.0, 0.1, 5.0, ["red", "white"])


def _remove_output() -> None:
    output = bpy.data.collections.get("Output")
    if output is None:
        return
    input_collection = bpy.data.collections.get("Input")
    input_objects = set(input_collection.all_objects) if input_collection else set()
    output_objects = set(output.all_objects)
    if input_objects & output_objects:
        raise RuntimeError("Example Input and Output unexpectedly share objects")
    meshes = [obj.data for obj in output_objects if isinstance(obj.data, bpy.types.Mesh)]
    for obj in output_objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.collections.remove(output, do_unlink=True)
    for mesh in meshes:
        if mesh.name in bpy.data.meshes and mesh.users == 0:
            bpy.data.meshes.remove(mesh)


def main() -> None:
    os.makedirs(EXAMPLE_DIRECTORY, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=SOURCE_PATH)
    _remove_output()
    scene = bpy.context.scene
    scene["track_builder_W"] = PARAMETERS[0]
    scene["track_builder_H"] = PARAMETERS[1]
    scene["track_builder_segment_length"] = PARAMETERS[2]
    scene["track_builder_material_names"] = '["red", "white"]'
    scene["track_builder_approach"] = "globally increased curve resolution"
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=INPUT_PATH, check_existing=False)
    TrackBuilder.build_track(*PARAMETERS)
    bpy.ops.wm.save_as_mainfile(filepath=OUTPUT_PATH, check_existing=False)
    print(f"TRACK_BUILDER_EXAMPLE_INPUT={INPUT_PATH}")
    print(f"TRACK_BUILDER_EXAMPLE_OUTPUT={OUTPUT_PATH}")


if __name__ == "__main__":
    main()
