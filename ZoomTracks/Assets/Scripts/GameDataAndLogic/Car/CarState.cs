using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CarState {
        private const float MaxRotationSpeed_DegreesPerSecond = 700f;
        private const float MinVelocityForRotation = 1f;
        private const float AxialDeadzone = 0.075f;

        private TrackSwitcher TrackSwitcher { get; }
        private CarSwitcher CarSwitcher { get; }
        private CameraController CameraController { get; }
        private InputManager InputManager { get; }

        public Vector3 Position { get; private set; }
        private Quaternion Rotation {
            get {
                if (this.Rotation_MostRecentNonZeroVelocity is null) {
                    return this.StartingRotation;
                } else {
                    return this.Rotation_MostRecentNonZeroVelocity.Value;
                }
            }
        }
        private float Rotation_Degrees => this.Rotation.eulerAngles.y;
        private Quaternion? Rotation_MostRecentNonZeroVelocity { get; set; }
        private Vector3 Velocity { get; set; }

        private Vector3 StartingPosition { get; }
        private Quaternion StartingRotation { get; }

        public CarState(Transform placeholderCarTransform, TrackSwitcher trackSwitcher, CarSwitcher carSwitcher, CameraController cameraController, InputManager inputManager) {
            this.TrackSwitcher = trackSwitcher;
            this.CarSwitcher = carSwitcher;
            this.CameraController = cameraController;
            this.InputManager = inputManager;
            this.StartingPosition = placeholderCarTransform.position;
            this.StartingRotation = placeholderCarTransform.rotation;
            this.Reset();
        }

        // Per-axis deadzone with rescaling so there's no snap at the threshold:
        // the surviving range [deadzone, 1] is remapped to [0, 1].
        private static float DeadzoneAxis(float value, float deadzone) {
            float magnitude = Mathf.Abs(value);
            if (magnitude < deadzone) {
                return 0f;
            }
            return Mathf.Sign(value) * (magnitude - deadzone) / (1f - deadzone);
        }

        public void ReadInputAndUpdateState() {
            Gamepad gamepad = this.InputManager.Gamepad;
            if (gamepad == null) {
                return;
            }

            float brakeInput = gamepad.leftTrigger.ReadValue();
            Vector2 accelerationInput_xyPlane = gamepad.rightStick.ReadValue();
            CarDynamic carDynamic = this.CarSwitcher.CurrentCarDynamic;
            float cameraTransformEulerAngleY = this.CameraController.CameraYawWorldSpace;
            Vector3 oldVelocity = this.Velocity;

            if (brakeInput == 0) {
                if (accelerationInput_xyPlane != Vector2.zero) {
                    Vector3 accelerationInput_xzPlane = new(accelerationInput_xyPlane.x, 0, accelerationInput_xyPlane.y);
                    Vector3 accelerationInput_worldSpace = Quaternion.Euler(0, cameraTransformEulerAngleY, 0) * accelerationInput_xzPlane;
                    Vector3 accelerationInput_carSpace = Quaternion.Inverse(this.Rotation) * accelerationInput_worldSpace;
                    accelerationInput_carSpace.x = DeadzoneAxis(accelerationInput_carSpace.x, AxialDeadzone);

                    Vector3 accelerationOutput_carSpace = default;
                    if (accelerationInput_carSpace.x > 0) {
                        accelerationOutput_carSpace.x = accelerationInput_carSpace.x * carDynamic.AccelerationMap.Right;
                    } else if (accelerationInput_carSpace.x < 0) {
                        accelerationOutput_carSpace.x = accelerationInput_carSpace.x * carDynamic.AccelerationMap.Left;
                    } else {
                        accelerationOutput_carSpace.x = 0;
                    }
                    if (accelerationInput_carSpace.z > 0) {
                        accelerationOutput_carSpace.z = accelerationInput_carSpace.z * carDynamic.AccelerationMap.Forward;
                    } else if (accelerationInput_carSpace.z < 0) {
                        accelerationOutput_carSpace.z = accelerationInput_carSpace.z * carDynamic.AccelerationMap.Reverse;
                    } else {
                        accelerationOutput_carSpace.z = 0;
                    }
                    accelerationOutput_carSpace.y = 0;

                    Vector3 accelerationOutput_worldSpace = this.Rotation * accelerationOutput_carSpace;
                    Vector3 deltaVelocity_worldSpace = Time.deltaTime * accelerationOutput_worldSpace;
                    deltaVelocity_worldSpace.y = 0;
                    //deltaVelocity_worldSpace = this.PreventRotationJitter(deltaVelocity_worldSpace);
                    this.Velocity += deltaVelocity_worldSpace;
                } else {
                    // Brake and acceleration are zero, so do nothing
                }
            } else {
                if (this.Velocity == Vector3.zero) {
                    // Brake is non-zero, but velocity is already zero, so do nothing
                } else {
                    Vector3 opposingVec = (-1 * this.Velocity).normalized;
                    Vector3 deltaVelocity = carDynamic.AccelerationMap.Reverse * brakeInput * Time.deltaTime * opposingVec;
                    if (deltaVelocity.sqrMagnitude >= this.Velocity.sqrMagnitude) {
                        this.Velocity = Vector3.zero;
                    } else {
                        this.Velocity += deltaVelocity;
                    }
                }
            }

            if (carDynamic.VelocityLimiter >= 0) {
                this.Velocity = Vector3.ClampMagnitude(this.Velocity, carDynamic.VelocityLimiter);
            }

            //float deltaAngle_degrees = Vector3.Angle(previousVelocity, this.Velocity);
            //float rotationSpeed = (deltaAngle_degrees / Time.deltaTime);
            //if (rotationSpeed > MaxRotationSpeed_DegreesPerSecond) {
            //    Debug.Log(rotationSpeed);
            //}

            //this.Velocity = VectorUtility.ClampAngularSpeed(this.Rotation_Degrees, newVelocity: this.Velocity, MaxRotationSpeed_DegreesPerSecond);
            //this.Velocity = VectorUtility.ClampAngularSpeed(this.Rotation_Degrees, newVelocity: this.Velocity, maxRotationSpeed_degreesPerSecond: 10f * this.Velocity.magnitude);
            //this.Velocity = VectorUtility.ClampAngularSpeed(this.Rotation_Degrees, newVelocity: this.Velocity, maxRotationSpeed_degreesPerSecond: 100f * this.Velocity.magnitude);
            //this.Velocity = VectorUtility.ClampAngularSpeed(this.Rotation_Degrees, newVelocity: this.Velocity, maxRotationSpeed_degreesPerSecond: 100f * this.Velocity.magnitude * this.Velocity.magnitude);

            if (this.Velocity.sqrMagnitude > 0) {
                this.Rotation_MostRecentNonZeroVelocity = Quaternion.LookRotation(this.Velocity, Vector3.up);
            }
        }

        private Vector3 PreventRotationJitter(Vector3 deltaVelocity_worldSpace) {
            //float minVelocityForRotation = this.CarSwitcher.CurrentCarDynamic.MinVelocityForRotation;
            //if (minVelocityForRotation == 0) {
            //    minVelocityForRotation = this.TrackSwitcher.CurrentTrackJson.MinVelocityForRotation;
            //}

            if (this.Velocity.magnitude < MinVelocityForRotation) {
                Vector3 deltaVelocity_carSpace = Quaternion.Inverse(this.Rotation) * deltaVelocity_worldSpace;
                deltaVelocity_carSpace.x = 0;
                Vector3 newDeltaVelocity_worldSpace = this.Rotation * deltaVelocity_carSpace;
                newDeltaVelocity_worldSpace.y = 0;
                return newDeltaVelocity_worldSpace;
            } else {
                return deltaVelocity_worldSpace;
            }
        }

        public void ApplyVelocityToPositionAndRotation() {
            this.Position += this.Velocity * Time.deltaTime;
        }

        public void ApplyStateToGameObject() {
            this.CarSwitcher.CurrentCarTransform.SetPositionAndRotation(this.Position, this.Rotation);
        }

        public void Reset() {
            this.Position = this.StartingPosition;
            this.Rotation_MostRecentNonZeroVelocity = null;
            this.Velocity = Vector3.zero;
        }
    }
}
