using UnityEngine;
using UnityEngine.Assertions;

namespace ZoomTracks {
    public static class CameraController {
        private const float CameraPanSpeed = 150;
        private const float CameraZoomSpeed = 100;
        private const float MinCameraOrthographicSize = 1;
        private const float MaxCameraOrthographicSize = 281.25f;

        private static Transform CameraPanAndYaw;
        private static Transform CameraYawOffset;
        private static Transform CameraPanOffsetAndPitch;
        private static Camera Camera;

        private static TransformStruct OriginalCameraPanAndYawTransform;
        private static float OriginalCameraOrthographicSize;

        public static bool ShouldFollowCarLocation { get; private set; } = false;

        public static void Init() {
            CameraPanAndYaw = GameObject.Find(nameof(CameraPanAndYaw)).transform;
            CameraYawOffset = GameObject.Find(nameof(CameraYawOffset)).transform;
            CameraPanOffsetAndPitch = GameObject.Find(nameof(CameraPanOffsetAndPitch)).transform;
            Camera = GameObject.Find(nameof(Camera)).GetComponent<Camera>();
            OriginalCameraPanAndYawTransform = new TransformStruct(CameraPanAndYaw.transform);
            OriginalCameraOrthographicSize = Camera.orthographicSize;

            ValidateCameraParameters();
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
            Assert.IsTrue(CameraPanAndYaw.position.y == 0);
            Assert.IsTrue(CameraPanAndYaw.rotation.x == 0);
            Assert.IsTrue(CameraPanAndYaw.rotation.z == 0);
            Assert.IsTrue(CameraPanAndYaw.localScale == Vector3.one);
        }
    }
}
