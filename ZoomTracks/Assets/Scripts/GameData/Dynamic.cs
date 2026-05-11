using System;

namespace ZoomTracks {
    [Serializable]
    public struct Dynamic {
        public float VelocityLimiter;
        public AccelerationMap AccelerationMap;

        public Dynamic(float velocityLimiter, AccelerationMap accelerationMap) {
            this.VelocityLimiter = velocityLimiter;
            this.AccelerationMap = accelerationMap;
        }
    }
}
