using TMPro;
using UnityEngine;

public static class SceneObjects {
    public static GameObject Car;
    public static Transform[] TireGroundContactPoints;

    public static Transform CameraPanAndYaw;
    public static Transform CameraYawOffset;
    public static Transform CameraPanOffsetAndPitch;
    public static Camera Camera;

    public static TMP_Text ControlModeLabel;
    public static TMP_Text TestLabel;

    public static void Init() {
        Car = GameObject.Find("SlopeCarPlaceholder");
        TireGroundContactPoints = new Transform[] {
            Car.transform.Find("CarFL"),
            Car.transform.Find("CarFR"),
            Car.transform.Find("CarRL"),
            Car.transform.Find("CarRR"),
        };

        CameraPanAndYaw = GameObject.Find(nameof(CameraPanAndYaw)).transform;
        CameraYawOffset = GameObject.Find(nameof(CameraYawOffset)).transform;
        CameraPanOffsetAndPitch = GameObject.Find(nameof(CameraPanOffsetAndPitch)).transform;
        Camera = GameObject.Find(nameof(Camera)).GetComponent<Camera>();

        ControlModeLabel = GameObject.Find(nameof(ControlModeLabel)).GetComponent<TMP_Text>();
        TestLabel = GameObject.Find(nameof(TestLabel)).GetComponent<TMP_Text>();
    }
}
