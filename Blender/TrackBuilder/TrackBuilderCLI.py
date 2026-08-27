"""Build a track from Blender's command line using the TrackBuilder library."""

from __future__ import annotations

import argparse
import os
import sys

import bpy

sys.dont_write_bytecode = True
SCRIPT_DIRECTORY = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIRECTORY not in sys.path:
    sys.path.insert(0, SCRIPT_DIRECTORY)

from TrackBuilder import build_track


def _script_arguments() -> list[str]:
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def _main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build", action="store_true", help="Build the current file")
    parser.add_argument("--barrier-width", type=float)
    parser.add_argument("--barrier-height", type=float)
    parser.add_argument("--segment-length", type=float)
    parser.add_argument("--materials", nargs="+")
    parser.add_argument("--save", help="Save the built current file to this path")
    arguments = parser.parse_args(_script_arguments())

    if not arguments.build:
        parser.error("--build is required")

    missing = [
        name
        for name, value in (
            ("--barrier-width", arguments.barrier_width),
            ("--barrier-height", arguments.barrier_height),
            ("--segment-length", arguments.segment_length),
            ("--materials", arguments.materials),
        )
        if value is None
    ]
    if missing:
        parser.error(f"--build requires {', '.join(missing)}")
    build_track(
        barrier_width=arguments.barrier_width,
        barrier_height=arguments.barrier_height,
        segment_length=arguments.segment_length,
        material_names=arguments.materials,
    )
    if arguments.save:
        save_path = os.path.abspath(arguments.save)
        os.makedirs(os.path.dirname(save_path), exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=save_path, check_existing=False)


if __name__ == "__main__":
    _main()
