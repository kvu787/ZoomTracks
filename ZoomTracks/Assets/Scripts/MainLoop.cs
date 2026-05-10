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
            InGame,
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
            Debug.Log($"GameLoop Awake on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
            QualitySettings.maxQueuedFrames = 0;
            QualitySettings.vSyncCount = 1;
        }

        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Start.html
        private void Start() {
            if (UnityEngine.SceneManagement.SceneManager.loadedSceneCount != 1) {
                throw new Exception($"Expected: Start with 1 loaded scene. Actual: Started with {UnityEngine.SceneManagement.SceneManager.loadedSceneCount} loaded scenes.");
            }

            Debug.Log($"Log path for standalone exe: {Application.persistentDataPath}/Player.enableLog".Replace("/", "\\"));
            Debug.Log("Start game");

            this.GameState = GameStateEnum.LoadingUiScene;
            this.SceneManager = new SceneManager(enableLog: false);
            this.TrackSwitcher = new TrackSwitcher(InitialTrackSceneIndex, TrackSceneNames.Count);
            this.Keyboard = null;
            this.Gamepad = null;

            this.GameState = GameStateEnum.LoadingUiScene;
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

        private void ProcessInGameState() {
            this.ControlModeSwitcher.UpdateControlMode(this.Keyboard, this.Gamepad);

            if (this.ControlModeSwitcher.ControlMode == ControlModeEnum.Camera) {
                this.CameraController.UpdateCameraSettings(this.Keyboard, this.Gamepad);
            } else if (this.ControlModeSwitcher.ControlMode == ControlModeEnum.Car) {
                this.CarMover.UpdateCarPosition(this.Keyboard, this.Gamepad);
                if (this.TrackSwitcher.SwitchTracks(this.Keyboard, this.Gamepad)) {
                    this.GameState = GameStateEnum.UnloadingOldTrack;
                }
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
                    Debug.Log($"...Finished {verb} {sceneName}");
                    this.GameState = nextState;
                } else {
                    Debug.Log($"Started {verb} {sceneName}...");
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
