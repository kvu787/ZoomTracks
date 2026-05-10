using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public class MainLoop : MonoBehaviour {
        private const float CarForwardBackwardSpeed = 150;
        private const float CarRotateSpeed = 540;

        private ZtSceneManager ZtSceneManager;

        private enum ControlModeEnum {
            DebugMoveCar,
            Camera,
        }

        private static ControlModeEnum ControlMode;

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
        private static int CurrentTrackIndex = Constants.InitialTrackSceneIndex;
        private static int OldTrackIndex = -1;
        private static int NewTrackIndex = Constants.InitialTrackSceneIndex;

        private void UpdateBusyAnimation() {
            Debug.Log($"Busy... frameCount={Time.frameCount}");
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
        }

        // Update is called once per frame
        private void Update() {
            this.ZtSceneManager.UpdateBeforeAll();

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
                        sceneName: Constants.TrackSceneNames[NewTrackIndex],
                        isLoad: true,
                        nextState: GameStateEnum.InitNewTrack);
                    break;
                case GameStateEnum.InitNewTrack:
                    this.UpdateBusyAnimation();
                    Debug.Log("Started initializing track");
                    SceneObjects.Init();
                    SceneObjects.TestLabel.text = "Test passed";
                    CameraController.Init();
                    ControlMode = ControlModeEnum.DebugMoveCar;
                    CurrentTrackIndex = NewTrackIndex;
                    OldTrackIndex = -1;
                    NewTrackIndex = -1;
                    Debug.Log("Finished initializing track");
                    this.GameState = GameStateEnum.InGame;
                    break;
                case GameStateEnum.InGame:
                    this.HandleInGameState();
                    break;
                case GameStateEnum.UnloadingOldTrack:
                    this.LoadUnloadOrWait(
                        sceneName: Constants.TrackSceneNames[OldTrackIndex],
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

                // Switch tracks
                bool isPrevTrack = (Keyboard.leftArrowKey.wasPressedThisFrame) || (Gamepad?.leftShoulder.isPressed is true);
                bool isNextTrack = (Keyboard.rightArrowKey.isPressed) || (Gamepad?.rightShoulder.isPressed is true);
                if (isPrevTrack || isNextTrack) {
                    OldTrackIndex = CurrentTrackIndex;
                    if (isPrevTrack) {
                        NewTrackIndex = (CurrentTrackIndex - 1 + Constants.TrackSceneNames.Count) % Constants.TrackSceneNames.Count;
                    } else if (isNextTrack) {
                        NewTrackIndex = (CurrentTrackIndex + 1) % Constants.TrackSceneNames.Count;
                    }
                    CurrentTrackIndex = -1;
                    this.GameState = GameStateEnum.UnloadingOldTrack;
                }
            }

            CameraController.UpdateCameraFollow();
            UpdateUi();
        }

        private static void UpdateUi() {
            SceneObjects.CameraFollowCarLocationBoolLabel.text = $"Camera following car location: {CameraController.ShouldFollowCarLocation}";
            SceneObjects.ControlModeLabel.text = $"Control mode: {ControlMode}";
        }
    }
}
