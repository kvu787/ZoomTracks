using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public class MainLoop : MonoBehaviour {
        private enum GameStateEnum {
            Start,
            LoadingUiScene,
            LoadingNewTrack,
            InitNewTrack,
            InGame,
            UnloadingOldTrack,
            DoNothing,
        }

        private GameStateEnum GameState;

        private ZtSceneManager ZtSceneManager;
        private TrackSwitcher TrackSwitcher;

        private Keyboard Keyboard;
        private Gamepad Gamepad;

        private ControlModeSwitcher ControlModeSwitcher;
        private TrackObjects TrackObjects;
        private CarMover CarMover;
        private CameraController CameraController;
        private UiManager UiManager;

        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Awake.html
        private void Awake() {
            Debug.Log($"GameLoop Awake on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
            QualitySettings.maxQueuedFrames = 0;
            QualitySettings.vSyncCount = 1;
        }

        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Start.html
        private void Start() {
            this.GameState = GameStateEnum.Start;
            this.ZtSceneManager = new ZtSceneManager(log: false);
            this.TrackSwitcher = new TrackSwitcher();
            this.Keyboard = null;
            this.Gamepad = null;
        }

        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Update.html
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
                    Debug.Log("Start initializing track...");
                    this.ControlModeSwitcher = new ControlModeSwitcher();
                    this.TrackObjects = new TrackObjects();
                    this.CarMover = new CarMover(this.TrackObjects);
                    this.CameraController = new CameraController(this.TrackObjects);
                    this.UiManager = new UiManager(this.CameraController, this.ControlModeSwitcher);
                    this.TrackSwitcher.SwitchingTrackFinished();
                    Debug.Log("...Finished initializing track");
                    this.GameState = GameStateEnum.InGame;
                    break;

                case GameStateEnum.InGame:
                    this.ProcessInGameState();
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
                    throw new Exception($"Unhandled GameState: {this.GameState}");
            }

            this.ZtSceneManager.UpdateAfterAll();
        }

        private void UpdateBeforeAll() {
            this.Keyboard = Keyboard.current;
            this.Gamepad = Gamepad.current;
        }

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

        private void ProcessInGameState() {
            this.ControlModeSwitcher.UpdateControlMode(this.Keyboard, this.Gamepad);

            if (this.ControlModeSwitcher.ControlMode == ControlModeEnum.Camera) {
                this.CameraController.UpdateCameraSettings(this.Keyboard, this.Gamepad);
            } else if (this.ControlModeSwitcher.ControlMode == ControlModeEnum.DebugMoveCar) {
                this.CarMover.UpdateCarPosition(this.Keyboard, this.Gamepad);
                if (this.TrackSwitcher.SwitchTracks(this.Keyboard, this.Gamepad)) {
                    this.GameState = GameStateEnum.UnloadingOldTrack;
                }
            }

            this.CameraController.UpdateCameraPosition();
            this.UiManager.Update();
        }
    }
}
