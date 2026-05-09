// Assets/Editor/SetPlayModeScene.cs
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ZoomTracks {
    public class SetPlayModeStartScene : AssetPostprocessor {
        private const string MainScenePath = "Assets/Scenes/Main.unity";

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload) {
            if (didDomainReload) {
                SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath);

                if (scene == null) {
                    EditorSceneManager.playModeStartScene = null;
                    UnityEngine.Debug.LogWarning($"SetPlayModeStartScene: Main scene not found at: {MainScenePath}");
                    return;
                }

                EditorSceneManager.playModeStartScene = scene;
            }
        }
    }
}
