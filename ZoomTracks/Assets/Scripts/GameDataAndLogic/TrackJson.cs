using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZoomTracks {
    [Serializable]
    public class TrackJson {
        [SerializeField]
        public bool CameraFollowsCarLocation;

        [SerializeField]
        public float FollowCameraSize;

        [SerializeField]
        public float FixedCameraSize;

        [SerializeField]
        public int StartCarIndex;

        [SerializeField]
        public List<Car> Cars;
    }
}
