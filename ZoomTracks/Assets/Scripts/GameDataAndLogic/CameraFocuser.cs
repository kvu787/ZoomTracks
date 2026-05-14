using UnityEngine;
using UnityEngine.Assertions;

namespace ZoomTracks {
    public class CameraFocuser {
        private CarState CarState { get; }
        private InputManager InputManager { get; }
        private Transform CameraPanAndYaw { get; }
        private TransformStruct OriginalCameraPanAndYawTransform { get; }

        public CameraFocuser(CarState carState, InputManager inputManager) {
            this.CarState = carState;
            this.InputManager = inputManager;
            this.CameraPanAndYaw = GameObject.Find(nameof(this.CameraPanAndYaw)).transform;
            Assert.IsTrue(this.CameraPanAndYaw.localPosition.y == 0);
            Assert.IsTrue(this.CameraPanAndYaw.localEulerAngles.x == 0);
            Assert.IsTrue(this.CameraPanAndYaw.localEulerAngles.z == 0);
            Assert.IsTrue(this.CameraPanAndYaw.localScale == Vector3.one);
            this.OriginalCameraPanAndYawTransform = new TransformStruct(this.CameraPanAndYaw.transform);

            this.FollowsCar = true;
        }

        public bool FollowsCar { get; private set; }

        public void ReadInputAndToggleFocus() {
            // A key: Toggle follow
            if (this.InputManager.Keyboard?.aKey.wasPressedThisFrame == true) {
                this.FollowsCar = !this.FollowsCar;
            }

            // South button: Toggle follow
            if (this.InputManager.Gamepad?.buttonSouth.wasPressedThisFrame == true) {
                this.FollowsCar = !this.FollowsCar;
            }
        }

        public void UpdateCameraFocusPoint() {
            Vector3 newPosition = this.FollowsCar ? this.CarState.Position : this.OriginalCameraPanAndYawTransform.Position;
            this.CameraPanAndYaw.transform.position = newPosition;
        }
    }
}
