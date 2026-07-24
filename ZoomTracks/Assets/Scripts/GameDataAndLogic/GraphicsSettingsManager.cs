using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZoomTracks {
    public class GraphicsSettingsManager {
        private enum MsaaModeEnum {
            Off,
            //Msaa2x,
            //Msaa4x,
            Msaa8x,
        }

        private enum TaaModeEnum {
            Off,
            VeryLow,
            Low,
            Medium,
            High,
            VeryHigh,
        }

        private enum VsyncModeEnum {
            Off,
            EveryVBlank,
        }

        private enum RenderScaleEnum {
            Scale0_125,
            Scale0_25,
            Scale0_5,
            Scale1,
            Scale1_5,
            Scale2,
        }

        private UniversalAdditionalCameraData CameraData { get; }
        private InputManager InputManager { get; }

        private MsaaModeEnum MsaaMode { get; set; }
        private TaaModeEnum TaaMode { get; set; }
        //private VsyncModeEnum VsyncMode { get; set; }
        private RenderScaleEnum RenderScale { get; set; }

        /// <summary>
        /// This is necessary to prevent changes to the URP asset from persisting between different game start/stop
        /// sessions within the same Unity Editor session.
        /// </summary>
        public static void UseRuntimeOnlyCopyOfUrpAsset() {
            Assert.IsNull(GraphicsSettings.defaultRenderPipeline);
            UniversalRenderPipelineAsset urpOriginal = UniversalRenderPipeline.asset;
            Assert.IsNotNull(urpOriginal);
            UniversalRenderPipelineAsset urpRuntimeCopy = Object.Instantiate(urpOriginal);
            Assert.IsNotNull(urpRuntimeCopy);
            QualitySettings.renderPipeline = urpRuntimeCopy;
            UniversalRenderPipelineAsset urp = UniversalRenderPipeline.asset;
            Assert.IsNotNull(urp);
        }

        public static void ConfigureSessionGraphicsSettings() {
            UseRuntimeOnlyCopyOfUrpAsset();
            UniversalRenderPipeline.asset.supportsHDR = false;
            QualitySettings.maxQueuedFrames = 0;
            Application.targetFrameRate = -1;
        }

        public GraphicsSettingsManager(CameraController cameraController, InputManager inputManager) {
            this.CameraData = cameraController.CameraData;
            this.InputManager = inputManager;

            this.MsaaMode = MsaaModeEnum.Off;
            this.TaaMode = TaaModeEnum.Off;
            //this.VsyncMode = VsyncModeEnum.EveryVBlank;
            this.RenderScale = RenderScaleEnum.Scale1;

            this.CameraData.taaSettings = TemporalAA.Settings.Create();
            this.ApplyGraphicsSettings();
        }

        public void ReadInputAndUpdate() {
            if (this.InputManager.Gamepad != null) {
                Gamepad gamepad = this.InputManager.Gamepad;
                if (!gamepad.leftShoulder.IsPressed()) {
                    return;
                }
                if (gamepad.aButton.wasPressedThisFrame) {
                    this.MsaaMode = this.MsaaMode.Next();
                } else if (gamepad.bButton.wasPressedThisFrame) {
                    this.TaaMode = this.TaaMode.Next();
                } else if (gamepad.xButton.wasPressedThisFrame) {
                    //this.VsyncMode = this.VsyncMode.Next();
                } else if (gamepad.yButton.wasPressedThisFrame) {
                    this.RenderScale = this.RenderScale.Next();
                } else {
                    return;
                }
                this.ApplyGraphicsSettings();
            }
        }

        private void ApplyGraphicsSettings() {
            UniversalRenderPipeline.asset.msaaSampleCount = this.MsaaMode switch {
                MsaaModeEnum.Off => 1,
                //MsaaModeEnum.Msaa2x => 2,
                //MsaaModeEnum.Msaa4x => 4,
                MsaaModeEnum.Msaa8x => 8,
                _ => throw new System.Exception(),
            };

            if (this.TaaMode == TaaModeEnum.Off) {
                this.CameraData.renderPostProcessing = false;
                this.CameraData.antialiasing = AntialiasingMode.None;
            } else {
                this.CameraData.renderPostProcessing = true;
                this.CameraData.antialiasing = AntialiasingMode.TemporalAntiAliasing;
                this.CameraData.taaSettings.quality = this.TaaMode switch {
                    TaaModeEnum.Off => throw new System.Exception(),
                    TaaModeEnum.VeryLow => TemporalAAQuality.VeryLow,
                    TaaModeEnum.Low => TemporalAAQuality.Low,
                    TaaModeEnum.Medium => TemporalAAQuality.Medium,
                    TaaModeEnum.High => TemporalAAQuality.High,
                    TaaModeEnum.VeryHigh => TemporalAAQuality.VeryHigh,
                    _ => throw new System.Exception(),
                };
            }

            //QualitySettings.vSyncCount = this.VsyncMode switch {
            //    VsyncModeEnum.Off => 0,
            //    VsyncModeEnum.EveryVBlank => 1,
            //    _ => throw new System.Exception(),
            //};

            UniversalRenderPipeline.asset.renderScale = this.RenderScale switch {
                RenderScaleEnum.Scale0_125 => 0.125f,
                RenderScaleEnum.Scale0_25 => 0.25f,
                RenderScaleEnum.Scale0_5 => 0.5f,
                RenderScaleEnum.Scale1 => 1f,
                RenderScaleEnum.Scale1_5 => 1.5f,
                RenderScaleEnum.Scale2 => 2f,
                _ => throw new System.Exception()
            };
        }
    }
}
