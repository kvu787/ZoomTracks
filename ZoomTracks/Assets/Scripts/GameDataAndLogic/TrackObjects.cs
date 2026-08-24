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
