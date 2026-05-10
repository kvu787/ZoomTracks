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
    public class ZtSceneManager {
        private enum SceneStateEnum {
            Loading,
            Loaded,
            Unloading,
        }
        public bool WasOperationFinishedThisFrame { get; private set; }

        /// <summary>
        /// Invariants:
        /// - SceneStates contains at most one scene that is in Loading or Unloading state
        /// - If-and-only-if SceneStates contains a scene in Loading/Unloading state, then InProgressSceneAwaitable and InProgressSceneName match that scene
        /// - If IsOperationRunning() is true, then WasOperationFinishedThisFrame is false
        /// </summary>
        private readonly Dictionary<string, SceneStateEnum> SceneStates;
        private Awaitable InProgressSceneAwaitable;
        private string InProgressSceneName;
        private readonly bool Log;

        public ZtSceneManager(bool log) {
            this.SceneStates = new();
            this.InProgressSceneAwaitable = null;
            this.InProgressSceneName = null;
            this.WasOperationFinishedThisFrame = false;
            this.Log = log;
        }

        public void UpdateBeforeAll() {
            if (this.IsOperationRunning() && this.InProgressSceneAwaitable.IsCompleted) {
                this.WasOperationFinishedThisFrame = true;

                if (this.SceneStates[this.InProgressSceneName] == SceneStateEnum.Loading) {
                    if (this.Log) { Debug.Log($"Post-process loaded scene='{this.InProgressSceneName}'"); }
                    this.SceneStates[this.InProgressSceneName] = SceneStateEnum.Loaded;
                } else if (this.SceneStates[this.InProgressSceneName] == SceneStateEnum.Unloading) {
                    if (this.Log) { Debug.Log($"Post-process unloaded scene='{this.InProgressSceneName}'"); }
                    _ = this.SceneStates.Remove(this.InProgressSceneName);
                }
                this.InProgressSceneAwaitable.GetAwaiter().GetResult();
                this.InProgressSceneAwaitable = null;
                this.InProgressSceneName = null;
            }
        }

        public void LoadScene(string sceneName) {
            if (sceneName == Constants.MainSceneName) {
                throw new Exception($"Should not load {Constants.MainSceneName}");
            }

            if (!this.IsOperationRunning() && !this.SceneStates.ContainsKey(sceneName)) {
                this.SceneStates[sceneName] = SceneStateEnum.Loading;
                this.InProgressSceneAwaitable = this.LoadSceneAsync(sceneName);
                this.InProgressSceneName = sceneName;
            }
        }

        public void UnloadScene(string sceneName) {
            if (sceneName == Constants.MainSceneName) {
                throw new Exception($"Should not load {Constants.MainSceneName}");
            }

            if (!this.IsOperationRunning() && this.SceneStates.ContainsKey(sceneName) && this.SceneStates[sceneName] == SceneStateEnum.Loaded) {
                this.SceneStates[sceneName] = SceneStateEnum.Unloading;
                this.InProgressSceneAwaitable = this.UnloadSceneAsync(sceneName);
                this.InProgressSceneName = sceneName;
            }
        }

        public bool IsOperationRunning() {
            return this.InProgressSceneAwaitable != null;
        }

        public void UpdateAfterAll() {
            this.WasOperationFinishedThisFrame = false;
        }

        private async Awaitable LoadSceneAsync(string sceneName) {
            DateTime startTime;
            AsyncOperation operation;

            if (this.Log) { Debug.Log($"Loading scene '{sceneName}'..."); }
            startTime = DateTime.Now;
            operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (!operation.isDone || ((DateTime.Now - startTime) < TimeSpan.FromSeconds(0.1))) {
                if (this.Log) { Debug.Log($"{Time.frameCount}"); }
                await Awaitable.NextFrameAsync();
            }
            await operation;
            if (this.Log) { Debug.Log($"...Finished loading scene '{sceneName}'"); }
        }

        private async Awaitable UnloadSceneAsync(string sceneName) {
            DateTime startTime;
            AsyncOperation operation;

            if (this.Log) { Debug.Log($"Unloading scene '{sceneName}'..."); }
            startTime = DateTime.Now;
            operation = SceneManager.UnloadSceneAsync(sceneName);
            while (!operation.isDone || ((DateTime.Now - startTime) < TimeSpan.FromSeconds(0.05))) {
                if (this.Log) { Debug.Log($"{Time.frameCount}"); }
                await Awaitable.NextFrameAsync();
            }
            await operation;
            if (this.Log) { Debug.Log($"...Finished unloading scene '{sceneName}'"); }

            if (this.Log) { Debug.Log("Executing Resources.UnloadUnusedAssets()..."); }
            startTime = DateTime.Now;
            operation = Resources.UnloadUnusedAssets();
            while (!operation.isDone || ((DateTime.Now - startTime) < TimeSpan.FromSeconds(0.05))) {
                if (this.Log) { Debug.Log($"{Time.frameCount}"); }
                await Awaitable.NextFrameAsync();
            }
            await operation;
            if (this.Log) { Debug.Log("...Finished executing Resources.UnloadUnusedAssets()"); }
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
