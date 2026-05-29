import bpy
from datetime import datetime
from pathlib import Path

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

def CheckOriginPositionAndRotation():
    objects = [obj for obj in bpy.context.selected_objects if obj.type == "MESH"]
    objects.sort(key=lambda obj: obj.name)
    if len(objects) > 0:
        for obj in objects:
            if obj.location.z not in ValidZHeights:
                print(f"Name: {obj.name}")
                print("Origin ERROR")
                print(f"Origin: z={repr(obj.location.z)}")
                print()
            for vertex in obj.data.vertices:
                if not (vertex.co.z == 0):
                    print(f"Name: {obj.name}")
                    print("Vertex ERROR")
                    print(f"Vertex {vertex.index}: x={repr(vertex.co.x)}, y={repr(vertex.co.y)}, z={repr(vertex.co.z)}")
                    print()
        print(f"{len(objects)} objects checked")
    else:
        print("No mesh objects found in selection")

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

    assert not FindLayerCollection(viewLayer.layer_collection, rootCollection).exclude
    for collection in rootCollection.children:
        if collection.name == "Camera" or collection.name == "Templates":
            assert FindLayerCollection(viewLayer.layer_collection, collection).exclude
        else:
            assert not FindLayerCollection(viewLayer.layer_collection, collection).exclude

    # TODO: Warn on any unapplied modifiers that aren't subd or custom generators
    print(f"{Path(__file__).name} finished at {datetime.now()}")

if __name__ == "__main__":
    Main()
