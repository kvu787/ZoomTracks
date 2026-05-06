using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public enum SceneSwitcherState {
        Unloaded,
        Loading,
        Loaded,
        Unloading,
    }

    public static class SceneSwitcher {
        private static SceneSwitcherState State = SceneSwitcherState.Unloaded;
        private static Awaitable LoadSceneAwaitable = null;
        private static Awaitable UnloadSceneAwaitable = null;

        public static void Update() {
            ValidateState();

            if (LoadSceneAwaitable?.IsCompleted is true) {
                Debug.Log($"Completed LoadSceneAwaitable: {LoadSceneAwaitable}");
                LoadSceneAwaitable.GetAwaiter().GetResult();
                LoadSceneAwaitable = null;
                State = SceneSwitcherState.Loaded;
            }

            if (UnloadSceneAwaitable?.IsCompleted is true) {
                Debug.Log($"Completed UnloadSceneAwaitable: {UnloadSceneAwaitable}");
                UnloadSceneAwaitable.GetAwaiter().GetResult();
                UnloadSceneAwaitable = null;
                State = SceneSwitcherState.Unloaded;
            }
        }

        public static void LoadTestScene() {
            if (State == SceneSwitcherState.Unloaded) {
                LoadSceneAwaitable = LoadTestSceneAsync();
                State = SceneSwitcherState.Loading;
            }
        }

        public static void UnloadTestScene() {
            if (State == SceneSwitcherState.Loaded) {
                UnloadSceneAwaitable = UnloadTestSceneAsync();
                State = SceneSwitcherState.Unloading;
            }
        }

        private static void ValidateState() {
            Assert.IsFalse(LoadSceneAwaitable != null && UnloadSceneAwaitable != null);

            if (State == SceneSwitcherState.Unloaded) {
                Assert.IsTrue(LoadSceneAwaitable == null && UnloadSceneAwaitable == null);
            } else if (State == SceneSwitcherState.Loading) {
                Assert.IsTrue(LoadSceneAwaitable != null && UnloadSceneAwaitable == null);
            } else if (State == SceneSwitcherState.Loaded) {
                Assert.IsTrue(LoadSceneAwaitable == null && UnloadSceneAwaitable == null);
            } else if (State == SceneSwitcherState.Unloading) {
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
