using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace ZoomTracks {
    public class TrackObjects {
        private static IReadOnlyList<float> ValidZHeights { get; } = Array.AsReadOnly(new[] {
            0f,
            0.015625f,
            0.03125f,
            0.046875f,
            0.0625f,
            0.078125f,
            0.09375f,
        });

        public TrackObjects() {
            GameObject placeholderCar = GameObject.Find("SlopeCarPlaceholder");
            Assert.IsNotNull(placeholderCar);
            ValidatePlaceholderCar(placeholderCar);
            placeholderCar.SetActive(false);
            this.PlaceholderCarTransform = placeholderCar.transform;

            this.TireGroundContactPoints = new Transform[] {
                placeholderCar.transform.Find("CarFL"),
                placeholderCar.transform.Find("CarFR"),
                placeholderCar.transform.Find("CarRL"),
                placeholderCar.transform.Find("CarRR"),
            };
        }

        public Transform PlaceholderCarTransform { get; }
        public Transform[] TireGroundContactPoints { get; }

        private static void ValidatePlaceholderCar(GameObject placeholderCar) {
            // These transform checks intentionally use a tolerance instead of exact floating-point equality.
            // Mathematically, a planar rotation has an orthonormal matrix whose basis-vector lengths are one. For
            // example, a Z rotation contains sin(theta) and cos(theta), and each affected column has length
            // sqrt(sin(theta)^2 + cos(theta)^2) = 1. In floating-point arithmetic, however, the stored angle and
            // evaluated sine and cosine are rounded, so the computed sum of squares need not be exactly one.
            //
            // This matters even when Blender displays the object's Scale as exactly (1, 1, 1). Blender's FBX
            // exporter decomposes the computed object matrix back into translation, rotation, and scale. Any small
            // error in the rotation matrix's basis-vector lengths can therefore be interpreted as scale and written
            // to FBX; this track has produced values such as 0.9999999403953552. Changing Blender's rotation mode
            // from Euler to Quaternion and back can re-orthonormalize a particular rotation and hide the error, but
            // that round trip is not guaranteed to produce bit-exact unit lengths for every angle.
            //
            // Unity then performs another basis conversion when Bake Axis Conversion maps Blender's right-handed,
            // Z-up coordinate system (as represented by the FBX) to Unity's left-handed, Y-up coordinate system. Its
            // matrix/quaternion decomposition can leave tiny off-axis quaternion values even when the authored
            // transform is mathematically a pure yaw. Reading Quaternion.eulerAngles converts that quaternion yet
            // again and can expose those values as nonzero X or Z angles (on the order of 1e-16 degrees in observed
            // files). DeltaAngle is used because angles wrap: 0 and 360 degrees represent the same orientation, but
            // subtracting them directly does not. The scale checks likewise validate the intended unit scale rather
            // than requiring identical IEEE-754 bit patterns after the Blender -> FBX -> Unity conversion pipeline.
            //
            // The height assertion remains exact because this unparented object's authored Blender Z position is
            // exactly zero and the axis conversion maps it directly to Unity Y; sign changes and unit scaling preserve
            // floating-point zero without evaluating trigonometric functions or decomposing a rotation matrix.
            float tolerance = 0.00001f;

            Assert.IsTrue(placeholderCar.transform.position.y == 0f);

            Assert.IsTrue(Mathf.Abs(Mathf.DeltaAngle(placeholderCar.transform.rotation.eulerAngles.x, 0f)) < tolerance);
            Assert.IsTrue(Mathf.Abs(Mathf.DeltaAngle(placeholderCar.transform.rotation.eulerAngles.z, 0f)) < tolerance);

            Assert.IsTrue(Mathf.Abs(placeholderCar.transform.localScale.x - 1f) < tolerance);
            Assert.IsTrue(Mathf.Abs(placeholderCar.transform.localScale.x - 1f) < tolerance);
            Assert.IsTrue(Mathf.Abs(placeholderCar.transform.localScale.x - 1f) < tolerance);
        }
    }
}
