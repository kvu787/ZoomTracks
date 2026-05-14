using TMPro;
using UnityEngine;

namespace ZoomTracks {
    public class UiManager {
        private CameraPivotManager CameraPivotManager { get; }
        private ControlModeSwitcher ControlModeSwitcher { get; }
        private CarControlModeSwitcher CarControlModeSwitcher { get; }
        private TMP_Text CameraFollowsCarLocationLabel { get; }
        private TMP_Text ControlModeLabel { get; }
        private TMP_Text CarControlModeLabel { get; }

        public UiManager(CameraPivotManager cameraFocuser, ControlModeSwitcher controlModeSwitcher, CarControlModeSwitcher carControlModeSwitcher) {
            this.CameraPivotManager = cameraFocuser;
            this.ControlModeSwitcher = controlModeSwitcher;
            this.CarControlModeSwitcher = carControlModeSwitcher;
            this.CameraFollowsCarLocationLabel = GameObject.Find(nameof(this.CameraFollowsCarLocationLabel)).GetComponent<TMP_Text>();
            this.ControlModeLabel = GameObject.Find(nameof(this.ControlModeLabel)).GetComponent<TMP_Text>();
            this.CarControlModeLabel = GameObject.Find(nameof(this.CarControlModeLabel)).GetComponent<TMP_Text>();
        }

        public void UpdateUi() {
            this.CameraFollowsCarLocationLabel.text = $"Camera follows car location: {this.CameraPivotManager.FollowsCarLocation}";
            this.ControlModeLabel.text = $"Control mode: {this.ControlModeSwitcher.Mode}";
            this.CarControlModeLabel.text = $"Car control mode: {this.CarControlModeSwitcher.Mode}";
        }
    }
}
