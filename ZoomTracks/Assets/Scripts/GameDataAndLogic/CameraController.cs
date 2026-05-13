using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CameraController {
        private const float CameraPanSpeed = 150;
        private const float CameraZoomSpeed = 100;
        private const float MinCameraOrthographicSize = 1;
        private const float MaxCameraOrthographicSize = 281.25f;

        public bool FollowsCarLocation { get; private set; }

        private InputManager InputManager { get; }
        private CarSwitcher CarSwitcher { get; }
        private Transform CameraPanAndYaw { get; }
        private Transform CameraYawOffset { get; }
        private Transform CameraPanOffsetAndPitch { get; }
        private Camera Camera { get; }

        private TransformStruct OriginalCameraPanAndYawTransform { get; }
        private float OriginalCameraOrthographicSize { get; }

        public CameraController(InputManager inputManager, CarSwitcher carSwitcher) {
            this.FollowsCarLocation = false;
            this.InputManager = inputManager;
            this.CarSwitcher = carSwitcher;

            this.CameraPanAndYaw = GameObject.Find(nameof(this.CameraPanAndYaw)).transform;
            this.CameraYawOffset = GameObject.Find(nameof(this.CameraYawOffset)).transform;
            this.CameraPanOffsetAndPitch = GameObject.Find(nameof(this.CameraPanOffsetAndPitch)).transform;
            this.Camera = GameObject.Find(nameof(this.Camera)).GetComponent<Camera>();
            this.ValidateCameraParameters();

            this.OriginalCameraPanAndYawTransform = new TransformStruct(this.CameraPanAndYaw.transform);
            this.OriginalCameraOrthographicSize = this.Camera.orthographicSize;
        }

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

            if (this.InputManager.Gamepad != null) {
                Gamepad gamepad = this.InputManager.Gamepad;

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
            Vector3 newPosition = this.FollowsCarLocation ? this.CarSwitcher.CurrentCarGameObject.transform.position : this.OriginalCameraPanAndYawTransform.Position;
            this.CameraPanAndYaw.transform.position = newPosition;
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
            this.Camera.orthographicSize = this.OriginalCameraOrthographicSize;
        }

        private void ToggleFollowLocation() {
            this.FollowsCarLocation = !this.FollowsCarLocation;
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
