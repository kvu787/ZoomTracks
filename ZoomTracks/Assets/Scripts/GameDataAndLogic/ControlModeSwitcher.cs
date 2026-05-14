namespace ZoomTracks {
    public enum ControlModeEnum {
        Car,
        Camera,
    }

    public class ControlModeSwitcher {
        private InputManager InputManager { get; }

        public ControlModeSwitcher(InputManager inputManager) {
            this.InputManager = inputManager;
            this.Mode = ControlModeEnum.Car;
        }

        public ControlModeEnum Mode { get; private set; }

        public ControlModeEnum ReadInputAndToggleMode() {
            if (this.InputManager.Keyboard?.escapeKey.wasPressedThisFrame == true || this.InputManager.Gamepad?.selectButton.wasPressedThisFrame == true) {
                if (this.Mode == ControlModeEnum.Camera) {
                    this.Mode = ControlModeEnum.Car;
                } else if (this.Mode == ControlModeEnum.Car) {
                    this.Mode = ControlModeEnum.Camera;
                }
            }
            return this.Mode;
        }
    }
}
