using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class MainLoop : MonoBehaviour {
        private const string UiSceneName = "Ui";
        private static readonly IReadOnlyList<string> TrackSceneNames = new List<string>() {
            "Track1",
            "Track2",
        };
        private const int InitialTrackSceneIndex = 1;

        private enum GameStateEnum {
            LoadingUiScene,
            LoadingNewTrack,
            InitNewTrack,
            RunGame,
            UnloadingOldTrack,
            DoNothing,
        }

        private GameStateEnum GameState;

        private SceneManager SceneManager;
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
            Debug.Log($"BEGIN: MainLoop.Awake on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
            Debug.Log($"Log path for standalone exe: {Application.persistentDataPath}/Player.enableLog".Replace("/", "\\"));
            QualitySettings.maxQueuedFrames = 0;
            QualitySettings.vSyncCount = 1;
            Debug.Log($"END: MainLoop.Awake on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
        }

        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Start.html
        private void Start() {
            Debug.Log($"BEGIN: MainLoop.Start on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
            if (UnityEngine.SceneManagement.SceneManager.loadedSceneCount != 1) {
                throw new Exception($"Expected: Start with 1 loaded scene. Actual: Started with {UnityEngine.SceneManagement.SceneManager.loadedSceneCount} loaded scenes.");
            }

            this.GameState = GameStateEnum.LoadingUiScene;
            this.SceneManager = new SceneManager(enableLog: false);
            this.TrackSwitcher = new TrackSwitcher(InitialTrackSceneIndex, TrackSceneNames.Count);
            this.Keyboard = null;
            this.Gamepad = null;
            Debug.Log($"END: MainLoop.Start on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
        }

        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Update.html
        private void Update() {
            this.SceneManager.UpdateBeforeAll();
            this.UpdateBeforeAll();

            switch (this.GameState) {
                case GameStateEnum.LoadingUiScene:
                    this.LoadUnloadOrWait(
                        sceneName: UiSceneName,
                        isLoad: true,
                        nextState: GameStateEnum.LoadingNewTrack);
                    break;

                case GameStateEnum.LoadingNewTrack:
                    this.LoadUnloadOrWait(
                        sceneName: TrackSceneNames[this.TrackSwitcher.NewTrackIndex],
                        isLoad: true,
                        nextState: GameStateEnum.InitNewTrack);
                    break;

                case GameStateEnum.InitNewTrack:
                    Debug.Log("Start initializing track...");
                    this.ControlModeSwitcher = new ControlModeSwitcher();
                    this.TrackObjects = new TrackObjects();
                    this.CarMover = new CarMover(this.TrackObjects);
                    this.CameraController = new CameraController(this.TrackObjects);
                    this.UiManager = new UiManager(this.CameraController, this.ControlModeSwitcher);
                    this.TrackSwitcher.SwitchingTrackFinished();
                    Debug.Log("...Finish initializing track");
                    this.GameState = GameStateEnum.RunGame;
                    break;

                case GameStateEnum.RunGame:
                    this.RunGame();
                    break;

                case GameStateEnum.UnloadingOldTrack:
                    this.LoadUnloadOrWait(
                        sceneName: TrackSceneNames[this.TrackSwitcher.OldTrackIndex],
                        isLoad: false,
                        nextState: GameStateEnum.LoadingNewTrack);
                    break;

                case GameStateEnum.DoNothing:
                    break;

                default:
                    throw new Exception($"Unhandled GameState: {this.GameState}");
            }

            this.SceneManager.UpdateAfterAll();
        }

        private void RunGame() {
            this.ControlModeSwitcher.ReadInputAndToggleControlMode(this.Keyboard, this.Gamepad);

            switch (this.ControlModeSwitcher.ControlMode) {
                case ControlModeEnum.Camera:
                    this.CameraController.ReadInputAndChangeCameraSettings(this.Keyboard, this.Gamepad);
                    break;

                case ControlModeEnum.Car:
                    this.CarMover.ReadInputAndMoveCar(this.Keyboard, this.Gamepad);
                    if (this.TrackSwitcher.ReadInputAndSwitchTracks(this.Keyboard, this.Gamepad)) {
                        this.GameState = GameStateEnum.UnloadingOldTrack;
                    }
                    break;

                default:
                    throw new Exception($"Unknown ControlMode='{this.ControlModeSwitcher.ControlMode}'");
            }

            this.CameraController.UpdateCameraPosition();
            this.UiManager.Update();
        }

        private void UpdateBeforeAll() {
            this.Keyboard = Keyboard.current;
            this.Gamepad = Gamepad.current;
        }

        private void UpdateBusyAnimation() {
            Debug.Log($"Busy {Time.realtimeSinceStartupAsDouble:F3}");
        }

        private void LoadUnloadOrWait(string sceneName, bool isLoad, GameStateEnum nextState) {
            if (this.SceneManager.IsOperationRunning()) {
                this.UpdateBusyAnimation();
            } else {
                string verb = isLoad ? "loading" : "unloading";
                if (this.SceneManager.WasOperationFinishedThisFrame) {
                    Debug.Log($"...Finish {verb} {sceneName}");
                    this.GameState = nextState;
                } else {
                    Debug.Log($"Start {verb} {sceneName}...");
                    if (isLoad) {
                        this.SceneManager.LoadScene(sceneName);
                    } else {
                        this.SceneManager.UnloadScene(sceneName);
                    }
                }
            }
        }
    }
}
