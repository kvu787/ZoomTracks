"""Benchmark TrackBuilder inside Blender and report reproducible geometry metadata."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import statistics
import sys
import time


sys.dont_write_bytecode = True
SCRIPT_DIRECTORY = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIRECTORY not in sys.path:
    sys.path.insert(0, SCRIPT_DIRECTORY)

import bpy

import TrackBuilder


def _arguments() -> argparse.Namespace:
    arguments = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--blend", required=True, help="Input .blend file to open")
    parser.add_argument("--runs", type=int, default=9, help="Number of timed rebuilds")
    parser.add_argument("--warmup", type=int, default=0, help="Untimed rebuilds before measurement")
    parser.add_argument("--barrier-width", type=float, default=1.0, help="Barrier width")
    parser.add_argument("--barrier-height", type=float, default=0.1, help="Barrier height")
    parser.add_argument("--segment-length", type=float, default=5.0)
    parser.add_argument(
        "--materials",
        nargs="+",
        default=["BarrierRed", "BarrierWhite"],
        help="Barrier material names in cycle order",
    )
    parser.add_argument(
        "--expected-hash",
        help="Fail if the final output geometry hash differs from this value",
    )
    parsed = parser.parse_args(arguments)
    if parsed.runs < 1:
        parser.error("--runs must be at least 1")
    if parsed.warmup < 0:
        parser.error("--warmup must not be negative")
    return parsed


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


def _main() -> None:
    arguments = _arguments()
    blend_path = os.path.abspath(arguments.blend)
    if not os.path.isfile(blend_path):
        raise SystemExit(f"Benchmark input does not exist: {blend_path}")
    bpy.ops.wm.open_mainfile(filepath=blend_path)

    parameters = (
        arguments.barrier_width,
        arguments.barrier_height,
        arguments.segment_length,
        arguments.materials,
    )
    for _ in range(arguments.warmup):
        TrackBuilder.build_track(*parameters)

    durations: list[float] = []
    output = None
    for _ in range(arguments.runs):
        started = time.perf_counter()
        output = TrackBuilder.build_track(*parameters)
        durations.append(time.perf_counter() - started)

    assert output is not None
    geometry_hash = _output_geometry_hash(output)
    if arguments.expected_hash is not None and geometry_hash != arguments.expected_hash:
        raise SystemExit(
            "Output geometry hash mismatch: "
            f"expected {arguments.expected_hash}, got {geometry_hash}"
        )

    role_counts: dict[str, int] = {}
    for obj in output.all_objects:
        role = str(obj.get("track_builder_role", ""))
        role_counts[role] = role_counts.get(role, 0) + 1
    result = {
        "blend": blend_path,
        "blender": bpy.app.version_string,
        "parameters": {
            "barrier_width": arguments.barrier_width,
            "barrier_height": arguments.barrier_height,
            "segment_length": arguments.segment_length,
            "materials": arguments.materials,
        },
        "runs": arguments.runs,
        "warmup": arguments.warmup,
        "seconds": durations,
        "median_seconds": statistics.median(durations),
        "mean_seconds": statistics.fmean(durations),
        "minimum_seconds": min(durations),
        "maximum_seconds": max(durations),
        "population_standard_deviation_seconds": statistics.pstdev(durations),
        "geometry_hash": geometry_hash,
        "output": {
            "objects": len(output.all_objects),
            "vertices": sum(len(obj.data.vertices) for obj in output.all_objects),
            "faces": sum(len(obj.data.polygons) for obj in output.all_objects),
            "roles": role_counts,
        },
    }
    print("TRACK_BUILDER_BENCHMARK=" + json.dumps(result, sort_keys=True))


if __name__ == "__main__":
    _main()
