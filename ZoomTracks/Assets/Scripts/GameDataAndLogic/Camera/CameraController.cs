using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace ZoomTracks {
    public class CameraController {
        private const float CameraRotationSpeed = 100;
        private const float CameraZoomSpeed = 50;
        private const float MinOrthographicCameraSize = 1;
        private const float MaxOrthographicCameraSize = 281.25f;

        private CameraFollowSettings CameraFollowSettings { get; }
        private InputManager InputManager { get; }

        private Transform CameraYawOffset { get; }
        private Transform CameraPanOffsetAndPitch { get; }
        private Camera Camera { get; }

        private float RotationOffset { get; set; }
        private float DefaultFixedCameraSize { get; }
        private float DefaultFollowCameraSize { get; }
        private float OrthographicCameraSize { get; set; }

        public CameraController(CameraFollowSettings cameraFollowSettings, TrackJson trackJson, InputManager inputManager) {
            this.CameraFollowSettings = cameraFollowSettings;
            this.InputManager = inputManager;

            this.CameraYawOffset = GameObject.Find(nameof(this.CameraYawOffset)).transform;
            this.CameraPanOffsetAndPitch = GameObject.Find(nameof(this.CameraPanOffsetAndPitch)).transform;
            this.Camera = GameObject.Find(nameof(this.Camera)).GetComponent<Camera>();

            this.RotationOffset = 0f;
            this.DefaultFixedCameraSize = this.Camera.orthographicSize;
            this.DefaultFollowCameraSize = trackJson.FollowCameraSize;
            this.ResetZoom();

            this.CameraData = this.Camera.GetUniversalAdditionalCameraData();

            this.ValidateCameraParameters();
        }

        public UniversalAdditionalCameraData CameraData { get; }

        public float CameraYawWorldSpace => this.Camera.transform.eulerAngles.y;

        public void Update() {
            this.CameraYawOffset.localEulerAngles = new Vector3(0f, this.RotationOffset, 0f);
            this.Camera.orthographicSize = this.OrthographicCameraSize;
        }

        public void ReadInputAndChangeCameraSettings() {
            Gamepad gamepad = this.InputManager.Gamepad;
            if (gamepad != null) {
                this.NoPanControl_RotationLeftStick_ZoomRightShoulderAndRightStick_CustomDeadzones(gamepad);
            }
        }

        private static float DeadzoneFilter(float input, float innerDeadzone, float outerDeadzone) {
            float sign = Mathf.Sign(input);
            input = Mathf.Abs(input);
            if (input > outerDeadzone) {
                input = 1f;
            } else {
                if (input < innerDeadzone) {
                    input = 0;
                } else {
                    input = (input - innerDeadzone) / (outerDeadzone - innerDeadzone);
                }
            }
            return sign * input;
        }

        private void NoPanControl_RotationLeftStick_ZoomRightShoulderAndRightStick_CustomDeadzones(Gamepad gamepad) {
            //float innerDeadzone = 0.125f;
            float outerDeadzone = 0.95f;
            //float innerDeadzone = 0.3f;
            //float outerDeadzone = 0.7f;
            //float innerDeadzone = 0.2f;
            //float outerDeadzone = 0.8f;

            if (gamepad.rightShoulder.isPressed) {
                float innerDeadzone = 0.0078125f;
                float y = DeadzoneFilter(gamepad.leftStick.ReadValue().y, innerDeadzone, outerDeadzone);
                if (y > 0) {
                    this.Zoom(0, y);
                } else if (y < 0) {
                    this.Zoom(Mathf.Abs(y), 0);
                }

                if (gamepad.xButton.wasPressedThisFrame) {
                    this.ResetRotationOffset();
                }
                if (gamepad.yButton.wasPressedThisFrame) {
                    this.ResetZoom();
                }
            } else {
                float innerDeadzone = 0.2f;
                float x = DeadzoneFilter(gamepad.leftStick.ReadUnprocessedValue().x, innerDeadzone, outerDeadzone);
                this.RotateOffset(x);
            }
        }

        /*
        private void NoPanControl_RotationLeftStick_ZoomRightShoulderAndRightStick(Gamepad gamepad) {
            if (gamepad.rightShoulder.isPressed) {
                float rightStickY = gamepad.rightStick.ReadValue().y;
                if (rightStickY > 0) {
                    this.Zoom(0, rightStickY);
                } else if (rightStickY < 0) {
                    this.Zoom(Mathf.Abs(rightStickY), 0);
                }

                if (gamepad.xButton.wasPressedThisFrame) {
                    this.ResetRotationOffset();
                }
                if (gamepad.yButton.wasPressedThisFrame) {
                    this.ResetZoom();
                }
            } else {
                float rightStickX = Gamepad.current.rightStick.ReadValue().x;
                float rightStickX_unprocessed = Gamepad.current.rightStick.ReadUnprocessedValue().x;
                this.RotateOffset(rightStickX);
            }
        }
        */

        /*
        private void NoPanControl_RotationLeftStick_ZoomLeftShoulderAndRightStick(Gamepad gamepad) {
            if (gamepad.leftShoulder.isPressed) {
                //this.PanOffset(gamepad.leftStick.ReadValue());
                float rightStickY = gamepad.rightStick.ReadValue().y;
                if (rightStickY > 0) {
                    this.Zoom(0, rightStickY);
                } else if (rightStickY < 0) {
                    this.Zoom(Mathf.Abs(rightStickY), 0);
                }

                if (gamepad.xButton.wasPressedThisFrame) {
                    this.ResetRotationOffset();
                }
                if (gamepad.yButton.wasPressedThisFrame) {
                    this.ResetZoom();
                }
            } else {
                float rightStickX = Gamepad.current.rightStick.ReadValue().x;
                float rightStickX_unprocessed = Gamepad.current.rightStick.ReadUnprocessedValue().x;
                //Debug.Log($"Right stick X: {rightStickX}, Right stick X unprocessed: {rightStickX_unprocessed}, Deadzone min: {InputSystem.settings.defaultDeadzoneMin}, Deadzone max: {InputSystem.settings.defaultDeadzoneMax}");
                this.RotateOffset(rightStickX);
            }
        }
        */

        private void RotateOffset(float f) {
            this.RotationOffset += Time.deltaTime * CameraRotationSpeed * f;
        }

        //private void PanOffset(Vector2 vector2) {
        //    this.CameraPanOffsetAndPitch.localPosition += Time.deltaTime * CameraPanSpeed * new Vector3(vector2.x, 0, vector2.y);
        //}

        private void Zoom(float zoomOut, float zoomIn) {
            this.OrthographicCameraSize += Time.deltaTime * CameraZoomSpeed * (zoomOut - zoomIn);
            this.OrthographicCameraSize = Mathf.Clamp(this.OrthographicCameraSize, MinOrthographicCameraSize, MaxOrthographicCameraSize);
        }

        private void ResetRotationOffset() {
            this.RotationOffset = 0f;
        }

        //private void ResetPanOffset() {
        //    this.CameraPanOffsetAndPitch.localPosition = Vector3.zero;
        //}

        public void ResetZoom() {
            if (this.CameraFollowSettings.FollowsCarLocation) {
                this.OrthographicCameraSize = this.DefaultFollowCameraSize;
            } else {
                this.OrthographicCameraSize = this.DefaultFixedCameraSize;
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
            Assert.IsTrue(MinOrthographicCameraSize <= this.DefaultFixedCameraSize && this.DefaultFixedCameraSize <= MaxOrthographicCameraSize);
            Assert.IsTrue(MinOrthographicCameraSize <= this.DefaultFollowCameraSize && this.DefaultFollowCameraSize <= MaxOrthographicCameraSize);
            Assert.IsTrue(MinOrthographicCameraSize <= this.OrthographicCameraSize && this.OrthographicCameraSize <= MaxOrthographicCameraSize);
            Assert.IsTrue(this.Camera.nearClipPlane == 1);
            Assert.IsTrue(this.Camera.farClipPlane == 1000);
            Assert.IsTrue(this.Camera.clearFlags == CameraClearFlags.SolidColor);
            Assert.IsTrue(ColorUtility.ToHtmlStringRGB(this.Camera.backgroundColor) == "404040");
        }
    }
}
