using System;
using UnityEngine;

namespace ZoomTracks {
    [Serializable]
    public class Car {
        [SerializeField]
        public string GameObjectName;

        [SerializeField]
        public CarDynamic Dynamic;

        [NonSerialized]
        public GameObject GameObject;
    }
}
