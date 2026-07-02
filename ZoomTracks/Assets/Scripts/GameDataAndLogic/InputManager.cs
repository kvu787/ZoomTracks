using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ZoomTracks {
    public class InputManager {
        public Keyboard Keyboard { get; private set; }
        public Gamepad Gamepad { get; private set; }
        public bool InsertHitchLogSpacer { get; private set; }
        public bool ToggleBetweenBorderlessAndExclusiveFullScreen { get; private set; }

        public void UpdateInputs() {
            this.Keyboard = Keyboard.current;
            this.Gamepad = Gamepad.current;
            //this.LogGamepadRightStick();

            this.InsertHitchLogSpacer = false;
            this.InsertHitchLogSpacer |= this.Keyboard?.enterKey.wasPressedThisFrame ?? false;
            this.InsertHitchLogSpacer |= this.Gamepad?.selectButton.wasPressedThisFrame ?? false;

            this.ToggleBetweenBorderlessAndExclusiveFullScreen = false;
            this.ToggleBetweenBorderlessAndExclusiveFullScreen |= this.Keyboard?.backquoteKey.wasPressedThisFrame ?? false;
            this.ToggleBetweenBorderlessAndExclusiveFullScreen |= this.Gamepad?.rightStickButton.wasPressedThisFrame ?? false;
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

        // public struct RotationAndZoomDelta {
        //     // The "camera rotation" is equivalent to CameraPivot.localEulerAngles.y
        //     // For this frame, we will execute CameraPivot.transform.localEulerAngles.y += this.RotationDelta
        //     public float RotationDelta;

        //     // The "camera zoom" is equivalent to UnityEngine.Camera.orthographicSize.
        //     // The default orthographicSize is 30f.
        //     // The min and max orthographicSize are 1f and 281.25f.
        //     // For this frame, we will execute UnityEngine.Camera.orthographicSize += this.ZoomDelta
        //     public float ZoomDelta;
        // }

        // // The camera pivot is a transform that represents the place in the game world that the camera is pointing at.
        // // The camera is a child of the camera pivot, so when the player wants to "rotating the camera" this means rotating the yaw of the camera pivot.
        // //
        // // Roughly speaking, moving the stick left/right rotates the camera, and moving the stick up/down zooms in/out.
        // public RotationAndZoomDelta ReadStickInputAndComputeRotationAndZoomDelta(Vector2 rawUnprocessedGamepadStickInput, float timeDelta) {
        //     throw new NotImplementedException();
        // }

        // /*
        //  * Context:
        //  * 
        //  * This is for a 3/4 overhead perspective 3d game, like an RTS game or a RC racing game.
        //  * This is for Unity Engine 6000.3.x LTS.
        //  * 
        //  * Your task:
        //  * 
        //  * Provide a complete implementation for ReadStickInputAndComputeRotationAndZoomDelta
        //  * Add as many additional required or tuning parameters to the method signature as you feel necessary
        //  * Add references to other hypothetical state members if you feel it necessary
        //  * 
        //  * Keep this in mind:
        //  * I did a simple implementation for ReadStickInputAndComputeRotationAndZoomDelta that worked at a basic level,
        //  * but this "felt bad" when I tested it due to lack of deadzones and/or other things I haven't considered or realized.
        //  */
    }
}
