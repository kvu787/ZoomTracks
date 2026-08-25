using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public class TrackSwitcher {
        private InputManager InputManager { get; }
        private IReadOnlyList<string> TrackNames { get; }
        private int CurrentTrackIndex { get; set; }

        public TrackSwitcher(InputManager inputManager, IReadOnlyList<string> trackNames, int currentTrackIndex) {
            this.InputManager = inputManager;
            this.TrackNames = trackNames;
            this.CurrentTrackIndex = currentTrackIndex;
            this.CurrentTrackScene = SceneManager.GetSceneByName(this.CurrentTrackName);
            Assert.IsTrue(this.CurrentTrackScene.IsValid());
            this.CurrentTrackJson = this.ReadCurrentTrackJson();
        }

        public string CurrentTrackName => this.TrackNames[this.CurrentTrackIndex];
        public Scene CurrentTrackScene { get; private set; }
        public TrackJson CurrentTrackJson { get; private set; }

        public async Awaitable<bool> ReadInputAndSwitchTracksAsync() {
            if (this.InputManager.PreviousTrack == this.InputManager.NextTrack) {
                return false;
            } else {
                int newTrackIndex;
                if (this.InputManager.PreviousTrack) {
                    newTrackIndex = this.CurrentTrackIndex.CyclePrev(this.TrackNames.Count);
                } else /* if (isNextTrack) */ {
                    newTrackIndex = this.CurrentTrackIndex.CycleNext(this.TrackNames.Count);
                }

                int oldTrackIndex = this.CurrentTrackIndex;

                this.CurrentTrackIndex = -1;
                this.CurrentTrackScene = default;

                Debug.Log($"Unload old track scene...");
                await AwaitableUtility.RunWithPrintBusyEachFrameAsync(async () => await SceneManager.UnloadSceneAsync(this.TrackNames[oldTrackIndex]));
                Debug.Log($"...done");

                Debug.Log($"Load new track scene...");
                await AwaitableUtility.RunWithPrintBusyEachFrameAsync(async () => await SceneManager.LoadSceneAsync(this.TrackNames[newTrackIndex], LoadSceneMode.Additive));
                Debug.Log($"...done");

                this.CurrentTrackIndex = newTrackIndex;
                this.CurrentTrackScene = SceneManager.GetSceneByName(this.CurrentTrackName);
                this.CurrentTrackJson = this.ReadCurrentTrackJson();

                return true;
            }
        }

        private TrackJson ReadCurrentTrackJson() {
            string relativePath = $"{this.CurrentTrackName}.json";
            return JsonUtility.Deserialize<TrackJson>(relativePath);
        }
    }
}
