using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CarState {
        /// <summary>
        /// A "hardware deadzone" refers to deadzone processing done by the controller hardware prior to sending input data via USB or wireless connection to the PC.
        /// A "game deadzone" refers to deadzone processing by the game software via Unity built-in code or this custom code.
        /// 
        /// 1. For an 8BitDo Ultimate 2 Wireless controller:
        ///    * This controller has hardware deadzones that are enabled by default.
        ///    * The at-rest analog stick value is always reported as 0.0000152587890625 for both X and Y.
        ///      * (I don't know why it is 0.0000152587890625 instead of just 0.0.)
        ///    * Therefore, if the hardware deadzone is enabled and set high enough, then a minimum safe game inner deadzone is 0.0001, which is effectively 0.0.
        ///    * However, if the hardware deadzone is disabled or enabled but set low enough, then you will need a high enough game inner deadzone.
        /// 
        /// 2. For a Razer Wolverine Pro 8K PC controller:
        ///    * Turn off "Prevent Double Deadzones".
        ///    * Same as 1.
        ///    
        /// 3. For a Gamesir G7 Pro 8K PC controller:
        ///    * Same as 1.
        ///
        /// 4. For a standard Xbox Series controller:
        ///    * This controller does not have hardware deadzones.
        ///    * This means the at-rest analog stick value will bounce around from 0.00 to +/-0.02.
        ///    * A minimum safe game inner deadzone is 0.03.
        ///
        /// 5. For a standard PlayStation 5 DualSense controller:
        ///    * Same as 3.
        ///
        /// However, just because a controller's minimum safe game inner deadzone is N doesn't mean it should be set to N.
        /// I have set the inner deadzone value to 0.05, which is well above all the minimums for the controllers I use,
        /// because my thumb's precision is too janky below 0.05.
        ///
        /// In general, a player should start by setting the inner deadzone to the minimum for their controller.
        /// Then, they should test it out and increase the deadzone in small increments (~0.01) until they have
        /// good control of the thumbstick even at its smallest actuations.
        /// </summary>
        private const float AxialDeadzoneInner = 0.05f;

        /// <summary>
        /// This value (0.95) seems to work fine with all controllers I tested. 
        /// </summary>
        private const float AxialDeadzoneOuter = 0.95f;

        private CarSwitcher CarSwitcher { get; }
        private CameraController CameraController { get; }
        private InputManager InputManager { get; }
        private TimeManager TimeManager { get; }

        private Vector3 StartingPosition { get; }
        private float StartingRotation { get; }

        public Vector3 Position { get; private set; }
        private float? Rotation_ForMostRecentNonZeroVelocity { get; set; }
        private Vector3 Velocity { get; set; }

        private float Rotation => this.Rotation_ForMostRecentNonZeroVelocity ?? this.StartingRotation;

        public CarState(Transform placeholderCarTransform, CarSwitcher carSwitcher, CameraController cameraController, InputManager inputManager, TimeManager timeManager) {
            this.CarSwitcher = carSwitcher;
            this.CameraController = cameraController;
            this.InputManager = inputManager;
            this.TimeManager = timeManager;

            this.StartingPosition = placeholderCarTransform.position;
            this.StartingRotation = placeholderCarTransform.rotation.eulerAngles.y;

            this.Reset_PositionRotationVelocity();
        }

        public void ReadInputAndUpdateState() {
            Gamepad gamepad = this.InputManager.Gamepad;
            if (gamepad == null) {
                return;
            }

            float brakeInput = gamepad.leftTrigger.ReadValue();
            Vector2 accelerationInput_xyPlane = gamepad.rightStick.ReadUnprocessedValue();
            CarDynamic carDynamic = this.CarSwitcher.CurrentCarDynamic;
            float cameraYaw = this.CameraController.CameraYaw;

            if (brakeInput == 0f) {
                Vector3 accelerationInput_xzPlane = new(accelerationInput_xyPlane.x, 0f, accelerationInput_xyPlane.y);
                Vector3 accelerationInput_worldSpace = accelerationInput_xzPlane.Rotate2D(cameraYaw);

                Vector3 accelerationInput_carSpace = accelerationInput_worldSpace.Rotate2D(-1f * this.Rotation);
                accelerationInput_carSpace.x = InputUtility.AxialDeadzone(accelerationInput_carSpace.x, AxialDeadzoneInner, AxialDeadzoneOuter);
                accelerationInput_carSpace.y = 0f;
                accelerationInput_carSpace.z = InputUtility.AxialDeadzone(accelerationInput_carSpace.z, AxialDeadzoneInner, AxialDeadzoneOuter);
                accelerationInput_carSpace = Vector3.ClampMagnitude(accelerationInput_carSpace, 1f);

                if (accelerationInput_carSpace != Vector3.zero) {
                    Vector3 accelerationOutput_carSpace = default;
                    if (accelerationInput_carSpace.x > 0f) {
                        accelerationOutput_carSpace.x = accelerationInput_carSpace.x * carDynamic.AccelerationMap.Right;
                    } else if (accelerationInput_carSpace.x < 0f) {
                        accelerationOutput_carSpace.x = accelerationInput_carSpace.x * carDynamic.AccelerationMap.Left;
                    } else {
                        accelerationOutput_carSpace.x = 0f;
                    }
                    accelerationOutput_carSpace.y = 0f;
                    if (accelerationInput_carSpace.z > 0f) {
                        accelerationOutput_carSpace.z = accelerationInput_carSpace.z * carDynamic.AccelerationMap.Forward;
                    } else if (accelerationInput_carSpace.z < 0f) {
                        accelerationOutput_carSpace.z = accelerationInput_carSpace.z * carDynamic.AccelerationMap.Reverse;
                    } else {
                        accelerationOutput_carSpace.z = 0f;
                    }

                    Vector3 accelerationOutput_worldSpace = accelerationOutput_carSpace.Rotate2D(this.Rotation);
                    Vector3 deltaVelocity_worldSpace = this.TimeManager.DeltaTime * accelerationOutput_worldSpace;
                    this.Velocity += deltaVelocity_worldSpace;
                } else {
                    // Brake and acceleration are zero, so do nothing
                }
            } else {
                if (this.Velocity == Vector3.zero) {
                    // Brake is non-zero, but velocity is already zero, so do nothing
                } else {
                    float velocitySqrMagnitude = this.Velocity.sqrMagnitude;
                    if (velocitySqrMagnitude < 0.0001f) {
                        this.Velocity = Vector3.zero;
                    } else {
                        Vector3 brakeDirection = (-1f * this.Velocity).normalized;
                        Vector3 brakeDeltaVelocity = carDynamic.AccelerationMap.Reverse * brakeInput * this.TimeManager.DeltaTime * brakeDirection;
                        if (brakeDeltaVelocity.sqrMagnitude >= velocitySqrMagnitude) {
                            this.Velocity = Vector3.zero;
                        } else {
                            this.Velocity += brakeDeltaVelocity;
                        }
                    }
                }
            }

            if (carDynamic.VelocityLimiter > 0f) {
                this.Velocity = Vector3.ClampMagnitude(this.Velocity, carDynamic.VelocityLimiter);
            }

            if (this.Velocity != Vector3.zero) {
                this.Rotation_ForMostRecentNonZeroVelocity = this.Velocity.Get2DRotation();
            }

            this.Position += this.Velocity * this.TimeManager.DeltaTime;
        }

        public void ApplyStateToGameObject() {
            this.CarSwitcher.CurrentCarTransform.SetPositionAndRotation(this.Position, Quaternion.Euler(0f, this.Rotation, 0f));
        }

        public void Reset_PositionRotationVelocity() {
            this.Position = this.StartingPosition;
            this.Rotation_ForMostRecentNonZeroVelocity = null;
            this.Velocity = Vector3.zero;
        }
    }
}
