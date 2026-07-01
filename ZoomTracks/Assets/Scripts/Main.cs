using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public class Main : MonoBehaviour {
        private const bool RecordHitches = false;
        private HitchLogger HitchLogger { get; set; }

        private const string UiSceneName = "Ui";
        private const int InitialTrackSceneIndex = 13;
        private static IReadOnlyList<string> TrackSceneNames { get; } = Array.AsReadOnly(new[] {
            "Track001",
            "Track002",
            "Track003",
            "Track004",
            "Track005",
            "Track007",
            "Track008",
            "Track009",
            "Track011",
            "Track012",
            "Track019",
            "Track020",
            "Track021",
            "Track022",
            "Track023",
        });

        private TimeSpan TimeoutDurationSeconds { get; } = TimeSpan.FromSeconds(0.35);
        private bool SkipOneIterationOfCarControlInput { get; set; } = false;

        private DateTime CarControlTimeoutStart { get; set; }
        private InputManager InputManager { get; set; }
        private TrackSwitcher TrackSwitcher { get; set; }

        private CameraFollowSettings CameraFollowSettings { get; set; }
        private TrackObjects TrackObjects { get; set; }
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
            Debug.Log($"Log path for standalone exe: {Application.persistentDataPath}/Player.log".Replace("/", "\\"));

            this.HitchLogger = new HitchLogger(
                enabled: true,
                logOnlyHitches: true,
                hitchThresholdMs: (1000.0 / 60.0) * 1.1,
                fileName: $"{DateTime.Now.Ticks}_hitches.csv");

            GraphicsSettingsManager.Awake();
            DebugManager.instance.enableRuntimeUI = false;
            Debug.Log($"END: Main.Awake on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
        }

        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Start.html
        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Awaitable.html
        private async void Start() {
            Debug.Log($"BEGIN: Main.Start on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");

            PrintInfoUtility.PrintDisplayMode();
            PrintInfoUtility.PrintGraphicsInfo();

            if (SceneManager.loadedSceneCount != 1) {
                throw new Exception($"Expected: Start with 1 loaded scene. Actual: Started with {SceneManager.loadedSceneCount} loaded scenes.");
            }

            if (InitialTrackSceneIndex < 0 || InitialTrackSceneIndex > (TrackSceneNames.Count - 1)) {
                throw new Exception($"Invalid InitialTrackSceneIndex={InitialTrackSceneIndex}. TrackSceneNames.Count={TrackSceneNames.Count}.");
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
            this.CameraController = new CameraController(this.CameraFollowSettings, this.TrackSwitcher.CurrentTrackJson, this.InputManager);
            this.GraphicsSettingsManager = new GraphicsSettingsManager(this.CameraController, this.InputManager);
            this.CarSwitcher = new CarSwitcher(this.TrackSwitcher.CurrentTrackScene, this.TrackSwitcher.CurrentTrackJson, this.InputManager);
            this.CarState = new CarState(this.TrackObjects.PlaceholderCarTransform, this.CarSwitcher, this.CameraController, this.InputManager);
            this.CameraPivotManager = new CameraPivotManager(this.CameraFollowSettings, this.CameraController, this.CarState, this.InputManager);
            this.CollisionManager = new CollisionManager(this.TrackObjects, this.CarSwitcher, this.CarState);
            this.UiManager = new UiManager(this.CameraController);
            Debug.Log("...done");
        }

        private async Awaitable UpdateLoopAsync() {
            Debug.Log($"BEGIN: Main.UpdateLoopAsync");
            while (true) {
                if (RecordHitches) {
#pragma warning disable CS0162 // Unreachable code detected
                    this.HitchLogger.LogFrameTimingIfNeeded("UpdateLoopStart");
#pragma warning restore CS0162 // Unreachable code detected
                }

                this.InputManager.UpdateInputs();

                if (this.InputManager.Keyboard != null && this.InputManager.Keyboard.escapeKey.wasPressedThisFrame) {
                    Application.Quit();
                }
                if (this.InputManager.Gamepad != null && this.InputManager.Gamepad.startButton.wasPressedThisFrame) {
                    Application.Quit();
                }

                bool wasTrackSwitched = await this.TrackSwitcher.ReadInputAndSwitchTracksAsync();
                if (wasTrackSwitched) {
                    this.InitializeTrack();
                } else {
                    if (this.CollisionManager.ResetCarIfColliding()) {
                        /*
                        Explanation for collision behavior:
                        Let frame N be the update iteration that results in the car colliding an obstacle.
                        This means that the current execution is in frame N+1.
                        We want frame N to show that car overlapping the obstacle.
                        We want frame N+1 to reset the car position and skip execution of `this.CarState.ReadInputAndUpdateState()` for at least one frame.
                        */
                        this.CarControlTimeoutStart = DateTime.Now;
                        this.SkipOneIterationOfCarControlInput = true;
                    }

                    this.CameraController.ReadInputAndChangeCameraSettings();
                    this.CameraPivotManager.ReadInputAndToggle();
                    this.GraphicsSettingsManager.ReadInputAndUpdate();
                    if (this.CarSwitcher.ReadInputAndSwitchCar()) {
                        this.CarState.Reset_PositionRotationVelocity();
                        this.CarControlTimeoutStart = DateTime.Now;
                    } else if (!this.SkipOneIterationOfCarControlInput && !this.InCarControlTimeout()) {
                        this.CarState.ReadInputAndUpdateState();
                    }
                }

                this.SkipOneIterationOfCarControlInput = false;

                this.CarState.ApplyStateToGameObject();
                this.CameraController.Update();
                this.CameraPivotManager.UpdateCameraPivot();
                this.UiManager.UpdateUi();

                if (wasTrackSwitched) {
                    GarbageCollectionUtility.ForceGarbageCollection();
                    Debug.Log($"Unload unused assets...");
                    await AwaitableUtility.RunWithPrintBusyEachFrameAsync(async () => await Resources.UnloadUnusedAssets());
                    Debug.Log($"...done");
                    GarbageCollectionUtility.ForceGarbageCollection();
                }

                await Awaitable.NextFrameAsync();
            }
        }

        private bool InCarControlTimeout() {
            return (DateTime.Now - this.CarControlTimeoutStart) <= this.TimeoutDurationSeconds;
        }
    }
}
