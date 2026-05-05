using UnityEngine;
using UnityEngine.Assertions;

namespace ZoomTracks {
    public static class CameraController {
        public static Transform CameraPanAndYaw;
        public static Transform CameraYawOffset;
        public static Transform CameraPanOffsetAndPitch;
        public static Camera Camera;

        private static TransformStruct OriginalCameraPanAndYawTransform;

        public static bool ShouldFollowCarLocation = false;

        public static void Init() {
            CameraPanAndYaw = GameObject.Find(nameof(CameraPanAndYaw)).transform;
            CameraYawOffset = GameObject.Find(nameof(CameraYawOffset)).transform;
            CameraPanOffsetAndPitch = GameObject.Find(nameof(CameraPanOffsetAndPitch)).transform;
            Camera = GameObject.Find(nameof(Camera)).GetComponent<Camera>();
            OriginalCameraPanAndYawTransform = new TransformStruct(CameraPanAndYaw.transform);

            ValidateCameraParameters();
        }

        private static void ValidateCameraParameters() {
            Assert.IsTrue(CameraPanAndYaw.position.y == 0);
            Assert.IsTrue(CameraPanAndYaw.rotation.x == 0);
            Assert.IsTrue(CameraPanAndYaw.rotation.z == 0);
            Assert.IsTrue(CameraPanAndYaw.localScale == Vector3.one);
        }

        public static void UpdateCameraFollow() {
            if (ShouldFollowCarLocation) {
                CameraPanAndYaw.transform.position = SceneObjects.Car.transform.position;
            } else {
                CameraPanAndYaw.transform.position = OriginalCameraPanAndYawTransform.Position;
            }
        }
    }
}
