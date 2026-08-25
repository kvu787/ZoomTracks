using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZoomTracks {
    [Serializable]
    public class Outline {
        [SerializeField]
        public List<CoordinateXY> Vertices;
    }
}
