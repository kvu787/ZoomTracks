using System;
using UnityEngine;

namespace ZoomTracks {
    [Serializable]
    public class CarAccelerationMap {
        [SerializeField]
        public float Forward;

        [SerializeField]
        public float Reverse;

        [SerializeField]
        public float Left;

        [SerializeField]
        public float Right;
    }
}
