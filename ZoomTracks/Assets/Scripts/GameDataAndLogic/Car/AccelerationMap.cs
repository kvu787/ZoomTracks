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
    }
}
