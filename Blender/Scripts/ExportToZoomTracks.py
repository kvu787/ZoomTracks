# This is meant to be run from a script panel from Blender opened on the .blend file you want to export.

import json
from pathlib import Path

import bpy


ZOOMTRACKS_ASSETS_PATH = Path(
    r"C:\Users\k\Repository\Unity\ZoomTracks\ZoomTracks\Assets"
)


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
        if obj.type != "MESH":
            raise RuntimeError(f"Outline object '{obj.name}' is not a mesh")
        if len(obj.data.vertices) < 3:
            raise RuntimeError(
                f"Outline mesh '{obj.name}' must contain at least three vertices"
            )

        vertices = []
        for vertex in obj.data.vertices:
            world_position = obj.matrix_world @ vertex.co
            vertices.append(
                {
                    "X": float(world_position.x),
                    "Y": float(world_position.y),
                }
            )
        outlines.append({"Vertices": vertices})

    collider_data = {"Outlines": outlines}
    filepath.parent.mkdir(parents=True, exist_ok=True)
    with filepath.open("w", encoding="utf-8", newline="\n") as output_file:
        json.dump(collider_data, output_file, indent=4, allow_nan=False)
        output_file.write("\n")

    print(f"Exported collider data: {filepath}")


def main() -> None:
    track_name = Path(bpy.data.filepath).stem
    filepath = ZOOMTRACKS_ASSETS_PATH / "FBX" / f"{track_name}.fbx"
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
