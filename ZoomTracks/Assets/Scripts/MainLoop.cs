using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public class MainLoop : MonoBehaviour {
        private const string TestSceneName = "TestScene";
        private const float CarForwardBackwardSpeed = 150;
        private const float CarRotateSpeed = 540;

        private enum ControlModeEnum {
            DebugMoveCar,
            Camera,
        }

        private static ControlModeEnum ControlMode = ControlModeEnum.DebugMoveCar;

        private static Keyboard Keyboard = null;
        private static Gamepad Gamepad = null;

        private bool IsStartFinished = false;

        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Awake.html
        private void Awake() {
            Debug.Log($"GameLoop Awake on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
            QualitySettings.maxQueuedFrames = 0;
            QualitySettings.vSyncCount = 1;
        }

        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Start.html
        private async void Start() {
            Debug.Log($"Log path for standalone exe: {Application.persistentDataPath}/Player.log".Replace("/", "\\"));

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

            this.IsStartFinished = true;
        }

        // Update is called once per frame
        private void Update() {
            if (!this.IsStartFinished) {
                return;
            }

            ZtSceneManager.Update();

            if (!ZtSceneManager.IsBusy()) {
                // Update input convenience fields
                Keyboard = Keyboard.current ?? throw new Exception("No keyboard connected");
                Gamepad = Gamepad.current;

                // Switch control mode
                if (Gamepad?.startButton.wasPressedThisFrame is true) {
                    if (ControlMode == ControlModeEnum.Camera) {
                        ControlMode = ControlModeEnum.DebugMoveCar;
                    } else if (ControlMode == ControlModeEnum.DebugMoveCar) {
                        ControlMode = ControlModeEnum.Camera;
                    }
                }

                if (ControlMode == ControlModeEnum.Camera) {
                    if (Gamepad != null) {
                        // Left stick pan offset
                        CameraController.PanOffset(Gamepad.leftStick.ReadValue());

                        // Left/right trigger zoom
                        CameraController.Zoom(Gamepad.leftTrigger.ReadValue(), Gamepad.rightTrigger.ReadValue());

                        // D-pad up reset pan offset
                        if (Gamepad.dpad.up.wasPressedThisFrame) {
                            CameraController.ResetPanOffset();
                        }

                        // Left shoulder toggle follow
                        if (Gamepad.leftShoulder.wasPressedThisFrame) {
                            CameraController.ToggleFollowLocation();
                        }
                    }
                } else if (ControlMode == ControlModeEnum.DebugMoveCar) {
                    // ESDF debug move car
                    if (Keyboard.eKey.isPressed) {
                        SceneObjects.Car.transform.Translate(Time.deltaTime * CarForwardBackwardSpeed * Vector3.forward);
                    }
                    if (Keyboard.dKey.isPressed) {
                        SceneObjects.Car.transform.Translate(Time.deltaTime * CarForwardBackwardSpeed * Vector3.back);
                    }
                    if (Keyboard.sKey.isPressed) {
                        SceneObjects.Car.transform.Rotate(axis: Vector3.up, -1 * Time.deltaTime * CarRotateSpeed);
                    }
                    if (Keyboard.fKey.isPressed) {
                        SceneObjects.Car.transform.Rotate(axis: Vector3.up, Time.deltaTime * CarRotateSpeed);
                    }

                    if (Gamepad != null) {
                        // Left stick debug move car
                        Vector2 leftStick = Gamepad.leftStick.ReadValue();
                        SceneObjects.Car.transform.Translate(Time.deltaTime * CarForwardBackwardSpeed * leftStick.y * Vector3.forward);
                        SceneObjects.Car.transform.Rotate(axis: Vector3.up, Time.deltaTime * leftStick.x * CarRotateSpeed);
                    }

                    // Load/unload test scene
                    if ((Keyboard.ctrlKey.isPressed && Keyboard.pauseKey.wasPressedThisFrame) || (Gamepad?.leftShoulder.isPressed is true)) {
                        ZtSceneManager.LoadScene(TestSceneName);
                    }
                    if ((Keyboard.shiftKey.isPressed && Keyboard.pauseKey.wasPressedThisFrame) || (Gamepad?.rightShoulder.isPressed is true)) {
                        ZtSceneManager.UnloadScene(TestSceneName);
                    }
                }

                CameraController.UpdateCameraFollow();
                UpdateUi();
            }
        }

        private static void UpdateUi() {
            SceneObjects.CameraFollowCarLocationBoolLabel.text = $"Camera following car location: {CameraController.ShouldFollowCarLocation}";
            SceneObjects.ControlModeLabel.text = $"Control mode: {ControlMode}";
        }
    }
}
