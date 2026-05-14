namespace ZoomTracks {
    public enum CarControlModeEnum {
        Standard,
        Debug,
    }

    public class CarControlModeSwitcher {
        private InputManager InputManager { get; }

        public CarControlModeSwitcher(InputManager inputManager) {
            this.InputManager = inputManager;
            this.Mode = CarControlModeEnum.Standard;
        }

        private CarControlModeEnum Mode { get; set; }

        public CarControlModeEnum ReadInputAndToggleMode() {
            if (this.InputManager.Keyboard?.tabKey.wasPressedThisFrame is true || this.InputManager.Gamepad?.startButton.wasPressedThisFrame is true) {
                if (this.Mode == CarControlModeEnum.Standard) {
                    this.Mode = CarControlModeEnum.Debug;
                } else if (this.Mode == CarControlModeEnum.Debug) {
                    this.Mode = CarControlModeEnum.Standard;
                }
            }
            return this.Mode;
        }
    }
}
