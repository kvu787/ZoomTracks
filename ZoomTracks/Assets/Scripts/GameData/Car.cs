using System;
using UnityEngine;
using UnityEngine.Assertions;

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

        public void Init(Transform placeholderCarTransform) {
            Assert.IsTrue(!string.IsNullOrEmpty(this.GameObjectName));
            GameObject decorativeGameObject = GameObject.Find(this.GameObjectName);
            Assert.IsNotNull(decorativeGameObject);

            this.GameObject = UnityEngine.Object.Instantiate(decorativeGameObject, Vector3.zero, Quaternion.identity);
            this.GameObject.transform.SetFrom(placeholderCarTransform);
            this.GameObject.SetActive(false);

            this.Collider = this.GameObject.GetComponentInChildren<Collider>();
            Assert.IsNotNull(this.Collider);
        }
    }
}
