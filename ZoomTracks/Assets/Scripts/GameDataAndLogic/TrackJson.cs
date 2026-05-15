using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZoomTracks {
    [Serializable]
    public class TrackJson {
        [SerializeField]
        public bool CameraFollowsCarLocation;

        [SerializeField]
        public int FollowCameraSize;

        [SerializeField]
        public int FixedCameraSize;

        [SerializeField]
        public int StartCarIndex;

        [SerializeField]
        public List<Car> Cars;
    }
}
