using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public static class SetupNewTrackSceneTool {
        private static IReadOnlyList<string> MeshColliderPrefixes { get; } = Array.AsReadOnly(new[] {
            "Road",
            "Grass",
            "Gravel",
        });

        private static IReadOnlyList<string> BoxColliderPrefixes { get; } = Array.AsReadOnly(new[] {
            "Barrier",
            "BigCone",
            "CheckeredLine",
            "Checkpoint",
            "Cone",
            "SlopeCar",
            "VehicleRoad",
        });

        [MenuItem(itemName: "Tools/Setup new track scene", isValidateFunction: false, priority: 3)]
        public static void SetupNewTrackScene() {
            GameObject[] allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (allObjects.Length == 0) {
                throw new Exception("No objects found in scene");
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
}
