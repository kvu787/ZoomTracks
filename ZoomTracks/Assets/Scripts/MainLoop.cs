using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace ZoomTracks {
    public class MainLoop : MonoBehaviour {
        private const string UiSceneName = "Ui";
        private static readonly IReadOnlyList<string> TrackSceneNames = new List<string>() {
            "Track1",
            "Track2",
        };
        private const int InitialTrackSceneIndex = 1;

        private enum GameStateEnum {
            Start,
            LoadNewTrack,
            RunGame,
            UnloadOldTrack,
            DoNothing,
        }

        private GameStateEnum GameState;
        private bool IsStartFinished;
        private InputManager InputManager;
        private SceneManager SceneManager;
        private TrackSwitcher TrackSwitcher;

        private ControlModeSwitcher ControlModeSwitcher;
        private TrackObjects TrackObjects;
        private CarSwitcher CarSwitcher;
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
        private async void Start() {
            Debug.Log($"BEGIN: MainLoop.Start on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
            Debug.Log($"Start initializing game...");
            if (UnitySceneManager.loadedSceneCount != 1) {
                throw new Exception($"Expected: Start with 1 loaded scene. Actual: Started with {UnitySceneManager.loadedSceneCount} loaded scenes.");
            }

            this.GameState = GameStateEnum.LoadNewTrack;
            this.IsStartFinished = false;
            this.InputManager = new InputManager();
            this.SceneManager = new SceneManager(enableLog: false);
            this.TrackSwitcher = new TrackSwitcher(InitialTrackSceneIndex, TrackSceneNames.Count, TrackSceneNames);
            await UnitySceneManager.LoadSceneAsync(UiSceneName, LoadSceneMode.Additive);
            this.IsStartFinished = true;
            Debug.Log($"END: MainLoop.Start on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
        }

        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.UpdateBeforeAll.html
        private void Update() {
            this.InputManager.UpdateBeforeAll();
            this.SceneManager.UpdateBeforeAll();

            switch (this.GameState) {
                case GameStateEnum.Start:
                    if (this.IsStartFinished) {
                        Debug.Log($"...Finish initializing game...");
                        // TODO: Trigger loading of new track
                    } else {
                        this.UpdateBusyAnimation();
                    }
                    break;
                case GameStateEnum.LoadNewTrack:
                    if (this.TrackSwitcher.WasOperationFinishedThisFrame) {
                        this.GameState = GameStateEnum.RunGame;
                        this.TrackSwitcher.UnloadTrack();
                    } else {
                        Debug.Log($"Loading new track {Time.frameCount}");
                    }
                    break;
                case GameStateEnum.RunGame:
                    this.RunGame();
                    break;
                case GameStateEnum.UnloadOldTrack:
                    if (this.TrackSwitcher.WasOperationFinishedThisFrame) {
                        this.GameState = GameStateEnum.LoadNewTrack;
                    } else {
                        Debug.Log($"Unloading old track {Time.frameCount}");
                    }
                    break;
                case GameStateEnum.DoNothing:
                    break;
                default:
                    throw new Exception($"Unhandled GameState: {this.GameState}");
            }

            this.SceneManager.UpdateAfterAll();
        }

        private void RunGame() {
            this.ControlModeSwitcher.ReadInputAndToggleControlMode(this.InputManager.Keyboard, this.InputManager.Gamepad);

            switch (this.ControlModeSwitcher.ControlMode) {
                case ControlModeEnum.Camera:
                    this.CameraController.ReadInputAndChangeCameraSettings(this.InputManager.Keyboard, this.InputManager.Gamepad);
                    break;

                case ControlModeEnum.Car:
                    if (!this.CarSwitcher.ReadInputAndSwitchCar(this.InputManager.Keyboard, this.InputManager.Gamepad)) {
                        this.CarMover.ReadInputAndMoveCar(this.InputManager.Keyboard, this.InputManager.Gamepad);
                    }
                    if (this.TrackSwitcher.ReadInputAndSwitchTracks(this.InputManager.Keyboard, this.InputManager.Gamepad)) {
                        this.GameState = GameStateEnum.UnloadOldTrack;
                    }
                    break;

                default:
                    throw new Exception($"Unknown ControlMode='{this.ControlModeSwitcher.ControlMode}'");
            }

            this.CameraController.UpdateCameraPosition();
            this.UiManager.Update();
        }

        private void UpdateBusyAnimation() {
            Debug.Log($"Busy {Time.realtimeSinceStartupAsDouble:F3}");
        }
    }
}
