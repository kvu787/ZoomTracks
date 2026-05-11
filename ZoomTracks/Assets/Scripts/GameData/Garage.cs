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
            this.StartCarIndex = garage.StartCarIndex;
            this.Cars = garage.Cars;
            foreach (Car car in garage.Cars) {
                car.InitAfterCreateFromJson(placeholderCarTransform);
            }
        }
    }
}
