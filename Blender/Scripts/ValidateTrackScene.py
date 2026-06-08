import bpy
from datetime import datetime
from pathlib import Path
from collections.abc import Iterable
import math

AllowedCollectionNames = [
    "Barriers",
    "BigCones",
    "Camera",
    "Checkpoints",
    "Cones",
    "Templates",
    "Track",
    "Uncategorized",
    "Vehicles",
]

ValidZHeights = [
    0,
    0.015625,
    0.03125,
    0.046875,
    0.0625,
    0.078125,
    0.09375,
]

def CheckVertices(objects: Iterable[bpy.types.Object]):
    objects.sort(key=lambda obj: obj.name)
    for obj in objects:
        for vertex in obj.data.vertices:
            if not (vertex.co.z == 0):
                print(f"Name: {obj.name}")
                print("Vertex ERROR")
                print(f"Vertex {vertex.index}: x={repr(vertex.co.x)}, y={repr(vertex.co.y)}, z={repr(vertex.co.z)}")
                print()

def FindLayerCollection(layerCollection: bpy.types.LayerCollection, collection: bpy.types.Collection) -> bpy.types.LayerCollection | None:
    if layerCollection.collection == collection:
        return layerCollection
    for childLayerCollection in layerCollection.children:
        found = FindLayerCollection(childLayerCollection, collection)
        if found is not None:
            return found
    return None

def PrintParentedUnparentedObjects():
    parentedObjects   = sorted([obj for obj in bpy.data.objects if obj.parent is not None], key=lambda obj: obj.name)
    unparentedObjects = sorted([obj for obj in bpy.data.objects if obj.parent is None],     key=lambda obj: obj.name)

    print()
    print("#### Parented objects:")
    for obj in parentedObjects:
        print(obj.name)
    print()

    print("#### Unparented objects:")
    for obj in unparentedObjects:
        print(obj.name)
    print()

def Main():
    print(f"{Path(__file__).name} started at {datetime.now()}")

    # Verify scene count
    assert len(bpy.data.scenes) == 1, "Blender file must have exactly one scene"
    scene = bpy.data.scenes[0]

    # Verify view layer
    assert len(scene.view_layers) == 1, "Scene must contain exactly one view layer"
    viewLayer = scene.view_layers[0]
    assert viewLayer.name == "ViewLayer"

    # Verify scene collection properties
    sceneCollection = bpy.data.scenes[0].collection
    assert len(sceneCollection.children) == 1, "Scene collection must contain exactly one collection"
    assert len(sceneCollection.objects) == 0, "Scene collection cannot contain objects"

    # Verify root collection properties
    rootCollection = sceneCollection.children[0]
    assert rootCollection.name == "Collection"
    assert len(rootCollection.children) == len(AllowedCollectionNames), f"Root collection must contain exactly {len(AllowedCollectionNames)} collection"
    assert len(rootCollection.objects) == 0, "Root collection cannot contain objects"

    # Check for unknown collection names
    collectionNames = [c.name for c in rootCollection.children]
    for collectionName in collectionNames:
        if collectionName not in AllowedCollectionNames:
            raise Exception(f"Disallowed collection name: {collectionName}")

    # Ensure collections are unhidden/hidden
    assert not FindLayerCollection(viewLayer.layer_collection, rootCollection).exclude, "Root collection is hidden"
    for collection in rootCollection.children:
        if collection.name in ("Camera", "Templates", "Uncategorized"):
            assert FindLayerCollection(viewLayer.layer_collection, collection).exclude, f"This collection should be hidden: {collection.name}"
        else:
            assert not FindLayerCollection(viewLayer.layer_collection, collection).exclude, f"This collection should not be hidden: {collection.name}"

    # Check for modifiers that should be applied
    for obj in bpy.data.objects:
        for modifier in obj.modifiers:
            if modifier.type == 'SUBSURF':
                # print(f"{modifier.name}, {modifier.type}, {type(modifier)}")
                pass
            elif modifier.type == 'NODES':
                # print(f"{modifier.name}, {modifier.type}, {type(modifier)}, {modifier.node_group.name}")
                pass
            else:
                print(f"Warning: Unapplied modifier of type '{modifier.type}' on object '{obj.name}'")

    # Check for non-uniform scales
    rel_tol = 1e-5
    abs_tol = 1e-4
    flag = False
    for obj in bpy.data.objects:
        isUniform = \
                math.isclose(obj.scale.x, obj.scale.y, rel_tol=rel_tol, abs_tol=abs_tol) \
            and math.isclose(obj.scale.y, obj.scale.z, rel_tol=rel_tol, abs_tol=abs_tol) \
            and math.isclose(obj.scale.x, obj.scale.z, rel_tol=rel_tol, abs_tol=abs_tol)
        if not isUniform:
            print(f"Non-uniform scale: {obj.name}, ({repr(obj.scale.x)}, {repr(obj.scale.y)}, {repr(obj.scale.z)})")
            flag = True
    assert not flag, "One or more non-uniform scale errors"

    # Check for non-zero pitch or twist
    unparentedObjects = sorted([obj for obj in bpy.data.objects if obj.parent is None], key=lambda obj: obj.name)
    for obj in unparentedObjects:
        if obj.name != "CameraPivot":
            assert obj.rotation_euler.x == 0 and obj.rotation_euler.y == 0, f"{obj.name}: Has a non-zero x (pitch) or y (twist) rotation"

    # Check for invalid Z-heights
    for obj in unparentedObjects:
        assert obj.location.z in ValidZHeights, f"{obj.name}: Has invalid Z-height"

    # Print out subd levels
    for obj in bpy.data.objects:
        for modifier in obj.modifiers:
            if modifier.type == 'SUBSURF':
                print(f"Subd modifier: {obj.name}, levels={modifier.levels}")

    print(f"{Path(__file__).name} finished at {datetime.now()}")
    print()

if __name__ == "__main__":
    Main()
