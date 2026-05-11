using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CarMover {
        private const float CarForwardBackwardSpeed = 150;
        private const float CarRotateSpeed = 540;

        private readonly CarSwitcher CarSwitcher;
        private Transform CurrentCarTransform => this.CarSwitcher.CurrentCar.GameObject.transform;

        public CarMover(CarSwitcher carSwitcher) {
            this.CarSwitcher = carSwitcher;
        }

        public void ReadInputAndMoveCar(Keyboard keyboard, Gamepad gamepad) {
            if (keyboard != null) {
                if (keyboard.eKey.isPressed) {
                    this.CurrentCarTransform.Translate(Time.deltaTime * CarForwardBackwardSpeed * Vector3.forward);
                }
                if (keyboard.dKey.isPressed) {
                    this.CurrentCarTransform.Translate(Time.deltaTime * CarForwardBackwardSpeed * Vector3.back);
                }
                if (keyboard.sKey.isPressed) {
                    this.CurrentCarTransform.Rotate(axis: Vector3.up, -1 * Time.deltaTime * CarRotateSpeed);
                }
                if (keyboard.fKey.isPressed) {
                    this.CurrentCarTransform.Rotate(axis: Vector3.up, Time.deltaTime * CarRotateSpeed);
                }
            }

            if (gamepad != null) {
                Vector2 leftStick = gamepad.leftStick.ReadValue();
                this.CurrentCarTransform.Translate(Time.deltaTime * CarForwardBackwardSpeed * leftStick.y * Vector3.forward);
                this.CurrentCarTransform.Rotate(axis: Vector3.up, Time.deltaTime * leftStick.x * CarRotateSpeed);
            }
        }
    }
}
