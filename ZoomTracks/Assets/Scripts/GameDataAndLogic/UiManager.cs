using System;
using TMPro;
using UnityEngine;

namespace ZoomTracks {
    public class UiManager {
        private TMP_Text ClockText { get; }
        private TMP_Text CameraSizeText { get; }
        private TMP_Text DisplayModeText { get; }
        private CameraController CameraController { get; }

        public UiManager(CameraController cameraController) {
            this.ClockText = GameObject.Find(nameof(this.ClockText)).GetComponent<TMP_Text>();
            this.CameraSizeText = GameObject.Find(nameof(this.CameraSizeText)).GetComponent<TMP_Text>();
            this.DisplayModeText = GameObject.Find(nameof(this.DisplayModeText)).GetComponent<TMP_Text>();
            this.CameraController = cameraController;
        }

        public void UpdateUi() {
            //this.ClockText.SetText(DateTimeUtility.GetDateTimeNowChars_Buffered());
            this.ClockText.text = $"{DateTime.Now:yyyy-MM-dd [tt] HH:mm:ss.fff}";
            this.CameraSizeText.text = $"Camera size: {this.CameraController.OrthographicCameraSize:F2}";
            this.DisplayModeText.text = Screen.fullScreenMode switch {
                FullScreenMode.ExclusiveFullScreen => "Display mode: Exclusive",
                FullScreenMode.FullScreenWindow => "Display mode: Borderless",
                FullScreenMode.Windowed => "Display mode: Windowed",
                FullScreenMode.MaximizedWindow => "Display mode: Windowed max",
                _ => throw new Exception($"Unrecognized FullScreenMode: {Screen.fullScreenMode}"),
            };
        }
    }
}
