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

        public Garage(string jsonString, Transform placeholderCarTransform) {
            Garage garage = JsonUtility.FromJson<Garage>(jsonString);
            foreach (Car car in garage.Cars) {
                car.Init(placeholderCarTransform);
            }
        }
    }
}
