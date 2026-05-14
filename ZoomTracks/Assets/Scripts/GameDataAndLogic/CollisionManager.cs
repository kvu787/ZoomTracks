using UnityEngine;

namespace ZoomTracks {
    public class CollisionManager {
        private CarState CarState { get; }
        private CarSwitcher CarSwitcher { get; }
        private TrackObjects TrackObjects { get; }

        public CollisionManager(TrackObjects trackObjects, CarSwitcher carSwitcher, CarState carState) {
            this.TrackObjects = trackObjects;
            this.CarSwitcher = carSwitcher;
            this.CarState = carState;
        }

        public bool ResetCarIfColliding() {
            foreach (BoxCollider obstacle in this.TrackObjects.Obstacles) {
                if (IsColliding(this.CarSwitcher.CurrentCarCollider, obstacle)) {
                    this.CarState.Reset();
                    return true;
                }
            }
            return false;
        }

        private static bool IsColliding(Collider a, Collider b) {
            // I use Physics.ComputePenetration because I was having issues with the more commonly used Collider.OnTriggerEnter.
            // When the car collided with a barrier right next to its reset position and the reset timeout was too low, OnTriggerEnter
            // would fail to trigger because the collisions happened too frequently (within 10 ms or less) and collision checking was tied
            // to the 50 Hz FixedUpdate. This meant OnTriggerEnter wouldn't always trigger. Increasing the FixedUpdate frequency resolved this,
            // but I think it's more straightforward to do inline collision checking with ComputePenetration.
            return Physics.ComputePenetration(
                a, a.transform.position, a.transform.rotation,
                b, b.transform.position, b.transform.rotation,
                out _, out _);
        }
    }
}
