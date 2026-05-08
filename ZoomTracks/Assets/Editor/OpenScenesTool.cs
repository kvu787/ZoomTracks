using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class OpenScenesTool {
    [MenuItem(itemName: "Tools/Open scenes for full game", isValidateFunction: false, priority: 1)]
    public static void OpenScenesForFullGame() {
        SceneSetup[] setup = new SceneSetup[] {
            new() { path = "Assets/Scenes/MainScene.unity", isLoaded = true, isActive = true },
            new() { path = "Assets/Scenes/UiScene.unity", isLoaded = true, isActive = false },
            new() { path = "Assets/Scenes/Track1Scene.unity", isLoaded = true, isActive = false },
        };
        EditorSceneManager.RestoreSceneManagerSetup(setup);
    }

    [MenuItem(itemName: "Tools/Open UI scene", isValidateFunction: false, priority: 2)]
    public static void OpenUiScene() {
        SceneSetup[] setup = new SceneSetup[] {
            new() { path = "Assets/Scenes/UiScene.unity", isLoaded = true, isActive = true },
        };
        EditorSceneManager.RestoreSceneManagerSetup(setup);
    }

    [MenuItem(itemName: "Tools/Open initial track scene", isValidateFunction: false, priority: 3)]
    public static void OpenGameScene() {
        SceneSetup[] setup = new SceneSetup[] {
            new() { path = "Assets/Scenes/Track1Scene.unity", isLoaded = true, isActive = true },
        };
        EditorSceneManager.RestoreSceneManagerSetup(setup);
    }

    private static readonly string[] MeshColliderPrefixes = {
        "Road",
        "Grass",
        "Gravel",
    };

    private static readonly string[] MeshColliderConvexPrefixes = {
        "Barrier",
        "Cone",
        "Checkpoint",
        "CheckeredLine",
        "SlopeCar",
    };

    [MenuItem(itemName: "Tools/Setup new scene", isValidateFunction: false, priority: 4)]
    public static void SetupSceneFromFbx() {
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject gameObject in allObjects.Where(obj => MeshColliderPrefixes.Any(prefix => obj.name.StartsWith(prefix)))) {
            MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
        }
        foreach (GameObject gameObject in allObjects.Where(obj => MeshColliderConvexPrefixes.Any(prefix => obj.name.StartsWith(prefix)))) {
            MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
            meshCollider.convex = true;
        }
    }
}
