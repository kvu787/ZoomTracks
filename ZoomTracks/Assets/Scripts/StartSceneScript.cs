using System;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StartSceneScript : MonoBehaviour {
    public Canvas Canvas;
    public TMP_Text LoadingLabel;
    public float DotChangeDurationSeconds;
    public float MinLoadDurationSeconds;

    private readonly Stopwatch DotsStopwatch = new();
    private readonly Stopwatch MinLoadStopwatch = new();
    private int NumDots = 0;
    private AsyncOperation loadSceneOp;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        this.DotsStopwatch.Start();
        this.MinLoadStopwatch.Start();
    }

    // Update is called once per frame
    void Update() {
        if (this.DotsStopwatch.Elapsed > TimeSpan.FromSeconds(this.DotChangeDurationSeconds)) {
            this.NumDots = (this.NumDots + 1) % 4;
            string text = "Loading";
            for (int i = 0; i < this.NumDots; i++) {
                text += ".";
            }
            this.LoadingLabel.text = text;
            this.DotsStopwatch.Restart();
        }

        if (this.loadSceneOp is not null) {
            if (this.loadSceneOp.progress >= 0.9f && (this.MinLoadStopwatch.Elapsed >= TimeSpan.FromSeconds(this.MinLoadDurationSeconds))) {
                this.loadSceneOp.allowSceneActivation = true;
                this.MinLoadStopwatch.Stop();
            }
        } else {
            if (!SceneManager.GetSceneByName("MainScene").isLoaded) {
                this.loadSceneOp = SceneManager.LoadSceneAsync("MainScene");
                this.loadSceneOp.allowSceneActivation = false;
            }
        }
    }
}
