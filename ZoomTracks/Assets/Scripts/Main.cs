using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public class Main : MonoBehaviour {
        private const string UiSceneName = "Ui";
        private const int InitialTrackSceneIndex = 4;
        private static IReadOnlyList<string> TrackSceneNames { get; } = Array.AsReadOnly(new[] {
            "Track001",
            "Track002",
            "Track003",
            "Track004",
            "Track005",
            "Track007",
            "Track008",
            "Track009",
        });
        private TimeSpan TimeoutDurationSeconds { get; } = TimeSpan.FromSeconds(0.35);

        private DateTime CarControlTimeoutStart { get; set; }
        private InputManager InputManager { get; set; }
        private TrackSwitcher TrackSwitcher { get; set; }

        private CameraFollowSettings CameraFollowSettings { get; set; }
        private TrackObjects TrackObjects { get; set; }
        private ControlModeSwitcher ControlModeSwitcher { get; set; }
        private CarSwitcher CarSwitcher { get; set; }
        private CameraController CameraController { get; set; }
        private GraphicsSettingsManager GraphicsSettingsManager { get; set; }
        private CarState CarState { get; set; }
        private CollisionManager CollisionManager { get; set; }
        private CameraPivotManager CameraPivotManager { get; set; }
        private UiManager UiManager { get; set; }

        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Awake.html
        private void Awake() {
            Debug.Log($"BEGIN: Main.Awake on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
            Debug.Log($"Log path for standalone exe: {Application.persistentDataPath}/Player.enableLog".Replace("/", "\\"));
            GraphicsSettingsManager.Awake();
            DebugManager.instance.enableRuntimeUI = false;
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
            this.CameraController = new CameraController(this.CameraFollowSettings, this.TrackSwitcher.CurrentTrackJson, this.InputManager);
            this.GraphicsSettingsManager = new GraphicsSettingsManager(this.CameraController, this.InputManager);
            this.CarSwitcher = new CarSwitcher(this.TrackSwitcher.CurrentTrackScene, this.TrackSwitcher.CurrentTrackJson, this.InputManager);
            this.CarState = new CarState(this.TrackObjects.PlaceholderCarTransform, this.TrackSwitcher, this.CarSwitcher, this.CameraController, this.InputManager);
            this.CameraPivotManager = new CameraPivotManager(this.CameraFollowSettings, this.CameraController, this.CarState, this.InputManager);
            this.CollisionManager = new CollisionManager(this.TrackObjects, this.CarSwitcher, this.CarState);
            this.UiManager = new UiManager();
            Debug.Log("...done");
        }

        private async Awaitable UpdateLoopAsync() {
            Debug.Log($"BEGIN: Main.UpdateLoopAsync");
            while (true) {
                this.InputManager.UpdateInputs();

                if (await this.TrackSwitcher.ReadInputAndSwitchTracksAsync()) {
                    this.InitializeTrack();
                    GarbageCollectionUtility.ForceGarbageCollection();
                } else {
                    if (this.CollisionManager.ResetCarIfColliding()) {
                        this.CarControlTimeoutStart = DateTime.Now;
                    }
                    this.CameraController.ReadInputAndChangeCameraSettings();
                    this.CameraPivotManager.ReadInputAndToggle();
                    this.GraphicsSettingsManager.ReadInputAndUpdate();
                    if (this.CarSwitcher.ReadInputAndSwitchCar()) {
                        this.CarState.Reset();
                        this.CarControlTimeoutStart = DateTime.Now;
                    } else if (!this.InCarControlTimeout()) {
                        this.CarState.ReadInputAndUpdateState();
                    }
                }

                this.CarState.ApplyVelocityToPositionAndRotation();
                this.CarState.ApplyStateToGameObject();
                this.CameraController.Update();
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
