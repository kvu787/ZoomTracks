using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZoomTracks {
    // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AssetPostprocessor.OnPostprocessAllAssets.html
    // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.EditorSceneManager-playModeStartScene.html
    class SetPlayModeStartScene : AssetPostprocessor {
        private const string MainScenePath = "Assets/Scenes/ain.unity";

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload) {
            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath);
            if (scene == null) {
                Debug.LogError($"SetPlayModeStartScene: Did not find main scene at '{MainScenePath}'. Using default scene-loading behavior instead.");
                EditorSceneManager.playModeStartScene = null;
            } else {
                EditorSceneManager.playModeStartScene = scene;
            }
        }
    }
}
