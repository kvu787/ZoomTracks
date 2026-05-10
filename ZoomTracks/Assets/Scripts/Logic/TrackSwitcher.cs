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

        public void PrevTrack() {
            this.NewTrackIndex = (this.CurrentTrackIndex - 1 + Constants.TrackSceneNames.Count) % Constants.TrackSceneNames.Count;
            this.SwitchTrackShared();
        }

        public void NextTrack() {
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
