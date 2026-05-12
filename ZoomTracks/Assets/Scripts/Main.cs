using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public class Main : MonoBehaviour {
        private const string UiSceneName = "Ui";
        private static readonly IReadOnlyList<string> TrackSceneNames = new List<string>() {
            "Track1",
            "Track2",
        };
        private const int InitialTrackSceneIndex = 1;

        private InputManager InputManager;
        private TrackSwitcher TrackSwitcher;

        private ControlModeSwitcher ControlModeSwitcher;
        private TrackObjects TrackObjects;
        private CarSwitcher CarSwitcher;
        private CarMover CarMover;
        private CameraController CameraController;
        private UiManager UiManager;

        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Awake.html
        private void Awake() {
            Debug.Log($"BEGIN: Main.Awake on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
            Debug.Log($"Log path for standalone exe: {Application.persistentDataPath}/Player.enableLog".Replace("/", "\\"));
            QualitySettings.maxQueuedFrames = 0;
            QualitySettings.vSyncCount = 1;
            Debug.Log($"END: Main.Awake on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
        }

        private async void Start() {
            Debug.Log($"BEGIN: Main.Start on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
            if (SceneManager.loadedSceneCount != 1) {
                throw new Exception($"Expected: Start with 1 loaded scene. Actual: Started with {SceneManager.loadedSceneCount} loaded scenes.");
            }

            this.InputManager = new InputManager();

            using (CancellationTokenSource printBusyCts = new()) {
                Awaitable printBusyAwaitable = this.PrintBusy(printBusyCts.Token);
                await SceneManager.LoadSceneAsync(UiSceneName, LoadSceneMode.Additive);
                printBusyCts.Cancel();
                await printBusyAwaitable;
            }

            using (CancellationTokenSource printBusyCts = new()) {
                Awaitable printBusyAwaitable = this.PrintBusy(printBusyCts.Token);
                await SceneManager.LoadSceneAsync(TrackSceneNames[InitialTrackSceneIndex], LoadSceneMode.Additive);
                printBusyCts.Cancel();
                await printBusyAwaitable;
            }

            this.TrackSwitcher = new TrackSwitcher(InitialTrackSceneIndex, TrackSceneNames);
            this.InitTrack();

            Debug.Log($"END: Main.Start on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
            await this.UpdateLoop();
        }

        private async Awaitable UpdateLoop() {
            while (true) {
                this.InputManager.UpdateBeforeAll();
                await this.RunGame();
                await Awaitable.NextFrameAsync();
            }
        }

        private void InitTrack() {
            Debug.Log("Start initializing track...");
            this.ControlModeSwitcher = new ControlModeSwitcher();
            this.TrackObjects = new TrackObjects();
            this.CarSwitcher = new CarSwitcher(this.TrackObjects, this.TrackSwitcher);
            this.CarMover = new CarMover(this.CarSwitcher);
            this.CameraController = new CameraController(this.CarSwitcher);
            this.UiManager = new UiManager(this.CameraController, this.ControlModeSwitcher);
            Debug.Log("...Finish initializing track");
        }

        private async Awaitable RunGame() {
            this.ControlModeSwitcher.ReadInputAndToggleControlMode(this.InputManager.Keyboard, this.InputManager.Gamepad);
            switch (this.ControlModeSwitcher.ControlMode) {
                case ControlModeEnum.Camera: {
                    this.CameraController.ReadInputAndChangeCameraSettings(this.InputManager.Keyboard, this.InputManager.Gamepad);
                    break;
                }
                case ControlModeEnum.Car: {
                    if (!this.CarSwitcher.ReadInputAndSwitchCar(this.InputManager.Keyboard, this.InputManager.Gamepad)) {
                        this.CarMover.ReadInputAndMoveCar(this.InputManager.Keyboard, this.InputManager.Gamepad);
                    }
                    if (await this.TrackSwitcher.ReadInputAndSwitchTracks(this.InputManager.Keyboard, this.InputManager.Gamepad)) {
                        this.InitTrack();
                    }
                    break;
                }
                default: {
                    throw new Exception($"Unknown ControlMode='{this.ControlModeSwitcher.ControlMode}'");
                }
            }

            this.CameraController.UpdateCameraPosition();
            this.UiManager.Update();
        }

        private async Awaitable PrintBusy(CancellationToken cancellationToken) {
            while (!cancellationToken.IsCancellationRequested) {
                Debug.Log($"Busy {Time.realtimeSinceStartupAsDouble:F3}");
                try {
                    await Awaitable.NextFrameAsync(cancellationToken);
                } catch (OperationCanceledException) { }
            }
        }
    }
}
