using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public class Main : MonoBehaviour {
        private const string UiSceneName = "Ui";
        private const int InitialTrackSceneIndex = 1;
        private static readonly IReadOnlyList<string> TrackSceneNames = new List<string>() {
            "Track1",
            "Track2",
        };

        private InputManager InputManager { get; set; }
        private TrackSwitcher TrackSwitcher { get; set; }
        private ControlModeSwitcher ControlModeSwitcher { get; set; }
        private TrackObjects TrackObjects { get; set; }
        private CarSwitcher CarSwitcher { get; set; }
        private CarDebugMover CarDebugMover { get; set; }
        private CameraController CameraController { get; set; }
        private UiManager UiManager { get; set; }

        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Awake.html
        private void Awake() {
            Debug.Log($"BEGIN: Main.Awake on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
            Debug.Log($"Log path for standalone exe: {Application.persistentDataPath}/Player.enableLog".Replace("/", "\\"));
            QualitySettings.maxQueuedFrames = 0;
            QualitySettings.vSyncCount = 1;
            Debug.Log($"END: Main.Awake on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
        }

        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Start.html
        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Awaitable.html
        private async void Start() {
            Debug.Log($"BEGIN: Main.Start on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
            if (SceneManager.loadedSceneCount != 1) {
                throw new Exception($"Expected: Start with 1 loaded scene. Actual: Started with {SceneManager.loadedSceneCount} loaded scenes.");
            }

            this.InputManager = new InputManager();

            Debug.Log($"Load UI scene...");
            await AwaitableUtility.RunWithPrintBusyEachFrameAsync(async () => await SceneManager.LoadSceneAsync(UiSceneName, LoadSceneMode.Additive));
            Debug.Log($"...done");

            Debug.Log($"Load initial track scene...");
            await AwaitableUtility.RunWithPrintBusyEachFrameAsync(async () => await SceneManager.LoadSceneAsync(TrackSceneNames[InitialTrackSceneIndex], LoadSceneMode.Additive));
            Debug.Log($"...done");

            this.TrackSwitcher = new TrackSwitcher(this.InputManager, InitialTrackSceneIndex, TrackSceneNames);
            this.InitNewlyLoadedTrack();

            Debug.Log($"END: Main.Start on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
            await this.UpdateLoopAsync();
        }

        private async Awaitable UpdateLoopAsync() {
            Debug.Log($"BEGIN: Main.UpdateLoopAsync");
            while (true) {
                this.InputManager.UpdateBeforeAll();

                this.ControlModeSwitcher.ReadInputAndToggleControlMode();

                switch (this.ControlModeSwitcher.ControlMode) {
                    case ControlModeEnum.Camera: {
                        this.CameraController.ReadInputAndChangeCameraSettings();
                        break;
                    }

                    case ControlModeEnum.Car: {
                        if (await this.TrackSwitcher.ReadInputAndSwitchTracksAsync()) {
                            this.InitNewlyLoadedTrack();
                        } else {
                            if (this.CarSwitcher.ReadInputAndSwitchCar()) {
                            } else {
                                this.CarDebugMover.ReadInputAndMoveCar();
                            }
                        }
                        break;
                    }

                    default: {
                        throw new Exception($"Unknown ControlMode='{this.ControlModeSwitcher.ControlMode}'");
                    }
                }

                this.CameraController.UpdateCameraPosition();
                this.UiManager.Update();

                await Awaitable.NextFrameAsync();
            }
        }

        private void InitNewlyLoadedTrack() {
            Debug.Log("Initialize track...");
            this.ControlModeSwitcher = new ControlModeSwitcher(this.InputManager);
            this.TrackObjects = new TrackObjects();
            this.CarSwitcher = new CarSwitcher(this.InputManager, this.TrackObjects, this.TrackSwitcher);
            this.CarDebugMover = new CarDebugMover(this.InputManager, this.CarSwitcher);
            this.CameraController = new CameraController(this.InputManager, this.CarSwitcher);
            this.UiManager = new UiManager(this.CameraController, this.ControlModeSwitcher);
            Debug.Log("...done");
        }
    }
}
