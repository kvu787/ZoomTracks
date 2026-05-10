using TMPro;
using UnityEngine;

namespace ZoomTracks {
    public class SceneObjects {
        public GameObject Car;
        public Transform[] TireGroundContactPoints;

        public TMP_Text ControlModeLabel;
        public TMP_Text CameraFollowCarLocationBoolLabel;
        public TMP_Text TestLabel;

        public SceneObjects() {
            this.Car = GameObject.Find("SlopeCarPlaceholder");
            this.TireGroundContactPoints = new Transform[] {
                this.Car.transform.Find("CarFL"),
                this.Car.transform.Find("CarFR"),
                this.Car.transform.Find("CarRL"),
                this.Car.transform.Find("CarRR"),
            };

            this.ControlModeLabel = GameObject.Find(nameof(this.ControlModeLabel)).GetComponent<TMP_Text>();
            this.CameraFollowCarLocationBoolLabel = GameObject.Find(nameof(this.CameraFollowCarLocationBoolLabel)).GetComponent<TMP_Text>();
            this.TestLabel = GameObject.Find(nameof(this.TestLabel)).GetComponent<TMP_Text>();
        }
    }
}
