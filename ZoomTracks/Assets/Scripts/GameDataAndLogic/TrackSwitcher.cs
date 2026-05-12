using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace ZoomTracks {
    public class TrackSwitcher {
        public int CurrentTrackIndex { get; private set; }
        private readonly IReadOnlyList<string> TrackSceneNames;
        public Scene CurrentTrackScene { get; private set; }

        public TrackSwitcher(int currentTrackSceneIndex, IReadOnlyList<string> trackSceneNames) {
            this.CurrentTrackIndex = currentTrackSceneIndex;
            this.TrackSceneNames = trackSceneNames;
            this.CurrentTrackScene = UnitySceneManager.GetSceneByName(this.TrackSceneNames[this.CurrentTrackIndex]);
        }

        public async Awaitable<bool> ReadInputAndSwitchTracks(Keyboard keyboard, Gamepad gamepad) {
            bool isPrevTrack = false;
            bool isNextTrack = false;

            if (keyboard != null) {
                isPrevTrack = isPrevTrack || keyboard.aKey.wasPressedThisFrame;
                isNextTrack = isNextTrack || keyboard.gKey.wasPressedThisFrame;
            }

            if (gamepad != null) {
                isPrevTrack = isPrevTrack || gamepad.leftShoulder.wasPressedThisFrame;
                isNextTrack = isNextTrack || gamepad.rightShoulder.wasPressedThisFrame;
            }

            if (isPrevTrack == isNextTrack) {
                return false;
            } else {
                int newTrackIndex;
                if (isPrevTrack) {
                    newTrackIndex = this.CurrentTrackIndex.CyclePrev(this.TrackSceneNames.Count);
                } else /* if (isNextTrack) */ {
                    newTrackIndex = this.CurrentTrackIndex.CycleNext(this.TrackSceneNames.Count);
                }

                int oldTrackIndex = this.CurrentTrackIndex;

                this.CurrentTrackIndex = -1;
                this.CurrentTrackScene = default;
                await AwaitableUtils.RunWithPrintBusy(async () => await UnitySceneManager.UnloadSceneAsync(this.TrackSceneNames[oldTrackIndex]));
                await AwaitableUtils.RunWithPrintBusy(async () => await Resources.UnloadUnusedAssets());
                await AwaitableUtils.RunWithPrintBusy(async () => await UnitySceneManager.LoadSceneAsync(this.TrackSceneNames[newTrackIndex], LoadSceneMode.Additive));
                this.CurrentTrackIndex = newTrackIndex;
                this.CurrentTrackScene = UnitySceneManager.GetSceneByName(this.TrackSceneNames[this.CurrentTrackIndex]);

                return true;
            }
        }
    }
}
