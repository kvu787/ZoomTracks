using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public class TrackSwitcher {
        public int CurrentTrackIndex { get; private set; }
        public int OldTrackIndex { get; private set; }
        public int NewTrackIndex { get; private set; }

        private readonly int tracksCount;

        public Scene CurrentTrackScene;
        private readonly IReadOnlyList<string> TrackSceneNames;

        public TrackSwitcher(int initialTrackSceneIndex, int tracksCount, IReadOnlyList<string> trackSceneNames) {
            this.CurrentTrackIndex = -1;
            this.OldTrackIndex = -1;
            this.NewTrackIndex = initialTrackSceneIndex;

            this.tracksCount = tracksCount;
            this.TrackSceneNames = trackSceneNames;
        }

        public bool ReadInputAndSwitchTracks(Keyboard keyboard, Gamepad gamepad) {
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
                    this.PrevTrack();
                } else if (isNextTrack) {
                    this.NextTrack();
                }
                return true;
            }
        }

        private void PrevTrack() {
            this.NewTrackIndex = this.NewTrackIndex.CyclePrev(this.tracksCount);
            this.SwitchTrackShared();
        }

        private void NextTrack() {
            this.NewTrackIndex = this.NewTrackIndex.CycleNext(this.tracksCount);
            this.SwitchTrackShared();
        }

        private void SwitchTrackShared() {
            this.OldTrackIndex = this.CurrentTrackIndex;
            this.CurrentTrackIndex = -1;
        }

        public void SwitchingTrackFinished() {
            this.CurrentTrackIndex = this.NewTrackIndex;
            this.OldTrackIndex = -1;
            this.NewTrackIndex = -1;
            this.CurrentTrackScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(this.TrackSceneNames[this.CurrentTrackIndex]);
        }
    }
}
