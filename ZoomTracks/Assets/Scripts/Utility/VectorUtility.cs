using UnityEngine;

namespace ZoomTracks {
    public static class VectorUtility {
        public static Vector3 ClampAngularSpeed(float oldRotation_degrees, Vector3 newVelocity, float maxRotationSpeed_degreesPerSecond) {
            if (newVelocity == Vector3.zero) {
                return newVelocity;
            }
            Vector3 oldDirectionUnitVector = Quaternion.AngleAxis(oldRotation_degrees, Vector3.up) * Vector3.forward;
            float maxDeltaRotation_degrees = maxRotationSpeed_degreesPerSecond * Time.deltaTime;
            float signedDeltaRotation_degrees = Vector3.SignedAngle(oldDirectionUnitVector, newVelocity, Vector3.up);
            if (Mathf.Abs(signedDeltaRotation_degrees) <= maxDeltaRotation_degrees) {
                return newVelocity;
            }
            Debug.Log(Mathf.Abs(signedDeltaRotation_degrees));
            float clampedRotation_degrees = Mathf.Sign(signedDeltaRotation_degrees) * maxDeltaRotation_degrees;
            Vector3 newDirectionUnitVector = Quaternion.AngleAxis(clampedRotation_degrees, Vector3.up) * oldDirectionUnitVector;
            return newDirectionUnitVector * newVelocity.magnitude;
        }
    }
}
