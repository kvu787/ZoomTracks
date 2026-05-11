using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZoomTracks {
    [Serializable]
    public class Garage {
        public int StartCarIndex;
        public List<CarGameObject> Cars;

        public Garage(string jsonString, Transform transform) {
            Garage garage = JsonUtility.FromJson<Garage>(jsonString);
            foreach (CarGameObject car in garage.Cars) {
                car.Init(transform);
            }
        }
    }
}
