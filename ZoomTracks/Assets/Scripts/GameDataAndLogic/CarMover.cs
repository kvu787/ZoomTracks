using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CarMover {
        private const float CarForwardBackwardSpeed = 150;
        private const float CarRotateSpeed = 540;

        private InputManager InputManager { get; }
        private CarSwitcher CarSwitcher { get; }
        private Transform CurrentCarTransform => this.CarSwitcher.CurrentCar.GameObject.transform;

        public CarMover(InputManager inputManager, CarSwitcher carSwitcher) {
            this.InputManager = inputManager;
            this.CarSwitcher = carSwitcher;
        }

        public void ReadInputAndMoveCar() {
            if (this.InputManager.Keyboard != null) {
                Keyboard keyboard = this.InputManager.Keyboard;
                this.CurrentCarTransform.Translate(Time.deltaTime * CarForwardBackwardSpeed * (keyboard.eKey.ReadValue() - keyboard.dKey.ReadValue()) * Vector3.forward);
                this.CurrentCarTransform.Rotate(axis: Vector3.up, Time.deltaTime * CarRotateSpeed * (keyboard.fKey.ReadValue() - keyboard.sKey.ReadValue()));
            }

            if (this.InputManager.Gamepad != null) {
                Gamepad gamepad = this.InputManager.Gamepad;
                Vector2 leftStick = gamepad.leftStick.ReadValue();
                this.CurrentCarTransform.Translate(Time.deltaTime * CarForwardBackwardSpeed * leftStick.y * Vector3.forward);
                this.CurrentCarTransform.Rotate(axis: Vector3.up, Time.deltaTime * CarRotateSpeed * leftStick.x);
            }
        }
    }
}
