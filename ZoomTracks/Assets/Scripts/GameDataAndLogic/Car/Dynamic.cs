using System;
using UnityEngine;

namespace ZoomTracks {
    [Serializable]
    public class Dynamic {
        [SerializeField]
        public float VelocityLimiter;

        [SerializeField]
        public AccelerationMap AccelerationMap;

        public Dynamic(float velocityLimiter, AccelerationMap accelerationMap) {
            this.VelocityLimiter = velocityLimiter;
            this.AccelerationMap = accelerationMap;
        }
    }
}
