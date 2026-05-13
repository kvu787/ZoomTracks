using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZoomTracks {
    [Serializable]
    public class Garage {
        [SerializeField]
        public int StartCarIndex;

        [SerializeField]
        public List<Car> Cars;
    }
}
