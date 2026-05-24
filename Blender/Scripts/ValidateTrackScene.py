import bpy

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

def Main():
    assert len(bpy.data.scenes) == 1

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

if __name__ == "__main__":
    Main()
