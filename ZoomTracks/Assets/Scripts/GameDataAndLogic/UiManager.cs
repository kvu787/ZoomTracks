using TMPro;
using UnityEngine;

namespace ZoomTracks {
    public class UiManager {
        private CameraFocuser CameraFocuser { get; }
        private ControlModeSwitcher ControlModeSwitcher { get; }
        private CarControlModeSwitcher CarControlModeSwitcher { get; }
        private TMP_Text CameraFollowsCarLabel { get; }
        private TMP_Text ControlModeLabel { get; }
        private TMP_Text CarControlModeLabel { get; }

        public UiManager(CameraFocuser cameraFocuser, ControlModeSwitcher controlModeSwitcher, CarControlModeSwitcher carControlModeSwitcher) {
            this.CameraFocuser = cameraFocuser;
            this.ControlModeSwitcher = controlModeSwitcher;
            this.CarControlModeSwitcher = carControlModeSwitcher;
            this.CameraFollowsCarLabel = GameObject.Find(nameof(this.CameraFollowsCarLabel)).GetComponent<TMP_Text>();
            this.ControlModeLabel = GameObject.Find(nameof(this.ControlModeLabel)).GetComponent<TMP_Text>();
            this.CarControlModeLabel = GameObject.Find(nameof(this.CarControlModeLabel)).GetComponent<TMP_Text>();
        }

        public void UpdateUi() {
            this.CameraFollowsCarLabel.text = $"Camera follows car location: {this.CameraFocuser.FollowsCar}";
            this.ControlModeLabel.text = $"Control mode: {this.ControlModeSwitcher.Mode}";
            this.CarControlModeLabel.text = $"Car control mode: {this.CarControlModeSwitcher.Mode}";
        }
    }
}
