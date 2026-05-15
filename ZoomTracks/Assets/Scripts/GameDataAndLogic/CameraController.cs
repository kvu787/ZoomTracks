using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CameraController {
        private const float CameraPanSpeed = 150;
        private const float CameraZoomSpeed = 100;
        private const float MinCameraOrthographicSize = 1;
        private const float MaxCameraOrthographicSize = 281.25f;

        private CameraFollowSettings CameraFollowSettings { get; }
        private TrackJson TrackJson { get; }
        private InputManager InputManager { get; }
        private Transform CameraYawOffset { get; }
        private Transform CameraPanOffsetAndPitch { get; }
        private Camera Camera { get; }

        public CameraController(CameraFollowSettings cameraFollowSettings, TrackJson trackJson, InputManager inputManager) {
            this.CameraFollowSettings = cameraFollowSettings;
            this.TrackJson = trackJson;
            this.InputManager = inputManager;

            this.CameraYawOffset = GameObject.Find(nameof(this.CameraYawOffset)).transform;
            this.CameraPanOffsetAndPitch = GameObject.Find(nameof(this.CameraPanOffsetAndPitch)).transform;
            this.Camera = GameObject.Find(nameof(this.Camera)).GetComponent<Camera>();
            this.ValidateCameraParameters();
        }

        public float CameraYawWorldSpace => this.Camera.transform.eulerAngles.y;

        public void ReadInputAndChangeCameraSettings() {
            if (this.InputManager.Keyboard != null) {
                Keyboard keyboard = this.InputManager.Keyboard;

                // ESDF: Pan
                Vector2 vector2 = new(
                    keyboard.fKey.ReadValue() - keyboard.sKey.ReadValue(),
                    keyboard.eKey.ReadValue() - keyboard.dKey.ReadValue());
                this.PanOffset(vector2);

                // W/R: Zoom
                this.Zoom(keyboard.wKey.ReadValue(), keyboard.rKey.ReadValue());

                // Z: Reset pan offset
                if (keyboard.zKey.wasPressedThisFrame) {
                    this.ResetPanOffset();
                }

                // C: Reset zoom
                if (keyboard.cKey.wasPressedThisFrame) {
                    this.ResetZoom();
                }
            }

            if (this.InputManager.Gamepad != null) {
                Gamepad gamepad = this.InputManager.Gamepad;

                // Left stick: Pan offset
                this.PanOffset(gamepad.leftStick.ReadValue());

                // Left/right trigger: Zoom
                this.Zoom(gamepad.leftTrigger.ReadValue(), gamepad.rightTrigger.ReadValue());

                // D-pad up: Reset pan offset
                if (gamepad.dpad.up.wasPressedThisFrame) {
                    this.ResetPanOffset();
                }

                // D-pad down: Reset zoom
                if (gamepad.dpad.down.wasPressedThisFrame) {
                    this.ResetZoom();
                }
            }
        }

        private void PanOffset(Vector2 vector2) {
            this.CameraPanOffsetAndPitch.localPosition += Time.deltaTime * CameraPanSpeed * new Vector3(vector2.x, 0, vector2.y);
        }

        private void Zoom(float zoomOut, float zoomIn) {
            this.Camera.orthographicSize += Time.deltaTime * CameraZoomSpeed * (zoomOut - zoomIn);
            this.Camera.orthographicSize = Mathf.Clamp(this.Camera.orthographicSize, MinCameraOrthographicSize, MaxCameraOrthographicSize);
        }

        private void ResetPanOffset() {
            this.CameraPanOffsetAndPitch.localPosition = Vector3.zero;
        }

        private void ResetZoom() {
            if (this.CameraFollowSettings.FollowsCarLocation) {
                this.Camera.orthographicSize = this.TrackJson.FollowCameraSize;
            } else {
                this.Camera.orthographicSize = this.TrackJson.FixedCameraSize;
            }
        }

        private void ValidateCameraParameters() {
            Assert.IsTrue(this.CameraYawOffset.localPosition == Vector3.zero);
            Assert.IsTrue(this.CameraYawOffset.localEulerAngles == Vector3.zero);
            Assert.IsTrue(this.CameraYawOffset.localScale == Vector3.one);

            Assert.IsTrue(this.CameraPanOffsetAndPitch.localPosition == Vector3.zero);
            Assert.IsTrue(this.CameraPanOffsetAndPitch.localEulerAngles.x == 45);
            Assert.IsTrue(this.CameraPanOffsetAndPitch.localEulerAngles.y == 0);
            Assert.IsTrue(this.CameraPanOffsetAndPitch.localEulerAngles.z == 0);
            Assert.IsTrue(this.CameraPanOffsetAndPitch.localScale == Vector3.one);

            Assert.IsTrue(this.Camera.transform.localPosition.x == 0);
            Assert.IsTrue(this.Camera.transform.localPosition.y == 0);
            Assert.IsTrue(this.Camera.transform.localPosition.z == -500);
            Assert.IsTrue(this.Camera.transform.localEulerAngles == Vector3.zero);
            Assert.IsTrue(this.Camera.transform.localScale == Vector3.one);
            Assert.IsTrue(this.Camera.orthographic);
            Assert.IsTrue(MinCameraOrthographicSize <= this.Camera.orthographicSize && this.Camera.orthographicSize <= MaxCameraOrthographicSize);
            Assert.IsTrue(this.Camera.nearClipPlane == 1);
            Assert.IsTrue(this.Camera.farClipPlane == 1000);
            Assert.IsTrue(this.Camera.clearFlags == CameraClearFlags.SolidColor);
            Assert.IsTrue(ColorUtility.ToHtmlStringRGB(this.Camera.backgroundColor) == "404040");
        }
    }
}
