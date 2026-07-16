using System;
using System.Globalization;

namespace ZoomTracks {
    public static class ParseUtility {
        public static float ParseFloat(string s) {
            if (string.IsNullOrEmpty(s)) {
                throw new ArgumentException("s must not be null or empty");
            }

            bool success = float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f);
            if (!success) {
                throw new Exception($"Failed to parse {s} as a float");
            }

            return f;
        }
    }
}
