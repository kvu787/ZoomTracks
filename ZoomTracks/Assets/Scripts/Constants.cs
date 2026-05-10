using System.Collections.Generic;

namespace ZoomTracks {
    public static class Constants {
        public const string MainSceneName = "Main";
        public const string UiSceneName = "Ui";
        public const string TestSceneName = "Test";
        public static readonly IReadOnlyList<string> TrackSceneNames = new List<string>() {
            "Track1",
            "Track2",
        };
        public const int InitialTrackSceneIndex = 1;
    }
}
