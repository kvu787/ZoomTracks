using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CarState {
        private const float CarForwardBackwardSpeed = 150;
        private const float CarRotateSpeed = 540;

        private Transform PlaceholderCarTransform { get; }
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

        public CarState(Transform placeholderCarTransform, CarSwitcher carSwitcher, CameraController cameraController, InputManager inputManager) {
            this.PlaceholderCarTransform = placeholderCarTransform;
            this.CarSwitcher = carSwitcher;
            this.CameraController = cameraController;
            this.InputManager = inputManager;
            this.Reset();
        }

        // cameraTransformEulerAngleY must be in world space
        // cameraTransformEulerAngleY = GameObject.Find("Camera").GetComponent<Camera>().transform.eulerAngles.y
        public void ReadInputAndUpdateState_Standard() {
            Gamepad gamepad = this.InputManager.Gamepad;
            if (gamepad == null) {
                return;
            }

            float brakeInput = gamepad.rightTrigger.ReadValue();
            Vector2 accelerationInput = gamepad.rightStick.IsActuated() ? gamepad.rightStick.ReadValue() : gamepad.leftStick.ReadValue();
            CarDynamic carDynamic = this.CarSwitcher.CurrentCarDynamic;
            float cameraTransformEulerAngleY = this.CameraController.CameraYawWorldSpace;

            if (brakeInput == 0) {
                if (accelerationInput.magnitude > 0) {
                    // Map XY input onto XZ world plane
                    Vector3 a = new(accelerationInput.x, 0, accelerationInput.y);

                    // Adjust for camera yaw
                    Vector3 b = Quaternion.Euler(0, cameraTransformEulerAngleY, 0) * a;

                    // Orient with respect to the car
                    Vector3 c = Quaternion.Inverse(this.RotationQuaternion) * b;

                    // Apply acceleration map
                    Vector3 d = c;
                    if (d.x > 0) {
                        d.x *= carDynamic.AccelerationMap.Right;
                    } else if (d.x < 0) {
                        d.x *= carDynamic.AccelerationMap.Left;
                    }
                    if (d.z > 0) {
                        d.z *= carDynamic.AccelerationMap.Forward;
                    } else if (d.z < 0) {
                        d.z *= carDynamic.AccelerationMap.Reverse;
                    }

                    // Rotate back to world space
                    Vector3 e = this.RotationQuaternion * d;

                    // Convert acceleration to change in velocity
                    Vector3 f = Time.deltaTime * e;

                    // Just to be safe, zero out the y coordinate
                    f.y = 0;

                    // The result is the change in velocity for this frame
                    Vector3 velocityDelta = f;

                    // Update car velocity
                    this.Velocity += velocityDelta;
                } else {
                    // Brake and acceleration is zero, so do nothing
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

        public void ApplyVelocityToPositionAndRotation() {
            // Apply velocity to position
            this.Position += this.Velocity * Time.deltaTime;

            if (this.Velocity != Vector3.zero) {
                // Rotate to match the velocity direction
                this.Rotation = Quaternion.LookRotation(this.Velocity, Vector3.up).eulerAngles.y;
            }
        }

        public void ReadInputAndUpdateState_Debug() {
            this.Velocity = Vector3.zero;

            Vector3 positionDelta = Vector3.zero;
            float rotationDelta = 0;
            Vector3 positionTerm = Time.deltaTime * CarForwardBackwardSpeed * (this.RotationQuaternion * Vector3.forward);
            float rotationTerm = Time.deltaTime * CarRotateSpeed;

            if (this.InputManager.Keyboard != null) {
                Keyboard keyboard = this.InputManager.Keyboard;
                positionDelta += positionTerm * (keyboard.eKey.ReadValue() - keyboard.dKey.ReadValue());
                rotationDelta += rotationTerm * (keyboard.fKey.ReadValue() - keyboard.sKey.ReadValue());
            }

            if (this.InputManager.Gamepad != null) {
                Vector2 leftStick = this.InputManager.Gamepad.leftStick.ReadValue();
                positionDelta += positionTerm * leftStick.y;
                rotationDelta += rotationTerm * leftStick.x;
            }

            this.Position += positionDelta;
            this.Rotation += rotationDelta;
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
