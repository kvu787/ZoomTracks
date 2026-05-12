using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CarMover {
        private const float CarForwardBackwardSpeed = 150;
        private const float CarRotateSpeed = 540;

        private CarSwitcher CarSwitcher { get; }
        private Transform CurrentCarTransform => this.CarSwitcher.CurrentCar.GameObject.transform;

        public CarMover(CarSwitcher carSwitcher) {
            this.CarSwitcher = carSwitcher;
        }

        public void ReadInputAndMoveCar(Keyboard keyboard, Gamepad gamepad) {
            if (keyboard != null) {
                this.CurrentCarTransform.Translate(Time.deltaTime * CarForwardBackwardSpeed * (keyboard.eKey.ReadValue() - keyboard.dKey.ReadValue()) * Vector3.forward);
                this.CurrentCarTransform.Rotate(axis: Vector3.up, Time.deltaTime * CarRotateSpeed * (keyboard.fKey.ReadValue() - keyboard.sKey.ReadValue()));
            }

            if (gamepad != null) {
                Vector2 leftStick = gamepad.leftStick.ReadValue();
                this.CurrentCarTransform.Translate(Time.deltaTime * CarForwardBackwardSpeed * leftStick.y * Vector3.forward);
                this.CurrentCarTransform.Rotate(axis: Vector3.up, Time.deltaTime * CarRotateSpeed * leftStick.x);
            }
        }
    }
}
