using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ZoomTracks {
    public class InputManager {
        public Keyboard Keyboard { get; private set; }
        public Gamepad Gamepad { get; private set; }

        public void UpdateInputs() {
            this.Keyboard = Keyboard.current;
            this.Gamepad = Gamepad.current;
            //this.LogGamepadRightStick();
        }

        private DateTime LastLogTime = DateTime.MinValue;
        private const double TimeoutSeconds = 0.5;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "<Pending>")]
        private void LogGamepadRightStick() {
            if (this.Gamepad != null) {
                if ((DateTime.Now - this.LastLogTime) > TimeSpan.FromSeconds(TimeoutSeconds)) {
                    this.LastLogTime = DateTime.Now;
                    StickControl stickControl = this.Gamepad.rightStick;
                    Vector2 processedValue = stickControl.ReadValue();
                    Vector2 rawValue = stickControl.ReadUnprocessedValue();
                    Debug.Log($"P: magnitude={processedValue.magnitude.ToExactDecimalString()}, x={processedValue.x.ToExactDecimalString()}, y={processedValue.y.ToExactDecimalString()}");
                    Debug.Log($"R: magnitude={rawValue.magnitude.ToExactDecimalString()}, x={rawValue.x.ToExactDecimalString()}, y={rawValue.y.ToExactDecimalString()}");
                    Debug.Log("==================================");
                }
            }
        }
    }
}
