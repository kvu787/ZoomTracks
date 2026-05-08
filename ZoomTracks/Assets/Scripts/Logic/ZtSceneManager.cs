using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    // Awaitable docs:
    // https://docs.unity3d.com/6000.3/Documentation/Manual/async-await-support.html
    public static class ZtSceneManager {
        private const string MainSceneName = "MainScene";

        private enum SceneState {
            Loading,
            Loaded,
            Unloading,
        }

        /*
         * Invariants:
         * - SceneStates contains at most one scene that is in Loading or Unloading state
         * - SceneStates contains a scene in Loading/Unloading state iff InProgressSceneAwaitable and InProgressSceneName match that scene
         */
        private static readonly Dictionary<string, SceneState> SceneStates = new();
        private static Awaitable InProgressSceneAwaitable = null;
        private static string InProgressSceneName = null;

        public static void Update() {
            //ValidateState();

            if (IsBusy() && InProgressSceneAwaitable.IsCompleted) {
                if (SceneStates[InProgressSceneName] == SceneState.Loading) {
                    Debug.Log($"Finished loading scene='{InProgressSceneName}'");
                    SceneStates[InProgressSceneName] = SceneState.Loaded;
                } else if (SceneStates[InProgressSceneName] == SceneState.Unloading) {
                    Debug.Log($"Finished unloading scene='{InProgressSceneName}'");
                    _ = SceneStates.Remove(InProgressSceneName);
                }

                InProgressSceneAwaitable.GetAwaiter().GetResult();
                InProgressSceneAwaitable = null;
                InProgressSceneName = null;
            }
        }

        public static void LoadScene(string sceneName) {
            if (sceneName == MainSceneName) {
                throw new Exception($"Should not load {MainSceneName}");
            }

            if (!IsBusy() && !SceneStates.ContainsKey(sceneName)) {
                SceneStates[sceneName] = SceneState.Loading;
                InProgressSceneAwaitable = LoadSceneAsync(sceneName);
                InProgressSceneName = sceneName;
            }
        }

        public static void UnloadScene(string sceneName) {
            if (sceneName == MainSceneName) {
                throw new Exception($"Should not load {MainSceneName}");
            }

            if (!IsBusy() && SceneStates.ContainsKey(sceneName) && SceneStates[sceneName] == SceneState.Loaded) {
                SceneStates[sceneName] = SceneState.Unloading;
                InProgressSceneAwaitable = UnloadSceneAsync(sceneName);
                InProgressSceneName = sceneName;
            }
        }

        public static bool IsBusy() {
            return InProgressSceneAwaitable != null;
        }

        private static async Awaitable LoadSceneAsync(string sceneName) {
            DateTime startTime;
            AsyncOperation operation;

            Debug.Log($"Loading scene '{sceneName}' ...");
            startTime = DateTime.Now;
            operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (!operation.isDone || ((DateTime.Now - startTime) < TimeSpan.FromSeconds(0.1))) {
                Debug.Log($"{Time.frameCount}");
                await Awaitable.NextFrameAsync();
            }
            await operation;
            Debug.Log($"... Finished loading scene '{sceneName}'");
        }

        private static async Awaitable UnloadSceneAsync(string sceneName) {
            DateTime startTime;
            AsyncOperation operation;

            Debug.Log($"Unloading scene '{sceneName}'...");
            startTime = DateTime.Now;
            operation = SceneManager.UnloadSceneAsync(sceneName);
            while (!operation.isDone || ((DateTime.Now - startTime) < TimeSpan.FromSeconds(0.05))) {
                Debug.Log($"{Time.frameCount}");
                await Awaitable.NextFrameAsync();
            }
            await operation;
            Debug.Log($"... Finished unloading scene '{sceneName}'");

            Debug.Log("Executing Resources.UnloadUnusedAssets()...");
            startTime = DateTime.Now;
            operation = Resources.UnloadUnusedAssets();
            while (!operation.isDone || ((DateTime.Now - startTime) < TimeSpan.FromSeconds(0.05))) {
                Debug.Log($"{Time.frameCount}");
                await Awaitable.NextFrameAsync();
            }
            await operation;
            Debug.Log("... Finished executing Resources.UnloadUnusedAssets()");
        }

        //private static void ValidateState() {
        //    Assert.IsFalse(LoadSceneAwaitable != null && UnloadSceneAwaitable != null);

        //    if (State == ZtSceneManagerState.Unloaded) {
        //        Assert.IsTrue(LoadSceneAwaitable == null && UnloadSceneAwaitable == null);
        //    } else if (State == ZtSceneManagerState.Loading) {
        //        Assert.IsTrue(LoadSceneAwaitable != null && UnloadSceneAwaitable == null);
        //    } else if (State == ZtSceneManagerState.Loaded) {
        //        Assert.IsTrue(LoadSceneAwaitable == null && UnloadSceneAwaitable == null);
        //    } else if (State == ZtSceneManagerState.Unloading) {
        //        Assert.IsTrue(LoadSceneAwaitable == null && UnloadSceneAwaitable != null);
        //    } else {
        //        throw new Exception("Unknown state");
        //    }
        //}
    }
}
