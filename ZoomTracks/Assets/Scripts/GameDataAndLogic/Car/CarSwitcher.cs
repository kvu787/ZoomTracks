using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public class CarSwitcher {
        private const string GarageFileName = "Garage.json";

        private InputManager InputManager { get; }
        private int CurrentCarIndex { get; set; }
        private List<Car> Cars { get; }

        private Car CurrentCar => this.Cars[this.CurrentCarIndex];
        private GameObject CurrentCarGameObject => this.CurrentCar.GameObject;

        public CarSwitcher(Scene currentTrackScene, InputManager inputManager) {
            string filePath = Path.Combine(Application.streamingAssetsPath.Replace('/', '\\'), GarageFileName);
            Assert.IsTrue(File.Exists(filePath), $"Garage JSON file does not exist at {filePath}");
            string fileContents = File.ReadAllText(filePath); // TODO: Use async file read
            Garage garage = JsonUtility.FromJson<Garage>(fileContents);

            this.InputManager = inputManager;
            this.CurrentCarIndex = garage.StartCarIndex;
            this.Cars = garage.Cars;

            foreach (Car car in this.Cars) {
                Assert.IsTrue(!string.IsNullOrEmpty(car.GameObjectName));
                GameObject decorativeGameObject = GameObject.Find(car.GameObjectName);
                Assert.IsNotNull(decorativeGameObject);

                car.GameObject = Object.Instantiate(original: decorativeGameObject, parameters: new InstantiateParameters() { scene = currentTrackScene });
                car.GameObject.SetActive(false);
            }

            this.CurrentCarGameObject.SetActive(true);
        }

        public BoxCollider CurrentCarCollider => this.CurrentCarGameObject.GetComponent<BoxCollider>();
        public Transform CurrentCarTransform => this.CurrentCarGameObject.transform;
        public CarDynamic CurrentCarDynamic => this.CurrentCar.Dynamic;

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
                this.CurrentCarGameObject.SetActive(true);
                return true;
            }
        }
    }
}
