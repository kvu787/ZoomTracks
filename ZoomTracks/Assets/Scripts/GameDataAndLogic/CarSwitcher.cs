using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CarSwitcher {
        public Car CurrentCar => this.Cars[this.CurrentCarIndex];
        private int CurrentCarIndex = 1;

        private readonly List<Car> Cars;
        private const string GarageFileName = "Garage.json";
        private readonly TrackObjects TrackObjects;

        public CarSwitcher(TrackObjects trackObjects, TrackSwitcher trackSwitcher) {
            this.TrackObjects = trackObjects;
            string filePath = Path.Combine(Application.streamingAssetsPath.Replace('/', '\\'), GarageFileName);
            Assert.IsTrue(File.Exists(filePath), $"Garage JSON file does not exist at {filePath}");
            string fileContents = File.ReadAllText(filePath); // TODO: Use async file read
            Garage garage = new(fileContents, this.TrackObjects.PlaceholderCar.transform, trackSwitcher.CurrentTrackScene);
            this.CurrentCarIndex = garage.StartCarIndex;
            this.Cars = garage.Cars;
            trackObjects.PlaceholderCar.SetActive(false);
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
                i = (i + 1) % this.Cars.Count;
            } else if (isPrevCar) {
                i = (i - 1 + this.Cars.Count) % this.Cars.Count;
            }
            this.CurrentCarIndex = i;
            this.CurrentCar.GameObject.SetActive(true);
            this.CurrentCar.GameObject.transform.SetFrom(this.TrackObjects.PlaceholderCar.transform);
            return true;
        }
    }
}
