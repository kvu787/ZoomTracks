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
        private Transform PlaceholderCarTransform { get; }
        private CarState CarState { get; }

        public CarSwitcher(InputManager inputManager, Scene currentTrackScene, Transform placeholderCarTransform, CarState carState) {
            string filePath = Path.Combine(Application.streamingAssetsPath.Replace('/', '\\'), GarageFileName);
            Assert.IsTrue(File.Exists(filePath), $"Garage JSON file does not exist at {filePath}");
            string fileContents = File.ReadAllText(filePath); // TODO: Use async file read
            Garage garage = JsonUtility.FromJson<Garage>(fileContents);

            this.InputManager = inputManager;
            this.CurrentCarIndex = garage.StartCarIndex;
            this.Cars = garage.Cars;
            this.PlaceholderCarTransform = placeholderCarTransform;
            this.CarState = carState;

            foreach (Car car in this.Cars) {
                Assert.IsTrue(!string.IsNullOrEmpty(car.GameObjectName));
                GameObject decorativeGameObject = GameObject.Find(car.GameObjectName);
                Assert.IsNotNull(decorativeGameObject);

                car.GameObject = Object.Instantiate(original: decorativeGameObject, parameters: new InstantiateParameters() { scene = currentTrackScene });
                car.GameObject.SetActive(false);

                //this.Collider = this.GameObject.GetComponentInChildren<Collider>();
                //Assert.IsNotNull(this.Collider);
            }

            this.InitializeCurrentCar();
        }

        public BoxCollider CurrentCarCollider => this.CurrentCarGameObject.GetComponent<BoxCollider>();

        public CarDynamic CurrentCarDynamic => this.Cars[this.CurrentCarIndex].Dynamic;

        public GameObject CurrentCarGameObject => this.Cars[this.CurrentCarIndex].GameObject;

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
                this.InitializeCurrentCar();
                return true;
            }
        }

        private void InitializeCurrentCar() {
            this.CurrentCarGameObject.SetActive(true);
            this.CarState.Reset(this.PlaceholderCarTransform);
        }
    }
}
