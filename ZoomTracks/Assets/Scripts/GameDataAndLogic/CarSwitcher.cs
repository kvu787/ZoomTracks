using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

namespace ZoomTracks {
    public class CarSwitcher {
        private const string GarageFileName = "Garage.json";

        public GameObject CurrentCarGameObject => this.Cars[this.CurrentCarIndex].GameObject;

        private InputManager InputManager { get; }
        private int CurrentCarIndex { get; set; }
        private List<Car> Cars { get; }
        private Transform PlaceholderCarTransform { get; }

        public CarSwitcher(InputManager inputManager, TrackSwitcher trackSwitcher, Transform placeholderCarTransform) {
            this.InputManager = inputManager;

            string filePath = Path.Combine(Application.streamingAssetsPath.Replace('/', '\\'), GarageFileName);
            Assert.IsTrue(File.Exists(filePath), $"Garage JSON file does not exist at {filePath}");
            string fileContents = File.ReadAllText(filePath); // TODO: Use async file read
            Garage garage = new(fileContents, trackSwitcher.CurrentTrackScene);

            this.CurrentCarIndex = garage.StartCarIndex;
            this.Cars = garage.Cars;
            this.PlaceholderCarTransform = placeholderCarTransform;

            this.ActivateCarAndMoveToStartingPosition();
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
                this.CurrentCarGameObject.SetActive(false);
                if (isNextCar) {
                    this.CurrentCarIndex = this.CurrentCarIndex.CycleNext(this.Cars.Count);
                } else /* if (isPrevCar) */ {
                    this.CurrentCarIndex = this.CurrentCarIndex.CyclePrev(this.Cars.Count);
                }
                this.ActivateCarAndMoveToStartingPosition();
                return true;
            }
        }

        private void ActivateCarAndMoveToStartingPosition() {
            this.CurrentCarGameObject.SetActive(true);
            this.CurrentCarGameObject.transform.SetFrom(this.PlaceholderCarTransform);
        }
    }
}
