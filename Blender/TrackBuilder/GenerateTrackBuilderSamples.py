"""Generate committed, input-only TrackBuilder test fixtures.

This module owns test-fixture geometry only. It deliberately does not import or
run TrackBuilder. Existing generated TrackBuilder/Output is stripped from the
original sample, and every fixture uses TrackBuilder/Input/Outlines.
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
    9: "TrackBuilderSampleInput09_SmallTurnAngle.blend",
    10: "TrackBuilderSampleInput10_SingleSegment.blend",
}

EXPECTED_RESULTS = {
    9: "minimum_turn_angle_error",
    10: "single_segment_error",
}

SAMPLE_MATERIAL_NAMES = {
    1: ("red", "blue"),
    2: ("BarrierRed", "BarrierWhite"),
    3: ("BarrierRed", "BarrierWhite", "BarrierBlue"),
    4: ("BarrierRed", "BarrierWhite", "BarrierBlue", "BarrierYellow"),
    5: ("BarrierRed", "BarrierWhite", "BarrierBlue", "BarrierYellow", "BarrierGreen"),
    6: (
        "BarrierRed",
        "BarrierWhite",
        "BarrierBlue",
        "BarrierYellow",
        "BarrierGreen",
        "BarrierBlack",
    ),
    7: ("BarrierRed", "BarrierWhite"),
    8: ("BarrierRed", "BarrierWhite"),
    9: ("BarrierRed", "BarrierWhite"),
    10: ("BarrierRed", "BarrierWhite"),
}

BARRIER_MATERIAL_COLORS = {
    "BarrierRed": (0.8, 0.03, 0.03, 1.0),
    "BarrierWhite": (0.9, 0.9, 0.9, 1.0),
    "BarrierBlue": (0.03, 0.18, 0.8, 1.0),
    "BarrierYellow": (0.95, 0.7, 0.03, 1.0),
    "BarrierGreen": (0.03, 0.65, 0.15, 1.0),
    "BarrierBlack": (0.03, 0.03, 0.03, 1.0),
}

ORIGINAL_SAMPLE_PARAMETERS: BuildParameters = (
    0.3,
    0.8,
    2.5,
    list(SAMPLE_MATERIAL_NAMES[1]),
)


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
        spline = data.splines.new(type="NURBS")
        spline.points.add(len(ordered) - 1)
        spline.order_u = min(4, len(ordered))
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


def _new_synthetic_input(
    number: int,
) -> tuple[bpy.types.Collection, dict[str, bpy.types.Material]]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    track_builder_collection = bpy.data.collections.new("TrackBuilder")
    input_collection = bpy.data.collections.new("Input")
    outlines_collection = bpy.data.collections.new("Outlines")
    bpy.context.scene.collection.children.link(track_builder_collection)
    track_builder_collection.children.link(input_collection)
    input_collection.children.link(outlines_collection)
    materials = {
        "ground": _ensure_material("GroundMaterial", (0.12, 0.45, 0.12, 1.0)),
        "track": _ensure_material("TrackMaterial", (0.12, 0.14, 0.18, 1.0)),
        "island_a": _ensure_material("IslandMaterialA", (0.22, 0.55, 0.18, 1.0)),
        "island_b": _ensure_material("IslandMaterialB", (0.18, 0.48, 0.28, 1.0)),
    }
    for material_name in SAMPLE_MATERIAL_NAMES[number]:
        material = _ensure_material(material_name, BARRIER_MATERIAL_COLORS[material_name])
        material.use_fake_user = True
    return outlines_collection, materials


def create_synthetic_sample(number: int) -> BuildParameters:
    """Replace the current scene with synthetic sample ``number`` (2 through 10)."""

    collection, materials = _new_synthetic_input(number)
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
        parameters = (0.55, 1.2, 1.5)
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
        parameters = (0.32, 0.75, 1.5)
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
        small_turn_rise = 9.0 * math.tan(math.radians(0.005))
        outer = [(-9, -5), (0, -5), (9, -5 + small_turn_rise), (9, 5), (-9, 5)]
        loops = [
            ("GroundOutline", _rectangle(-12, -8, 12, 8), "ground", False, False, identity),
            ("OuterTrackSmallTurn", outer, "track", False, False, identity),
            ("InnerTrackOutline", _rectangle(-2, -1.5, 2, 1.5), "island_a", False, False, identity),
        ]
        parameters = (0.45, 1.1, 2.8)
    elif number == 10:
        loops = [
            ("GroundOutline", _rectangle(-14, -10, 14, 10), "ground", False, False, identity),
            ("OuterTrackOutline", _rectangle(-10, -6, 10, 6), "track", False, False, identity),
            ("InnerTrackOutline", _rectangle(-2, -1.5, 2, 1.5), "island_a", False, False, identity),
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
    return width, height, target, list(SAMPLE_MATERIAL_NAMES[number])


def _remove_output_collection() -> None:
    output = bpy.data.collections.get("Output")
    if output is None:
        return

    input_collection = bpy.data.collections.get("Input")
    input_objects = set(input_collection.all_objects) if input_collection else set()
    output_objects = set(output.all_objects)
    shared_objects = input_objects & output_objects
    if shared_objects:
        names = ", ".join(sorted(obj.name for obj in shared_objects))
        raise ValueError(
            f"TrackBuilder/Input/Outlines and TrackBuilder/Output share objects: {names}"
        )

    output_collections: list[bpy.types.Collection] = []

    def collect(collection: bpy.types.Collection) -> None:
        output_collections.append(collection)
        for child in collection.children:
            collect(child)

    collect(output)
    output_data = {obj.data for obj in output_objects if obj.data is not None}
    for obj in output_objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    for data in output_data:
        if data.users == 0 and isinstance(data, bpy.types.Mesh):
            bpy.data.meshes.remove(data)
    for collection in reversed(output_collections):
        bpy.data.collections.remove(collection, do_unlink=True)


def _direct_child(
    parent: bpy.types.Collection,
    name: str,
) -> bpy.types.Collection | None:
    return next((child for child in parent.children if child.name == name), None)


def _unlink_collection_from_other_parents(
    collection: bpy.types.Collection,
    expected_parent: bpy.types.Collection,
) -> None:
    for scene in bpy.data.scenes:
        if collection in scene.collection.children[:]:
            scene.collection.children.unlink(collection)
    for parent in bpy.data.collections:
        if parent != expected_parent and collection in parent.children[:]:
            parent.children.unlink(collection)


def _ensure_input_collection_structure() -> bpy.types.Collection:
    """Return Outlines, migrating the legacy top-level Input fixture if needed."""

    track_builder = bpy.data.collections.get("TrackBuilder")
    if track_builder is None:
        legacy_input = bpy.data.collections.get("Input")
        if legacy_input is None:
            raise ValueError("Original sample has neither TrackBuilder nor legacy Input")
        if bpy.data.collections.get("Outlines") is not None:
            raise ValueError("Cannot migrate original sample because Outlines already exists")
        legacy_input.name = "Outlines"
        track_builder = bpy.data.collections.new("TrackBuilder")
        input_collection = bpy.data.collections.new("Input")
        bpy.context.scene.collection.children.link(track_builder)
        track_builder.children.link(input_collection)
        input_collection.children.link(legacy_input)
        _unlink_collection_from_other_parents(legacy_input, input_collection)

    input_collection = _direct_child(track_builder, "Input")
    if input_collection is None:
        raise ValueError("TrackBuilder is missing its direct Input child")
    outlines_collection = _direct_child(input_collection, "Outlines")
    if outlines_collection is None:
        raise ValueError("TrackBuilder/Input is missing its direct Outlines child")
    return outlines_collection


def _prune_obsolete_sample_inputs(output_directory: str) -> list[str]:
    expected_names = set(SAMPLE_FILENAMES.values())
    removed: list[str] = []
    for filename in os.listdir(output_directory):
        if not (
            filename.startswith("TrackBuilderSampleInput")
            and ".blend" in filename
            and filename not in expected_names
        ):
            continue
        path = os.path.join(output_directory, filename)
        os.remove(path)
        removed.append(path)
    return removed


def load_sample_input(number: int, original_sample_path: str) -> BuildParameters:
    """Load or create sample ``number`` in the current Blender process."""

    if number == 1:
        sample_path = os.path.abspath(original_sample_path)
        if not os.path.isfile(sample_path):
            raise FileNotFoundError(f"Original sample input does not exist: {sample_path}")
        bpy.ops.wm.open_mainfile(filepath=sample_path)
        parameters = ORIGINAL_SAMPLE_PARAMETERS
        _ensure_input_collection_structure()
        for material_name in parameters[3]:
            material = bpy.data.materials.get(material_name)
            if material is None:
                raise ValueError(
                    f"Original sample is missing barrier material {material_name!r}"
                )
            material.use_fake_user = True
        _remove_output_collection()
    else:
        parameters = create_synthetic_sample(number)

    _ensure_input_collection_structure()
    if bpy.data.collections.get("Output") is not None:
        raise ValueError(
            f"Sample input {number} unexpectedly contains TrackBuilder/Output"
        )
    return parameters


def expected_result(number: int) -> str:
    """Return the recorded result expected from sample ``number``."""

    return EXPECTED_RESULTS.get(number, "success")


def record_parameters(parameters: BuildParameters, result: str) -> None:
    """Record reproducible build parameters and the expected result in the scene."""

    barrier_width, barrier_height, target, material_names = parameters
    scene = bpy.context.scene
    scene["track_builder_barrier_width"] = barrier_width
    scene["track_builder_barrier_height"] = barrier_height
    scene["track_builder_segment_length"] = target
    scene["track_builder_material_names"] = json.dumps(material_names)
    scene["track_builder_expected_result"] = result


def generate_sample_inputs(output_directory: str, original_sample_path: str) -> list[str]:
    """Generate all sample-input .blend files and return their absolute paths."""

    output_directory = os.path.abspath(output_directory)
    os.makedirs(output_directory, exist_ok=True)
    for path in _prune_obsolete_sample_inputs(output_directory):
        print(f"TRACK_BUILDER_REMOVED_SAMPLE_INPUT={path}")
    written: list[str] = []

    for number, filename in SAMPLE_FILENAMES.items():
        parameters = load_sample_input(number, original_sample_path)
        record_parameters(parameters, expected_result(number))
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
    script_directory = os.path.dirname(os.path.abspath(__file__))
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output-dir",
        default=os.path.join(script_directory, "TestInputs"),
    )
    parser.add_argument(
        "--original-sample",
        default=os.path.join(
            script_directory,
            "TestInputs",
            SAMPLE_FILENAMES[1],
        ),
    )
    arguments = parser.parse_args(_script_arguments())
    for path in generate_sample_inputs(arguments.output_dir, arguments.original_sample):
        print(f"TRACK_BUILDER_SAMPLE_INPUT={path}")


if __name__ == "__main__":
    _main()
