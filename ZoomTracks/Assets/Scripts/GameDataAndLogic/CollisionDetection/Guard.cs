using System;

namespace ZoomTracks {
    public static class Guard {
        public static bool IsFinite(float value) {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static void ThrowIfNotFinite(float value, string parameterName) {
            if (!IsFinite(value)) {
                throw new ArgumentOutOfRangeException(parameterName, "The value must be finite.");
            }
        }
    }
}
