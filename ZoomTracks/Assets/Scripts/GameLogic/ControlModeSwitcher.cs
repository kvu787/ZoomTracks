using UnityEngine.InputSystem;

namespace ZoomTracks {
    public enum ControlModeEnum {
        Car,
        Camera,
    }

    public class ControlModeSwitcher {
        public ControlModeSwitcher() {
            this.ControlMode = ControlModeEnum.Car;
        }

        public ControlModeEnum ControlMode { get; private set; }

        public void ReadInputAndToggleControlMode(Keyboard keyboard, Gamepad gamepad) {
            if (keyboard?.tabKey.wasPressedThisFrame is true || gamepad?.startButton.wasPressedThisFrame is true) {
                if (this.ControlMode == ControlModeEnum.Camera) {
                    this.ControlMode = ControlModeEnum.Car;
                } else if (this.ControlMode == ControlModeEnum.Car) {
                    this.ControlMode = ControlModeEnum.Camera;
                }
            }
        }
    }
}
