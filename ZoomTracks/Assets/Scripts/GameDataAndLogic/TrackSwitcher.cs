using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/*
                case GameStateEnum.LoadUiScene:
                    this.LoadUnloadOrWait(
                        sceneName: UiSceneName,
                        isLoad: true,
                        nextState: GameStateEnum.LoadNewTrack);
                    break;

                case GameStateEnum.LoadNewTrack:
                    this.LoadUnloadOrWait(
                        sceneName: TrackSceneNames[this.TrackSwitcher.NewTrackIndex],
                        isLoad: true,
                        nextState: GameStateEnum.InitNewTrack);
                    break;

                case GameStateEnum.InitNewTrack:
                    this.TrackSwitcher.SwitchingTrackFinished();
                    Debug.Log("Start initializing track...");
                    this.ControlModeSwitcher = new ControlModeSwitcher();
                    this.TrackObjects = new TrackObjects();
                    this.CarSwitcher = new CarSwitcher(this.TrackObjects, this.TrackSwitcher);
                    this.CarMover = new CarMover(this.CarSwitcher);
                    this.CameraController = new CameraController(this.CarSwitcher);
                    this.UiManager = new UiManager(this.CameraController, this.ControlModeSwitcher);
                    Debug.Log("...Finish initializing track");
                    this.GameState = GameStateEnum.RunGame;
                    break;

                case GameStateEnum.UnloadOldTrack:
                    this.LoadUnloadOrWait(
                        sceneName: TrackSceneNames[this.TrackSwitcher.OldTrackIndex],
                        isLoad: false,
                        nextState: GameStateEnum.LoadNewTrack);
                    break;
*/

namespace ZoomTracks {
    public class TrackSwitcher {
        public bool WasOperationFinishedThisFrame { get; private set; }

        public int CurrentTrackIndex { get; private set; }
        public int OldTrackIndex { get; private set; }
        public int NewTrackIndex { get; private set; }

        private readonly int tracksCount;

        public Scene CurrentTrackScene;
        private readonly IReadOnlyList<string> TrackSceneNames;

        private readonly SceneManager SceneManager;

        public TrackSwitcher(int initialTrackSceneIndex, int tracksCount, IReadOnlyList<string> trackSceneNames) {
            this.CurrentTrackIndex = -1;
            this.OldTrackIndex = -1;
            this.NewTrackIndex = initialTrackSceneIndex;

            this.tracksCount = tracksCount;
            this.TrackSceneNames = trackSceneNames;
        }

        public bool ReadInputAndSwitchTracks(Keyboard keyboard, Gamepad gamepad) {
            bool isPrevTrack = false;
            bool isNextTrack = false;

            if (keyboard != null) {
                isPrevTrack = isPrevTrack || keyboard.aKey.wasPressedThisFrame;
                isNextTrack = isNextTrack || keyboard.gKey.wasPressedThisFrame;
            }

            if (gamepad != null) {
                isPrevTrack = isPrevTrack || gamepad.leftShoulder.wasPressedThisFrame;
                isNextTrack = isNextTrack || gamepad.rightShoulder.wasPressedThisFrame;
            }

            if (isPrevTrack == isNextTrack) {
                return false;
            } else {
                if (isPrevTrack) {
                    this.NewTrackIndex = this.NewTrackIndex.CyclePrev(this.tracksCount);
                } else if (isNextTrack) {
                    this.NewTrackIndex = this.NewTrackIndex.CycleNext(this.tracksCount);
                }
                this.OldTrackIndex = this.CurrentTrackIndex;
                this.CurrentTrackIndex = -1;
                return true;
            }
        }

        private enum TrackSwitcherStateEnum {
            LoadNewTrack,
            InitNewTrack,
            UnloadOldTrack,
        }

        private TrackSwitcherStateEnum TrackSwitcherState;

        public void LoadNewTrack(string sceneName) {
            this.SceneManager.LoadScene(sceneName);
            this.TrackSwitcherState = TrackSwitcherStateEnum.LoadNewTrack;
        }

        public void UpdateBeforeAll() {
            this.SceneManager.UpdateBeforeAll();
            this.WasOperationFinishedThisFrame = this.SceneManager.WasOperationFinishedThisFrame;
        }

        public void Update() {
            switch (this.TrackSwitcherState) {
                case TrackSwitcherStateEnum.LoadNewTrack:
                    if (!this.SceneManager.IsOperationRunning()) {
                        Assert.IsTrue(this.SceneManager.WasOperationFinishedThisFrame);
                        this.TrackSwitcherState = TrackSwitcherStateEnum.InitNewTrack;
                    }
                    break;
                case TrackSwitcherStateEnum.InitNewTrack:

                    break;
                case TrackSwitcherStateEnum.UnloadOldTrack:
                    break;
                default:
                    break;
            }
        }

        public void UpdateAfterAll() {
            this.WasOperationFinishedThisFrame = false;
            this.SceneManager.UpdateAfterAll();
        }

        public void SwitchingTrackFinished() {
            this.CurrentTrackIndex = this.NewTrackIndex;
            this.OldTrackIndex = -1;
            this.NewTrackIndex = -1;
            this.CurrentTrackScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(this.TrackSceneNames[this.CurrentTrackIndex]);
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
