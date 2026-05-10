using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public class MainLoop : MonoBehaviour {
        private const float CarForwardBackwardSpeed = 150;
        private const float CarRotateSpeed = 540;

        private ZtSceneManager ZtSceneManager;
        private TrackSwitcher TrackSwitcher;
        private SceneObjects SceneObjects;
        private CameraController CameraController;

        public enum ControlModeEnum {
            DebugMoveCar,
            Camera,
        }

        private ControlModeEnum ControlMode;

        private Keyboard Keyboard;
        private Gamepad Gamepad;

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
            Debug.Log($"Busy {Time.realtimeSinceStartupAsDouble:F3}...");
        }

        private void LoadUnloadOrWait(string sceneName, bool isLoad, GameStateEnum nextState) {
            if (this.ZtSceneManager.IsOperationRunning()) {
                this.UpdateBusyAnimation();
            } else {
                string verb = isLoad ? "loading" : "unloading";
                if (this.ZtSceneManager.WasOperationFinishedThisFrame) {
                    Debug.Log($"Finished {verb} {sceneName}");
                    this.GameState = nextState;
                } else {
                    Debug.Log($"Started {verb} {sceneName}");
                    if (isLoad) {
                        this.ZtSceneManager.LoadScene(sceneName);
                    } else {
                        this.ZtSceneManager.UnloadScene(sceneName);
                    }
                }
            }
        }

        private void Start() {
            this.ZtSceneManager = new ZtSceneManager(log: false);
            this.TrackSwitcher = new TrackSwitcher();
            this.Keyboard = null;
            this.Gamepad = null;
        }

        private void UpdateBeforeAll() {
            this.Keyboard = Keyboard.current ?? throw new Exception("No keyboard connected");
            this.Gamepad = Gamepad.current;
        }

        // Update is called once per frame
        private void Update() {
            this.ZtSceneManager.UpdateBeforeAll();
            this.UpdateBeforeAll();

            switch (this.GameState) {
                case GameStateEnum.Start:
                    if (SceneManager.loadedSceneCount != 1) {
                        throw new Exception($"Expected: Start with 1 loaded scene. Actual: Started with {SceneManager.loadedSceneCount} loaded scenes.");
                    }

                    Debug.Log($"Log path for standalone exe: {Application.persistentDataPath}/Player.log".Replace("/", "\\"));
                    Debug.Log("Starting game...");
                    this.GameState = GameStateEnum.LoadingUiScene;
                    break;
                case GameStateEnum.LoadingUiScene:
                    this.LoadUnloadOrWait(
                        sceneName: Constants.UiSceneName,
                        isLoad: true,
                        nextState: GameStateEnum.LoadingNewTrack);
                    break;
                case GameStateEnum.LoadingNewTrack:
                    this.LoadUnloadOrWait(
                        sceneName: Constants.TrackSceneNames[this.TrackSwitcher.NewTrackIndex],
                        isLoad: true,
                        nextState: GameStateEnum.InitNewTrack);
                    break;
                case GameStateEnum.InitNewTrack:
                    this.UpdateBusyAnimation();
                    Debug.Log("Started initializing track");
                    this.SceneObjects = new SceneObjects();
                    this.CameraController = new CameraController(this.SceneObjects);
                    this.ControlMode = ControlModeEnum.DebugMoveCar;
                    this.TrackSwitcher.FinishSwitchingTrack();
                    Debug.Log("Finished initializing track");
                    this.GameState = GameStateEnum.InGame;
                    break;
                case GameStateEnum.InGame:
                    this.HandleInGameState();
                    break;
                case GameStateEnum.UnloadingOldTrack:
                    this.LoadUnloadOrWait(
                        sceneName: Constants.TrackSceneNames[this.TrackSwitcher.OldTrackIndex],
                        isLoad: false,
                        nextState: GameStateEnum.LoadingNewTrack);
                    break;
                case GameStateEnum.DoNothing:
                    break;
                default:
                    throw new Exception();
            }

            this.ZtSceneManager.UpdateAfterAll();
        }

        private void HandleInGameState() {
            this.UpdateControlMode();

            if (this.ControlMode == ControlModeEnum.Camera) {
                this.UpdateCameraSettings();
            } else if (this.ControlMode == ControlModeEnum.DebugMoveCar) {
                this.UpdateDebugMoveCar();
                this.SwitchTracks();
            }

            this.CameraController.UpdateCameraFollow();
            this.UpdateUi();
        }

        private void UpdateControlMode() {
            if (this.Gamepad?.startButton.wasPressedThisFrame is true) {
                if (this.ControlMode == ControlModeEnum.Camera) {
                    this.ControlMode = ControlModeEnum.DebugMoveCar;
                } else if (this.ControlMode == ControlModeEnum.DebugMoveCar) {
                    this.ControlMode = ControlModeEnum.Camera;
                }
            }
        }

        private void UpdateCameraSettings() {
            if (this.Gamepad != null) {
                // Left stick pan offset
                this.CameraController.PanOffset(this.Gamepad.leftStick.ReadValue());

                // Left/right trigger zoom
                this.CameraController.Zoom(this.Gamepad.leftTrigger.ReadValue(), this.Gamepad.rightTrigger.ReadValue());

                // D-pad up reset pan offset
                if (this.Gamepad.dpad.up.wasPressedThisFrame) {
                    this.CameraController.ResetPanOffset();
                }

                // Left shoulder toggle follow
                if (this.Gamepad.leftShoulder.wasPressedThisFrame) {
                    this.CameraController.ToggleFollowLocation();
                }
            }
        }

        private void UpdateDebugMoveCar() {
            if (this.Keyboard.eKey.isPressed) {
                this.SceneObjects.Car.transform.Translate(Time.deltaTime * CarForwardBackwardSpeed * Vector3.forward);
            }
            if (this.Keyboard.dKey.isPressed) {
                this.SceneObjects.Car.transform.Translate(Time.deltaTime * CarForwardBackwardSpeed * Vector3.back);
            }
            if (this.Keyboard.sKey.isPressed) {
                this.SceneObjects.Car.transform.Rotate(axis: Vector3.up, -1 * Time.deltaTime * CarRotateSpeed);
            }
            if (this.Keyboard.fKey.isPressed) {
                this.SceneObjects.Car.transform.Rotate(axis: Vector3.up, Time.deltaTime * CarRotateSpeed);
            }

            if (this.Gamepad != null) {
                Vector2 leftStick = this.Gamepad.leftStick.ReadValue();
                this.SceneObjects.Car.transform.Translate(Time.deltaTime * CarForwardBackwardSpeed * leftStick.y * Vector3.forward);
                this.SceneObjects.Car.transform.Rotate(axis: Vector3.up, Time.deltaTime * leftStick.x * CarRotateSpeed);
            }
        }

        private void SwitchTracks() {
            bool isPrevTrack = this.Keyboard.leftArrowKey.wasPressedThisFrame;
            bool isNextTrack = this.Keyboard.rightArrowKey.isPressed;

            if (this.Gamepad != null) {
                isPrevTrack = isPrevTrack || this.Gamepad.leftShoulder.isPressed;
                isNextTrack = isNextTrack || this.Gamepad.rightShoulder.isPressed;
            }

            if (isPrevTrack) {
                this.TrackSwitcher.PrevTrack();
            } else if (isNextTrack) {
                this.TrackSwitcher.NextTrack();
            }

            if (isPrevTrack || isNextTrack) {
                this.GameState = GameStateEnum.UnloadingOldTrack;
            }
        }

        private void UpdateUi() {
            this.SceneObjects.CameraFollowCarLocationBoolLabel.text = $"Camera following car location: {this.CameraController.ShouldFollowCarLocation}";
            this.SceneObjects.ControlModeLabel.text = $"Control mode: {this.ControlMode}";
        }
    }
}
