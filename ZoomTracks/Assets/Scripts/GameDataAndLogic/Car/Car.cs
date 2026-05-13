using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace ZoomTracks {
    [Serializable]
    public class Car {
        [SerializeField]
        private string GameObjectName;

        [SerializeField]
        public Dynamic Dynamic;

        [NonSerialized]
        public GameObject GameObject;

        [NonSerialized]
        public Collider Collider;

        public void InitAfterCreateFromJson(Scene trackScene) {
            Assert.IsTrue(!string.IsNullOrEmpty(this.GameObjectName));
            GameObject decorativeGameObject = GameObject.Find(this.GameObjectName);
            Assert.IsNotNull(decorativeGameObject);

            this.GameObject = UnityEngine.Object.Instantiate(
                original: decorativeGameObject,
                position: Vector3.zero,
                rotation: Quaternion.identity,
                parameters: new InstantiateParameters() { scene = trackScene });
            this.GameObject.SetActive(false);

            //this.Collider = this.GameObject.GetComponentInChildren<Collider>();
            //Assert.IsNotNull(this.Collider);
        }
    }
}
