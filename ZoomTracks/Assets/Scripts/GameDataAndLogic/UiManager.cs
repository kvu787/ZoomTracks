using TMPro;
using UnityEngine;

namespace ZoomTracks {
    public class UiManager {
        private CameraController CameraController { get; }
        private ControlModeSwitcher ControlModeSwitcher { get; }
        private TMP_Text ControlModeLabel { get; }
        private TMP_Text CameraFollowCarLocationBoolLabel { get; }
        private TMP_Text TestLabel { get; }

        public UiManager(CameraController cameraController, ControlModeSwitcher controlModeSwitcher) {
            this.CameraController = cameraController;
            this.ControlModeSwitcher = controlModeSwitcher;

            this.ControlModeLabel = GameObject.Find(nameof(this.ControlModeLabel)).GetComponent<TMP_Text>();
            this.CameraFollowCarLocationBoolLabel = GameObject.Find(nameof(this.CameraFollowCarLocationBoolLabel)).GetComponent<TMP_Text>();
            this.TestLabel = GameObject.Find(nameof(this.TestLabel)).GetComponent<TMP_Text>();

            this.TestLabel.text = "Test passed";
        }

        public void Update() {
            this.CameraFollowCarLocationBoolLabel.text = $"Camera following car location: {this.CameraController.ShouldFollowCarLocation}";
            this.ControlModeLabel.text = $"Control mode: {this.ControlModeSwitcher.ControlMode}";
        }
    }
}
