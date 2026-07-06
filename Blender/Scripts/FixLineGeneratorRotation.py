import bpy

def main():
    obj = bpy.context.object

    if bpy.context.mode != "OBJECT":
        raise RuntimeError("Must be in Object Mode.")

    if obj is None or obj.type != "MESH":
        raise RuntimeError("Select a mesh object first.")

    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    # Object Mode > Object > Set Origin > Origin to Geometry
    bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY")

    # Object Mode > Object > Apply > Rotation
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)

if __name__ == "__main__":
    main()
