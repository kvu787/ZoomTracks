using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CarSwitcher {
        private const string GarageFileName = "Garage.json";

        public Car CurrentCar => this.Cars[this.CurrentCarIndex];

        private InputManager InputManager { get; }
        private int CurrentCarIndex { get; set; }
        private List<Car> Cars { get; }
        private TrackObjects TrackObjects { get; }

        public CarSwitcher(InputManager inputManager, TrackObjects trackObjects, TrackSwitcher trackSwitcher) {
            this.InputManager = inputManager;
            this.TrackObjects = trackObjects;

            string filePath = Path.Combine(Application.streamingAssetsPath.Replace('/', '\\'), GarageFileName);
            Assert.IsTrue(File.Exists(filePath), $"Garage JSON file does not exist at {filePath}");
            string fileContents = File.ReadAllText(filePath); // TODO: Use async file read
            Garage garage = new(fileContents, this.TrackObjects.PlaceholderCar.transform, trackSwitcher.CurrentTrackScene);

            this.CurrentCarIndex = garage.StartCarIndex;
            this.Cars = garage.Cars;
            this.TrackObjects.PlaceholderCar.SetActive(false);
            this.CurrentCar.GameObject.SetActive(true);
        }

        public bool ReadInputAndSwitchCar() {
            bool isPrevCar = false;
            bool isNextCar = false;
            if (this.InputManager.Keyboard != null) {
                Keyboard keyboard = this.InputManager.Keyboard;
                isPrevCar = isPrevCar || keyboard.cKey.wasPressedThisFrame;
                isNextCar = isNextCar || keyboard.vKey.wasPressedThisFrame;
            }
            if (this.InputManager.Gamepad != null) {
                Gamepad gamepad = this.InputManager.Gamepad;
                isPrevCar = isPrevCar || gamepad.dpad.left.wasPressedThisFrame;
                isNextCar = isNextCar || gamepad.dpad.right.wasPressedThisFrame;
            }

            if (isPrevCar == isNextCar) {
                return false;
            } else {
                this.CurrentCar.GameObject.SetActive(false);
                if (isNextCar) {
                    this.CurrentCarIndex = this.CurrentCarIndex.CycleNext(this.Cars.Count);
                } else /* if (isPrevCar) */ {
                    this.CurrentCarIndex = this.CurrentCarIndex.CyclePrev(this.Cars.Count);
                }
                this.CurrentCar.GameObject.SetActive(true);
                this.CurrentCar.GameObject.transform.SetFrom(this.TrackObjects.PlaceholderCar.transform);
                return true;
            }
        }
    }
}
