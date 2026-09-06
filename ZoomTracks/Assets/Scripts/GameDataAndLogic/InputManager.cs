using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ZoomTracks {
    public class InputManager {
        public Keyboard Keyboard { get; private set; }
        public Gamepad Gamepad { get; private set; }

        public bool QuitGame { get; private set; }

        public bool PreviousTrack { get; private set; }
        public bool NextTrack { get; private set; }
        public bool PreviousCar { get; private set; }
        public bool NextCar { get; private set; }

        public bool ToggleBetweenFixedAndFollowCamera { get; private set; }

        public bool ToggleBetweenBorderlessAndExclusiveFullScreen { get; private set; }
        public bool NextMsaaMode { get; private set; }
        public bool NextTaaMode { get; private set; }
        public bool NextVsyncMode { get; private set; }
        public bool NextRenderScale { get; private set; }

        public bool ResetCar { get; private set; }

        public bool InsertStutterLogSpacer { get; private set; }

        public void UpdateInputs() {
            this.Keyboard = Keyboard.current;
            this.Gamepad = Gamepad.current;
            //this.LogGamepadRightStick();

            this.QuitGame = (this.Keyboard?.escapeKey.wasPressedThisFrame is true) || (this.Gamepad?.startButton.wasPressedThisFrame is true);

            this.PreviousTrack = this.Gamepad?.dpad.down.wasPressedThisFrame is true;
            this.NextTrack = this.Gamepad?.dpad.up.wasPressedThisFrame is true;
            this.PreviousCar = this.Gamepad?.dpad.left.wasPressedThisFrame is true;
            this.NextCar = this.Gamepad?.dpad.right.wasPressedThisFrame is true;

            this.ToggleBetweenFixedAndFollowCamera = this.Gamepad?.selectButton.wasPressedThisFrame is true;

            this.ToggleBetweenBorderlessAndExclusiveFullScreen = false;

            bool isLeftShoulderPressed = this.Gamepad?.leftShoulder.isPressed is true;
            this.NextMsaaMode = isLeftShoulderPressed && this.Gamepad.aButton.wasPressedThisFrame;
            this.NextTaaMode = isLeftShoulderPressed && this.Gamepad.bButton.wasPressedThisFrame;
            this.NextVsyncMode = isLeftShoulderPressed && this.Gamepad.xButton.wasPressedThisFrame;
            this.NextRenderScale = isLeftShoulderPressed && this.Gamepad.yButton.wasPressedThisFrame;

            this.ResetCar = this.Gamepad != null && !this.Gamepad.leftShoulder.isPressed && this.Gamepad.xButton.wasPressedThisFrame;

            this.InsertStutterLogSpacer = false;
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
