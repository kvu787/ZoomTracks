using System;
using UnityEngine;

namespace ZoomTracks {
    [Serializable]
    public class AccelerationMap {
        [SerializeField]
        public float Forward;

        [SerializeField]
        public float Reverse;

        [SerializeField]
        public float Left;

        [SerializeField]
        public float Right;

        public AccelerationMap(float forward, float reverse, float left, float right) {
            this.Forward = forward;
            this.Reverse = reverse;
            this.Left = left;
            this.Right = right;
        }
    }
}
