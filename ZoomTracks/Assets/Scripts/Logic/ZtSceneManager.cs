using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public enum ZtSceneManagerState {
        Unloaded,
        Loading,
        Loaded,
        Unloading,
    }

    // Awaitable docs:
    // https://docs.unity3d.com/6000.3/Documentation/Manual/async-await-support.html
    public static class ZtSceneManager {
        private static ZtSceneManagerState State = ZtSceneManagerState.Unloaded;
        private static Awaitable LoadSceneAwaitable = null;
        private static Awaitable UnloadSceneAwaitable = null;

        public static void Update() {
            ValidateState();

            if (LoadSceneAwaitable?.IsCompleted is true) {
                Debug.Log($"Completed LoadSceneAwaitable: {LoadSceneAwaitable}");
                LoadSceneAwaitable.GetAwaiter().GetResult();
                LoadSceneAwaitable = null;
                State = ZtSceneManagerState.Loaded;
            }

            if (UnloadSceneAwaitable?.IsCompleted is true) {
                Debug.Log($"Completed UnloadSceneAwaitable: {UnloadSceneAwaitable}");
                UnloadSceneAwaitable.GetAwaiter().GetResult();
                UnloadSceneAwaitable = null;
                State = ZtSceneManagerState.Unloaded;
            }
        }

        public static void LoadTestScene() {
            if (State == ZtSceneManagerState.Unloaded) {
                LoadSceneAwaitable = LoadTestSceneAsync();
                State = ZtSceneManagerState.Loading;
            }
        }

        public static void UnloadTestScene() {
            if (State == ZtSceneManagerState.Loaded) {
                UnloadSceneAwaitable = UnloadTestSceneAsync();
                State = ZtSceneManagerState.Unloading;
            }
        }

        private static void ValidateState() {
            Assert.IsFalse(LoadSceneAwaitable != null && UnloadSceneAwaitable != null);

            if (State == ZtSceneManagerState.Unloaded) {
                Assert.IsTrue(LoadSceneAwaitable == null && UnloadSceneAwaitable == null);
            } else if (State == ZtSceneManagerState.Loading) {
                Assert.IsTrue(LoadSceneAwaitable != null && UnloadSceneAwaitable == null);
            } else if (State == ZtSceneManagerState.Loaded) {
                Assert.IsTrue(LoadSceneAwaitable == null && UnloadSceneAwaitable == null);
            } else if (State == ZtSceneManagerState.Unloading) {
                Assert.IsTrue(LoadSceneAwaitable == null && UnloadSceneAwaitable != null);
            } else {
                throw new Exception("Unknown state");
            }
        }

        private static async Awaitable LoadTestSceneAsync() {
            DateTime startTime;
            AsyncOperation operation;

            Debug.Log("Loading TestScene...");
            startTime = DateTime.Now;
            operation = SceneManager.LoadSceneAsync("TestScene", LoadSceneMode.Additive);
            while (!operation.isDone || ((DateTime.Now - startTime) < TimeSpan.FromSeconds(1))) {
                Debug.Log($"{Time.frameCount}");
                await Awaitable.NextFrameAsync();
            }
            await operation;
            Debug.Log("...done");
        }

        private static async Awaitable UnloadTestSceneAsync() {
            DateTime startTime;
            AsyncOperation operation;

            Debug.Log("Unloading TestScene...");
            startTime = DateTime.Now;
            operation = SceneManager.UnloadSceneAsync("TestScene");
            while (!operation.isDone || ((DateTime.Now - startTime) < TimeSpan.FromSeconds(0.5))) {
                Debug.Log($"{Time.frameCount}");
                await Awaitable.NextFrameAsync();
            }
            await operation;
            Debug.Log("...done");

            Debug.Log("Executing UnloadUnusedAssets...");
            startTime = DateTime.Now;
            operation = Resources.UnloadUnusedAssets();
            while (!operation.isDone || ((DateTime.Now - startTime) < TimeSpan.FromSeconds(0.5))) {
                Debug.Log($"{Time.frameCount}");
                await Awaitable.NextFrameAsync();
            }
            await operation;
            Debug.Log("...done");
        }
    }
}
