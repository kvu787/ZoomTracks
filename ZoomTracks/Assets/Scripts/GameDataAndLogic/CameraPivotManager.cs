using UnityEngine;
using UnityEngine.Assertions;

namespace ZoomTracks {
    public class CameraPivotManager {
        private CarState CarState { get; }
        private InputManager InputManager { get; }
        private Transform CameraPanAndYaw { get; }
        private TransformStruct OriginalCameraPanAndYawTransform { get; }

        public CameraPivotManager(CarState carState, InputManager inputManager) {
            this.CarState = carState;
            this.InputManager = inputManager;
            this.CameraPanAndYaw = GameObject.Find(nameof(this.CameraPanAndYaw)).transform;
            Assert.IsTrue(this.CameraPanAndYaw.localPosition.y == 0);
            Assert.IsTrue(this.CameraPanAndYaw.localEulerAngles.x == 0);
            Assert.IsTrue(this.CameraPanAndYaw.localEulerAngles.z == 0);
            Assert.IsTrue(this.CameraPanAndYaw.localScale == Vector3.one);
            this.OriginalCameraPanAndYawTransform = new TransformStruct(this.CameraPanAndYaw.transform);

            this.FollowsCarLocation = true;
        }

        public bool FollowsCarLocation { get; private set; }

        public void ReadInputAndToggle() {
            // TODO: Implement mode for following car yaw

            // A key: Toggle follow location
            if (this.InputManager.Keyboard?.aKey.wasPressedThisFrame == true) {
                this.FollowsCarLocation = !this.FollowsCarLocation;
            }

            // South button: Toggle follow location
            if (this.InputManager.Gamepad?.buttonSouth.wasPressedThisFrame == true) {
                this.FollowsCarLocation = !this.FollowsCarLocation;
            }
        }

        public void UpdateCameraPivot() {
            Vector3 newPosition = this.FollowsCarLocation ? this.CarState.Position : this.OriginalCameraPanAndYawTransform.Position;
            this.CameraPanAndYaw.transform.position = newPosition;
        }
    }
}
