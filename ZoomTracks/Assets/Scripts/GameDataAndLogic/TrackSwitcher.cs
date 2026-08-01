using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public class TrackSwitcher {
        private InputManager InputManager { get; }
        private IReadOnlyList<string> TrackSceneNames { get; }
        private string CurrentTrackSceneName => this.TrackSceneNames[this.CurrentTrackSceneIndex];
        private int CurrentTrackSceneIndex { get; set; }

        public TrackSwitcher(InputManager inputManager, IReadOnlyList<string> trackSceneNames, int currentTrackSceneIndex) {
            this.InputManager = inputManager;
            this.TrackSceneNames = trackSceneNames;
            this.CurrentTrackSceneIndex = currentTrackSceneIndex;
            this.CurrentTrackScene = SceneManager.GetSceneByName(this.CurrentTrackSceneName);
            Assert.IsTrue(this.CurrentTrackScene.IsValid());
            this.CurrentTrackJson = this.ReadCurrentTrackJson();
        }

        public Scene CurrentTrackScene { get; private set; }
        public TrackJson CurrentTrackJson { get; private set; }

        public async Awaitable<bool> ReadInputAndSwitchTracksAsync() {
            if (this.InputManager.PreviousTrack == this.InputManager.NextTrack) {
                return false;
            } else {
                int newTrackIndex;
                if (this.InputManager.PreviousTrack) {
                    newTrackIndex = this.CurrentTrackSceneIndex.CyclePrev(this.TrackSceneNames.Count);
                } else /* if (isNextTrack) */ {
                    newTrackIndex = this.CurrentTrackSceneIndex.CycleNext(this.TrackSceneNames.Count);
                }

                int oldTrackIndex = this.CurrentTrackSceneIndex;

                this.CurrentTrackSceneIndex = -1;
                this.CurrentTrackScene = default;

                Debug.Log($"Unload old track scene...");
                await AwaitableUtility.RunWithPrintBusyEachFrameAsync(async () => await SceneManager.UnloadSceneAsync(this.TrackSceneNames[oldTrackIndex]));
                Debug.Log($"...done");

                Debug.Log($"Load new track scene...");
                await AwaitableUtility.RunWithPrintBusyEachFrameAsync(async () => await SceneManager.LoadSceneAsync(this.TrackSceneNames[newTrackIndex], LoadSceneMode.Additive));
                Debug.Log($"...done");

                this.CurrentTrackSceneIndex = newTrackIndex;
                this.CurrentTrackScene = SceneManager.GetSceneByName(this.CurrentTrackSceneName);
                this.CurrentTrackJson = this.ReadCurrentTrackJson();

                return true;
            }
        }

        private TrackJson ReadCurrentTrackJson() {
            string relativePath = $"{this.CurrentTrackSceneName}.json";
            return JsonUtility.Deserialize<TrackJson>(relativePath);
        }
    }
}
