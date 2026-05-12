using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace ZoomTracks {
    public class TrackSwitcher {
        public int CurrentTrackIndex { get; private set; }
        public int OldTrackIndex { get; private set; }
        public int NewTrackIndex { get; private set; }

        private readonly int tracksCount;

        public Scene CurrentTrackScene;
        private readonly IReadOnlyList<string> TrackSceneNames;

        public TrackSwitcher(int currentTrackSceneIndex, int tracksCount, IReadOnlyList<string> trackSceneNames) {
            this.CurrentTrackIndex = currentTrackSceneIndex;
            this.OldTrackIndex = -1;
            this.NewTrackIndex = -1;

            this.tracksCount = tracksCount;
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
                if (isPrevTrack) {
                    this.NewTrackIndex = this.CurrentTrackIndex.CyclePrev(this.tracksCount);
                } else if (isNextTrack) {
                    this.NewTrackIndex = this.CurrentTrackIndex.CycleNext(this.tracksCount);
                }
                this.OldTrackIndex = this.CurrentTrackIndex;
                this.CurrentTrackIndex = -1;

                await UnitySceneManager.UnloadSceneAsync(this.TrackSceneNames[this.OldTrackIndex]);
                await UnitySceneManager.LoadSceneAsync(this.TrackSceneNames[this.NewTrackIndex], LoadSceneMode.Additive);

                this.CurrentTrackIndex = this.NewTrackIndex;
                this.OldTrackIndex = -1;
                this.NewTrackIndex = -1;
                this.CurrentTrackScene = UnitySceneManager.GetSceneByName(this.TrackSceneNames[this.CurrentTrackIndex]);

                return true;
            }
        }
    }
}
