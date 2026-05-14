using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CarState {
        private const float CarForwardBackwardSpeed = 150;
        private const float CarRotateSpeed = 540;

        private InputManager InputManager { get; }

        /// <summary>
        /// World space
        /// </summary>
        private Vector3 Position { get; set; }

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

        public CarState(InputManager inputManager) {
            this.InputManager = inputManager;
        }

        // cameraTransformEulerAngleY must be in world space
        // cameraTransformEulerAngleY = GameObject.Find("Camera").GetComponent<Camera>().transform.eulerAngles.y
        public void ReadInputAndUpdateStandard(Dynamic dynamic, float brakeInput, Vector2 accelerationInput, float cameraTransformEulerAngleY) {
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
                        d.x *= dynamic.AccelerationMap.Right;
                    } else if (d.x < 0) {
                        d.x *= dynamic.AccelerationMap.Left;
                    }
                    if (d.z > 0) {
                        d.z *= dynamic.AccelerationMap.Forward;
                    } else if (d.z < 0) {
                        d.z *= dynamic.AccelerationMap.Reverse;
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
                    Vector3 velocityDelta = dynamic.AccelerationMap.Reverse * brakeInput * Time.deltaTime * opposingVec;
                    if (velocityDelta.magnitude >= this.Velocity.magnitude) {
                        this.Velocity = Vector3.zero;
                    } else {
                        this.Velocity += velocityDelta;
                    }
                }
            }

            if (dynamic.VelocityLimiter >= 0) {
                // Limit velocity
                this.Velocity = Vector3.ClampMagnitude(this.Velocity, dynamic.VelocityLimiter);
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

        public void ReadInputAndUpdateDebug() {
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

        public void ApplyToGameObject(GameObject gameObject) {
            gameObject.transform.SetPositionAndRotation(this.Position, this.RotationQuaternion);
        }

        public void Reset(Transform placeholderCarTransform) {
            this.Position = placeholderCarTransform.position;
            this.Rotation = placeholderCarTransform.rotation.eulerAngles.y;
            this.Velocity = Vector3.zero;
        }
    }
}
