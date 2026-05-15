namespace ZoomTracks {
    public class CameraFollowSettings {

        public CameraFollowSettings(TrackJson trackJson) {
            this.FollowsCarLocation = trackJson.CameraFollowsCarLocation;
        }

        public bool FollowsCarLocation { get; set; }

        // TODO:
        // public bool CameraFollowsCarRotation { get; set; }
    }
}
