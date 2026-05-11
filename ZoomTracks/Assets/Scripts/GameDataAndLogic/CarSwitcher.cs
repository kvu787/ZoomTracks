using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Assertions;

namespace ZoomTracks {
    public class CarSwitcher {
        public static CarGameObject CurrentCar => Cars[CurrentCarIndex];
        private static int CurrentCarIndex = 1;

        private static List<CarGameObject> Cars;

        public CarSwitcher(GameObject placeholderCarGameObject, string filePath) {
            placeholderCarGameObject.SetActive(false);
            Assert.IsTrue(File.Exists(filePath), $"Garage does not exist at {filePath}");
            string fileContents = File.ReadAllText(filePath); // TODO: Use async file read
            Garage garage = new(fileContents, placeholderCarGameObject.transform);
            CurrentCarIndex = garage.StartCarIndex;
            Cars = garage.Cars;
            CurrentCar.GameObject.SetActive(true);
        }

        //public static bool ProcessCarSwitch() {
        //    if (!Input.NextCarEvent && !Input.PrevCarEvent) {
        //        return false;
        //    }
        //    CurrentCar.GameObject.SetActive(false);
        //    if (Input.NextCarEvent) {
        //        CurrentCarIndex += 1;
        //        CurrentCarIndex %= Cars.Count;
        //    } else if (Input.PrevCarEvent) {
        //        if (CurrentCarIndex == 0) {
        //            CurrentCarIndex = Cars.Count - 1;
        //        } else {
        //            CurrentCarIndex -= 1;
        //        }
        //    }
        //    CurrentCar.GameObject.SetActive(true);
        //    return true;
        //}
    }
}
