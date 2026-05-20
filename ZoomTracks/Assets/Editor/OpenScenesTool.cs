using UnityEditor;
using UnityEditor.SceneManagement;

namespace ZoomTracks {
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
    }
}
