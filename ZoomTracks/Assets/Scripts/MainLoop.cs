using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public class MainLoop : MonoBehaviour {
        private ZtSceneManager ZtSceneManager;
        private TrackSwitcher TrackSwitcher;
        private TrackObjects TrackObjects;
        private CarMover CarMover;
        private CameraController CameraController;
        private UiManager UiManager;
        private ControlModeSwitcher ControlModeSwitcher;

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
                    Debug.Log("Start initializing track...");
                    this.TrackObjects = new TrackObjects();
                    this.CarMover = new CarMover(this.TrackObjects);
                    this.ControlModeSwitcher = new ControlModeSwitcher();
                    this.CameraController = new CameraController(this.TrackObjects);
                    this.UiManager = new UiManager(this.CameraController, this.ControlModeSwitcher);
                    this.TrackSwitcher.SwitchingTrackFinished();
                    Debug.Log("...Finished initializing track");
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
            this.ControlModeSwitcher.UpdateControlMode(this.Keyboard, this.Gamepad);

            if (this.ControlModeSwitcher.ControlMode == ControlModeEnum.Camera) {
                if (this.Gamepad != null) {
                    this.CameraController.UpdateCameraSettings(this.Gamepad);
                }
            } else if (this.ControlModeSwitcher.ControlMode == ControlModeEnum.DebugMoveCar) {
                this.CarMover.UpdateDebugMoveCar(this.Keyboard, this.Gamepad);
                this.SwitchTracks();
            }

            this.CameraController.UpdateCameraFollow();
            this.UiManager.Update();
        }

        private void SwitchTracks() {
            if (this.TrackSwitcher.SwitchTracks(this.Keyboard, this.Gamepad)) {
                this.GameState = GameStateEnum.UnloadingOldTrack;
            }
        }
    }
}
