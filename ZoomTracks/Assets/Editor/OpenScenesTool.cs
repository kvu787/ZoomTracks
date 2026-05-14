using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OpenScenesTool {
    [MenuItem(itemName: "Tools/Open scenes for full game", isValidateFunction: false, priority: 1)]
    public static void OpenScenesForFullGame() {
        SceneSetup[] setup = new SceneSetup[] {
            new() { path = "Assets/Scenes/Main.unity", isLoaded = true, isActive = true },
            new() { path = "Assets/Scenes/Ui.unity", isLoaded = true, isActive = false },
            new() { path = "Assets/Scenes/Track1.unity", isLoaded = true, isActive = false },
        };
        EditorSceneManager.RestoreSceneManagerSetup(setup);
    }

    [MenuItem(itemName: "Tools/Open UI scene", isValidateFunction: false, priority: 2)]
    public static void OpenUiScene() {
        SceneSetup[] setup = new SceneSetup[] {
            new() { path = "Assets/Scenes/Ui.unity", isLoaded = true, isActive = true },
        };
        EditorSceneManager.RestoreSceneManagerSetup(setup);
    }

    [MenuItem(itemName: "Tools/Open initial track scene", isValidateFunction: false, priority: 3)]
    public static void OpenGameScene() {
        SceneSetup[] setup = new SceneSetup[] {
            new() { path = "Assets/Scenes/Track1.unity", isLoaded = true, isActive = true },
        };
        EditorSceneManager.RestoreSceneManagerSetup(setup);
    }

    private static IReadOnlyList<string> MeshColliderPrefixes { get; } = System.Array.AsReadOnly(new[] {
        "Road",
        "Grass",
        "Gravel",
    });

    private static IReadOnlyList<string> BoxColliderPrefixes { get; } = System.Array.AsReadOnly(new[] {
        "Barrier",
        "BigCone",
        "CheckeredLine",
        "Checkpoint",
        "Cone",
        "SlopeCar",
        "VehicleRoad",
    });

    [MenuItem(itemName: "Tools/Setup new track scene", isValidateFunction: false, priority: 4)]
    public static void SetupNewTrackScene() {
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (allObjects.Length == 0) {
            throw new System.Exception("No objects found in scene");
        }
        Scene scene = allObjects[0].scene;
        foreach (GameObject gameObject in allObjects.Where(obj => MeshColliderPrefixes.Any(prefix => obj.name.StartsWith(prefix)))) {
            _ = gameObject.AddComponent<MeshCollider>();
        }
        foreach (GameObject gameObject in allObjects.Where(obj => BoxColliderPrefixes.Any(prefix => obj.name.StartsWith(prefix)))) {
            _ = gameObject.AddComponent<BoxCollider>();
        }
        _ = EditorSceneManager.MarkSceneDirty(scene);
        _ = EditorSceneManager.SaveScene(scene);
    }
}
