using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public class MainLoop : MonoBehaviour {
        [SerializeField]
        private float CarForwardBackwardSpeed = 150;

        [SerializeField]
        private float CarRotateSpeed = 540;

        [SerializeField]
        private float _CameraPanSpeed = 150;
        public static float CameraPanSpeed;

        [SerializeField]
        private float CameraZoomSpeed = 100;

        public enum ControlModeEnum {
            DebugMoveCar,
            Camera,
        }
        public static ControlModeEnum ControlMode = ControlModeEnum.DebugMoveCar;
        private static Keyboard Keyboard = null;
        private bool IsStartFinished = false;

        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Awake.html
        private void Awake() {
            Debug.Log($"GameLoop Awake on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
            QualitySettings.maxQueuedFrames = 0;
            QualitySettings.vSyncCount = 1;
        }

        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Start.html
        private async void Start() {
            CameraPanSpeed = this._CameraPanSpeed;

            if (SceneManager.GetSceneByName("UiScene").isLoaded) {
                Debug.Log("UiScene is already loaded");
            } else {
                Debug.Log("UiScene is not loaded yet");
                Debug.Log("Loading UiScene...");
                await SceneManager.LoadSceneAsync("UiScene", LoadSceneMode.Additive);
                Debug.Log("...Finished loading UiScene");
            }

            SceneObjects.Init();
            SceneObjects.TestLabel.text = "Test passed";
            CameraController.Init();

            /*
            For my personal use case:
            I should always have a keyboard connected, so verify this and use the Keyboard static var as a shortcut.
            Usually, I don't always have a gamepad connected. After I start the game, I plug in the gamepad.
            So my game should handle controller connect/disconnect during runtime.
            */
            Keyboard = Keyboard.current ?? throw new Exception("Keyboard.current is null");

            this.IsStartFinished = true;
        }

        private async Awaitable LoadTestSceneAsync() {
            Debug.Log("Loading scene...");
            DateTime startTime = DateTime.Now;
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync("TestScene", LoadSceneMode.Additive);
            while (!loadOperation.isDone || ((DateTime.Now - startTime) < TimeSpan.FromSeconds(5))) {
                Debug.Log($"{Time.frameCount}");
                await Awaitable.NextFrameAsync();
            }
            Debug.Log("...done");
        }

        private Awaitable SceneLoadOperation = null;

        // Update is called once per frame
        private void Update() {
            if (!this.IsStartFinished) {
                return;
            }

            if (this.SceneLoadOperation != null && this.SceneLoadOperation.IsCompleted) {
                Transform t = GameObject.Find("TestSceneObject").GetComponent<Transform>();
                Debug.Log($"Test transform: {t.position.x}, {t.position.y}, {t.position.z}");
                this.SceneLoadOperation = null;
            }

            if (this.SceneLoadOperation == null) {
                if (Keyboard.pauseKey.wasPressedThisFrame) {
                    this.SceneLoadOperation = this.LoadTestSceneAsync();
                }

                Gamepad gamepad = Gamepad.current;

                // Switch control mode
                if (gamepad != null && gamepad.startButton.wasPressedThisFrame) {
                    if (ControlMode == ControlModeEnum.Camera) {
                        ControlMode = ControlModeEnum.DebugMoveCar;
                    } else if (ControlMode == ControlModeEnum.DebugMoveCar) {
                        ControlMode = ControlModeEnum.Camera;
                    }
                }

                // Left stick pan offset
                if (ControlMode == ControlModeEnum.Camera && gamepad != null) {
                    // TODO: Don't execute this for non-zero actuation
                    Vector2 leftStick = gamepad.leftStick.ReadValue();
                    CameraController.CameraPanOffsetAndPitch.localPosition += Time.deltaTime * CameraPanSpeed * new Vector3(leftStick.x, 0, leftStick.y);
                }

                // D-pad up reset pan offset
                if (ControlMode == ControlModeEnum.Camera && gamepad != null && gamepad.dpad.up.wasPressedThisFrame) {
                    CameraController.CameraPanOffsetAndPitch.localPosition = Vector3.zero;
                }

                // Left shoulder toggle follow
                if (ControlMode == ControlModeEnum.Camera && gamepad != null && gamepad.leftShoulder.wasPressedThisFrame) {
                    CameraController.ShouldFollowCarLocation = !CameraController.ShouldFollowCarLocation;
                }

                // Left/right trigger zoom
                if (ControlMode == ControlModeEnum.Camera && gamepad != null) {
                    CameraController.Camera.orthographicSize += Time.deltaTime * this.CameraZoomSpeed * (gamepad.leftTrigger.ReadValue() - gamepad.rightTrigger.ReadValue());
                    CameraController.Camera.orthographicSize = Mathf.Clamp(CameraController.Camera.orthographicSize, CameraController.MinCameraOrthographicSize, CameraController.MaxCameraOrthographicSize);
                }

                // Left stick debug move car
                if (ControlMode == ControlModeEnum.DebugMoveCar && gamepad != null) {
                    // TODO: Don't execute this for non-zero actuation
                    Vector2 leftStick = gamepad.leftStick.ReadValue();
                    SceneObjects.Car.transform.Translate(Time.deltaTime * this.CarForwardBackwardSpeed * leftStick.y * Vector3.forward);
                    SceneObjects.Car.transform.Rotate(axis: Vector3.up, Time.deltaTime * leftStick.x * this.CarRotateSpeed);
                }

                // ESDF debug move car
                if (ControlMode == ControlModeEnum.DebugMoveCar) {
                    if (Keyboard.eKey.isPressed) {
                        SceneObjects.Car.transform.Translate(Time.deltaTime * this.CarForwardBackwardSpeed * Vector3.forward);
                    }
                    if (Keyboard.dKey.isPressed) {
                        SceneObjects.Car.transform.Translate(Time.deltaTime * this.CarForwardBackwardSpeed * Vector3.back);
                    }
                    if (Keyboard.sKey.isPressed) {
                        SceneObjects.Car.transform.Rotate(axis: Vector3.up, -1 * Time.deltaTime * this.CarRotateSpeed);
                    }
                    if (Keyboard.fKey.isPressed) {
                        SceneObjects.Car.transform.Rotate(axis: Vector3.up, Time.deltaTime * this.CarRotateSpeed);
                    }
                }

                CameraController.UpdateCameraFollow();
                this.UpdateUi();
            }
        }

        private void UpdateUi() {
            SceneObjects.CameraFollowCarLocationBoolLabel.text = $"Camera following car location: {CameraController.ShouldFollowCarLocation}";
            SceneObjects.ControlModeLabel.text = $"Control mode: {ControlMode}";
        }

        /*
        private void Update_CameraControl() {
            if (Gamepad.current?.startButton.wasPressedThisFrame == true) {
                this.ControlMode = ControlModeEnum.DebugMoveCar;
                return;
            }
        }

        private void Update_DebugMoveCar_ControlCamera() {
            if (Gamepad.current?.startButton.wasPressedThisFrame == true) {
                this.ControlMode = ControlModeEnum.Camera;
                return;
            }

            if (Keyboard.eKey.isPressed) {
                SceneObjects.Car.transform.Translate(Time.deltaTime * this.CarTranslationSpeed * Vector3.forward);
            }
            if (Keyboard.sKey.isPressed) {
                SceneObjects.Car.transform.Rotate(new Vector3(0, 1, 0), -1 * Time.deltaTime * this.CarRotateSpeed);
            }
            if (Keyboard.dKey.isPressed) {
                SceneObjects.Car.transform.Translate(Time.deltaTime * this.CarTranslationSpeed * Vector3.back);
            }
            if (Keyboard.fKey.isPressed) {
                SceneObjects.Car.transform.Rotate(new Vector3(0, 1, 0), Time.deltaTime * this.CarRotateSpeed);
            }
        }

        private void Update_MoveCarToCursor() {
            Vector3 mousePosition = new(Mouse.current.position.x.ReadValue(), Mouse.current.position.y.ReadValue(), 0);
            Ray ray = CameraController.Camera.ScreenPointToRay(mousePosition);
            float t = -ray.origin.y / ray.direction.y;
            float x = ray.origin.x + ray.direction.x * t;
            float z = ray.origin.z + ray.direction.z * t;
            Vector3 mousePositionWorld = new(x, 0, z);
            Draw.ingame.Cross(mousePositionWorld, 10, Color.red);
            SceneObjects.Car.transform.position = mousePositionWorld;
            foreach (Transform transform in SceneObjects.TireGroundContactPoints) {
                Draw.ingame.Cross(transform.position, 1, Color.red);
            }
        }

        private void Update_PrintTicks() {
            Debug.Log(DateTime.Now.Ticks);
        }

        private Matrix4x4 originMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

        private void Update_TestAline() {
            Draw.ingame.Line(Vector3.zero, Vector3.one * 50, Color.red);
            using (Draw.ingame.WithMatrix(this.originMatrix)) {
                using (Draw.ingame.WithColor(Color.blue)) {
                    Draw.ingame.WireBox(Vector3.zero, Vector3.one * 100);
                    Draw.ingame.WireCylinder(Vector3.up * 1f, Vector3.up, 1f, 0.5f);
                }
            }
        }

        private void Update_DrawCursor() {
            Vector3 mousePosition = new(Mouse.current.position.x.ReadValue(), Mouse.current.position.y.ReadValue(), 0);
            Ray ray = CameraController.Camera.ScreenPointToRay(mousePosition);
            float t = -ray.origin.y / ray.direction.y;
            float x = ray.origin.x + ray.direction.x * t;
            float z = ray.origin.z + ray.direction.z * t;
            Draw.ingame.WireSphere(new Vector3(x, 0, z), 10, Color.red);
        }
        */
    }
}
