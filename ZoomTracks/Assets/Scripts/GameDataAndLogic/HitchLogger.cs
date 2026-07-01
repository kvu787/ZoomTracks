using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public sealed class HitchLogger {
        private readonly bool enabled;
        private readonly bool logOnlyHitches;
        private readonly double hitchThresholdMs;
        private readonly FrameTiming[] frameTimings;
        private readonly Stopwatch stopwatch;

        private readonly StreamWriter writer;
        private double previousLoopStartMs;
        private bool hasBaseline;

        public string LogPath { get; }

        public HitchLogger(bool enabled, bool logOnlyHitches, double hitchThresholdMs, string fileName) {
            this.enabled = enabled;
            this.logOnlyHitches = logOnlyHitches;
            this.hitchThresholdMs = hitchThresholdMs;
            this.frameTimings = new FrameTiming[4];

            if (!this.enabled) {
                return;
            }

            Assert.IsTrue(Directory.Exists(Application.persistentDataPath));

            this.LogPath = Path.Combine(Application.persistentDataPath, fileName);
            this.writer = new StreamWriter(this.LogPath, append: false);
            this.stopwatch = Stopwatch.StartNew();

            this.writer.WriteLine(
                "datetime," +
                "frame,phase," +
                "wallDeltaMs,unityUnscaledDeltaMs,unityDeltaMs,realtimeSinceStartup," +
                "cpuFrameMs,cpuMainMs,cpuRenderMs,presentWaitMs,gpuFrameMs," +
                "fixedTime,timeScale,activeScene"
            );

            this.writer.Flush();

            UnityEngine.Debug.Log($"Hitch log path: {this.LogPath.Replace("/", "\\")}");
        }

        public void LogFrameTimingIfNeeded(string phase) {
            if (!this.enabled || this.writer == null) {
                return;
            }

            double nowMs = this.stopwatch.Elapsed.TotalMilliseconds;

            // Avoid a bogus first sample caused by scene loading / startup time before the loop begins.
            if (!this.hasBaseline) {
                this.previousLoopStartMs = nowMs;
                this.hasBaseline = true;
                FrameTimingManager.CaptureFrameTimings();
                return;
            }

            double wallDeltaMs = nowMs - this.previousLoopStartMs;
            this.previousLoopStartMs = nowMs;

            // Captures timing data for recently completed frames.
            // The returned timing generally describes previous frame(s), not necessarily this exact line.
            FrameTimingManager.CaptureFrameTimings();

            double unityUnscaledDeltaMs = Time.unscaledDeltaTime * 1000.0;
            double unityDeltaMs = Time.deltaTime * 1000.0;

            int timingCount = (int)FrameTimingManager.GetLatestTimings(
                (uint)this.frameTimings.Length,
                this.frameTimings
            );

            FrameTiming frameTiming = timingCount > 0 ? this.frameTimings[0] : default;

            bool isHitch =
                wallDeltaMs >= this.hitchThresholdMs ||
                unityUnscaledDeltaMs >= this.hitchThresholdMs ||
                frameTiming.cpuFrameTime >= this.hitchThresholdMs ||
                frameTiming.cpuMainThreadFrameTime >= this.hitchThresholdMs ||
                frameTiming.cpuRenderThreadFrameTime >= this.hitchThresholdMs ||
                frameTiming.cpuMainThreadPresentWaitTime >= this.hitchThresholdMs ||
                frameTiming.gpuFrameTime >= this.hitchThresholdMs;

            if (this.logOnlyHitches && !isHitch) {
                return;
            }

            string activeSceneName = SceneManager.GetActiveScene().name;

            this.writer.WriteLine(string.Join(",",
                $"{DateTime.Now:dd/MM/yyyy hh:mm:ss tt}",
                Time.frameCount.ToString(CultureInfo.InvariantCulture),
                Csv(phase),
                FormatMs(wallDeltaMs),
                FormatMs(unityUnscaledDeltaMs),
                FormatMs(unityDeltaMs),
                Time.realtimeSinceStartupAsDouble.ToString("F10", CultureInfo.InvariantCulture),
                FormatMs(frameTiming.cpuFrameTime),
                FormatMs(frameTiming.cpuMainThreadFrameTime),
                FormatMs(frameTiming.cpuRenderThreadFrameTime),
                FormatMs(frameTiming.cpuMainThreadPresentWaitTime),
                FormatMs(frameTiming.gpuFrameTime),
                Time.fixedTimeAsDouble.ToString("F10", CultureInfo.InvariantCulture),
                Time.timeScale.ToString("F10", CultureInfo.InvariantCulture),
                Csv(activeSceneName)
            ));

            // Hitches are rare, so flushing is useful while investigating.
            // If you suspect this causes a follow-up hitch, remove this and rely on Dispose().
            this.writer.Flush();
        }

        private static string FormatMs(double value) {
            return value.ToString("F10", CultureInfo.InvariantCulture);
        }

        private static string Csv(string value) {
            if (string.IsNullOrEmpty(value)) {
                return "";
            }

            bool mustQuote =
                value.Contains(",") ||
                value.Contains("\"") ||
                value.Contains("\n") ||
                value.Contains("\r");

            if (!mustQuote) {
                return value;
            }

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
