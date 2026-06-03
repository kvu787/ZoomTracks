import bpy
from datetime import datetime
from pathlib import Path
from collections.abc import Iterable

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

CheckOriginPrefixes = [
    "Grass",
    "Road",
    "Gravel",
    "Barrier",
    "Checkpoint",
    "CheckeredLine",
    "VehicleRoad",
    "Cone",
    "BigCone",
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

def CheckOriginPositionAndRotation(objects: Iterable[bpy.types.Object]):
    objects = sorted(objects, key=lambda obj: obj.name)
    for obj in objects:
        if obj.name != "CameraPivot":
            assert obj.rotation_euler.x == 0 and obj.rotation_euler.y == 0, f"{obj.name} has a non-zero x or y rotation"
    for obj in objects:
        assert obj.location.z in ValidZHeights, f"{obj.name} has invalid z height"

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

def Main():
    print(f"{Path(__file__).name} started at {datetime.now()}")

    assert len(bpy.data.scenes) == 1, "Blender file must have exactly one scene"
    scene = bpy.data.scenes[0]

    assert len(scene.view_layers) == 1, "Scene must contain exactly one view layer"
    viewLayer = scene.view_layers[0]
    assert viewLayer.name == "ViewLayer"

    sceneCollection = bpy.data.scenes[0].collection
    assert len(sceneCollection.children) == 1, "Scene collection must contain exactly one collection"
    assert len(sceneCollection.objects) == 0, "Scene collection cannot contain objects"

    rootCollection = sceneCollection.children[0]
    assert rootCollection.name == "Collection"
    assert len(rootCollection.children) == len(AllowedCollectionNames), f"Root collection must contain exactly {len(AllowedCollectionNames)} collection"
    assert len(rootCollection.objects) == 0, "Root collection cannot contain objects"

    collectionNames = [c.name for c in rootCollection.children]
    for collectionName in collectionNames:
        if collectionName not in AllowedCollectionNames:
            raise Exception(f"Disallowed collection name: {collectionName}")

    assert not FindLayerCollection(viewLayer.layer_collection, rootCollection).exclude, "Root collection is hidden"
    for collection in rootCollection.children:
        if collection.name == "Camera" or collection.name == "Templates":
            assert FindLayerCollection(viewLayer.layer_collection, collection).exclude, "Camera or Template collection is not hidden"
        else:
            assert not FindLayerCollection(viewLayer.layer_collection, collection).exclude, "This collection should not be hidden"

    CheckOriginPositionAndRotation(bpy.data.objects)

    # TODO: Warn on any unapplied modifiers that aren't subd or custom generators
    print(f"{Path(__file__).name} finished at {datetime.now()}")

if __name__ == "__main__":
    Main()
