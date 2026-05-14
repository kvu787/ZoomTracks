import bpy
import bmesh
import math

obj = bpy.context.edit_object

if obj is None or obj.type != 'MESH':
    raise RuntimeError("You must be in Edit Mode on a mesh object.")

bm = bmesh.from_edit_mesh(obj.data)

selected_verts = [v for v in bm.verts if v.select]

if len(selected_verts) != 2:
    raise RuntimeError(f"Expected exactly 2 selected vertices, found {len(selected_verts)}.")

v1, v2 = selected_verts

# Convert from object-local coordinates to world-space coordinates.
p1 = obj.matrix_world @ v1.co
p2 = obj.matrix_world @ v2.co

dx = p2.x - p1.x
dy = p2.y - p1.y

if dx == 0 and dy == 0:
    raise RuntimeError("The selected vertices have the same world-space XY position, so the angle is undefined.")

# Signed angle from world +X, about world +Z.
angle_rad = math.atan2(dy, dx)
angle_deg = math.degrees(angle_rad)

print(f"World-space angle radians: {angle_rad}")
print(f"World-space angle degrees signed: {angle_deg}")
