"""Generate standalone TrackBuilder sample-input .blend files.

This module owns test-fixture geometry only. It deliberately does not import or
run TrackBuilder. The original sample is copied as-is; synthetic samples contain
only their Input collection.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import sys

import bpy
from mathutils import Matrix


BuildParameters = tuple[float, float, float, list[str]]

SAMPLE_FILENAMES = {
    1: "TrackBuilderSampleInput01_Original.blend",
    2: "TrackBuilderSampleInput02_NoInner.blend",
    3: "TrackBuilderSampleInput03_OneInner.blend",
    4: "TrackBuilderSampleInput04_MultipleInner.blend",
    5: "TrackBuilderSampleInput05_Concave.blend",
    6: "TrackBuilderSampleInput06_Transformed.blend",
    7: "TrackBuilderSampleInput07_Curves.blend",
    8: "TrackBuilderSampleInput08_ReversedWinding.blend",
    9: "TrackBuilderSampleInput09_Collinear.blend",
    10: "TrackBuilderSampleInput10_SingleSegment.blend",
}

ORIGINAL_SAMPLE_PARAMETERS: BuildParameters = (0.3, 0.8, 2.5, ["red", "blue"])


def _ensure_material(name: str, color: tuple[float, float, float, float]) -> bpy.types.Material:
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.diffuse_color = color
    return material


def _rectangle(min_x: float, min_y: float, max_x: float, max_y: float) -> list[tuple[float, float]]:
    return [(min_x, min_y), (max_x, min_y), (max_x, max_y), (min_x, max_y)]


def _ellipse(
    center_x: float,
    center_y: float,
    radius_x: float,
    radius_y: float,
    count: int,
    phase: float = 0.0,
) -> list[tuple[float, float]]:
    return [
        (
            center_x + radius_x * math.cos(phase + math.tau * index / count),
            center_y + radius_y * math.sin(phase + math.tau * index / count),
        )
        for index in range(count)
    ]


def _create_outline_object(
    collection: bpy.types.Collection,
    name: str,
    points: list[tuple[float, float]],
    material: bpy.types.Material,
    *,
    use_curve: bool = False,
    reverse: bool = False,
    matrix: Matrix | None = None,
) -> bpy.types.Object:
    ordered = list(reversed(points)) if reverse else list(points)
    if use_curve:
        data = bpy.data.curves.new(f"{name}Curve", type="CURVE")
        data.dimensions = "2D"
        data.resolution_u = 12
        spline = data.splines.new(type="POLY")
        spline.points.add(len(ordered) - 1)
        for spline_point, (x, y) in zip(spline.points, ordered):
            spline_point.co = (x, y, 0.0, 1.0)
        spline.use_cyclic_u = True
    else:
        data = bpy.data.meshes.new(f"{name}Mesh")
        vertices = [(x, y, 0.0) for x, y in ordered]
        edges = [(index, (index + 1) % len(vertices)) for index in range(len(vertices))]
        data.from_pydata(vertices, edges, [])
        data.update()
    data.materials.append(material)
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    if matrix is not None:
        obj.matrix_world = matrix
    return obj


def _new_synthetic_input() -> tuple[bpy.types.Collection, dict[str, bpy.types.Material]]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    input_collection = bpy.data.collections.new("Input")
    bpy.context.scene.collection.children.link(input_collection)
    materials = {
        "ground": _ensure_material("GroundMaterial", (0.12, 0.45, 0.12, 1.0)),
        "track": _ensure_material("TrackMaterial", (0.12, 0.14, 0.18, 1.0)),
        "island_a": _ensure_material("IslandMaterialA", (0.22, 0.55, 0.18, 1.0)),
        "island_b": _ensure_material("IslandMaterialB", (0.18, 0.48, 0.28, 1.0)),
        "barrier_red": _ensure_material("BarrierRed", (0.8, 0.03, 0.03, 1.0)),
        "barrier_white": _ensure_material("BarrierWhite", (0.9, 0.9, 0.9, 1.0)),
    }
    return input_collection, materials


def create_synthetic_sample(number: int) -> BuildParameters:
    """Replace the current scene with synthetic sample ``number`` (2 through 10)."""

    collection, materials = _new_synthetic_input()
    identity = Matrix.Identity(4)

    if number == 2:
        loops = [
            ("GroundOutline", _rectangle(-12, -8, 12, 8), "ground", False, False, identity),
            ("OuterTrackOutline", _ellipse(0, 0, 9, 5, 32), "track", False, False, identity),
        ]
        parameters = (0.35, 0.8, 2.75)
    elif number == 3:
        loops = [
            ("GroundOutline", _rectangle(-13, -9, 13, 9), "ground", False, False, identity),
            ("OuterTrackOutline", _ellipse(0, 0, 10, 6, 40), "track", False, False, identity),
            ("InnerTrackOutline", _ellipse(0, 0, 2.5, 1.8, 20), "island_a", False, False, identity),
        ]
        parameters = (0.4, 1.0, 3.1)
    elif number == 4:
        loops = [
            ("GroundOutline", _rectangle(-15, -10, 15, 10), "ground", False, False, identity),
            ("OuterTrackOutline", _ellipse(0, 0, 12, 7, 48), "track", False, False, identity),
            ("InnerTrackLeft", _ellipse(-4.2, 0, 2.0, 1.8, 20), "island_a", False, False, identity),
            ("InnerTrackRight", _ellipse(4.2, 0, 1.8, 2.1, 24), "island_b", False, False, identity),
        ]
        parameters = (0.3, 0.9, 2.4)
    elif number == 5:
        concave = [
            (-10, -6),
            (10, -6),
            (10, 6),
            (4, 6),
            (4, 3),
            (2, 3),
            (2, 5),
            (-2, 5),
            (-2, 3),
            (-4, 3),
            (-4, 6),
            (-10, 6),
        ]
        loops = [
            ("GroundOutline", _rectangle(-13, -9, 13, 9), "ground", False, False, identity),
            ("OuterTrackConcave", concave, "track", False, False, identity),
            ("InnerTrackOutline", _ellipse(0, 0, 1.6, 1.4, 16), "island_a", False, False, identity),
        ]
        parameters = (0.55, 1.2, 2.2)
    elif number == 6:
        transform = (
            Matrix.Translation((8.0, -4.0, 0.0))
            @ Matrix.Rotation(math.radians(27.0), 4, "Z")
            @ Matrix.Diagonal((1.25, 0.8, 1.0, 1.0))
        )
        loops = [
            ("GroundOutline", _rectangle(-12, -8, 12, 8), "ground", False, False, transform),
            ("OuterTrackOutline", _ellipse(0, 0, 9, 5.5, 32), "track", False, False, transform),
            ("InnerTrackOutline", _ellipse(0, 0, 2.2, 1.7, 18), "island_a", False, False, transform),
        ]
        parameters = (0.32, 0.75, 2.5)
    elif number == 7:
        loops = [
            ("GroundCurve", _rectangle(-13, -9, 13, 9), "ground", True, False, identity),
            ("OuterTrackCurve", _ellipse(0, 0, 10, 6, 36), "track", True, False, identity),
            ("InnerTrackCurve", _ellipse(0, 0, 2.4, 1.7, 20), "island_a", True, False, identity),
        ]
        parameters = (0.28, 0.7, 2.0)
    elif number == 8:
        loops = [
            ("GroundOutline", _rectangle(-13, -9, 13, 9), "ground", False, True, identity),
            ("OuterTrackOutline", _ellipse(0, 0, 10, 6, 36), "track", False, False, identity),
            ("InnerTrackOutline", _ellipse(0, 0, 2.2, 1.6, 18), "island_a", False, True, identity),
        ]
        parameters = (0.36, 0.85, 2.6)
    elif number == 9:
        ground = [(-12, -8), (0, -8), (12, -8), (12, 0), (12, 8), (0, 8), (-12, 8), (-12, 0)]
        outer = [(-9, -5), (0, -5), (9, -5), (9, 0), (9, 5), (0, 5), (-9, 5), (-9, 0)]
        inner = [(-2, -1.5), (0, -1.5), (2, -1.5), (2, 1.5), (0, 1.5), (-2, 1.5)]
        loops = [
            ("GroundCollinear", ground, "ground", False, False, identity),
            ("OuterTrackCollinear", outer, "track", False, False, identity),
            ("InnerTrackCollinear", inner, "island_a", False, False, identity),
        ]
        parameters = (0.45, 1.1, 2.8)
    elif number == 10:
        dense_outer = [
            (
                (10.0 + 0.7 * math.sin(5.0 * angle)) * math.cos(angle),
                (6.0 + 0.35 * math.sin(5.0 * angle)) * math.sin(angle),
            )
            for angle in (math.tau * index / 96 for index in range(96))
        ]
        loops = [
            ("GroundDense", _ellipse(0, 0, 14, 10, 64), "ground", False, False, identity),
            ("OuterTrackDense", dense_outer, "track", False, False, identity),
            ("InnerTrackDense", _ellipse(0, 0, 2.1, 1.5, 32), "island_a", False, False, identity),
        ]
        parameters = (0.25, 0.65, 1000.0)
    else:
        raise ValueError(f"Unknown synthetic sample {number}; expected a number from 2 through 10")

    for name, points, material_key, use_curve, reverse, matrix in loops:
        _create_outline_object(
            collection,
            name,
            points,
            materials[material_key],
            use_curve=use_curve,
            reverse=reverse,
            matrix=matrix,
        )
    width, height, target = parameters
    return width, height, target, ["BarrierRed", "BarrierWhite"]


def load_sample_input(number: int, original_sample_path: str) -> BuildParameters:
    """Load or create sample ``number`` in the current Blender process."""

    if number == 1:
        sample_path = os.path.abspath(original_sample_path)
        if not os.path.isfile(sample_path):
            raise FileNotFoundError(f"Original sample input does not exist: {sample_path}")
        bpy.ops.wm.open_mainfile(filepath=sample_path)
        parameters = ORIGINAL_SAMPLE_PARAMETERS
    else:
        parameters = create_synthetic_sample(number)

    if number != 1 and bpy.data.collections.get("Output") is not None:
        raise ValueError(f"Sample input {number} unexpectedly contains an Output collection")
    return parameters


def _record_parameters(parameters: BuildParameters, expected_result: str) -> None:
    width, height, target, material_names = parameters
    scene = bpy.context.scene
    scene["track_builder_W"] = width
    scene["track_builder_H"] = height
    scene["track_builder_segment_length"] = target
    scene["track_builder_material_names"] = json.dumps(material_names)
    scene["track_builder_expected_result"] = expected_result


def generate_sample_inputs(output_directory: str, original_sample_path: str) -> list[str]:
    """Generate all sample-input .blend files and return their absolute paths."""

    output_directory = os.path.abspath(output_directory)
    os.makedirs(output_directory, exist_ok=True)
    written: list[str] = []

    for number, filename in SAMPLE_FILENAMES.items():
        parameters = load_sample_input(number, original_sample_path)
        expected_result = "single_segment_error" if number == 10 else "success"
        _record_parameters(parameters, expected_result)
        path = os.path.join(output_directory, filename)
        bpy.ops.wm.save_as_mainfile(filepath=path, check_existing=False)
        written.append(path)
    return written


def _script_arguments() -> list[str]:
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def _main() -> None:
    script_directory = os.path.dirname(os.path.abspath(__file__))
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output-dir",
        default=os.path.join(script_directory, "SampleInputs"),
    )
    parser.add_argument(
        "--original-sample",
        default=os.path.join(script_directory, "SampleInput.blend"),
    )
    arguments = parser.parse_args(_script_arguments())
    for path in generate_sample_inputs(arguments.output_dir, arguments.original_sample):
        print(f"TRACK_BUILDER_SAMPLE_INPUT={path}")


if __name__ == "__main__":
    _main()
