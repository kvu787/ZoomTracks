using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CameraController {
        private const float CameraPanSpeed = 150;
        private const float CameraZoomSpeed = 100;
        private const float MinCameraOrthographicSize = 1;
        private const float MaxCameraOrthographicSize = 281.25f;

        public bool ShouldFollowCarLocation { get; private set; } = false;

        private readonly TrackObjects TrackObjects;

        private readonly Transform CameraPanAndYaw;
        private readonly Transform CameraYawOffset;
        private readonly Transform CameraPanOffsetAndPitch;
        private readonly Camera Camera;

        private readonly TransformStruct OriginalCameraPanAndYawTransform;
        private readonly float OriginalCameraOrthographicSize;

        public CameraController(TrackObjects trackObjects) {
            this.ShouldFollowCarLocation = false;
            this.TrackObjects = trackObjects;

            this.CameraPanAndYaw = GameObject.Find(nameof(this.CameraPanAndYaw)).transform;
            this.CameraYawOffset = GameObject.Find(nameof(this.CameraYawOffset)).transform;
            this.CameraPanOffsetAndPitch = GameObject.Find(nameof(this.CameraPanOffsetAndPitch)).transform;
            this.Camera = GameObject.Find(nameof(this.Camera)).GetComponent<Camera>();

            this.ValidateCameraParameters();

            this.OriginalCameraPanAndYawTransform = new TransformStruct(this.CameraPanAndYaw.transform);
            this.OriginalCameraOrthographicSize = this.Camera.orthographicSize;
        }

        public void ReadInputAndChangeCameraSettings(Keyboard keyboard, Gamepad gamepad) {
            if (keyboard != null) {
                // ESDF: Pan
                Vector2 vector2 = new(
                    keyboard.fKey.ReadValue() - keyboard.sKey.ReadValue(),
                    keyboard.eKey.ReadValue() - keyboard.dKey.ReadValue());
                this.PanOffset(vector2);

                // W/R: Zoom
                this.Zoom(keyboard.wKey.ReadValue(), keyboard.rKey.ReadValue());

                // A: Toggle follow
                if (keyboard.aKey.wasPressedThisFrame) {
                    this.ToggleFollowLocation();
                }

                // Z: Reset pan offset
                if (keyboard.zKey.wasPressedThisFrame) {
                    this.ResetPanOffset();
                }

                // C: Reset zoom
                if (keyboard.cKey.wasPressedThisFrame) {
                    this.ResetZoom();
                }
            }

            if (gamepad != null) {
                // Left stick: Pan offset
                this.PanOffset(gamepad.leftStick.ReadValue());

                // Left/right trigger: Zoom
                this.Zoom(gamepad.leftTrigger.ReadValue(), gamepad.rightTrigger.ReadValue());

                // Left shoulder: Toggle follow
                if (gamepad.leftShoulder.wasPressedThisFrame) {
                    this.ToggleFollowLocation();
                }

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

        public void UpdateCameraPosition() {
            if (this.ShouldFollowCarLocation) {
                this.CameraPanAndYaw.transform.position = this.TrackObjects.Car.transform.position;
            } else {
                this.CameraPanAndYaw.transform.position = this.OriginalCameraPanAndYawTransform.Position;
            }
        }

        private void PanOffset(Vector2 vector2) {
            this.CameraPanOffsetAndPitch.localPosition += Time.deltaTime * CameraPanSpeed * new Vector3(vector2.x, 0, vector2.y);
        }

        private void Zoom(float a, float b) {
            this.Camera.orthographicSize += Time.deltaTime * CameraZoomSpeed * (a - b);
            this.Camera.orthographicSize = Mathf.Clamp(this.Camera.orthographicSize, MinCameraOrthographicSize, MaxCameraOrthographicSize);
        }

        private void ResetPanOffset() {
            this.CameraPanOffsetAndPitch.localPosition = Vector3.zero;
        }

        private void ResetZoom() {
            this.Camera.orthographicSize = this.OriginalCameraOrthographicSize;
        }

        private void ToggleFollowLocation() {
            this.ShouldFollowCarLocation = !this.ShouldFollowCarLocation;
        }

        private void ValidateCameraParameters() {
            Assert.IsTrue(this.CameraPanAndYaw.localPosition.y == 0);
            Assert.IsTrue(this.CameraPanAndYaw.localEulerAngles.x == 0);
            Assert.IsTrue(this.CameraPanAndYaw.localEulerAngles.z == 0);
            Assert.IsTrue(this.CameraPanAndYaw.localScale == Vector3.one);

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
