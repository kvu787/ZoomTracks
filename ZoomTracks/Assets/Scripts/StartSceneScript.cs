using System;
using System.Diagnostics;
using TMPro;
using UnityEngine;

public class StartSceneScript : MonoBehaviour {
    public TMP_Text LoadingLabel;
    public float DotChangeDurationSeconds;

    private readonly Stopwatch Stopwatch = new();
    private int NumDots = 3;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        this.Stopwatch.Start();
    }

    // Update is called once per frame
    void Update() {
        if (this.Stopwatch.Elapsed > TimeSpan.FromSeconds(this.DotChangeDurationSeconds)) {
            this.NumDots = (this.NumDots + 1) % 4;
            string text = "Loading";
            for (int i = 0; i < this.NumDots; i++) {
                text += ".";
            }
            this.LoadingLabel.text = text;
            this.Stopwatch.Restart();
        }
    }
}
