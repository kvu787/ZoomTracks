using TMPro;
using UnityEngine;

namespace ZoomTracks {
    public class UiManager {
        private CameraFollowSettings CameraFollowSettings { get; }
        private ControlModeSwitcher ControlModeSwitcher { get; }
        private TMP_Text CameraFollowsCarLocationLabel { get; }
        private TMP_Text ControlModeLabel { get; }

        public UiManager(CameraFollowSettings cameraFollowSettings, ControlModeSwitcher controlModeSwitcher) {
            this.CameraFollowSettings = cameraFollowSettings;
            this.ControlModeSwitcher = controlModeSwitcher;
            this.CameraFollowsCarLocationLabel = GameObject.Find(nameof(this.CameraFollowsCarLocationLabel)).GetComponent<TMP_Text>();
            this.ControlModeLabel = GameObject.Find(nameof(this.ControlModeLabel)).GetComponent<TMP_Text>();
        }

        public void UpdateUi() {
            this.CameraFollowsCarLocationLabel.text = $"Camera follows car location: {this.CameraFollowSettings.FollowsCarLocation}";
            this.ControlModeLabel.text = $"Control mode: {this.ControlModeSwitcher.Mode}";
        }
    }
}
