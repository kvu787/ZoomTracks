using Drawing;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameLoop : MonoBehaviour {
    [SerializeField]
    private float CarTranslationSpeed = 150;

    [SerializeField]
    private float CarRotateSpeed = 540;

    void Awake() {
        Debug.Log($"GameLoop Awake on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
        QualitySettings.maxQueuedFrames = 0;
        QualitySettings.vSyncCount = 1;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    IEnumerator Start() {
        if (SceneManager.GetSceneByName("UiScene").isLoaded) {
            Debug.Log("UiScene is already loaded.");
        } else {
            Debug.Log("UiScene is not loadeasdfd yet.");
            Debug.Log("Will execute the following: yield return SceneManager.LoadSceneAsync(\"UiScene\", LoadSceneMode.Additive); ...");
            yield return SceneManager.LoadSceneAsync("UiScene", LoadSceneMode.Additive);
            Debug.Log("Finished executing: yield return SceneManager.LoadSceneAsync(\"UiScene\", LoadSceneMode.Additive);");
        }

        SceneObjects.Init();
        SceneObjects.TestLabel.text = "Test passed";
    }

    // Update is called once per frame
    void Update() {
        this.Update_DebugMoveCarWithKeyboard();
    }

    private void Update_DebugMoveCarWithKeyboard() {
        if (Keyboard.current.eKey.isPressed) {
            SceneObjects.Car.transform.Translate(Time.deltaTime * this.CarTranslationSpeed * Vector3.forward);
        }
        if (Keyboard.current.sKey.isPressed) {
            SceneObjects.Car.transform.Rotate(new Vector3(0, 1, 0), -1 * Time.deltaTime * this.CarRotateSpeed);
        }
        if (Keyboard.current.dKey.isPressed) {
            SceneObjects.Car.transform.Translate(Time.deltaTime * this.CarTranslationSpeed * Vector3.back);
        }
        if (Keyboard.current.fKey.isPressed) {
            SceneObjects.Car.transform.Rotate(new Vector3(0, 1, 0), Time.deltaTime * this.CarRotateSpeed);
        }
    }

    private void Update_MoveCarToCursor() {
        Vector3 mousePosition = new(Mouse.current.position.x.ReadValue(), Mouse.current.position.y.ReadValue(), 0);
        Ray ray = SceneObjects.Camera.ScreenPointToRay(mousePosition);
        float t = -ray.origin.y / ray.direction.y;
        float x = ray.origin.x + ray.direction.x * t;
        float z = ray.origin.z + ray.direction.z * t;
        Vector3 mousePositionWorld = new(x, 0, z);
        Draw.ingame.Cross(mousePositionWorld, 10, Color.red);
        SceneObjects.Car.transform.position = mousePositionWorld;
        foreach (Transform transform in SceneObjects.TireGroundContactPoints) {
            Draw.ingame.Cross(transform.position, 1, Color.red);
        }
    }

    private void Update_PrintTicks() {
        Debug.Log(DateTime.Now.Ticks);
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

    private void Update_DrawCursor() {
        Vector3 mousePosition = new(Mouse.current.position.x.ReadValue(), Mouse.current.position.y.ReadValue(), 0);
        Ray ray = SceneObjects.Camera.ScreenPointToRay(mousePosition);
        float t = -ray.origin.y / ray.direction.y;
        float x = ray.origin.x + ray.direction.x * t;
        float z = ray.origin.z + ray.direction.z * t;
        Draw.ingame.WireSphere(new Vector3(x, 0, z), 10, Color.red);
    }
}
