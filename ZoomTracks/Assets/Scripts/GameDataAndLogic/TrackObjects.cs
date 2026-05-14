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
            this.PlaceholderCar = GameObject.Find("SlopeCarPlaceholder");
            this.TireGroundContactPoints = new Transform[] {
                this.PlaceholderCar.transform.Find("CarFL"),
                this.PlaceholderCar.transform.Find("CarFR"),
                this.PlaceholderCar.transform.Find("CarRL"),
                this.PlaceholderCar.transform.Find("CarRR"),
            };
            this.PlaceholderCar.SetActive(false);

            this.Obstacles =
                Object
                    .FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(obj => ObstaclePrefixes.Any(prefix => obj.name.StartsWith(prefix)))
                    .Select(obj => obj.GetComponent<BoxCollider>())
                    .ToList()
                    .AsReadOnly();
            Assert.IsFalse(this.Obstacles.Any(x => x == null));
        }

        public GameObject PlaceholderCar { get; }
        public Transform[] TireGroundContactPoints { get; }
        public IReadOnlyCollection<BoxCollider> Obstacles { get; }
    }
}
