using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public class Main : MonoBehaviour {
        private StutterLogger StutterLogger { get; set; }

        private const string RefreshRateFlag = "-refreshRate";
        private const string StutterLogFilePathFlag = "-stutterLogFilePath";
        private const string UiSceneName = "Ui";
        private const int InitialTrackIndex = 0;
        private static IReadOnlyList<string> TrackNames { get; } = Array.AsReadOnly(new[] {
            "Track001",
        });

        private TimeSpan TimeoutDurationSeconds { get; } = TimeSpan.FromSeconds(0.35);
        private bool SkipOneIterationOfCarControlInput { get; set; } = false;

        private DateTime CarControlTimeoutStart { get; set; }
        private TimeManager TimeManager { get; set; }
        private InputManager InputManager { get; set; }
        private TrackSwitcher TrackSwitcher { get; set; }

        private CameraFollowSettings CameraFollowSettings { get; set; }
        private TrackObjects TrackObjects { get; set; }
        private CarSwitcher CarSwitcher { get; set; }
        private CameraController CameraController { get; set; }
        private GraphicsSettingsManager GraphicsSettingsManager { get; set; }
        private CarState CarState { get; set; }
        private CollisionManager2 CollisionManager2 { get; set; }
        private CameraPivotManager CameraPivotManager { get; set; }
        private UiManager UiManager { get; set; }

        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Awake.html
        private void Awake() {
            Debug.Log($"BEGIN: Main.Awake on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
            Debug.Log($"Log path for standalone exe: {Application.persistentDataPath}/Player.log".Replace("/", "\\"));

            GraphicsSettingsManager.UseRuntimeOnlyCopyOfUrpAsset();
            GraphicsSettingsManager.ConfigureSessionGraphicsSettings();
            DebugManager.instance.enableRuntimeUI = false;

            Debug.Log($"END: Main.Awake on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
        }

        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Start.html
        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Awaitable.html
        private async void Start() {
            Debug.Log($"BEGIN: Main.Start on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");

            PrintInfoUtility.PrintDisplayInfo();
            PrintInfoUtility.PrintGraphicsInfo();

            if (SceneManager.loadedSceneCount != 1) {
                throw new Exception($"Expected: Start with 1 loaded scene. Actual: Started with {SceneManager.loadedSceneCount} loaded scenes.");
            }

            if (InitialTrackIndex < 0 || InitialTrackIndex > (TrackNames.Count - 1)) {
                throw new Exception($"Invalid InitialTrackIndex={InitialTrackIndex}. TrackNames.Count={TrackNames.Count}.");
            }

            this.CarControlTimeoutStart = DateTime.MinValue;
            this.ProcessCommandLineArguments();
            this.InputManager = new InputManager();

            Debug.Log($"Load UI scene...");
            await AwaitableUtility.RunWithPrintBusyEachFrameAsync(async () => await SceneManager.LoadSceneAsync(UiSceneName, LoadSceneMode.Additive));
            Debug.Log($"...done");

            Debug.Log($"Load initial track scene...");
            await AwaitableUtility.RunWithPrintBusyEachFrameAsync(async () => await SceneManager.LoadSceneAsync(TrackNames[InitialTrackIndex], LoadSceneMode.Additive));
            Debug.Log($"...done");

            this.TrackSwitcher = new TrackSwitcher(this.InputManager, TrackNames, InitialTrackIndex);
            this.InitializeTrack();

            Debug.Log($"END: Main.Start on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
            await this.UpdateLoopAsync();
        }

        private void InitializeTrack() {
            Debug.Log("Initialize track...");
            this.CameraFollowSettings = new CameraFollowSettings(this.TrackSwitcher.CurrentTrackJson);
            this.TrackObjects = new TrackObjects();
            this.CameraController = new CameraController(this.CameraFollowSettings, this.TrackSwitcher.CurrentTrackJson, this.InputManager, this.TimeManager);
            this.GraphicsSettingsManager = new GraphicsSettingsManager(this.CameraController, this.InputManager);
            this.CarSwitcher = new CarSwitcher(this.TrackSwitcher.CurrentTrackScene, this.TrackSwitcher.CurrentTrackJson, this.InputManager);
            this.CarState = new CarState(this.TrackObjects.PlaceholderCarTransform, this.CarSwitcher, this.CameraController, this.InputManager, this.TimeManager);
            this.CarState.ApplyStateToGameObject();
            this.CameraPivotManager = new CameraPivotManager(this.CameraFollowSettings, this.CameraController, this.CarState, this.InputManager);
            this.CollisionManager2 = new CollisionManager2(this.TrackSwitcher.CurrentTrackName, this.CarSwitcher);
            this.UiManager = new UiManager(this.CameraController);
            Debug.Log("...done");
        }

        private async Awaitable UpdateLoopAsync() {
            Debug.Log($"BEGIN: Main.UpdateLoopAsync");
            while (true) {
                //this.StutterLogger.Update();
                //this.TimeManager.Update();
                //this.InputManager.UpdateInputs();

                //if (this.InputManager.QuitGame) {
                //    Application.Quit();
                //}

                //if (this.InputManager.InsertStutterLogSpacer) {
                //    this.StutterLogger.InsertSpacer();
                //}

                //if (this.InputManager.ToggleBetweenBorderlessAndExclusiveFullScreen) {
                //    if (Screen.fullScreenMode == FullScreenMode.FullScreenWindow) {
                //        Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                //    } else if (Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen) {
                //        Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                //    } else {
                //        throw new Exception($"Tried to toggle borderless/fullscreen with an invalid Screen.fullScreenMode of {Screen.fullScreenMode}");
                //    }
                //    Debug.Log($"Fullscreen mode changed to {Screen.fullScreenMode}");
                //}

                ////
                //// Update lap time
                ////
                //// If car intersects with next checkpoint:
                ////   If next checkpoint is checkered line:
                ////     Set previous lap time to current lap time
                ////     Check if lap time beats best lap time
                ////     Reset current lap time
                ////     Update next checkpoint
                ////   Else:
                ////     Update next checkpoint
                ////
                //// Keep the current track's lap times in memory
                //// Non-current track's lap times are guaranteed to be on disk
                ////
                //// One JSON file for each track
                ////
                //// Save to file for these events:
                //// - Switch track
                //// - Switch car
                //// - Reset car
                //// - OnApplicationQuit
                //// - OnApplicationPause
                ////

                //bool wasTrackSwitched = await this.TrackSwitcher.ReadInputAndSwitchTracksAsync();
                //if (wasTrackSwitched) {
                //    this.InitializeTrack();
                //} else {
                //    if (this.InputManager.ResetCar || this.CollisionManager2.IsCarColliding()) {
                //        /*
                //        Explanation for collision behavior:
                //        Let frame N be the update iteration that results in the car colliding an obstacle.
                //        This means that the current execution is in frame N+1.
                //        We want frame N to show that car overlapping the obstacle.
                //        We want frame N+1 to reset the car position and skip execution of `this.CarState.ReadInputAndUpdateState()` for at least one frame.
                //        */
                //        this.CarState.Reset_PositionRotationVelocity();
                //        this.CarControlTimeoutStart = DateTime.Now;
                //        this.SkipOneIterationOfCarControlInput = true;
                //    }

                //    this.CameraController.ReadInputAndChangeCameraSettings();
                //    this.CameraPivotManager.ReadInputAndToggle();
                //    this.GraphicsSettingsManager.ReadInputAndUpdate();
                //    if (this.CarSwitcher.ReadInputAndSwitchCar()) {
                //        this.CarState.Reset_PositionRotationVelocity();
                //        this.CarControlTimeoutStart = DateTime.Now;
                //    } else if (!this.SkipOneIterationOfCarControlInput && !this.InCarControlTimeout()) {
                //        this.CarState.ReadInputAndUpdateState();
                //    }
                //}

                //this.SkipOneIterationOfCarControlInput = false;

                //this.CarState.ApplyStateToGameObject();
                //this.CameraController.Update();
                //this.CameraPivotManager.UpdateCameraPivot();
                //this.UiManager.UpdateUi();

                //if (wasTrackSwitched) {
                //    GarbageCollectionUtility.ForceGarbageCollection();
                //    Debug.Log($"Unload unused assets...");
                //    await AwaitableUtility.RunWithPrintBusyEachFrameAsync(async () => await Resources.UnloadUnusedAssets());
                //    Debug.Log($"...done");
                //    GarbageCollectionUtility.ForceGarbageCollection();
                //}

                await Awaitable.NextFrameAsync();
            }
        }

        private bool InCarControlTimeout() {
            return (DateTime.Now - this.CarControlTimeoutStart) <= this.TimeoutDurationSeconds;
        }

        private void ProcessCommandLineArguments() {
            string[] commandLineArgs = Environment.GetCommandLineArgs();

            if (commandLineArgs.Contains(RefreshRateFlag)) {
                int i = Array.IndexOf(commandLineArgs, RefreshRateFlag);
                if ((i + 1) >= commandLineArgs.Length) {
                    throw new Exception($"No value found for {RefreshRateFlag}");
                }
                float refreshRate = ParseUtility.ParseFloat(commandLineArgs[i + 1]);
                if (refreshRate <= 0f) {
                    Debug.Log("Received zero or negative refresh rate, so using Time.deltaTime for the timestep, which means a variable timestep");
                    this.TimeManager = new TimeManager(refreshRate: null, useTimeDeltaTime: true);
                } else {
                    Debug.Log($"Received a refresh rate of {refreshRate} Hz, which means a fixed delta and recording stutters");
                    this.TimeManager = new TimeManager(refreshRate, useTimeDeltaTime: false);
                }
            } else {
                Debug.Log($"Didn't receive {RefreshRateFlag} flag, so using Time.deltaTime for the timestep, which means a variable timestep");
                this.TimeManager = new TimeManager(refreshRate: null, useTimeDeltaTime: true);
            }

            {
                string stutterLogFilePath;
                if (commandLineArgs.Contains(StutterLogFilePathFlag)) {
                    int i = Array.IndexOf(commandLineArgs, StutterLogFilePathFlag);
                    if (i != commandLineArgs.Length - 2) {
                        throw new Exception($"If {StutterLogFilePathFlag} is provided, it must be at the end of the command line arguments");
                    }
                    if ((i + 1) >= commandLineArgs.Length) {
                        throw new Exception($"No value found for {StutterLogFilePathFlag}");
                    }
                    stutterLogFilePath = commandLineArgs[i + 1];
                    Debug.Log($"Using a file path specified from the command line for stutter log: ${stutterLogFilePath}");
                } else {
                    stutterLogFilePath = $"{Application.persistentDataPath}/Stutter.log".Replace("/", "\\");
                    Debug.Log($"Didn't receive {StutterLogFilePathFlag} flag, so using the default file path for stutter log: ${stutterLogFilePath}");
                }
                this.StutterLogger = new StutterLogger(stutterLogFilePath, this.TimeManager);
            }
        }
    }
}
