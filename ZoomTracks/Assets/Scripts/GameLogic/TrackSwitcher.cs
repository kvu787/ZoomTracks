using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class TrackSwitcher {
        public int CurrentTrackIndex { get; private set; }
        public int OldTrackIndex { get; private set; }
        public int NewTrackIndex { get; private set; }

        private readonly int tracksCount;

        public TrackSwitcher(int initialTrackSceneIndex, int tracksCount) {
            this.CurrentTrackIndex = -1;
            this.OldTrackIndex = -1;
            this.NewTrackIndex = initialTrackSceneIndex;
            this.tracksCount = tracksCount;
        }

        public bool ReadInputAndSwitchTracks(Keyboard keyboard, Gamepad gamepad) {
            bool isPrevTrack = false;
            bool isNextTrack = false;

            if (keyboard != null) {
                isPrevTrack = isPrevTrack || keyboard.leftArrowKey.wasPressedThisFrame;
                isNextTrack = isNextTrack || keyboard.rightArrowKey.wasPressedThisFrame;
            }

            if (gamepad != null) {
                isPrevTrack = isPrevTrack || gamepad.leftShoulder.wasPressedThisFrame;
                isNextTrack = isNextTrack || gamepad.rightShoulder.wasPressedThisFrame;
            }

            if (isPrevTrack) {
                this.PrevTrack();
            } else if (isNextTrack) {
                this.NextTrack();
            }

            return isPrevTrack || isNextTrack;
        }

        private void PrevTrack() {
            this.NewTrackIndex = (this.CurrentTrackIndex - 1 + this.tracksCount) % this.tracksCount;
            this.SwitchTrackShared();
        }

        private void NextTrack() {
            this.NewTrackIndex = (this.CurrentTrackIndex + 1) % this.tracksCount;
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
        }
    }
}
