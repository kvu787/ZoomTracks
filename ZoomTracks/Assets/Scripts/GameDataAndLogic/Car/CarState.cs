using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CarState {
        private const float CarForwardBackwardSpeed = 150;
        private const float CarRotateSpeed = 540;

        private InputManager InputManager { get; }

        private Vector3 Position { get; set; }
        private float Rotation { get; set; }
        private Quaternion RotationQuaternion => Quaternion.Euler(0f, this.Rotation, 0f);
        private Vector3 Velocity { get; set; }

        public CarState(InputManager inputManager) {
            this.InputManager = inputManager;
        }

        public void ReadInputAndUpdateDebug() {
            Vector3 positionDelta = Vector3.zero;
            float rotationDelta = 0;
            Vector3 positionTerm = Time.deltaTime * CarForwardBackwardSpeed * (this.RotationQuaternion * Vector3.forward);
            float rotationTerm = Time.deltaTime * CarRotateSpeed;

            if (this.InputManager.Keyboard != null) {
                Keyboard keyboard = this.InputManager.Keyboard;
                positionDelta += positionTerm * (keyboard.eKey.ReadValue() - keyboard.dKey.ReadValue());
                rotationDelta += rotationTerm * (keyboard.fKey.ReadValue() - keyboard.sKey.ReadValue());
            }

            if (this.InputManager.Gamepad != null) {
                Vector2 leftStick = this.InputManager.Gamepad.leftStick.ReadValue();
                positionDelta += positionTerm * leftStick.y;
                rotationDelta += rotationTerm * leftStick.x;
            }

            this.Position += positionDelta;
            this.Rotation += rotationDelta;
        }

        public void ApplyToGameObject(GameObject gameObject) {
            gameObject.transform.SetPositionAndRotation(this.Position, this.RotationQuaternion);
        }

        public void Reset(Transform placeholderCarTransform) {
            this.Position = placeholderCarTransform.position;
            this.Rotation = placeholderCarTransform.rotation.eulerAngles.y;
            this.Velocity = Vector3.zero;
        }
    }
}
