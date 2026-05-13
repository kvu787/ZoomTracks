using UnityEngine;

namespace ZoomTracks {
    public class CarLogic {
        private readonly CarSwitcher CarSwitcher;
        public CarLogic(CarSwitcher carSwitcher) {
            this.CarSwitcher = carSwitcher;
        }

        public static CarState CreateNewCarState(GameObject placeholderCar) {
            return new CarState {
                Position = placeholderCar.transform.position,
                Rotation = placeholderCar.transform.rotation,
                Velocity = Vector3.zero,
            };
        }

        public void ProcessCarInputAndPhysics(CarState carState, float brake, Vector2 accel, Transform cameraYawTransform) {
            Dynamic dynamic = this.CarSwitcher.CurrentCar.Dynamic;
            if (brake == 0) {
                if (accel.magnitude > 0) {
                    // Map XY input onto XZ world plane
                    Vector3 a = new(accel.x, 0, accel.y);

                    // Adjust for camera rotation
                    Vector3 b = Quaternion.Euler(0, cameraYawTransform.eulerAngles.y, 0) * a;

                    // Orient with respect to the car
                    Vector3 c = Quaternion.Inverse(carState.Rotation) * b;

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
                    Vector3 e = carState.Rotation * d;

                    // Convert acceleration to change in velocity
                    Vector3 f = Time.deltaTime * e;

                    // Just to be safe, zero out the y coordinate
                    f.y = 0;

                    // The result is the change in velocity for this frame
                    Vector3 velocityDelta = f;

                    // Update car velocity
                    carState.Velocity += velocityDelta;
                }
            } else if (carState.Velocity != Vector3.zero) {
                Vector3 opposingVec = (-1 * carState.Velocity).normalized;
                Vector3 velocityDelta = dynamic.AccelerationMap.Reverse * brake * Time.deltaTime * opposingVec;
                if (velocityDelta.magnitude >= carState.Velocity.magnitude) {
                    carState.Velocity = Vector3.zero;
                } else {
                    carState.Velocity += velocityDelta;
                }
            }

            // update car position
            if (dynamic.VelocityLimiter >= 0) {
                carState.Velocity = Vector3.ClampMagnitude(carState.Velocity, dynamic.VelocityLimiter);
            }
            carState.Position += carState.Velocity * Time.deltaTime;

            // rotate the car to match the velocity direction
            if (carState.Velocity != Vector3.zero) {
                carState.Rotation = Quaternion.LookRotation(carState.Velocity);
            }
        }

        public static void WriteCarStateToCarObject(CarState carState, GameObject car) {
            car.transform.SetPositionAndRotation(carState.Position, carState.Rotation);
        }
    }
}
