using UnityEngine;
using UnityEngine.Assertions;

namespace ZoomTracks {
    public static class CameraController {
        private const float CameraPanSpeed = 150;
        private const float CameraZoomSpeed = 100;
        private const float MinCameraOrthographicSize = 1;
        private const float MaxCameraOrthographicSize = 281.25f;
        public static bool ShouldFollowCarLocation { get; private set; } = false;

        private static Transform CameraPanAndYaw;
        private static Transform CameraYawOffset;
        private static Transform CameraPanOffsetAndPitch;
        private static Camera Camera;

        private static TransformStruct OriginalCameraPanAndYawTransform;
        private static float OriginalCameraOrthographicSize;


        public static void Init() {
            ShouldFollowCarLocation = false;

            CameraPanAndYaw = GameObject.Find(nameof(CameraPanAndYaw)).transform;
            CameraYawOffset = GameObject.Find(nameof(CameraYawOffset)).transform;
            CameraPanOffsetAndPitch = GameObject.Find(nameof(CameraPanOffsetAndPitch)).transform;
            Camera = GameObject.Find(nameof(Camera)).GetComponent<Camera>();

            ValidateCameraParameters();

            OriginalCameraPanAndYawTransform = new TransformStruct(CameraPanAndYaw.transform);
            OriginalCameraOrthographicSize = Camera.orthographicSize;
        }

        public static void UpdateCameraFollow() {
            if (ShouldFollowCarLocation) {
                CameraPanAndYaw.transform.position = SceneObjects.Car.transform.position;
            } else {
                CameraPanAndYaw.transform.position = OriginalCameraPanAndYawTransform.Position;
            }
        }

        public static void PanOffset(Vector2 vector2) {
            CameraPanOffsetAndPitch.localPosition += Time.deltaTime * CameraPanSpeed * new Vector3(vector2.x, 0, vector2.y);
        }

        public static void Zoom(float a, float b) {
            Camera.orthographicSize += Time.deltaTime * CameraZoomSpeed * (a - b);
            Camera.orthographicSize = Mathf.Clamp(Camera.orthographicSize, MinCameraOrthographicSize, MaxCameraOrthographicSize);
        }

        public static void ResetPanOffset() {
            CameraPanOffsetAndPitch.localPosition = Vector3.zero;
        }

        public static void ToggleFollowLocation() {
            ShouldFollowCarLocation = !ShouldFollowCarLocation;
        }

        private static void ValidateCameraParameters() {
            Assert.IsTrue(CameraPanAndYaw.localPosition.y == 0);
            Assert.IsTrue(CameraPanAndYaw.localEulerAngles.x == 0);
            Assert.IsTrue(CameraPanAndYaw.localEulerAngles.z == 0);
            Assert.IsTrue(CameraPanAndYaw.localScale == Vector3.one);

            Assert.IsTrue(CameraYawOffset.localPosition == Vector3.zero);
            Assert.IsTrue(CameraYawOffset.localEulerAngles == Vector3.zero);
            Assert.IsTrue(CameraYawOffset.localScale == Vector3.one);

            Assert.IsTrue(CameraPanOffsetAndPitch.localPosition == Vector3.zero);
            Assert.IsTrue(CameraPanOffsetAndPitch.localEulerAngles.x == 45);
            Assert.IsTrue(CameraPanOffsetAndPitch.localEulerAngles.y == 0);
            Assert.IsTrue(CameraPanOffsetAndPitch.localEulerAngles.z == 0);
            Assert.IsTrue(CameraPanOffsetAndPitch.localScale == Vector3.one);

            Assert.IsTrue(Camera.transform.localPosition.x == 0);
            Assert.IsTrue(Camera.transform.localPosition.y == 0);
            Assert.IsTrue(Camera.transform.localPosition.z == -500);
            Assert.IsTrue(Camera.transform.localEulerAngles == Vector3.zero);
            Assert.IsTrue(Camera.transform.localScale == Vector3.one);
            Assert.IsTrue(Camera.orthographic);
            Assert.IsTrue(MinCameraOrthographicSize <= Camera.orthographicSize && Camera.orthographicSize <= MaxCameraOrthographicSize);
            Assert.IsTrue(Camera.nearClipPlane == 1);
            Assert.IsTrue(Camera.farClipPlane == 1000);
            Assert.IsTrue(Camera.clearFlags == CameraClearFlags.SolidColor);
            Assert.IsTrue(ColorUtility.ToHtmlStringRGB(Camera.backgroundColor) == "404040");
        }
    }
}
