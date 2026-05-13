using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CarState {
        private const float CarForwardBackwardSpeed = 150;
        private const float CarRotateSpeed = 540;

        private InputManager InputManager { get; }

        public CarState(InputManager inputManager) {
            this.InputManager = inputManager;
        }

        public Vector3 Position { get; set; }
        public float Rotation { get; set; }
        public Vector3 Velocity { get; set; }

        public void ReadInputAndUpdateDebug() {
            if (this.InputManager.Keyboard != null) {
                Keyboard keyboard = this.InputManager.Keyboard;
                this.Position += Time.deltaTime * CarForwardBackwardSpeed * (keyboard.eKey.ReadValue() - keyboard.dKey.ReadValue()) * (Quaternion.Euler(0f, this.Rotation, 0f) * Vector3.forward);
                this.Rotation += Time.deltaTime * CarRotateSpeed * (keyboard.fKey.ReadValue() - keyboard.sKey.ReadValue());
            }

            if (this.InputManager.Gamepad != null) {
                Vector2 leftStick = this.InputManager.Gamepad.leftStick.ReadValue();
                this.Position += Time.deltaTime * CarForwardBackwardSpeed * leftStick.y * (Quaternion.Euler(0f, this.Rotation, 0f) * Vector3.forward);
                this.Rotation += Time.deltaTime * CarRotateSpeed * leftStick.x;
            }
        }

        public void ApplyToGameObject(GameObject gameObject) {
            gameObject.transform.SetPositionAndRotation(this.Position, Quaternion.Euler(0f, this.Rotation, 0f));
        }

        public void Reset(Transform placeholderCarTransform) {
            this.Position = placeholderCarTransform.position;
            this.Rotation = placeholderCarTransform.rotation.eulerAngles.y;
            this.Velocity = Vector3.zero;
        }
    }
}
