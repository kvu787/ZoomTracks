using UnityEngine;

namespace ZoomTracks {
    public class SceneObjects {
        public GameObject Car;
        public Transform[] TireGroundContactPoints;

        public SceneObjects() {
            this.Car = GameObject.Find("SlopeCarPlaceholder");
            this.TireGroundContactPoints = new Transform[] {
                this.Car.transform.Find("CarFL"),
                this.Car.transform.Find("CarFR"),
                this.Car.transform.Find("CarRL"),
                this.Car.transform.Find("CarRR"),
            };
        }
    }
}
