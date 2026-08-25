using System;
using UnityEngine;

namespace ZoomTracks {
    public class CollisionManager2 {
        public CollisionManager2(string trackName, CarSwitcher carSwitcher) {
            string relativePath = $"{trackName}_ColliderData.example.json";
            ColliderJson colliderJson = JsonUtility.Deserialize<ColliderJson>(relativePath);
            Debug.Log($"Collider outlines count: {colliderJson.Outlines.Count}");
            Debug.Log(carSwitcher.CurrentCarTransform.position);
        }

        public bool IsCarColliding_SuperOptimalImplementation() {
            throw new NotImplementedException();
        }

        public bool IsCarColliding_SuperSimpleImplementation() {
            throw new NotImplementedException();
        }
    }
}
