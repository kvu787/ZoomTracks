using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CarMover {
        private const float CarForwardBackwardSpeed = 150;
        private const float CarRotateSpeed = 540;

        private readonly TrackObjects TrackObjects;

        public CarMover(TrackObjects trackObjects) {
            this.TrackObjects = trackObjects;
        }

        public void UpdateCarPosition(Keyboard keyboard, Gamepad gamepad) {
            if (keyboard != null) {
                if (keyboard.eKey.isPressed) {
                    this.TrackObjects.Car.transform.Translate(Time.deltaTime * CarForwardBackwardSpeed * Vector3.forward);
                }
                if (keyboard.dKey.isPressed) {
                    this.TrackObjects.Car.transform.Translate(Time.deltaTime * CarForwardBackwardSpeed * Vector3.back);
                }
                if (keyboard.sKey.isPressed) {
                    this.TrackObjects.Car.transform.Rotate(axis: Vector3.up, -1 * Time.deltaTime * CarRotateSpeed);
                }
                if (keyboard.fKey.isPressed) {
                    this.TrackObjects.Car.transform.Rotate(axis: Vector3.up, Time.deltaTime * CarRotateSpeed);
                }
            }

            if (gamepad != null) {
                Vector2 leftStick = gamepad.leftStick.ReadValue();
                this.TrackObjects.Car.transform.Translate(Time.deltaTime * CarForwardBackwardSpeed * leftStick.y * Vector3.forward);
                this.TrackObjects.Car.transform.Rotate(axis: Vector3.up, Time.deltaTime * leftStick.x * CarRotateSpeed);
            }
        }
    }
}
