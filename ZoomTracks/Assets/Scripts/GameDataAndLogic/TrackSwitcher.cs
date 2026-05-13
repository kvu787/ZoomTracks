using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace ZoomTracks {
    public class TrackSwitcher {
        private InputManager InputManager { get; }
        public int CurrentTrackSceneIndex { get; private set; }
        private IReadOnlyList<string> TrackSceneNames { get; }
        public Scene CurrentTrackScene { get; private set; }

        public TrackSwitcher(InputManager inputManager, int currentTrackSceneIndex, IReadOnlyList<string> trackSceneNames) {
            this.InputManager = inputManager;
            this.CurrentTrackSceneIndex = currentTrackSceneIndex;
            this.TrackSceneNames = trackSceneNames;
            this.CurrentTrackScene = UnitySceneManager.GetSceneByName(this.TrackSceneNames[this.CurrentTrackSceneIndex]);
        }

        public async Awaitable<bool> ReadInputAndSwitchTracksAsync() {
            bool isPrevTrack = false;
            bool isNextTrack = false;

            if (this.InputManager.Keyboard != null) {
                Keyboard keyboard = this.InputManager.Keyboard;
                isPrevTrack = isPrevTrack || keyboard.aKey.wasPressedThisFrame;
                isNextTrack = isNextTrack || keyboard.gKey.wasPressedThisFrame;
            }

            if (this.InputManager.Gamepad != null) {
                Gamepad gamepad = this.InputManager.Gamepad;
                isPrevTrack = isPrevTrack || gamepad.leftShoulder.wasPressedThisFrame;
                isNextTrack = isNextTrack || gamepad.rightShoulder.wasPressedThisFrame;
            }

            if (isPrevTrack == isNextTrack) {
                return false;
            } else {
                int newTrackIndex;
                if (isPrevTrack) {
                    newTrackIndex = this.CurrentTrackSceneIndex.CyclePrev(this.TrackSceneNames.Count);
                } else /* if (isNextTrack) */ {
                    newTrackIndex = this.CurrentTrackSceneIndex.CycleNext(this.TrackSceneNames.Count);
                }

                int oldTrackIndex = this.CurrentTrackSceneIndex;

                this.CurrentTrackSceneIndex = -1;
                this.CurrentTrackScene = default;

                Debug.Log($"Unload old track scene...");
                await AwaitableUtility.RunWithPrintBusyEachFrameAsync(async () => await UnitySceneManager.UnloadSceneAsync(this.TrackSceneNames[oldTrackIndex]));
                Debug.Log($"...done");

                Debug.Log($"Unload unused assets...");
                await AwaitableUtility.RunWithPrintBusyEachFrameAsync(async () => await Resources.UnloadUnusedAssets());
                Debug.Log($"...done");

                Debug.Log($"Load new track scene...");
                await AwaitableUtility.RunWithPrintBusyEachFrameAsync(async () => await UnitySceneManager.LoadSceneAsync(this.TrackSceneNames[newTrackIndex], LoadSceneMode.Additive));
                Debug.Log($"...done");

                this.CurrentTrackSceneIndex = newTrackIndex;
                this.CurrentTrackScene = UnitySceneManager.GetSceneByName(this.TrackSceneNames[this.CurrentTrackSceneIndex]);

                return true;
            }
        }
    }
}
