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

def main():
    assert len(bpy.data.scenes) == 1
    scene = bpy.data.scenes[0]
    assert len(scene.collection.children) == 1
    assert scene.collection.children[0].name == "Collection"
    collectionNames = [c.name for c in scene.collection.children[0].children]
    print(collectionNames)
    for collectionName in collectionNames:
        if collectionName not in AllowedCollectionNames:
            raise Exception(f"disallowed collection name: {collectionName}")

if __name__ == "__main__":
    main()
