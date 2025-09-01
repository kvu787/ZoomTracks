using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Stopwatch = System.Diagnostics.Stopwatch;

public class StartSceneScript : MonoBehaviour {
    public Canvas Canvas;
    public TMP_Text LoadingLabel;
    public float DotChangeDurationSeconds;
    public float MinLoadDurationSeconds;

    private const string SceneToLoad = "MainScene";
    private const string SceneToUnload = "StartScene";
    private readonly Stopwatch DotsStopwatch = new();
    private readonly Stopwatch MinLoadStopwatch = new();
    private int NumDots = 0;
    private AsyncOperation loadOp;

    private static readonly string[] LoadingStrings = {
        "Loading",
        "Loading.",
        "Loading..",
        "Loading...",
    };

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        this.DotsStopwatch.Start();
        this.MinLoadStopwatch.Start();
    }

    // Update is called once per frame
    void Update() {
        if (this.DotsStopwatch.Elapsed > TimeSpan.FromSeconds(this.DotChangeDurationSeconds)) {
            this.NumDots = (this.NumDots + 1) % 4;
            this.LoadingLabel.text = LoadingStrings[this.NumDots];
            this.DotsStopwatch.Restart();
        }

        if (this.loadOp is null) {
            this.loadOp = SceneManager.LoadSceneAsync(SceneToLoad);
            this.loadOp.allowSceneActivation = false;
        }

        if ((this.loadOp is not null) && (this.loadOp.progress >= 0.9f) && (this.MinLoadStopwatch.Elapsed > TimeSpan.FromSeconds(this.MinLoadDurationSeconds))) {
            this.loadOp.allowSceneActivation = true;
        }
    }
}
