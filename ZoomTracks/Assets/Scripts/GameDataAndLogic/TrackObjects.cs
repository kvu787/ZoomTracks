using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

namespace ZoomTracks {
    public class TrackObjects {
        private static IReadOnlyList<string> ObstaclePrefixes { get; } = System.Array.AsReadOnly(new[] {
            "Barrier",
            "BigCone",
            "Cone",
            "VehicleRoad",
        });

        public TrackObjects() {
            GameObject placeholderCar = GameObject.Find("SlopeCarPlaceholder");
            Assert.IsNotNull(placeholderCar);
            placeholderCar.SetActive(false);
            this.PlaceholderCarTransform = placeholderCar.transform;

            this.TireGroundContactPoints = new Transform[] {
                placeholderCar.transform.Find("CarFL"),
                placeholderCar.transform.Find("CarFR"),
                placeholderCar.transform.Find("CarRL"),
                placeholderCar.transform.Find("CarRR"),
            };

            IReadOnlyCollection<BoxCollider> obstacles =
                Object
                    .FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(obj => ObstaclePrefixes.Any(prefix => obj.name.StartsWith(prefix)))
                    .Select(obj => obj.GetComponent<BoxCollider>())
                    .ToList()
                    .AsReadOnly();
            Assert.IsFalse(obstacles.Any(x => x == null));
            this.Obstacles = obstacles;
        }

        public Transform PlaceholderCarTransform { get; }
        public Transform[] TireGroundContactPoints { get; }
        public IReadOnlyCollection<BoxCollider> Obstacles { get; }
    }
}
