namespace ZoomTracks {
    public static class IntExtensions {
        public static int CyclePrev(this int i, int count) {
            return (i - 1 + count) % count;
        }

        public static int CycleNext(this int i, int count) {
            return (i + 1) % count;
        }
    }
}
