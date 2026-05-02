using System;
using UnityEngine;

public class GameLoop : MonoBehaviour {
    void Awake() {
        Debug.Log($"GameLoop Awake on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
        QualitySettings.maxQueuedFrames = 0;
        QualitySettings.vSyncCount = 1;
    }

    /*
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() { }
    */

    // Update is called once per frame
    void Update() {
        Debug.Log(DateTime.Now.Ticks);
    }
}
