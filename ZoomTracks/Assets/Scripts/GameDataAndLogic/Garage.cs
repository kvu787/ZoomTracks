using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    [Serializable]
    public class Garage {
        [SerializeField]
        public int StartCarIndex;

        [SerializeField]
        public List<Car> Cars;

        public Garage(string jsonString, Scene trackScene) {
            Garage garage = JsonUtility.FromJson<Garage>(jsonString);
            this.StartCarIndex = garage.StartCarIndex;
            this.Cars = garage.Cars;
            foreach (Car car in garage.Cars) {
                car.InitAfterCreateFromJson(trackScene);
            }
        }
    }
}
