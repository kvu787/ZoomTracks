using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CarSwitcher {
        public CarGameObject CurrentCar => this.Cars[this.CurrentCarIndex];
        private int CurrentCarIndex = 1;

        private readonly List<CarGameObject> Cars;
        private const string GarageFileName = "Garage.json";

        public CarSwitcher(GameObject placeholderCarGameObject) {
            placeholderCarGameObject.SetActive(false);

            string filePath = Path.Combine(Application.streamingAssetsPath.Replace('/', '\\'), GarageFileName);
            Assert.IsTrue(File.Exists(filePath), $"Garage JSON file does not exist at {filePath}");
            string fileContents = File.ReadAllText(filePath); // TODO: Use async file read
            Garage garage = new(fileContents, placeholderCarGameObject.transform);
            this.CurrentCarIndex = garage.StartCarIndex;
            this.Cars = garage.Cars;
            this.CurrentCar.GameObject.SetActive(true);
        }

        public bool ReadInputAndSwitchCar(Keyboard keyboard, Gamepad gamepad) {
            bool isPrevCar = false;
            bool isNextCar = false;
            if (keyboard != null) {
                isPrevCar = isPrevCar || keyboard.cKey.wasPressedThisFrame;
                isNextCar = isNextCar || keyboard.vKey.wasPressedThisFrame;
            }
            if (gamepad != null) {
                isPrevCar = isPrevCar || gamepad.dpad.left.wasPressedThisFrame;
                isNextCar = isNextCar || gamepad.dpad.right.wasPressedThisFrame;
            }

            if (!isPrevCar && !isNextCar) {
                return false;
            }

            this.CurrentCar.GameObject.SetActive(false);
            int i = this.CurrentCarIndex;
            if (isNextCar) {
                i = (i - 1 + this.Cars.Count) % this.Cars.Count;
            } else if (isPrevCar) {
                i = (i + 1) % this.Cars.Count;
            }
            this.CurrentCarIndex = i;
            this.CurrentCar.GameObject.SetActive(true);
            return true;
        }
    }
}
