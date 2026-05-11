using System;
using System.Diagnostics;

namespace ZoomTracks {
    public class CarResetter {
        public bool IsTimedOut = false;
        private readonly CarState InitialCarState;

        private static readonly TimeSpan ResetTimerDuration = TimeSpan.FromSeconds(0.5f);
        private static readonly Stopwatch ResetCarStopwatch = new();

        public CarResetter(CarState initialCarState) {
            this.InitialCarState = initialCarState;
        }

        public void UpdateTimeout() {
            if (ResetCarStopwatch.IsRunning) {
                if (ResetCarStopwatch.Elapsed > ResetTimerDuration) {
                    ResetCarStopwatch.Reset();
                    this.IsTimedOut = false;
                } else {
                    this.IsTimedOut = true;
                }
            } else {
                this.IsTimedOut = false;
            }
        }

        public void ResetCar(out CarState carState) {
            ResetCarStopwatch.Restart();
            carState = this.InitialCarState;
        }
    }
}
