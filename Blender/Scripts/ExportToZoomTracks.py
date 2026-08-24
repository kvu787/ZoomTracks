# This is meant to be run from a script panel from Blender opened on the .blend file you want to export.

from pathlib import Path

import bpy

def main() -> None:
    filepath = rf"C:\Users\k\Repository\Unity\ZoomTracks\ZoomTracks\Assets\FBX\{Path(bpy.data.filepath).stem}.fbx"
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


if __name__ == "__main__":
    main()
