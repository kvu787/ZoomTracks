using System;
using UnityEngine;

namespace ZoomTracks {
    public class TimeManager {
        private float RefreshRate { get; set; }
        private bool UseTimeDeltaTime { get; set; }

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
