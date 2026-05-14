using System;
using UnityEngine;

namespace ZoomTracks {
    [Serializable]
    public class CarDynamic {
        [SerializeField]
        public float VelocityLimiter;

        [SerializeField]
        public CarAccelerationMap AccelerationMap;
    }
}
