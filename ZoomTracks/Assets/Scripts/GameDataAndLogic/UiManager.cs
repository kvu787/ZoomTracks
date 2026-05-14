using TMPro;
using UnityEngine;

namespace ZoomTracks {
    public class UiManager {
        private CameraFocuser CameraFocuser { get; }
        private ControlModeSwitcher ControlModeSwitcher { get; }
        private TMP_Text ControlModeLabel { get; }
        private TMP_Text CameraFollowCarLocationBoolLabel { get; }

        public UiManager(CameraFocuser cameraFocuser, ControlModeSwitcher controlModeSwitcher) {
            this.CameraFocuser = cameraFocuser;
            this.ControlModeSwitcher = controlModeSwitcher;
            this.ControlModeLabel = GameObject.Find(nameof(this.ControlModeLabel)).GetComponent<TMP_Text>();
            this.CameraFollowCarLocationBoolLabel = GameObject.Find(nameof(this.CameraFollowCarLocationBoolLabel)).GetComponent<TMP_Text>();
        }

        public void Update() {
            this.CameraFollowCarLocationBoolLabel.text = $"Camera following car location: {this.CameraFocuser.FollowsCarLocation}";
            this.ControlModeLabel.text = $"Control mode: {this.ControlModeSwitcher.Mode}";
        }
    }
}
