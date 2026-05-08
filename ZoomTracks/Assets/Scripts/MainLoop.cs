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

        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Awake.html
        private void Awake() {
            Debug.Log($"GameLoop Awake on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
            QualitySettings.maxQueuedFrames = 0;
            QualitySettings.vSyncCount = 1;
        }

        private enum GameStateEnum {
            Start,
            LoadingUiScene,
            LoadingNewTrack,
            InitNewTrack,
            InGame,
            UnloadingOldTrack,
            DoNothing,
        }

        private GameStateEnum GameState = GameStateEnum.Start;

        private void UpdateBusyAnimation() {
            Debug.Log($"Busy... (frameCount={Time.frameCount})");
        }

        // Update is called once per frame
        private void Update() {
            ZtSceneManager.UpdateBeforeAll();

            switch (this.GameState) {
                case GameStateEnum.Start:
                    if (SceneManager.loadedSceneCount != 1) {
                        throw new Exception($"Started with {SceneManager.loadedSceneCount} loaded scenes");
                    }

                    Debug.Log($"Log path for standalone exe: {Application.persistentDataPath}/Player.log".Replace("/", "\\"));
                    this.UpdateBusyAnimation();
                    Debug.Log("Start game");
                    ZtSceneManager.LoadScene("UiScene");
                    this.GameState = GameStateEnum.LoadingUiScene;
                    break;
                case GameStateEnum.LoadingUiScene:
                    this.UpdateBusyAnimation();
                    if (ZtSceneManager.WasOperationFinishedThisFrame) {
                        Debug.Log("Finished loading UI");
                        ZtSceneManager.LoadScene("Track1Scene");
                        this.GameState = GameStateEnum.LoadingNewTrack;
                    }
                    break;
                case GameStateEnum.LoadingNewTrack:
                    this.UpdateBusyAnimation();
                    if (ZtSceneManager.WasOperationFinishedThisFrame) {
                        Debug.Log("Finished loading new track");
                        this.GameState = GameStateEnum.InitNewTrack;
                    }
                    break;
                case GameStateEnum.InitNewTrack:
                    this.UpdateBusyAnimation();
                    SceneObjects.Init();
                    SceneObjects.TestLabel.text = "Test passed";
                    CameraController.Init();
                    Debug.Log("Finished initializing new track");
                    this.GameState = GameStateEnum.InGame;
                    break;
                case GameStateEnum.InGame:
                    this.HandleInGameState();
                    break;
                case GameStateEnum.UnloadingOldTrack:
                    this.UpdateBusyAnimation();
                    if (ZtSceneManager.WasOperationFinishedThisFrame) {
                        Debug.Log("Finished unloading old track");
                        ZtSceneManager.LoadScene("NewTrack");
                        this.GameState = GameStateEnum.LoadingNewTrack;
                    }
                    break;
                case GameStateEnum.DoNothing:
                    break;
                default:
                    throw new Exception();
            }

            ZtSceneManager.UpdateAfterAll();
        }

        private void HandleInGameState() {
            if (!ZtSceneManager.IsOperationRunning()) {
                if (ZtSceneManager.WasOperationFinishedThisFrame) {
                    // Run init on newly loaded scene
                    Debug.Log("Scene finished loading/unloading");
                }

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

                    //// Load/unload test scene
                    //if ((Keyboard.ctrlKey.isPressed && Keyboard.pauseKey.wasPressedThisFrame) || (Gamepad?.leftShoulder.isPressed is true)) {
                    //    ZtSceneManager.LoadScene(TestSceneName);
                    //    this.GameState = GameStateEnum.LoadingNewTrack;
                    //}
                    //if ((Keyboard.shiftKey.isPressed && Keyboard.pauseKey.wasPressedThisFrame) || (Gamepad?.rightShoulder.isPressed is true)) {
                    //    ZtSceneManager.UnloadScene(TestSceneName);
                    //    this.GameState = GameStateEnum.UnloadingOldTrack;
                    //}
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
