using System;
using UnityEngine;

namespace ZoomTracks {
    [Serializable]
    public struct CoordinateXY {
        public CoordinateXY(float x, float y) {
            Guard.ThrowIfNotFinite(x, nameof(x));
            Guard.ThrowIfNotFinite(y, nameof(y));
            this.X = x;
            this.Y = y;
        }

        [SerializeField]
        public float X;

        [SerializeField]
        public float Y;
    }
}
