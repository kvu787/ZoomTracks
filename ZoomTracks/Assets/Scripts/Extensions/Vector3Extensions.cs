using UnityEngine;
using UnityEngine.Assertions;

namespace ZoomTracks {
    public static class Vector3Extensions {
        /// <summary>
        /// Rotates a Vector3 lying in the XZ plane (y == 0) by the given angle
        /// around the Y axis. Matches Unity's yaw sense (clockwise from +Z,
        /// viewed from above) for positive angles.
        /// </summary>
        public static Vector3 Rotate2D(this Vector3 v, float rotationDegrees) {
            Assert.IsTrue(v.y == 0f);
            float radians = rotationDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector3(
                v.x * cos + v.z * sin,
                0f,
                -v.x * sin + v.z * cos
            );
        }

        public static Vector3 Rotate2D(this Vector3 v, Quaternion rotation) {
            Assert.IsTrue(rotation.eulerAngles.x == 0f);
            Assert.IsTrue(rotation.eulerAngles.z == 0f);
            return v.Rotate2D(rotation.eulerAngles.y);
        }

        /// <summary>
        /// Returns the yaw angle in degrees of a Vector3 lying in the XZ plane
        /// (y == 0), measured clockwise from +Z when viewed from above (left-handed system). Range is
        /// (-180, 180]. Returns 0 for a zero-length vector.
        /// </summary>
        public static float Get2DRotation(this Vector3 v) {
            Assert.IsTrue(v.y == 0f);
            return Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
        }

        public static Quaternion Get2DRotationQuaternion(this Vector3 v) {
            return Quaternion.Euler(0f, v.Get2DRotation(), 0f);
        }
    }
}
