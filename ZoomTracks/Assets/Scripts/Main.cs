using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace ZoomTracks {
    public class Main : MonoBehaviour {
        private const string UiSceneName = "Ui";
        private const int InitialTrackSceneIndex = 1;
        private static IReadOnlyList<string> TrackSceneNames { get; } = Array.AsReadOnly(new[] {
            "Track1",
            "Track2",
        });
        private TimeSpan TimeoutDurationSeconds { get; } = TimeSpan.FromSeconds(0.35);

        private DateTime CarControlTimeoutStart { get; set; }
        private InputManager InputManager { get; set; }
        private TrackSwitcher TrackSwitcher { get; set; }

        private CameraFollowSettings CameraFollowSettings { get; set; }
        private TrackObjects TrackObjects { get; set; }
        private ControlModeSwitcher ControlModeSwitcher { get; set; }
        private CarControlModeSwitcher CarControlModeSwitcher { get; set; }
        private CarSwitcher CarSwitcher { get; set; }
        private CameraController CameraController { get; set; }
        private CarState CarState { get; set; }
        private CollisionManager CollisionManager { get; set; }
        private CameraPivotManager CameraPivotManager { get; set; }
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

            this.CarControlTimeoutStart = DateTime.MinValue;
            this.InputManager = new InputManager();

            Debug.Log($"Load UI scene...");
            await AwaitableUtility.RunWithPrintBusyEachFrameAsync(async () => await SceneManager.LoadSceneAsync(UiSceneName, LoadSceneMode.Additive));
            Debug.Log($"...done");

            Debug.Log($"Load initial track scene...");
            await AwaitableUtility.RunWithPrintBusyEachFrameAsync(async () => await SceneManager.LoadSceneAsync(TrackSceneNames[InitialTrackSceneIndex], LoadSceneMode.Additive));
            Debug.Log($"...done");

            this.TrackSwitcher = new TrackSwitcher(this.InputManager, TrackSceneNames, InitialTrackSceneIndex);
            this.InitializeTrack();

            Debug.Log($"END: Main.Start on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
            await this.UpdateLoopAsync();
        }

        private void InitializeTrack() {
            Debug.Log("Initialize track...");
            this.CameraFollowSettings = new CameraFollowSettings(this.TrackSwitcher.CurrentTrackJson);
            this.TrackObjects = new TrackObjects();
            this.ControlModeSwitcher = new ControlModeSwitcher(this.InputManager);
            this.CarControlModeSwitcher = new CarControlModeSwitcher(this.InputManager);
            this.CameraController = new CameraController(this.CameraFollowSettings, this.TrackSwitcher.CurrentTrackJson, this.InputManager);
            this.CarSwitcher = new CarSwitcher(this.TrackSwitcher.CurrentTrackScene, this.TrackSwitcher.CurrentTrackJson, this.InputManager);
            this.CarState = new CarState(this.TrackObjects.PlaceholderCarTransform, this.CarSwitcher, this.CameraController, this.InputManager);
            this.CameraPivotManager = new CameraPivotManager(this.CameraFollowSettings, this.CameraController, this.CarState, this.InputManager);
            this.CollisionManager = new CollisionManager(this.TrackObjects, this.CarSwitcher, this.CarState);
            this.UiManager = new UiManager(this.CameraFollowSettings, this.ControlModeSwitcher, this.CarControlModeSwitcher);
            Debug.Log("...done");
        }

        private static void ForceGarbageCollection() {
            Assert.IsTrue(GarbageCollector.GCMode == GarbageCollector.Mode.Enabled);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private async Awaitable UpdateLoopAsync() {
            Debug.Log($"BEGIN: Main.UpdateLoopAsync");
            while (true) {
                this.InputManager.UpdateInputs();
                if (await this.TrackSwitcher.ReadInputAndSwitchTracksAsync()) {
                    this.InitializeTrack();
                    ForceGarbageCollection();
                }
                if (this.CollisionManager.ResetCarIfColliding()) {
                    this.CarControlTimeoutStart = DateTime.Now;
                }
                ControlModeEnum controlMode = this.ControlModeSwitcher.ReadInputAndToggleMode();
                switch (controlMode) {
                case ControlModeEnum.Camera:
                    this.CameraController.ReadInputAndChangeCameraSettings();
                    this.CameraPivotManager.ReadInputAndToggle();
                    break;
                case ControlModeEnum.Car:
                    CarControlModeEnum carControlMode = this.CarControlModeSwitcher.ReadInputAndToggleMode();
                    if (this.CarSwitcher.ReadInputAndSwitchCar()) {
                        this.CarState.Reset();
                        this.CarControlTimeoutStart = DateTime.Now;
                    }
                    if (!this.InCarControlTimeout()) {
                        switch (carControlMode) {
                        case CarControlModeEnum.Standard:
                            this.CarState.ReadInputAndUpdateState_Standard();
                            break;
                        case CarControlModeEnum.Debug:
                            this.CarState.ReadInputAndUpdateState_Debug();
                            break;
                        default:
                            throw new Exception($"Unknown CarControlMode='{carControlMode}'");
                        }
                    }
                    break;
                default:
                    throw new Exception($"Unknown ControlMode='{controlMode}'");
                }

                this.CarState.ApplyVelocityToPositionAndRotation();
                this.CarState.ApplyStateToGameObject();
                this.CameraPivotManager.UpdateCameraPivot();
                this.UiManager.UpdateUi();

                await Awaitable.NextFrameAsync();
            }
        }

        private bool InCarControlTimeout() {
            return (DateTime.Now - this.CarControlTimeoutStart) <= this.TimeoutDurationSeconds;
        }
    }
}
