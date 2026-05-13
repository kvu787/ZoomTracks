namespace ZoomTracks {
    public enum ControlModeEnum {
        Car,
        Camera,
    }

    public class ControlModeSwitcher {
        private InputManager InputManager { get; }

        public ControlModeSwitcher(InputManager inputManager) {
            this.InputManager = inputManager;
            this.ControlMode = ControlModeEnum.Car;
        }

        public ControlModeEnum ControlMode { get; private set; }

        public void ReadInputAndToggleControlMode() {
            if (this.InputManager.Keyboard?.escapeKey.wasPressedThisFrame is true || this.InputManager.Gamepad?.selectButton.wasPressedThisFrame is true) {
                if (this.ControlMode == ControlModeEnum.Camera) {
                    this.ControlMode = ControlModeEnum.Car;
                } else if (this.ControlMode == ControlModeEnum.Car) {
                    this.ControlMode = ControlModeEnum.Camera;
                }
            }
        }
    }
}
