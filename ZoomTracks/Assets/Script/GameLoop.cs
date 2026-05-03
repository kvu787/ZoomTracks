using Drawing;
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
        this.Update_TestAline();
    }

    private Matrix4x4 originMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

    private void Update_TestAline() {
        Draw.ingame.Line(Vector3.zero, Vector3.one * 50, Color.red);
        using (Draw.ingame.WithMatrix(this.originMatrix)) {
            using (Draw.ingame.WithColor(Color.blue)) {
                Draw.ingame.WireBox(Vector3.zero, Vector3.one * 100);
                Draw.ingame.WireCylinder(Vector3.up * 1f, Vector3.up, 1f, 0.5f);
            }
        }
    }
}
