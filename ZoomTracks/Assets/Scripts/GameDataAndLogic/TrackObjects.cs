using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

namespace ZoomTracks {
    public class TrackObjects {
        private static IReadOnlyList<string> ObstaclePrefixes { get; } = Array.AsReadOnly(new[] {
            "Barrier",
            "BigCone",
            "Cone",
            "VehicleRoad",
        });

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

            IReadOnlyCollection<BoxCollider> obstacles =
                UnityEngine.Object
                    .FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(obj => ObstaclePrefixes.Any(prefix => obj.name.StartsWith(prefix)))
                    .Select(obj => obj.GetComponent<BoxCollider>())
                    .ToList()
                    .AsReadOnly();
            Assert.IsFalse(obstacles.Any(x => x == null), "One or more null colliders found. You probably need to run 'Tools > Setup new track scene'.");
            this.Obstacles = obstacles;
        }

        public Transform PlaceholderCarTransform { get; }
        public Transform[] TireGroundContactPoints { get; }
        public IReadOnlyCollection<BoxCollider> Obstacles { get; }

        private static void ValidatePlaceholderCar(GameObject placeholderCar) {
            Assert.IsTrue(ValidZHeights.Contains(placeholderCar.transform.position.y));
            Assert.IsTrue(placeholderCar.transform.rotation.eulerAngles.x == 0f);
            Assert.IsTrue(placeholderCar.transform.rotation.eulerAngles.z == 0f);
            Assert.IsTrue(placeholderCar.transform.localScale == Vector3.one);
        }
    }
}
