namespace ZoomTracks {
    public static class TimeManager {
        public static float DeltaTime { get; private set; }

        public static void Update() {
            //DeltaTime = UnityEngine.Time.deltaTime;
            DeltaTime = 1f / 60f;
        }
    }
}
