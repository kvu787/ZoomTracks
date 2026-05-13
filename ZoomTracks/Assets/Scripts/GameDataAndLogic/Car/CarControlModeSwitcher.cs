namespace ZoomTracks {
    public enum CarControlModeEnum {
        Standard,
        Debug,
    }

    public class CarControlModeSwitcher {
        private InputManager InputManager { get; }

        public CarControlModeSwitcher(InputManager inputManager) {
            this.InputManager = inputManager;
            this.CarControlMode = CarControlModeEnum.Debug;
        }

        public CarControlModeEnum CarControlMode { get; private set; }

        public void ReadInputAndToggleControlMode() {
            if (this.InputManager.Keyboard?.tabKey.wasPressedThisFrame is true || this.InputManager.Gamepad?.startButton.wasPressedThisFrame is true) {
                if (this.CarControlMode == CarControlModeEnum.Standard) {
                    this.CarControlMode = CarControlModeEnum.Debug;
                } else if (this.CarControlMode == CarControlModeEnum.Debug) {
                    this.CarControlMode = CarControlModeEnum.Standard;
                }
            }
        }
    }
}
