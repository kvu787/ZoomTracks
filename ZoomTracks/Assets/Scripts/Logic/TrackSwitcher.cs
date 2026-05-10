using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class TrackSwitcher {
        public int CurrentTrackIndex { get; private set; }
        public int OldTrackIndex { get; private set; }
        public int NewTrackIndex { get; private set; }

        public TrackSwitcher() {
            this.CurrentTrackIndex = -1;
            this.OldTrackIndex = -1;
            this.NewTrackIndex = Constants.InitialTrackSceneIndex;
        }

        public bool SwitchTracks(Keyboard Keyboard, Gamepad Gamepad) {
            bool isPrevTrack = false;
            bool isNextTrack = false;

            if (Keyboard != null) {
                isPrevTrack = isPrevTrack || Keyboard.leftArrowKey.wasPressedThisFrame;
                isNextTrack = isNextTrack || Keyboard.rightArrowKey.wasPressedThisFrame;
            }

            if (Gamepad != null) {
                isPrevTrack = isPrevTrack || Gamepad.leftShoulder.wasPressedThisFrame;
                isNextTrack = isNextTrack || Gamepad.rightShoulder.wasPressedThisFrame;
            }

            if (isPrevTrack) {
                this.PrevTrack();
            } else if (isNextTrack) {
                this.NextTrack();
            }

            return isPrevTrack || isNextTrack;
        }

        private void PrevTrack() {
            this.NewTrackIndex = (this.CurrentTrackIndex - 1 + Constants.TrackSceneNames.Count) % Constants.TrackSceneNames.Count;
            this.SwitchTrackShared();
        }

        private void NextTrack() {
            this.NewTrackIndex = (this.CurrentTrackIndex + 1) % Constants.TrackSceneNames.Count;
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
