using UnityEngine;
using UnityEngine.Assertions;

namespace ZoomTracks {
    public class CameraFocuser {
        private InputManager InputManager { get; }
        private Transform CameraPanAndYaw { get; }
        private CarState CarState { get; }
        public bool FollowsCarLocation { get; private set; }
        private TransformStruct OriginalCameraPanAndYawTransform { get; }

        public CameraFocuser(CarState carState, InputManager inputManager) {
            this.InputManager = inputManager;
            this.CarState = carState;
            this.CameraPanAndYaw = GameObject.Find(nameof(this.CameraPanAndYaw)).transform;

            Assert.IsTrue(this.CameraPanAndYaw.localPosition.y == 0);
            Assert.IsTrue(this.CameraPanAndYaw.localEulerAngles.x == 0);
            Assert.IsTrue(this.CameraPanAndYaw.localEulerAngles.z == 0);
            Assert.IsTrue(this.CameraPanAndYaw.localScale == Vector3.one);

            this.OriginalCameraPanAndYawTransform = new TransformStruct(this.CameraPanAndYaw.transform);
        }

        public void ReadInputAndToggleFocus() {
            // A: Toggle follow
            if (this.InputManager.Keyboard?.aKey.wasPressedThisFrame is true) {
                this.FollowsCarLocation = !this.FollowsCarLocation;
            }

            // Left shoulder: Toggle follow
            if (this.InputManager.Gamepad?.leftShoulder.wasPressedThisFrame is true) {
                this.FollowsCarLocation = !this.FollowsCarLocation;
            }
        }

        public void UpdateCameraPosition() {
            Vector3 newPosition = this.FollowsCarLocation ? this.CarState.Position : this.OriginalCameraPanAndYawTransform.Position;
            this.CameraPanAndYaw.transform.position = newPosition;
        }
    }
}
