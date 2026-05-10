using TMPro;
using UnityEngine;

namespace ZoomTracks {
    public class UiObjects {
        private readonly CameraController CameraController;
        private readonly MainLoop MainLoop;
        private readonly TMP_Text ControlModeLabel;
        private readonly TMP_Text CameraFollowCarLocationBoolLabel;
        private readonly TMP_Text TestLabel;

        public UiObjects(CameraController cameraController, MainLoop mainLoop) {
            this.CameraController = cameraController;
            this.MainLoop = mainLoop;

            this.ControlModeLabel = GameObject.Find(nameof(this.ControlModeLabel)).GetComponent<TMP_Text>();
            this.CameraFollowCarLocationBoolLabel = GameObject.Find(nameof(this.CameraFollowCarLocationBoolLabel)).GetComponent<TMP_Text>();
            this.TestLabel = GameObject.Find(nameof(this.TestLabel)).GetComponent<TMP_Text>();

            this.TestLabel.text = "Test passed";
        }

        public void Update() {
            this.CameraFollowCarLocationBoolLabel.text = $"Camera following car location: {this.CameraController.ShouldFollowCarLocation}";
            this.ControlModeLabel.text = $"Control mode: {this.MainLoop.ControlMode}";
        }
    }
}
