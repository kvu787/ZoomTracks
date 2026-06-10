using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CarState {
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
        private Vector3 TranslationalVelocity { get; set; }

        private float DriftAngle { get; set; }

        /// <summary>
        /// Positive = Clockwise
        /// Negative = Counter-clockwise
        /// </summary>
        private float RotationalVelocity { get; set; }

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
            Vector2 translationalAccelerationInput_xyPlanar = gamepad.leftStick.ReadValue();
            CarDynamic carDynamic = this.CarSwitcher.CurrentCarDynamic;
            float cameraTransformEulerAngleY = this.CameraController.CameraYawWorldSpace;
            Vector3 previousVelocity = this.TranslationalVelocity;

            if (brakeInput == 0) {
                if (translationalAccelerationInput_xyPlanar.magnitude > 0) {
                    Vector3 accelerationInput_xzPlanar = new(translationalAccelerationInput_xyPlanar.x, 0, translationalAccelerationInput_xyPlanar.y);
                    Vector3 accelerationInput_worldSpace = Quaternion.Euler(0, cameraTransformEulerAngleY, 0) * accelerationInput_xzPlanar;
                    Vector3 translationalAccelerationInput_carSpace = Quaternion.Inverse(this.RotationQuaternion) * accelerationInput_worldSpace;

                    Vector3 translationalAccelerationOutput_carSpace = default;
                    if (translationalAccelerationInput_carSpace.x > 0) {
                        translationalAccelerationOutput_carSpace.x = translationalAccelerationInput_carSpace.x * carDynamic.AccelerationMap.Right;
                    } else if (translationalAccelerationInput_carSpace.x < 0) {
                        translationalAccelerationOutput_carSpace.x = translationalAccelerationInput_carSpace.x * carDynamic.AccelerationMap.Left;
                    } else {
                        translationalAccelerationOutput_carSpace.x = 0;
                    }
                    if (translationalAccelerationInput_carSpace.z > 0) {
                        translationalAccelerationOutput_carSpace.z = translationalAccelerationInput_carSpace.z * carDynamic.AccelerationMap.Forward;
                    } else if (translationalAccelerationInput_carSpace.z < 0) {
                        translationalAccelerationOutput_carSpace.z = translationalAccelerationInput_carSpace.z * carDynamic.AccelerationMap.Reverse;
                    } else {
                        translationalAccelerationOutput_carSpace.z = 0;
                    }
                    translationalAccelerationOutput_carSpace.y = 0;

                    Vector3 translationalAccelerationOutput_worldSpace = this.RotationQuaternion * translationalAccelerationOutput_carSpace;
                    Vector3 deltaTranslationalVelocity_worldSpace = Time.deltaTime * translationalAccelerationOutput_worldSpace;
                    deltaTranslationalVelocity_worldSpace.y = 0;
                    this.TranslationalVelocity += deltaTranslationalVelocity_worldSpace;
                } else {
                    // Brake and acceleration are zero, so do nothing
                }
            } else {
                if (this.TranslationalVelocity == Vector3.zero) {
                    // Brake is non-zero, but velocity is already zero, so do nothing
                } else {
                    Vector3 opposingVec = (-1 * this.TranslationalVelocity).normalized;
                    Vector3 velocityDelta = carDynamic.AccelerationMap.Reverse * brakeInput * Time.deltaTime * opposingVec;
                    if (velocityDelta.magnitude >= this.TranslationalVelocity.magnitude) {
                        this.TranslationalVelocity = Vector3.zero;
                    } else {
                        this.TranslationalVelocity += velocityDelta;
                    }
                }
            }

            {
                // Get drift input
                Vector2 driftInput_xyPlane = gamepad.rightStick.ReadValue();
                Vector3 driftInput_xzPlane = new(driftInput_xyPlane.x, 0f, driftInput_xyPlane.y);
                Vector3 driftInput_worldSpace = Quaternion.Euler(0f, cameraTransformEulerAngleY, 0f) * driftInput_xzPlane;
                Vector3 driftInput_carSpace = Quaternion.Inverse(this.RotationQuaternion) * driftInput_worldSpace;

                // Remove Z-component and apply drift map
                this.DriftAngle = driftInput_carSpace.x * 60f;
                this.Rotation += this.DriftAngle * Time.deltaTime * 1f;
            }

            //{
            //    // Get drift input
            //    Vector2 driftInput = gamepad.leftStick.ReadValue();

            //    // Map XY input onto XZ world plane
            //    Vector3 a = new(driftInput.x, 0, driftInput.y);

            //    // Convert to world-space
            //    Vector3 b = Quaternion.Euler(0, cameraTransformEulerAngleY, 0) * a;

            //    // Convert to car-space
            //    Vector3 c = Quaternion.Inverse(this.RotationQuaternion) * b;

            //    // Remove Z-component and apply drift map
            //    Vector3 d = new(c.x, 0f, 0f);
            //    float driftMap = 1f;
            //    d *= driftMap * previousVelocity.magnitude;
            //    this.Drift = d.x;

            //    // Convert back to world-space
            //    Vector3 e = this.RotationQuaternion * d;

            //    // Convert acceleration to change in velocity
            //    Vector3 f = Time.deltaTime * e;

            //    // Just to be safe, zero out the y coordinate
            //    f.y = 0;

            //    // The result is the change in velocity for this frame
            //    Vector3 velocityDelta = f;

            //    // Update car velocity
            //    this.Velocity += velocityDelta;
            //}

            //float speed = 500f;
            //this.Velocity = Quaternion.Euler(0f, speed * Time.deltaTime * gamepad.leftStick.x.ReadValue(), 0f) * this.Velocity;

            //if (carDynamic.VelocityLimiter >= 0) {
            //    // Limit velocity
            //    this.Velocity = Vector3.ClampMagnitude(this.Velocity, carDynamic.VelocityLimiter);
            //}
        }

        private Vector3 PreventRotationJitter(Vector3 velocityDelta) {
            float minVelocityForRotation = this.CarSwitcher.CurrentCarDynamic.MinVelocityForRotation;
            if (minVelocityForRotation == 0) {
                minVelocityForRotation = this.TrackSwitcher.CurrentTrackJson.MinVelocityForRotation;
            }
            if (this.TranslationalVelocity.magnitude < minVelocityForRotation) {
                Vector3 carSpaceVelocityDelta = Quaternion.Inverse(this.RotationQuaternion) * velocityDelta;
                // Zero out the Vector3.left and Vector3.right (with respect to the car yaw) component of the velocity delta
                carSpaceVelocityDelta.x = 0;
                Vector3 newVelocityDelta = this.RotationQuaternion * carSpaceVelocityDelta;
                return newVelocityDelta;
            } else {
                return velocityDelta;
            }
        }

        public void ApplyVelocities() {
            // Apply velocity to position
            this.Position += this.TranslationalVelocity * Time.deltaTime;

            if (this.TranslationalVelocity != Vector3.zero) {
                // Rotate to match the velocity direction
                this.Rotation = Quaternion.LookRotation(this.TranslationalVelocity, Vector3.up).eulerAngles.y;
            }
        }

        public void ApplyStateToGameObject() {
            this.CarSwitcher.CurrentCarTransform.SetPositionAndRotation(this.Position, this.RotationQuaternion * Quaternion.Euler(0f, this.DriftAngle, 0f));
        }

        public void Reset() {
            this.Position = this.PlaceholderCarTransform.position;
            this.Rotation = this.PlaceholderCarTransform.rotation.eulerAngles.y;
            this.TranslationalVelocity = Vector3.zero;
            this.RotationalVelocity = 0f;
        }
    }
}
