import importlib.util
import json
import math
from pathlib import Path

import bpy


EXPECTED_BLENDER_VERSION = (4, 5, 12)
EXPECTED_OUTLINE_VERTEX_COUNTS = (1024, 448, 512)
EXPECTED_CAR_NAMES = (
    "SlopeCarGreen",
    "SlopeCarBlue",
    "SlopeCarRed",
    "SlopeCarYellow",
    "SlopeCarCyan",
    "SlopeCarMagenta",
    "SlopeCarPlaceholder",
)
EXPECTED_CAR_FOOTPRINT = (-1.5, -3.157522, 1.5, 3.0)
CAR_VALUE_TOLERANCE = 0.00001

SCRIPT_PATH = Path(__file__).resolve()
REPOSITORY_ROOT = SCRIPT_PATH.parents[2]
EXPECTED_BLEND_PATH = REPOSITORY_ROOT / "Blender" / "Tracks2" / "Track001.blend"
EXPORTER_PATH = REPOSITORY_ROOT / "Blender" / "Scripts" / "ExportToZoomTracks.py"
COMMITTED_JSON_PATH = (
    REPOSITORY_ROOT
    / "ZoomTracks"
    / "Assets"
    / "StreamingAssets"
    / "Track001_ColliderData.json"
)
ARTIFACT_DIRECTORY = SCRIPT_PATH.parent / "TestArtifacts"
GENERATED_ASSETS_DIRECTORY = (
    ARTIFACT_DIRECTORY / "FullExport" / "ZoomTracks" / "Assets"
)
GENERATED_FBX_PATH = GENERATED_ASSETS_DIRECTORY / "FBX" / "Track001.fbx"
GENERATED_JSON_PATH = (
    GENERATED_ASSETS_DIRECTORY
    / "StreamingAssets"
    / "Track001_ColliderData.json"
)


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def _load_exporter():
    spec = importlib.util.spec_from_file_location(
        "zoomtracks_export_to_zoomtracks",
        EXPORTER_PATH,
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load collider exporter from '{EXPORTER_PATH}'")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def _outline_objects(exporter) -> list[bpy.types.Object]:
    track_builder = bpy.data.collections.get("TrackBuilder")
    _require(track_builder is not None, "TrackBuilder collection is missing")
    output = exporter._direct_child_collection(track_builder, "Output")
    outline_meshes = exporter._direct_child_collection(output, "OutlineMeshes")

    outer = [
        obj
        for obj in outline_meshes.objects
        if obj.get("track_builder_role") == "outer_outline"
    ]
    inner = sorted(
        (
            obj
            for obj in outline_meshes.objects
            if obj.get("track_builder_role") == "inner_outline"
        ),
        key=lambda obj: obj.name,
    )
    _require(len(outer) == 1, f"Expected one outer outline, found {len(outer)}")
    return outer + inner


def _validate_track001_outlines(exporter) -> None:
    outline_objects = _outline_objects(exporter)
    actual_counts = tuple(len(obj.data.vertices) for obj in outline_objects)
    _require(
        actual_counts == EXPECTED_OUTLINE_VERTEX_COUNTS,
        "Unexpected Track001 outline vertex counts: "
        f"expected {EXPECTED_OUTLINE_VERTEX_COUNTS}, found {actual_counts}",
    )
    _require(
        sum(actual_counts) == 1984,
        f"Expected 1984 Track001 outline edges, found {sum(actual_counts)}",
    )

    for obj in outline_objects:
        mesh = obj.data
        vertex_count = len(mesh.vertices)
        expected_edges = {
            tuple(sorted((index, (index + 1) % vertex_count)))
            for index in range(vertex_count)
        }
        actual_edges = {
            tuple(sorted((int(edge.vertices[0]), int(edge.vertices[1]))))
            for edge in mesh.edges
        }
        _require(
            len(mesh.edges) == vertex_count and actual_edges == expected_edges,
            f"Outline '{obj.name}' is not one ordered closed edge loop",
        )
        _require(
            len(mesh.polygons) == 0,
            f"Outline '{obj.name}' unexpectedly contains faces",
        )
        world_positions = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
        _require(
            all(float(position.z) == 0.0 for position in world_positions),
            f"Track001 outline '{obj.name}' is not on Blender world Z=0",
        )


def _local_mesh_bounds(obj: bpy.types.Object) -> tuple[float, float, float, float]:
    _require(obj.type == "MESH", f"Car object '{obj.name}' is not a mesh")
    _require(len(obj.data.vertices) > 0, f"Car mesh '{obj.name}' has no vertices")
    coordinates = [vertex.co for vertex in obj.data.vertices]
    return (
        min(float(coordinate.x) for coordinate in coordinates),
        min(float(coordinate.y) for coordinate in coordinates),
        max(float(coordinate.x) for coordinate in coordinates),
        max(float(coordinate.y) for coordinate in coordinates),
    )


def _validate_car_footprints_and_scales() -> None:
    footprints = []
    for name in EXPECTED_CAR_NAMES:
        obj = bpy.data.objects.get(name)
        _require(obj is not None, f"Required car object '{name}' is missing")
        footprint = _local_mesh_bounds(obj)
        footprints.append(footprint)

        _require(
            all(math.isfinite(value) for value in footprint),
            f"Car '{name}' has a non-finite local footprint",
        )
        _require(
            footprint[0] < footprint[2] and footprint[1] < footprint[3],
            f"Car '{name}' has a degenerate local footprint {footprint}",
        )
        _require(
            all(
                abs(float(scale_component) - 1.0) < CAR_VALUE_TOLERANCE
                for scale_component in obj.scale
            ),
            f"Car '{name}' does not have approximately unit object scale: "
            f"{tuple(float(value) for value in obj.scale)}",
        )
        _require(
            all(
                abs(float(scale_component) - 1.0) < CAR_VALUE_TOLERANCE
                for scale_component in obj.matrix_world.to_scale()
            ),
            f"Car '{name}' does not have approximately unit world scale: "
            f"{tuple(float(value) for value in obj.matrix_world.to_scale())}",
        )

    first_footprint = footprints[0]
    _require(
        all(footprint == first_footprint for footprint in footprints[1:]),
        f"Track001 car meshes do not share one local footprint: {footprints}",
    )
    _require(
        all(
            abs(actual - expected) < CAR_VALUE_TOLERANCE
            for actual, expected in zip(first_footprint, EXPECTED_CAR_FOOTPRINT)
        ),
        "Unexpected Track001 car footprint: "
        f"expected approximately {EXPECTED_CAR_FOOTPRINT}, found {first_footprint}",
    )


def _load_json(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as input_file:
        value = json.load(input_file)
    _require(isinstance(value, dict), f"JSON root in '{path}' is not an object")
    return value


def main() -> None:
    _require(
        bpy.app.version == EXPECTED_BLENDER_VERSION,
        "This validation must run with Blender "
        f"{'.'.join(str(value) for value in EXPECTED_BLENDER_VERSION)}; "
        f"found {bpy.app.version_string}",
    )
    loaded_blend_path = Path(bpy.data.filepath).resolve()
    _require(
        loaded_blend_path == EXPECTED_BLEND_PATH.resolve(),
        f"Expected loaded blend '{EXPECTED_BLEND_PATH}', found '{loaded_blend_path}'",
    )

    exporter = _load_exporter()
    _require(
        exporter.REPOSITORY_ROOT == REPOSITORY_ROOT,
        f"Exporter resolved unexpected repository root '{exporter.REPOSITORY_ROOT}'",
    )

    _validate_track001_outlines(exporter)
    _validate_car_footprints_and_scales()

    exporter.ZOOMTRACKS_ASSETS_PATH = GENERATED_ASSETS_DIRECTORY
    exporter.main()
    _require(
        GENERATED_FBX_PATH.is_file() and GENERATED_FBX_PATH.stat().st_size > 0,
        f"Full production export did not create '{GENERATED_FBX_PATH}'",
    )

    generated = _load_json(GENERATED_JSON_PATH)
    committed = _load_json(COMMITTED_JSON_PATH)
    _require(
        generated == committed,
        "Generated Track001 collider JSON differs semantically from the committed "
        f"file. Generated: '{GENERATED_JSON_PATH}'. Committed: "
        f"'{COMMITTED_JSON_PATH}'. Regenerate the committed collider JSON with the "
        "production exporter if the source change was deliberate.",
    )
    _require(
        generated.get("FormatVersion") == 1,
        f"Unexpected collider FormatVersion: {generated.get('FormatVersion')!r}",
    )
    _require(
        generated.get("CoordinateSystem") == "BlenderWorldXY",
        "Unexpected collider CoordinateSystem: "
        f"{generated.get('CoordinateSystem')!r}",
    )
    generated_counts = tuple(
        len(outline["Vertices"]) for outline in generated["Outlines"]
    )
    _require(
        generated_counts == EXPECTED_OUTLINE_VERTEX_COUNTS,
        f"Generated JSON has unexpected outline counts {generated_counts}",
    )

    print(
        "PASS: Track001 production collider export matches the committed JSON; "
        "validated 3 loops, 1984 edges, and 7 car footprints/scales."
    )


if __name__ == "__main__":
    main()
