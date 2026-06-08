using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CameraPivotManager {
        private CameraFollowSettings CameraFollowSettings { get; }
        private CameraController CameraController { get; }
        private CarState CarState { get; }
        private InputManager InputManager { get; }
        private Transform CameraPanAndYaw { get; }
        private TransformStruct OriginalCameraPanAndYawTransform { get; }

        public CameraPivotManager(CameraFollowSettings cameraFollowSettings, CameraController cameraController, CarState carState, InputManager inputManager) {
            this.CameraFollowSettings = cameraFollowSettings;
            this.CameraController = cameraController;
            this.CarState = carState;
            this.InputManager = inputManager;

            this.CameraPanAndYaw = GameObject.Find(nameof(this.CameraPanAndYaw)).transform;
            this.Validate();
            this.OriginalCameraPanAndYawTransform = new TransformStruct(this.CameraPanAndYaw.transform);
        }

        public void ReadInputAndToggle() {
            Gamepad gamepad = this.InputManager.Gamepad;
            if (gamepad != null) {
                if (gamepad.rightShoulder.isPressed && gamepad.aButton.wasPressedThisFrame) {
                    this.CameraFollowSettings.FollowsCarLocation = !this.CameraFollowSettings.FollowsCarLocation;
                    this.CameraController.ResetZoom();
                }
            }
        }

        public void UpdateCameraPivot() {
            Vector3 newPosition = this.CameraFollowSettings.FollowsCarLocation ? this.CarState.Position : this.OriginalCameraPanAndYawTransform.Position;
            this.CameraPanAndYaw.transform.position = newPosition;
        }

        private void Validate() {
            Assert.IsTrue(this.CameraPanAndYaw.localPosition.y == 0);
            Assert.IsTrue(this.CameraPanAndYaw.localEulerAngles.x == 0);
            Assert.IsTrue(this.CameraPanAndYaw.localEulerAngles.z == 0);
            Assert.IsTrue(this.CameraPanAndYaw.localScale == Vector3.one);
        }
    }
}
