using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CarState {
        // Inner deadzone can be as small as 0.02 for Razer and 8bitdo controllers.
        // private const float AxialDeadzoneInner = 0.02f;
        private const float AxialDeadzoneInner = 0.05f;
        private const float AxialDeadzoneOuter = 0.95f;

        private CarSwitcher CarSwitcher { get; }
        private CameraController CameraController { get; }
        private InputManager InputManager { get; }

        private Vector3 StartingPosition { get; }
        private float StartingRotation { get; }

        public Vector3 Position { get; private set; }
        private float? Rotation_ForMostRecentNonZeroVelocity { get; set; }
        private Vector3 Velocity { get; set; }

        private float Rotation => this.Rotation_ForMostRecentNonZeroVelocity ?? this.StartingRotation;

        public CarState(Transform placeholderCarTransform, CarSwitcher carSwitcher, CameraController cameraController, InputManager inputManager) {
            this.CarSwitcher = carSwitcher;
            this.CameraController = cameraController;
            this.InputManager = inputManager;

            this.StartingPosition = placeholderCarTransform.position;
            this.StartingRotation = placeholderCarTransform.rotation.eulerAngles.y;

            this.Reset_PositionRotationVelocity();
        }

        public static float AxialDeadzone(float value, float innerDeadzone, float outerDeadzone) {
            if (!(0f <= innerDeadzone && innerDeadzone <= 1f)) {
                throw new ArgumentException($"Expected: innerDeadzone must be in [0, 1]. Got: innerDeadzone={innerDeadzone}.");
            }
            if (!(0f <= outerDeadzone && outerDeadzone <= 1f)) {
                throw new ArgumentException($"Expected: outerDeadzone must be in [0, 1]. Got: outerDeadzone={outerDeadzone}.");
            }
            if (innerDeadzone >= outerDeadzone) {
                throw new ArgumentException($"Expected: innerDeadzone must be less than outerDeadzone. Got: innerDeadzone={innerDeadzone}, outerDeadzone={outerDeadzone}.");
            }

            if (value == 0f) {
                return 0f;
            }

            float magnitude = Mathf.Abs(value);
            if (magnitude < innerDeadzone) {
                return 0f;
            } else {
                float sign = Mathf.Sign(value);
                if (magnitude > outerDeadzone) {
                    return sign;
                } else {
                    return sign * ((magnitude - innerDeadzone) / (outerDeadzone - innerDeadzone));
                }
            }
        }

        public void ReadInputAndUpdateState() {
            Gamepad gamepad = this.InputManager.Gamepad;
            if (gamepad == null) {
                return;
            }

            float brakeInput = gamepad.leftTrigger.ReadValue();
            Vector2 accelerationInput_xyPlane = Gamepad.current.rightStick.ReadUnprocessedValue();
            CarDynamic carDynamic = this.CarSwitcher.CurrentCarDynamic;
            float cameraYaw = this.CameraController.CameraYaw;

            if (brakeInput == 0f) {
                Vector3 accelerationInput_xzPlane = new(accelerationInput_xyPlane.x, 0f, accelerationInput_xyPlane.y);
                Vector3 accelerationInput_worldSpace = accelerationInput_xzPlane.Rotate2D(cameraYaw);

                Vector3 accelerationInput_carSpace = accelerationInput_worldSpace.Rotate2D(-1f * this.Rotation);
                accelerationInput_carSpace.x = AxialDeadzone(accelerationInput_carSpace.x, AxialDeadzoneInner, AxialDeadzoneOuter);
                accelerationInput_carSpace.y = 0f;
                accelerationInput_carSpace.z = AxialDeadzone(accelerationInput_carSpace.z, AxialDeadzoneInner, AxialDeadzoneOuter);
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
                    Vector3 deltaVelocity_worldSpace = Time.deltaTime * accelerationOutput_worldSpace;
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
                        Vector3 brakeDeltaVelocity = carDynamic.AccelerationMap.Reverse * brakeInput * Time.deltaTime * brakeDirection;
                        if (brakeDeltaVelocity.sqrMagnitude >= velocitySqrMagnitude) {
                            this.Velocity = Vector3.zero;
                        } else {
                            this.Velocity += brakeDeltaVelocity;
                        }
                    }
                }
            }

            if (carDynamic.VelocityLimiter >= 0f) {
                this.Velocity = Vector3.ClampMagnitude(this.Velocity, carDynamic.VelocityLimiter);
            }

            if (this.Velocity != Vector3.zero) {
                this.Rotation_ForMostRecentNonZeroVelocity = this.Velocity.Get2DRotation();
            }

            this.Position += this.Velocity * Time.deltaTime;
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
