# This is meant to be run from a script panel from Blender opened on the .blend file you want to export.

import json
import math
from pathlib import Path

import bpy


FORMAT_VERSION = 1
COORDINATE_SYSTEM = "BlenderWorldXY"


def _find_repository_root() -> Path:
    source_paths = []
    script_path = globals().get("__file__")
    if script_path:
        source_paths.append(Path(script_path).resolve())
    if bpy.data.filepath:
        source_paths.append(Path(bpy.data.filepath).resolve())

    for source_path in source_paths:
        for candidate in source_path.parents:
            if (
                (candidate / "Blender").is_dir()
                and (candidate / "ZoomTracks" / "Assets").is_dir()
            ):
                return candidate

    searched_paths = ", ".join(str(path) for path in source_paths)
    raise RuntimeError(
        "Could not locate the ZoomTracks repository root from the script or "
        f"loaded .blend paths. Searched from: {searched_paths or '<none>'}"
    )


REPOSITORY_ROOT = _find_repository_root()
ZOOMTRACKS_ASSETS_PATH = REPOSITORY_ROOT / "ZoomTracks" / "Assets"


def _direct_child_collection(
    parent: bpy.types.Collection,
    name: str,
) -> bpy.types.Collection:
    collection = parent.children.get(name)
    if collection is None:
        raise RuntimeError(
            f"Required collection '{parent.name}/{name}' does not exist"
        )
    return collection


def _validated_world_xy_vertices(obj: bpy.types.Object) -> list[dict[str, float]]:
    if obj.type != "MESH":
        raise RuntimeError(f"Outline object '{obj.name}' is not a mesh")

    mesh = obj.data
    vertex_count = len(mesh.vertices)
    if vertex_count < 3:
        raise RuntimeError(
            f"Outline mesh '{obj.name}' must contain at least three vertices"
        )
    if len(mesh.polygons) != 0:
        raise RuntimeError(
            f"Outline mesh '{obj.name}' must be an edge-only mesh with no faces"
        )

    expected_edges = {
        tuple(sorted((vertex_index, (vertex_index + 1) % vertex_count)))
        for vertex_index in range(vertex_count)
    }
    actual_edge_list = [
        tuple(sorted((int(edge.vertices[0]), int(edge.vertices[1]))))
        for edge in mesh.edges
    ]
    actual_edges = set(actual_edge_list)
    if len(actual_edge_list) != vertex_count or actual_edges != expected_edges:
        raise RuntimeError(
            f"Outline mesh '{obj.name}' must be one closed loop whose edges follow "
            "the mesh vertex order"
        )

    world_positions = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
    for vertex_index, position in enumerate(world_positions):
        if not all(math.isfinite(float(component)) for component in position):
            raise RuntimeError(
                f"Outline mesh '{obj.name}' vertex {vertex_index} has a non-finite "
                "world-space coordinate"
            )

    plane_z = float(world_positions[0].z)
    for vertex_index, position in enumerate(world_positions[1:], start=1):
        if float(position.z) != plane_z:
            raise RuntimeError(
                f"Outline mesh '{obj.name}' is not planar in Blender world XY; "
                f"vertex 0 has Z={plane_z!r} and vertex {vertex_index} has "
                f"Z={float(position.z)!r}"
            )

    vertices = []
    for vertex_index, position in enumerate(world_positions):
        next_position = world_positions[(vertex_index + 1) % vertex_count]
        x = float(position.x)
        y = float(position.y)
        next_x = float(next_position.x)
        next_y = float(next_position.y)
        if x == next_x and y == next_y:
            raise RuntimeError(
                f"Outline mesh '{obj.name}' edge {vertex_index} has zero length "
                "in Blender world XY"
            )
        vertices.append({"X": x, "Y": y})

    return vertices


def export_collider_data(filepath: Path) -> None:
    track_builder = bpy.data.collections.get("TrackBuilder")
    if track_builder is None:
        raise RuntimeError("Required collection 'TrackBuilder' does not exist")

    output = _direct_child_collection(track_builder, "Output")
    outline_meshes = _direct_child_collection(output, "OutlineMeshes")

    outer_outlines = []
    inner_outlines = []
    for obj in outline_meshes.objects:
        role = obj.get("track_builder_role")
        if role == "outer_outline":
            outer_outlines.append(obj)
        elif role == "inner_outline":
            inner_outlines.append(obj)
        else:
            raise RuntimeError(
                f"Object '{obj.name}' in '{outline_meshes.name}' has unexpected "
                f"track_builder_role {role!r}"
            )

    if len(outer_outlines) != 1:
        raise RuntimeError(
            f"Expected one outer outline in '{outline_meshes.name}', "
            f"found {len(outer_outlines)}"
        )

    outline_objects = outer_outlines + sorted(inner_outlines, key=lambda obj: obj.name)
    outlines = []
    for obj in outline_objects:
        outlines.append({"Vertices": _validated_world_xy_vertices(obj)})

    collider_data = {
        "FormatVersion": FORMAT_VERSION,
        "CoordinateSystem": COORDINATE_SYSTEM,
        "Outlines": outlines,
    }
    filepath.parent.mkdir(parents=True, exist_ok=True)
    with filepath.open("w", encoding="utf-8", newline="\n") as output_file:
        json.dump(collider_data, output_file, indent=4, allow_nan=False)
        output_file.write("\n")

    print(f"Exported collider data: {filepath}")


def main() -> None:
    track_name = Path(bpy.data.filepath).stem
    filepath = ZOOMTRACKS_ASSETS_PATH / "FBX" / f"{track_name}.fbx"
    filepath.parent.mkdir(parents=True, exist_ok=True)
    result = bpy.ops.export_scene.fbx(
        filepath=str(filepath),
        check_existing=False,
        path_mode="AUTO",
        batch_mode="OFF",

        # Include
        use_selection=False,
        use_visible=False,
        use_active_collection=False,
        object_types={"EMPTY", "CAMERA", "LIGHT", "ARMATURE", "MESH", "OTHER"},
        use_custom_props=False,

        # Transform
        global_scale=1.0,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Y",
        axis_up="Z",
        apply_unit_scale=True,
        use_space_transform=False,
        bake_space_transform=False,

        # Geometry
        mesh_smooth_type="OFF",  # Normals Only
        use_subsurf=False,
        use_mesh_modifiers=True,
        use_mesh_modifiers_render=True,
        use_mesh_edges=False,
        use_triangles=True,
        use_tspace=True,
        colors_type="SRGB",
        prioritize_active_color=False,

        # Armature
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        armature_nodetype="NULL",
        use_armature_deform_only=False,
        add_leaf_bones=False,

        # Animation
        bake_anim=False,
    )

    if result != {"FINISHED"}:
        raise RuntimeError(f"FBX export did not finish: {result}")

    print(f"Exported FBX: {filepath}")

    collider_filepath = (
        ZOOMTRACKS_ASSETS_PATH
        / "StreamingAssets"
        / f"{track_name}_ColliderData.json"
    )
    export_collider_data(collider_filepath)


if __name__ == "__main__":
    main()
