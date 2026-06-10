using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CarState {
        private const float CarForwardBackwardSpeed = 150;
        private const float CarRotateSpeed = 540;

        private Transform PlaceholderCarTransform { get; }
        private TrackSwitcher TrackSwitcher { get; }
        private CarSwitcher CarSwitcher { get; }
        private CameraController CameraController { get; }
        private InputManager InputManager { get; }

        /// <summary>
        /// World space
        /// </summary>
        public Vector3 Position { get; private set; }

        /// <summary>
        /// World space
        /// </summary>
        private float Rotation { get; set; }

        /// <summary>
        /// World space
        /// </summary>
        private Quaternion RotationQuaternion => Quaternion.Euler(0f, this.Rotation, 0f);

        /// <summary>
        /// World space
        /// </summary>
        private Vector3 Velocity { get; set; }

        public CarState(Transform placeholderCarTransform, TrackSwitcher trackSwitcher, CarSwitcher carSwitcher, CameraController cameraController, InputManager inputManager) {
            this.PlaceholderCarTransform = placeholderCarTransform;
            this.TrackSwitcher = trackSwitcher;
            this.CarSwitcher = carSwitcher;
            this.CameraController = cameraController;
            this.InputManager = inputManager;
            this.Reset();
        }

        // cameraTransformEulerAngleY must be in world space
        // cameraTransformEulerAngleY = GameObject.Find("Camera").GetComponent<Camera>().transform.eulerAngles.y
        public void ReadInputAndUpdateState() {
            Gamepad gamepad = this.InputManager.Gamepad;
            if (gamepad == null) {
                return;
            }

            float brakeInput = gamepad.bButton.ReadValue();
            Vector2 accelerationInput_xyPlanar = gamepad.leftStick.ReadValue();
            CarDynamic carDynamic = this.CarSwitcher.CurrentCarDynamic;
            float cameraTransformEulerAngleY = this.CameraController.CameraYawWorldSpace;

            if (brakeInput == 0) {
                if (accelerationInput_xyPlanar.magnitude > 0) {
                    Vector3 accelerationInput_xzPlanar = new(accelerationInput_xyPlanar.x, 0, accelerationInput_xyPlanar.y);
                    Vector3 accelerationInput_worldSpace = Quaternion.Euler(0, cameraTransformEulerAngleY, 0) * accelerationInput_xzPlanar;
                    Vector3 accelerationInput_carSpace = Quaternion.Inverse(this.RotationQuaternion) * accelerationInput_worldSpace;

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

                    Vector3 accelerationOutput_worldSpace = this.RotationQuaternion * accelerationOutput_carSpace;
                    Vector3 deltaVelocity_worldSpace = Time.deltaTime * accelerationOutput_worldSpace;
                    deltaVelocity_worldSpace.y = 0;
                    deltaVelocity_worldSpace = this.PreventRotationJitter(deltaVelocity_worldSpace);
                    this.Velocity += deltaVelocity_worldSpace;
                } else {
                    // Brake and acceleration are zero, so do nothing
                }
            } else {
                if (this.Velocity == Vector3.zero) {
                    // Brake is non-zero, but velocity is already zero, so do nothing
                } else {
                    Vector3 opposingVec = (-1 * this.Velocity).normalized;
                    Vector3 velocityDelta = carDynamic.AccelerationMap.Reverse * brakeInput * Time.deltaTime * opposingVec;
                    if (velocityDelta.magnitude >= this.Velocity.magnitude) {
                        this.Velocity = Vector3.zero;
                    } else {
                        this.Velocity += velocityDelta;
                    }
                }
            }

            if (carDynamic.VelocityLimiter >= 0) {
                // Limit velocity
                this.Velocity = Vector3.ClampMagnitude(this.Velocity, carDynamic.VelocityLimiter);
            }
        }

        private Vector3 PreventRotationJitter(Vector3 velocityDelta) {
            float minVelocityForRotation = this.CarSwitcher.CurrentCarDynamic.MinVelocityForRotation;
            if (minVelocityForRotation == 0) {
                minVelocityForRotation = this.TrackSwitcher.CurrentTrackJson.MinVelocityForRotation;
            }
            if (this.Velocity.magnitude < minVelocityForRotation) {
                Vector3 carSpaceVelocityDelta = Quaternion.Inverse(this.RotationQuaternion) * velocityDelta;
                // Zero out the Vector3.left and Vector3.right (with respect to the car yaw) component of the velocity delta
                carSpaceVelocityDelta.x = 0;
                Vector3 newVelocityDelta = this.RotationQuaternion * carSpaceVelocityDelta;
                return newVelocityDelta;
            } else {
                return velocityDelta;
            }
        }

        public void ApplyVelocityToPositionAndRotation() {
            // Apply velocity to position
            this.Position += this.Velocity * Time.deltaTime;

            if (this.Velocity != Vector3.zero) {
                // Rotate to match the velocity direction
                this.Rotation = Quaternion.LookRotation(this.Velocity, Vector3.up).eulerAngles.y;
            }
        }

        public void ApplyStateToGameObject() {
            this.CarSwitcher.CurrentCarTransform.SetPositionAndRotation(this.Position, this.RotationQuaternion);
        }

        public void Reset() {
            this.Position = this.PlaceholderCarTransform.position;
            this.Rotation = this.PlaceholderCarTransform.rotation.eulerAngles.y;
            this.Velocity = Vector3.zero;
        }
    }
}
