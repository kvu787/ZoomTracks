using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class InputManager {
        public Keyboard Keyboard { get; private set; }
        public Gamepad Gamepad { get; private set; }

        public void UpdateBeforeAll() {
            this.Keyboard = Keyboard.current;
            this.Gamepad = Gamepad.current;
        }
    }
}
