using UnityEngine;

namespace ZoomTracks {
    public class TrackObjects {
        public GameObject PlaceholderCar;
        public Transform[] TireGroundContactPoints;

        public TrackObjects() {
            this.PlaceholderCar = GameObject.Find("SlopeCarPlaceholder");
            this.TireGroundContactPoints = new Transform[] {
                this.PlaceholderCar.transform.Find("CarFL"),
                this.PlaceholderCar.transform.Find("CarFR"),
                this.PlaceholderCar.transform.Find("CarRL"),
                this.PlaceholderCar.transform.Find("CarRR"),
            };
        }
    }
}
