using System;
using UnityEngine;

namespace ZoomTracks {
    public class TimeManager {
        /// <summary>
        /// Assume this remains constant during runtime.
        /// </summary>
        public float RefreshRate { get; set; }

        /// <summary>
        /// Assume this remains constant during runtime.
        /// </summary>
        public bool UseTimeDeltaTime { get; set; }

        public TimeManager(float? refreshRate, bool useTimeDeltaTime) {
            this.UseTimeDeltaTime = useTimeDeltaTime;
            if (!this.UseTimeDeltaTime && (refreshRate == null || (refreshRate.Value <= 0f))) {
                throw new ArgumentException();
            }

            if (!this.UseTimeDeltaTime) {
                this.RefreshRate = refreshRate.Value;
            }
        }

        public float DeltaTime { get; private set; }

        public void Update() {
            if (this.UseTimeDeltaTime) {
                this.DeltaTime = Time.deltaTime;
            } else {
                this.DeltaTime = 1f / this.RefreshRate;
            }
        }
    }
}
