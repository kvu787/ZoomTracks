using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    // Awaitable docs:
    // https://docs.unity3d.com/6000.3/Documentation/Manual/async-await-support.html
    /// <summary>
    /// ZtSceneManager provides a simple and complete way to load/unload scenes by doing the following:
    ///   1. Propagates any exceptions, including from async operations.
    ///   2. Throws an exception if you try to do any invalid operations, such as:
    ///      - Load/unload any special scenes, such as MainScene or UiScene
    ///      - Load/unload a non-existent scene
    ///      - Load a scene that is already loaded
    ///      - Unload a scene that is not loaded
    ///      - Start a load/unload if there is an in-progress load/unload
    ///
    /// To use ZtSceneManager:
    ///   1. Call ZtSceneManager.UpdateBeforeAll and ZtSceneManager.UpdateAfterAll at the start and end of MainLoop.Update.
    ///   2. Call ZtSceneManager.Update anywhere in MainLoop.Update.
    /// </summary>
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

        public static bool WasOperationFinishedThisFrame { get; private set; } = false;

        public static void UpdateBeforeAll() {
            if (IsOperationRunning() && InProgressSceneAwaitable.IsCompleted) {
                WasOperationFinishedThisFrame = true;

                if (SceneStates[InProgressSceneName] == SceneState.Loading) {
                    //Debug.Log($"Post-process loaded scene='{InProgressSceneName}'");
                    SceneStates[InProgressSceneName] = SceneState.Loaded;
                } else if (SceneStates[InProgressSceneName] == SceneState.Unloading) {
                    //Debug.Log($"Post-process unloaded scene='{InProgressSceneName}'");
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

            if (!IsOperationRunning() && !SceneStates.ContainsKey(sceneName)) {
                SceneStates[sceneName] = SceneState.Loading;
                InProgressSceneAwaitable = LoadSceneAsync(sceneName);
                InProgressSceneName = sceneName;
            }
        }

        public static void UnloadScene(string sceneName) {
            if (sceneName == MainSceneName) {
                throw new Exception($"Should not load {MainSceneName}");
            }

            if (!IsOperationRunning() && SceneStates.ContainsKey(sceneName) && SceneStates[sceneName] == SceneState.Loaded) {
                SceneStates[sceneName] = SceneState.Unloading;
                InProgressSceneAwaitable = UnloadSceneAsync(sceneName);
                InProgressSceneName = sceneName;
            }
        }
        public static bool IsOperationRunning() {
            return InProgressSceneAwaitable != null;
        }


        public static void UpdateAfterAll() {
            WasOperationFinishedThisFrame = false;
        }

        private static async Awaitable LoadSceneAsync(string sceneName) {
            DateTime startTime;
            AsyncOperation operation;

            //Debug.Log($"Loading scene '{sceneName}'...");
            startTime = DateTime.Now;
            operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (!operation.isDone || ((DateTime.Now - startTime) < TimeSpan.FromSeconds(0.1))) {
                //Debug.Log($"{Time.frameCount}");
                await Awaitable.NextFrameAsync();
            }
            await operation;
            //Debug.Log($"...Finished loading scene '{sceneName}'");
        }

        private static async Awaitable UnloadSceneAsync(string sceneName) {
            DateTime startTime;
            AsyncOperation operation;

            //Debug.Log($"Unloading scene '{sceneName}'...");
            startTime = DateTime.Now;
            operation = SceneManager.UnloadSceneAsync(sceneName);
            while (!operation.isDone || ((DateTime.Now - startTime) < TimeSpan.FromSeconds(0.05))) {
                //Debug.Log($"{Time.frameCount}");
                await Awaitable.NextFrameAsync();
            }
            await operation;
            //Debug.Log($"...Finished unloading scene '{sceneName}'");

            //Debug.Log("Executing Resources.UnloadUnusedAssets()...");
            startTime = DateTime.Now;
            operation = Resources.UnloadUnusedAssets();
            while (!operation.isDone || ((DateTime.Now - startTime) < TimeSpan.FromSeconds(0.05))) {
                //Debug.Log($"{Time.frameCount}");
                await Awaitable.NextFrameAsync();
            }
            await operation;
            //Debug.Log("...Finished executing Resources.UnloadUnusedAssets()");
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
