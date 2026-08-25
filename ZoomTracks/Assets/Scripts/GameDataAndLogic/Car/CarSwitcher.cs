using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    public class CarSwitcher {
        private InputManager InputManager { get; }
        private int CurrentCarIndex { get; set; }
        private List<Car> Cars { get; }

        private Car CurrentCar => this.Cars[this.CurrentCarIndex];
        private GameObject CurrentCarGameObject => this.CurrentCar.GameObject;

        public CarSwitcher(Scene currentTrackScene, TrackJson currentTrackJson, InputManager inputManager) {
            this.InputManager = inputManager;
            this.CurrentCarIndex = currentTrackJson.StartCarIndex;
            this.Cars = currentTrackJson.Cars;

            foreach (Car car in this.Cars) {
                Assert.IsTrue(!string.IsNullOrEmpty(car.GameObjectName));
                GameObject decorativeGameObject = GameObject.Find(car.GameObjectName);
                Assert.IsNotNull(decorativeGameObject);

                car.GameObject = Object.Instantiate(original: decorativeGameObject, parameters: new InstantiateParameters() { scene = currentTrackScene });
                car.GameObject.transform.localScale = Vector3.one;
                if (currentTrackJson.CarScale > 0f) {
                    car.GameObject.transform.localScale *= currentTrackJson.CarScale;
                }
                car.GameObject.SetActive(false);
            }

            this.CurrentCarGameObject.SetActive(true);
        }

        public Transform CurrentCarTransform => this.CurrentCarGameObject.transform;
        public CarDynamic CurrentCarDynamic => this.CurrentCar.Dynamic;

        public bool ReadInputAndSwitchCar() {
            if (this.InputManager.PreviousCar == this.InputManager.NextCar) {
                return false;
            } else {
                this.CurrentCarGameObject.SetActive(false);
                if (this.InputManager.NextCar) {
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
