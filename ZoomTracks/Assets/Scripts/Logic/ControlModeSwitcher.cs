using UnityEngine.InputSystem;

namespace ZoomTracks {
    public enum ControlModeEnum {
        DebugMoveCar,
        Camera,
    }

    public class ControlModeSwitcher {
        public ControlModeSwitcher() {
            this.ControlMode = ControlModeEnum.DebugMoveCar;
        }

        public ControlModeEnum ControlMode { get; private set; }

        public void UpdateControlMode(Keyboard keyboard, Gamepad gamepad) {
            if (keyboard?.tabKey.isPressed is true || gamepad?.startButton.wasPressedThisFrame is true) {
                if (this.ControlMode == ControlModeEnum.Camera) {
                    this.ControlMode = ControlModeEnum.DebugMoveCar;
                } else if (this.ControlMode == ControlModeEnum.DebugMoveCar) {
                    this.ControlMode = ControlModeEnum.Camera;
                }
            }
        }
    }
}
