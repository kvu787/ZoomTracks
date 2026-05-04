using Drawing;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameLoop : MonoBehaviour {
    [SerializeField]
    private float CarTranslationSpeed = 150;

    [SerializeField]
    private float CarRotateSpeed = 540;

    private GameObject Car;
    private Transform[] TireGroundContactPoints;

    private Transform CameraPanAndYaw;
    private Transform CameraYawOffset;
    private Transform CameraPanOffsetAndPitch;
    private Camera Camera;

    void Awake() {
        Debug.Log($"GameLoop Awake on object='{this.gameObject.name}' in scene='{this.gameObject.scene.name}'");
        QualitySettings.maxQueuedFrames = 0;
        QualitySettings.vSyncCount = 1;

        this.Car = GameObject.Find("SlopeCarPlaceholder");
        this.TireGroundContactPoints = new Transform[] {
            this.Car.transform.Find("CarFL"),
            this.Car.transform.Find("CarFR"),
            this.Car.transform.Find("CarRL"),
            this.Car.transform.Find("CarRR"),
        };

        this.CameraPanAndYaw = GameObject.Find(nameof(this.CameraPanAndYaw)).transform;
        this.CameraYawOffset = GameObject.Find(nameof(this.CameraYawOffset)).transform;
        this.CameraPanOffsetAndPitch = GameObject.Find(nameof(this.CameraPanOffsetAndPitch)).transform;
        this.Camera = GameObject.Find(nameof(this.Camera)).GetComponent<Camera>();

    }

    /*
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() { }
    */

    // Update is called once per frame
    void Update() {
        this.Update_DebugMoveCarWithKeyboard();
    }

    private void Update_DebugMoveCarWithKeyboard() {
        if (Keyboard.current.eKey.isPressed) {
            this.Car.transform.Translate(Time.deltaTime * this.CarTranslationSpeed * Vector3.forward);
        }
        if (Keyboard.current.sKey.isPressed) {
            this.Car.transform.Rotate(new Vector3(0, 1, 0), -1 * Time.deltaTime * this.CarRotateSpeed);
        }
        if (Keyboard.current.dKey.isPressed) {
            this.Car.transform.Translate(Time.deltaTime * this.CarTranslationSpeed * Vector3.back);
        }
        if (Keyboard.current.fKey.isPressed) {
            this.Car.transform.Rotate(new Vector3(0, 1, 0), Time.deltaTime * this.CarRotateSpeed);
        }
    }

    private void Update_MoveCarToCursor() {
        Vector3 mousePosition = new(Mouse.current.position.x.ReadValue(), Mouse.current.position.y.ReadValue(), 0);
        Ray ray = this.Camera.ScreenPointToRay(mousePosition);
        float t = -ray.origin.y / ray.direction.y;
        float x = ray.origin.x + ray.direction.x * t;
        float z = ray.origin.z + ray.direction.z * t;
        Vector3 mousePositionWorld = new(x, 0, z);
        Draw.ingame.Cross(mousePositionWorld, 10, Color.red);
        this.Car.transform.position = mousePositionWorld;
        foreach (Transform transform in this.TireGroundContactPoints) {
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
        Ray ray = this.Camera.ScreenPointToRay(mousePosition);
        float t = -ray.origin.y / ray.direction.y;
        float x = ray.origin.x + ray.direction.x * t;
        float z = ray.origin.z + ray.direction.z * t;
        Draw.ingame.WireSphere(new Vector3(x, 0, z), 10, Color.red);
    }
}
