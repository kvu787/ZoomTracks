using TMPro;
using UnityEngine;

namespace ZoomTracks {
    public static class SceneObjects {
        public static GameObject Car;
        public static Transform[] TireGroundContactPoints;

        public static TMP_Text ControlModeLabel;
        public static TMP_Text CameraFollowCarLocationBoolLabel;
        public static TMP_Text TestLabel;

        public static void Init() {
            Car = GameObject.Find("SlopeCarPlaceholder");
            TireGroundContactPoints = new Transform[] {
                Car.transform.Find("CarFL"),
                Car.transform.Find("CarFR"),
                Car.transform.Find("CarRL"),
                Car.transform.Find("CarRR"),
            };

            ControlModeLabel = GameObject.Find(nameof(ControlModeLabel)).GetComponent<TMP_Text>();
            CameraFollowCarLocationBoolLabel = GameObject.Find(nameof(CameraFollowCarLocationBoolLabel)).GetComponent<TMP_Text>();
            TestLabel = GameObject.Find(nameof(TestLabel)).GetComponent<TMP_Text>();
        }
    }
}
