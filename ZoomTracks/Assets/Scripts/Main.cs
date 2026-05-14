using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public class Main : MonoBehaviour {
        private const string UiSceneName = "Ui";
        private const int InitialTrackSceneIndex = 1;
        private static readonly IReadOnlyList<string> TrackSceneNames = new List<string>() {
            "Track1",
            "Track2",
        };
        private const double CollisionTimeoutSeconds = 0.35;

        private DateTime LatestCollisionTime { get; set; }
        private InputManager InputManager { get; set; }
        private TrackSwitcher TrackSwitcher { get; set; }
        private ControlModeSwitcher ControlModeSwitcher { get; set; }
        private CarControlModeSwitcher CarControlModeSwitcher { get; set; }
        private TrackObjects TrackObjects { get; set; }
        private CarSwitcher CarSwitcher { get; set; }
        private CameraController CameraController { get; set; }
        private CarState CarState { get; set; }
        private CollisionManager CollisionManager { get; set; }
        private CameraFocuser CameraFocuser { get; set; }
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

            this.LatestCollisionTime = DateTime.MinValue;
            this.InputManager = new InputManager();

            Debug.Log($"Load UI scene...");
            await AwaitableUtility.RunWithPrintBusyEachFrameAsync(async () => await SceneManager.LoadSceneAsync(UiSceneName, LoadSceneMode.Additive));
            Debug.Log($"...done");

            Debug.Log($"Load initial track scene...");
            await AwaitableUtility.RunWithPrintBusyEachFrameAsync(async () => await SceneManager.LoadSceneAsync(TrackSceneNames[InitialTrackSceneIndex], LoadSceneMode.Additive));
            Debug.Log($"...done");

            this.TrackSwitcher = new TrackSwitcher(this.InputManager, InitialTrackSceneIndex, TrackSceneNames);
            this.InitializeTrack();

            Debug.Log($"END: Main.Start on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
            await this.UpdateLoopAsync();
        }

        private void InitializeTrack() {
            Debug.Log("Initialize track...");
            this.TrackObjects = new TrackObjects();
            this.ControlModeSwitcher = new ControlModeSwitcher(this.InputManager);
            this.CarControlModeSwitcher = new CarControlModeSwitcher(this.InputManager);
            this.CameraController = new CameraController(this.InputManager);
            this.CarSwitcher = new CarSwitcher(this.TrackSwitcher.CurrentTrackScene, this.InputManager);
            this.CarState = new CarState(this.TrackObjects.PlaceholderCarTransform, this.CarSwitcher, this.CameraController, this.InputManager);
            this.CameraFocuser = new CameraFocuser(this.CarState, this.InputManager);
            this.CollisionManager = new CollisionManager(this.TrackObjects, this.CarSwitcher, this.CarState);
            this.UiManager = new UiManager(this.CameraFocuser, this.ControlModeSwitcher);
            Debug.Log("...done");
        }

        private async Awaitable UpdateLoopAsync() {
            Debug.Log($"BEGIN: Main.UpdateLoopAsync");
            while (true) {
                this.InputManager.UpdateInputs();

                if (this.CollisionManager.ResetCarIfColliding()) {
                    this.LatestCollisionTime = DateTime.Now;
                }

                this.ControlModeSwitcher.ReadInputAndToggleMode();
                switch (this.ControlModeSwitcher.Mode) {
                case ControlModeEnum.Camera:
                    this.CameraController.ReadInputAndChangeCameraSettings();
                    this.CameraFocuser.ReadInputAndToggleFocus();
                    break;
                case ControlModeEnum.Car:
                    if (await this.TrackSwitcher.ReadInputAndSwitchTracksAsync()) {
                        this.InitializeTrack();
                    } else {
                        this.CarControlModeSwitcher.ReadInputAndToggleMode();
                        if (this.CarSwitcher.ReadInputAndSwitchCar()) {
                            this.CarState.Reset();
                        } else {
                            if (!this.InCollisionTimeout()) {
                                switch (this.CarControlModeSwitcher.Mode) {
                                case CarControlModeEnum.Standard:
                                    Gamepad gamepad = this.InputManager.Gamepad;
                                    if (gamepad != null) {
                                        this.CarState.ReadInputAndUpdateState_Standard();
                                    }
                                    break;
                                case CarControlModeEnum.Debug:
                                    this.CarState.ReadInputAndUpdateState_Debug();
                                    break;
                                default:
                                    throw new Exception($"Unknown CarControlMode='{this.CarControlModeSwitcher.Mode}'");
                                }
                            }
                        }
                    }
                    break;
                default:
                    throw new Exception($"Unknown ControlMode='{this.ControlModeSwitcher.Mode}'");
                }

                this.CarState.ApplyVelocityToPositionAndRotation();
                this.CarState.ApplyStateToGameObject();
                this.CameraFocuser.UpdateCameraFocusPoint();
                this.UiManager.UpdateUi();

                await Awaitable.NextFrameAsync();
            }
        }

        private bool InCollisionTimeout() {
            return (DateTime.Now - this.LatestCollisionTime) <= TimeSpan.FromSeconds(CollisionTimeoutSeconds);
        }
    }
}
