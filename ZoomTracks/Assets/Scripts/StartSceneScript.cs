using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using Stopwatch = System.Diagnostics.Stopwatch;

public class StartSceneScript : MonoBehaviour {
    public TMP_Text LoadingLabel;
    public float DotChangeDurationSeconds;
    public float MinLoadDurationSeconds;

    private const string SceneToLoad = "MainScene";
    private readonly Stopwatch DotsStopwatch = new();
    private readonly Stopwatch MinLoadStopwatch = new();
    private int NumDots = 0;

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
        _ = this.StartCoroutine(this.TransitionScene());
    }

    private IEnumerator TransitionScene() {
        TimeSpan minLoadDuration = TimeSpan.FromSeconds(this.MinLoadDurationSeconds);
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(SceneToLoad, LoadSceneMode.Additive);
        while (this.MinLoadStopwatch.Elapsed < minLoadDuration) {
            yield return new WaitForSecondsRealtime((float)(minLoadDuration - this.MinLoadStopwatch.Elapsed).TotalSeconds);
        }
        while (!loadOp.isDone) {
            yield return null;
        }
        Scene scene = SceneManager.GetSceneByName(SceneToLoad);
        Assert.IsTrue(scene.IsValid() && scene.isLoaded);
        Assert.IsTrue(SceneManager.SetActiveScene(scene));
        _ = SceneManager.UnloadSceneAsync(this.gameObject.scene);
    }

    // Update is called once per frame
    void Update() {
        if (this.DotsStopwatch.Elapsed > TimeSpan.FromSeconds(this.DotChangeDurationSeconds)) {
            this.NumDots = (this.NumDots + 1) % LoadingStrings.Length;
            this.LoadingLabel.text = LoadingStrings[this.NumDots];
            this.DotsStopwatch.Restart();
        }
    }
}
