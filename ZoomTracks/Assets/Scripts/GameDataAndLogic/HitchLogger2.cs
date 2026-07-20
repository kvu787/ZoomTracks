using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace ZoomTracks {
    public sealed class HitchLogger2 {
        private StreamWriter StreamWriter { get; }
        private long PreviousFrameTime_Ticks { get; set; }

        private TimeManager TimeManager { get; set; }

        private double FrameDurationThreshold_Milliseconds { get; }

        public HitchLogger2(string filePath, TimeManager timeManager) {
            this.StreamWriter = new StreamWriter(filePath);
            this.PreviousFrameTime_Ticks = Stopwatch.GetTimestamp();
            this.TimeManager = timeManager;
            this.FrameDurationThreshold_Milliseconds = ((1.0 / this.TimeManager.RefreshRate) * 1.05) * 1000;
        }

        public void Update() {
            if (this.TimeManager.UseTimeDeltaTime) {
                return;
            }

            long previousFrameTime_Ticks = this.PreviousFrameTime_Ticks;
            long currentFrameTime_Ticks = Stopwatch.GetTimestamp();

            long frameDuration_Ticks = currentFrameTime_Ticks - previousFrameTime_Ticks;
            double frameDuration_Milliseconds = (frameDuration_Ticks * (1.0 / Stopwatch.Frequency)) * 1000.0;
            if (frameDuration_Milliseconds > this.FrameDurationThreshold_Milliseconds) {
                this.StreamWriter.WriteLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] Frame {Time.frameCount} took {frameDuration_Milliseconds:F4} ms ({frameDuration_Ticks} ticks)");
                this.StreamWriter.Flush();
            }

            this.PreviousFrameTime_Ticks = currentFrameTime_Ticks;
        }

        public void InsertSpacer() {
            this.StreamWriter.WriteLine();
            this.StreamWriter.WriteLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] Spacer ########################################");
            this.StreamWriter.WriteLine();
            this.StreamWriter.Flush();
        }
    }
}
